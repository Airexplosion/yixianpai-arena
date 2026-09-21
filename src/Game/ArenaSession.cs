using System;
using System.Collections.Generic;
using System.Globalization;
using Proto;
using UnityEngine;
using Yx.ModSdk;

namespace YxArena.Game
{
    /// <summary>
    /// 一次练习场会话：进场 → 本地对局状态 → 发牌 / 改设置 / 切换编辑方 / 开打（可反复）→ 离场。全程不发包。
    /// 依据（探针实机验证）：不在房间里进 Battle 场景走的是 BattleManager.OnStart 的「回放」分支；
    /// BattleManager.Refresh(GameStatus) 是 public；战斗 = 本地造 BattleResult + PlayBattle()，引擎在客户端。
    ///
    /// 两方（我 / 对手）各有一份 <see cref="ArenaSide"/>。备战界面一次只能编辑一方：把正在编辑的那一方当作「自己」
    /// （用真实 uid）喂给游戏，另一方当对手；切换 = 把界面读回当前方，再把另一方喂进去。游戏的复盘切换是服务器做的，
    /// 这里在本地做同样的事。开打时永远是「我」在左、用真实 uid。
    /// </summary>
    public sealed class ArenaSession
    {
        public const int StateIdle = 0;
        public const int StateEntering = 1;
        public const int StateReady = 2;
        public const int StateLeaving = 3;

        const string OtherUid = "YxArenaDummy";
        const int ParamCount = 4096;
        const int SettleFrames = 30;
        const float EnterTimeoutSeconds = 20f;
        const float LeaveTimeoutSeconds = 10f;

        static ArenaSession s_current;

        readonly ModContext _ctx;
        readonly ArenaConfig _cfg;
        readonly OfflineHooks _hooks;
        readonly ArenaSide[] _sides = new ArenaSide[2];
        readonly BattlePlayerData[] _live = new BattlePlayerData[2];
        int _editing;
        int _state;
        int _settle;
        float _deadline;
        int _battleIndex;
        bool _warnedReplace;

        public uint LastSeed;

        public ArenaSession(ModContext ctx, ArenaConfig cfg, OfflineHooks hooks)
        {
            _ctx = ctx;
            _cfg = cfg;
            _hooks = hooks;
            _sides[0] = new ArenaSide("我", _cfg.CharacterId, _cfg.Level, 0);
            _sides[1] = new ArenaSide("木人", ArenaConfig.DefaultCharacterId, ArenaConfig.MinLevel, _cfg.DummyHp);
            LoadSides();
            s_current = this;
        }

        public int State { get { return _state; } }

        /// <summary>正在备战界面里编辑的那一方。</summary>
        public ArenaSide Editing { get { return _sides[_editing]; } }

        public bool EditingOpponent { get { return _editing == 1; } }

        /// <summary>在本 mod 带进来的练习场里，且不在任何服务器房间里。钩子处理器都先问这个。</summary>
        public static bool Active
        {
            get { return s_current != null && s_current._state != StateIdle && RoomGuard.Reason() == null; }
        }

        /// <summary>备战界面（修炼阶段）且没有战斗在演。</summary>
        public bool InPlacement
        {
            get
            {
                if (_state != StateReady) return false;
                BattleManager bm = BattleManager.Instance;
                if (bm == null || bm.currentGameStatus == null || bm.currentScene != SceneType.修炼阶段) return false;
                BattleExecuter be = bm.defaultBattleExecuter;
                return be == null || !be.isExecuting;
            }
        }

        /// <summary>练习场里的斗法阶段（战斗在演，或演完了还没点退出）。</summary>
        public bool InBattlePhase
        {
            get
            {
                if (_state != StateReady) return false;
                BattleManager bm = BattleManager.Instance;
                return bm != null && bm.currentGameStatus != null && bm.currentScene == SceneType.斗法阶段;
            }
        }

        public string SideName(int index) { return _sides[index == 1 ? 1 : 0].Name; }

        /// <summary>一方真实的总血量（可以超过游戏装得下的 <see cref="BigHpPool.Cap"/>）。</summary>
        public long TrueTotalHp(int index)
        {
            ArenaSide side = _sides[index == 1 ? 1 : 0];
            return side.TrueTotalHp(BaseHp(side.Level));
        }

        /// <summary>一方喂给游戏的总血量。</summary>
        public int GameTotalHp(int index)
        {
            ArenaSide side = _sides[index == 1 ? 1 : 0];
            return side.TotalHp(BaseHp(side.Level));
        }

        static string N(int value) { return value.ToString(CultureInfo.InvariantCulture); }

        void Say(string message)
        {
            _ctx.Log.Info(message);
            Yx.ModSdk.Unity.Ui.Toast(message);
        }

        // ── 存档：双方的设置、手牌、场上的牌跨会话保留 ─────────────────────────────

        const string SaveKeyPrefix = "side";

        void LoadSides()
        {
            for (int i = 0; i < 2; i++)
            {
                string text = _ctx.Data.Get<string>(SaveKeyPrefix + N(i), "");
                if (text.Length > 0 && !_sides[i].Load(text)) _ctx.Log.Warn("练习场存档读不出来，" + _sides[i].Name + "用默认设置");
            }
        }

        public void Persist()
        {
            try
            {
                for (int i = 0; i < 2; i++) _ctx.Data.Set(SaveKeyPrefix + N(i), _sides[i].Save());
            }
            catch (Exception e) { _ctx.Log.Warn("练习场存档失败：" + e.Message); }
        }

        /// <summary>备战界面里隔一会儿存一次：玩家可能用游戏自己的方式直接回大厅，那时已经读不到界面了。</summary>
        public void Autosave()
        {
            if (!InPlacement) return;
            try { if (Capture()) Persist(); }
            catch (Exception e) { _ctx.Log.Warn("练习场自动存档失败：" + e.Message); }
        }

        /// <summary>某一方（0 = 我，1 = 对手）。设置窗直接改它，改完调 <see cref="Persist"/>。</summary>
        public ArenaSide SideAt(int index)
        {
            return _sides[index == 1 ? 1 : 0];
        }

        /// <summary>两方的手牌和场上的牌全部清掉（设置不动）。</summary>
        public void ClearCards()
        {
            for (int i = 0; i < 2; i++)
            {
                _sides[i].Hand.Clear();
                _sides[i].SetBoard(null);
            }
            Persist();
        }

        // ── 进场 ──────────────────────────────────────────────────────────

        public void Enter()
        {
            if (_state != StateIdle) { Say("已经在练习场里了"); return; }
            if (!_hooks.Complete) { Say("离线保护不完整，禁止进场（没挂上：" + _hooks.Missing + "）"); return; }
            string guard = RoomGuard.Reason();
            if (guard != null) { Say(guard); return; }
            if (SceneLoader.isLoading || SceneLoader.currentSceneName != "Lobby") { Say("只能从大厅进练习场"); return; }
            _live[0] = null;
            _live[1] = null;
            _editing = 0;
            // 没有要播的战斗：OnStart 的回放分支 → Execute(null) 直接返回，场景空着立起来（黑屏是游戏自己的幕布）。
            BattleManager.currentBattleResult = null;
            _state = StateEntering;
            _settle = 0;
            _deadline = Time.realtimeSinceStartup + EnterTimeoutSeconds;
            SceneLoader.LoadScene("Battle");
            _ctx.Log.Info("进场：已请求加载 Battle 场景");
        }

        public void Tick()
        {
            if (_state == StateEntering) TickEntering();
            else if (_state == StateLeaving) TickLeaving();
        }

        void TickEntering()
        {
            if (RoomGuard.Reason() != null) { Abort("进场途中进了房间，练习场退出"); return; }
            if (!BattleReady())
            {
                _settle = 0;
                if (Time.realtimeSinceStartup > _deadline) Abort("等战斗场景超时，练习场退出");
                return;
            }
            _settle++;
            if (_settle < SettleFrames) return;
            try
            {
                Present();
                _state = StateReady;
                _ctx.Log.Info("进场：本地对局状态已喂给 Refresh");
            }
            catch (Exception e)
            {
                _ctx.Log.Error("立起备战界面失败", e);
                Abort("立起备战界面失败，详见日志");
            }
        }

        /// <summary>
        /// BattleManager.OnStart 是异步的：先 ShowILRPanelAsync(BattlePanel)、再加载默认战斗执行器，最后在「回放」分支里
        /// 把 replaying 置 true。在那之前调 Refresh 会被悄悄丢掉、幕布永远不拉开（0.1.1 实机：场景请求后 1.7 秒就喂了状态，黑屏）。
        /// 所以等的是 replaying，而不只是 BattleManager.Instance 出现。
        /// </summary>
        static bool BattleReady()
        {
            if (SceneLoader.isLoading || SceneLoader.currentSceneName != "Battle") return false;
            BattleManager bm = BattleManager.Instance;
            if (bm == null || !bm.replaying || bm.defaultBattleExecuter == null) return false;
            BattlePanel bp = ILRPanelBase.FindILRPanel<BattlePanel>();
            return bp != null && bp.readyLayer != null;
        }

        void Abort(string message)
        {
            _state = StateIdle;
            Say(message);
        }

        /// <summary>玩家用游戏自己的方式回了大厅（或场景被别的东西换掉了）。</summary>
        public void OnLobby()
        {
            if (_state == StateIdle || _state == StateEntering) return;
            _state = StateIdle;
            _ctx.Log.Info("已回到大厅，练习场会话结束");
        }

        // ── 本地对局状态 ──────────────────────────────────────────────────

        static int BaseHp(int level)
        {
            LevelConfig config = CardFactory.FindLevelConfig((Level)level);
            return config != null ? config.baseMaxHp : 0;
        }

        static void CopyInts(Dictionary<int, int> from, Dictionary<int, int> to)
        {
            if (from == null) return;
            foreach (KeyValuePair<int, int> pair in from) to[pair.Key] = pair.Value;
        }

        static void PutBuff(Dictionary<int, int> buffs, BuffType type, int value)
        {
            int key = (int)type;
            if (value > 0) buffs[key] = value;
            else if (buffs.ContainsKey(key)) buffs.Remove(key);
        }

        static int ReadBuff(Dictionary<int, int> buffs, BuffType type)
        {
            int value;
            return buffs != null && buffs.TryGetValue((int)type, out value) ? value : 0;
        }

        /// <summary>
        /// 一方的公开数据。live 是上次从界面读回来的那份（炼化攒下的修为、仙命、常驻 buff 都在它上面），没有就从零开始。
        /// lastRoundData 是战斗引擎读的那一份，两边填成一样的。
        /// </summary>
        BattlePlayerData MakePublic(int index, string uid)
        {
            ArenaSide side = _sides[index];
            BattlePlayerData live = _live[index];
            int extra = side.ExtraMaxHp(BaseHp(side.Level));
            var d = new BattlePlayerData();
            d.uid = uid;
            d.username = side.Name;
            d.characterId = side.CharacterId;
            // 皮肤来自游戏自己的选英雄界面（只能选到拥有的）。皮肤键不对会让战斗卡死（旧练习场的事故），所以不接受别的来源。
            d.skinNumber = side.SkinNumber;
            d.skinColor = side.SkinColor;
            d.cardBack = 1;
            d.level = (Level)side.Level;
            d.life = 10;
            d.extraMaxHp = extra;
            if (live != null)
            {
                d.exp = live.exp;
                CopyInts(live.permanentBuffTempDatas, d.permanentBuffTempDatas);
            }
            // 仙命与它们的计数以设置为准（备战期游戏自己改的计数在 Capture 里读回了设置）。
            int[] talents = side.ChosenTalents();
            for (int i = 0; i < talents.Length; i++)
            {
                d.talents.Add(talents[i]);
                int value = side.ValueOfTalent(talents[i]);
                if (value > 0) d.talentTempDatas[talents[i]] = value;
            }
            PutBuff(d.permanentBuffTempDatas, BuffType.TiPo, side.TiPo);
            PutBuff(d.permanentBuffTempDatas, BuffType.TiPoShangXian, side.TiPoMax);

            BattlePlayerLastRoundData last = d.lastRoundData;
            last.level = d.level;
            last.life = d.life;
            last.exp = d.exp;
            last.extraMaxHp = extra;
            last.unlockGrids = ArenaSide.Grids;
            for (int i = 0; i < d.talents.Count; i++) last.talents.Add(d.talents[i]);
            CopyInts(d.talentTempDatas, last.talentTempDatas);
            CopyInts(d.permanentBuffTempDatas, last.permanentBuffTempDatas);
            for (int i = 0; i < ArenaSide.Grids; i++) last.usedCards.Add(side.Board[i]);
            for (int i = 0; i < side.Fates.Count; i++) last.fateStrategies.Add(side.Fates[i]);
            return d;
        }

        /// <summary>天衍仙命：战斗和备战界面读的都是 privateData.fateStrategyData.strategies 里 selected == id 的项。</summary>
        static void FillFates(BattlePlayerPrivateData priv, ArenaSide side)
        {
            priv.fateStrategyData.strategies.Clear();
            for (int i = 0; i < side.Fates.Count; i++)
            {
                var pick = new SelectionData();
                pick.id = i;
                pick.selected = side.Fates[i];
                priv.fateStrategyData.strategies.Add(pick);
            }
        }

        bool AnyFates()
        {
            return _sides[0].Fates.Count > 0 || _sides[1].Fates.Count > 0;
        }

        /// <summary>天衍仙命有「第几轮起生效」：把对局轮次定到所选的里最大的那个，保证都生效。</summary>
        int RoundForFates()
        {
            int round = 1;
            for (int s = 0; s < 2; s++)
            {
                List<int> fates = _sides[s].Fates;
                for (int i = 0; i < fates.Count; i++)
                {
                    FateStrategyConfig config = ConfigManager.GetFateStrategyConfig(fates[i]);
                    if (config != null && config.effectRound > round) round = config.effectRound;
                }
            }
            return round;
        }

        /// <summary>把正在编辑的一方当作「自己」喂给备战界面。</summary>
        void Present()
        {
            string myUid = GameClientUtil.uid;
            ArenaSide side = _sides[_editing];
            var gs = new GameStatus();
            gs.round = RoundForFates();
            gs.timer = 999;
            gs.gameMode = GameMode.PracticeMode;
            if (AnyFates()) gs.seasonMec = SeasonMechanismType.FateStrategy;
            BattlePlayerData self = MakePublic(_editing, myUid);
            BattlePlayerData other = MakePublic(1 - _editing, OtherUid);
            other.isAI = true;
            self.nextOpponent = OtherUid;
            other.nextOpponent = myUid;
            gs.battlePlayerDatas.Add(self);
            gs.battlePlayerDatas.Add(other);
            BattlePlayerPrivateData priv = gs.playerPrivateData;
            priv.uid = myUid;
            priv.unlockGrids = ArenaSide.Grids;
            for (int i = 0; i < side.Hand.Count; i++) priv.handCards.Add(side.Hand[i]);
            for (int i = 0; i < ArenaSide.Grids; i++) priv.usedCards.Add(side.Board[i]);
            FillFates(priv, side);
            BattleManager.Instance.Refresh(gs);
            HideReplaceArea();
        }

        /// <summary>
        /// 换牌本期不支持（要发包）；把换牌区藏起来，牌就拖不进去。Refresh 是异步的，之后可能又把它显示出来，
        /// 所以入口类在备战界面里每隔一会儿再调一次。
        /// </summary>
        public void HideReplaceArea()
        {
            try
            {
                CardPanel cp = FindCardPanel();
                Transform area = cp != null ? cp.GetReplaceTrans() : null;
                if (area != null)
                {
                    if (area.gameObject.activeSelf) area.gameObject.SetActive(false);
                    return;
                }
                if (!_warnedReplace) _ctx.Log.Warn("没找到换牌区，没能隐藏它——别往换牌区拖牌");
                _warnedReplace = true;
            }
            catch (Exception e)
            {
                if (!_warnedReplace) _ctx.Log.Warn("隐藏换牌区失败：" + e.Message);
                _warnedReplace = true;
            }
        }

        // ── 读界面 ────────────────────────────────────────────────────────

        static CardPanel FindCardPanel()
        {
            BattlePanel bp = ILRPanelBase.FindILRPanel<BattlePanel>();
            return bp != null ? bp.FindILRSubPanel<CardPanel>() : null;
        }

        /// <summary>备战界面自己持有的那份公开数据（炼化加的修为等都记在它上面）；取不到退回对局状态里的。</summary>
        static BattlePlayerData SelfPublicData(GameStatus gs)
        {
            BattlePanel bp = ILRPanelBase.FindILRPanel<BattlePanel>();
            if (bp != null && bp.readyLayer != null && bp.readyLayer.playerSelfInfoItem != null
                && bp.readyLayer.playerSelfInfoItem.battlePlayerData != null)
                return bp.readyLayer.playerSelfInfoItem.battlePlayerData;
            return gs.GetSelfBattlePlayerData() ?? gs.GetMainPlayerData();
        }

        /// <summary>把界面上的真实状态（玩家拖过牌、炼化过）读回正在编辑的一方。没有对局状态时返回 false。</summary>
        bool Capture()
        {
            return Capture(true);
        }

        /// <param name="talentValues">是否把备战界面里仙命的当前计数读回设置。刚在小窗里改过计数时不能读（界面上还是旧值，会把新值盖掉）。</param>
        bool Capture(bool talentValues)
        {
            BattleManager bm = BattleManager.Instance;
            GameStatus gs = bm != null ? bm.currentGameStatus : null;
            CardPanel cp = FindCardPanel();
            if (gs == null || cp == null) return false;
            ArenaSide side = _sides[_editing];

            side.Hand.Clear();
            List<CardItem> items = cp.GetHandCards();
            for (int i = 0; i < items.Count; i++)
                if (items[i] != null && items[i].cardInfo != null) side.Hand.Add(items[i].cardInfo.id);

            var board = new int[ArenaSide.Grids];
            int filled = 0;
            List<CardGrid> grids = cp.GetCardGrids();
            for (int i = 0; i < grids.Count && filled < ArenaSide.Grids; i++)
            {
                if (grids[i] == null || !grids[i].unlocked) continue;
                CardItem card = grids[i].GetCard();
                board[filled] = card != null && card.cardInfo != null ? card.cardInfo.id : 0;
                filled++;
            }
            side.SetBoard(board);

            BattlePlayerData live = SelfPublicData(gs);
            if (live != null)
            {
                _live[_editing] = live;
                side.CaptureExtra(BaseHp(side.Level), live.extraMaxHp);
                side.TiPo = ReadBuff(live.permanentBuffTempDatas, BuffType.TiPo);
                side.TiPoMax = ReadBuff(live.permanentBuffTempDatas, BuffType.TiPoShangXian);
                int[] talents = talentValues ? side.ChosenTalents() : new int[0];
                for (int i = 0; i < talents.Length; i++)
                {
                    int value;
                    if (live.talentTempDatas != null && live.talentTempDatas.TryGetValue(talents[i], out value)) side.CaptureTalentValue(talents[i], value);
                }
            }
            return true;
        }

        /// <summary>只刷新手牌 / 场上的牌（不重喂整份状态，牌不会闪）。</summary>
        void PushCards()
        {
            BattleManager bm = BattleManager.Instance;
            GameStatus gs = bm.currentGameStatus;
            ArenaSide side = _sides[_editing];
            BattlePlayerPrivateData priv = gs.playerPrivateData;
            priv.handCards.Clear();
            for (int i = 0; i < side.Hand.Count; i++) priv.handCards.Add(side.Hand[i]);
            priv.usedCards.Clear();
            for (int i = 0; i < ArenaSide.Grids; i++) priv.usedCards.Add(side.Board[i]);
            var pd = new PlayerData();
            pd.publicData = SelfPublicData(gs);
            pd.privateData = priv;
            bm.RefreshMainPlayerInfo(pd);
        }

        // ── 发牌 ──────────────────────────────────────────────────────────

        /// <summary>往正在编辑的一方的手牌末尾发一张牌。成功返回 true。</summary>
        public bool Deal(int cardId)
        {
            if (!InPlacement) { Say("只能在备战界面发牌"); return false; }
            try
            {
                if (!Capture()) return false;
                ArenaSide side = _sides[_editing];
                if (side.Hand.Count >= CardIds.HandLimit) { Say("手牌满了（" + N(CardIds.HandLimit) + " 张）"); return false; }
                side.Hand.Add(cardId);
                PushCards();
                return true;
            }
            catch (Exception e)
            {
                _ctx.Log.Error("发牌失败 id=" + N(cardId), e);
                return false;
            }
        }

        public void ClearHand()
        {
            if (!InPlacement) { Say("只能在备战界面清手牌"); return; }
            try
            {
                if (!Capture()) return;
                _sides[_editing].Hand.Clear();
                PushCards();
            }
            catch (Exception e) { _ctx.Log.Error("清空手牌失败", e); }
        }

        // ── 改设置（都要重喂一次状态）────────────────────────────────────────

        bool BeginEdit(string what)
        {
            if (!InPlacement) { Say("只能在备战界面" + what); return false; }
            return Capture();
        }

        /// <summary>仙命 / 天衍仙命 / 计数在小窗里改过了：带着现在的牌重新喂一次对局状态。</summary>
        public void ReapplySettings()
        {
            if (!InPlacement) return;
            try
            {
                if (!Capture(false)) return;
                Present();
                Persist();
            }
            catch (Exception e) { _ctx.Log.Error("重新应用仙命设置失败", e); }
        }

        public int EditingIndex { get { return _editing; } }

        /// <summary>切换正在编辑的一方（我 ↔ 对手）。</summary>
        public void SwitchSide()
        {
            try
            {
                if (!BeginEdit("切换编辑方")) return;
                _editing = 1 - _editing;
                Present();
                Persist();
                _ctx.Log.Info("现在编辑：" + _sides[_editing].Name);
            }
            catch (Exception e) { _ctx.Log.Error("切换编辑方失败", e); }
        }

        public void NextLevel()
        {
            try
            {
                if (!BeginEdit("改境界")) return;
                ArenaSide side = _sides[_editing];
                side.Level = side.Level >= ArenaConfig.MaxLevel || side.Level < ArenaConfig.MinLevel ? ArenaConfig.MinLevel : side.Level + 1;
                if (_editing == 0) _cfg.Level = side.Level;
                Present();
            }
            catch (Exception e) { _ctx.Log.Error("改境界失败", e); }
        }

        public void SetHp(string text)
        {
            try
            {
                if (!BeginEdit("改血量")) return;
                ArenaSide side = _sides[_editing];
                if (!side.SetHp(text)) { Say("血量要填最多 18 位的整数（0 = 跟随境界；超过 20 亿的部分进 64 位血量池）"); return; }
                if (_editing == 1) _cfg.DummyHp = side.Hp > ArenaSide.MaxNumber ? ArenaSide.MaxNumber : side.Hp;
                Present();
            }
            catch (Exception e) { _ctx.Log.Error("改血量失败", e); }
        }

        public void SetTiPo(string text)
        {
            try
            {
                if (!BeginEdit("改体魄")) return;
                if (!_sides[_editing].SetTiPo(text)) { Say("体魄要填 0–" + N(ArenaSide.MaxNumber) + " 的整数"); return; }
                Present();
            }
            catch (Exception e) { _ctx.Log.Error("改体魄失败", e); }
        }

        public void SetTiPoMax(string text)
        {
            try
            {
                if (!BeginEdit("改体魄上限")) return;
                if (!_sides[_editing].SetTiPoMax(text)) { Say("体魄上限要填 0–" + N(ArenaSide.MaxNumber) + " 的整数"); return; }
                Present();
            }
            catch (Exception e) { _ctx.Log.Error("改体魄上限失败", e); }
        }

        /// <summary>某一方开打时保留的手牌（0 = 我，1 = 对手）。与手牌有关的随机取值要用。</summary>
        public int[] HandOf(int side)
        {
            if (side < 0 || side > 1) return new int[0];
            List<int> hand = _sides[side].Hand;
            var ids = new int[hand.Count];
            for (int i = 0; i < hand.Count; i++) ids[i] = hand[i];
            return ids;
        }

        /// <summary>正在编辑的一方现在的总血量（显示用）。</summary>
        public long EditingTotalHp()
        {
            ArenaSide side = _sides[_editing];
            return side.TrueTotalHp(BaseHp(side.Level));
        }

        // ── 开打 ──────────────────────────────────────────────────────────

        public void Fight()
        {
            if (!InPlacement) { Say("只能在备战界面开打"); return; }
            try
            {
                if (!Capture()) return;
                int mine = _sides[0].PlacedCount();
                int theirs = _sides[1].PlacedCount();
                if (mine + theirs == 0) { Say("场上一张牌都没有，先摆牌"); return; }

                string myUid = GameClientUtil.uid;
                var r = new BattleResult();
                r.battleScene = 1;
                r.battleSubScene = 1;
                r.battleTime = 1800;
                r.round = RoundForFates();
                r.gameMode = GameMode.PracticeMode;
                r.p1.publicData = MakePublic(0, myUid);
                r.p2.publicData = MakePublic(1, OtherUid);
                r.p1.privateData.uid = myUid;
                r.p2.privateData.uid = OtherUid;
                FillFates(r.p1.privateData, _sides[0]);
                FillFates(r.p2.privateData, _sides[1]);
                Persist();
                r.firstPlayerId = _cfg.PlayerFirst ? myUid : OtherUid;
                r.homePlayerId = myUid;
                r.mainViewId = myUid;

                // 随机数流平时由服务器预生成；有两处取用是裸 Dequeue，空了会抛并被战斗循环吞掉，所以给足。
                _battleIndex++;
                LastSeed = ParamStream.MixSeed(Time.frameCount, _battleIndex);
                int[] stream = ParamStream.Generate(LastSeed, ParamCount);
                for (int i = 0; i < stream.Length; i++) r.battleParams.Add(stream[i]);

                BattleManager.currentBattleResult = r;
                BattleManager.Instance.PlayBattle();
                string mineText = N(mine) + " 张 / " + DamageTally.Group(TrueTotalHp(0)) + " 血";
                string theirsText = N(theirs) + " 张 / " + DamageTally.Group(TrueTotalHp(1)) + " 血";
                _ctx.Log.Info("开打：我方 " + mineText + "，对手 " + theirsText + "，种子 " + LastSeed.ToString(CultureInfo.InvariantCulture));
            }
            catch (Exception e) { _ctx.Log.Error("开打失败", e); }
        }

        // ── 离场 ──────────────────────────────────────────────────────────

        public void Leave()
        {
            if (_state == StateIdle) { Say("不在练习场里"); return; }
            if (_state == StateLeaving) return;
            Autosave();
            string guard = RoomGuard.Reason();
            if (guard != null) { _state = StateIdle; Say(guard); return; }
            try
            {
                BattleManager bm = BattleManager.Instance;
                BattleExecuter be = bm != null ? bm.defaultBattleExecuter : null;
                if (be != null && be.isExecuting)
                {
                    // 战斗在演时直接切场景，异步战斗链会在场景销毁后抛 NullReference（探针实机）。
                    // 先走回放面板自己的退出流程：打断执行器 → 等停 → 回修炼阶段。
                    BattlePanel bp = ILRPanelBase.FindILRPanel<BattlePanel>();
                    BattleReplayPanel rp = bp != null ? bp.FindILRSubPanel<BattleReplayPanel>() : null;
                    if (rp != null) rp.OnExitBtnClick();
                    else be.forceBreakExecuting = true;
                    _state = StateLeaving;
                    _deadline = Time.realtimeSinceStartup + LeaveTimeoutSeconds;
                    return;
                }
            }
            catch (Exception e) { _ctx.Log.Error("离场前收尾失败（照常回大厅）", e); }
            ToLobby();
        }

        void TickLeaving()
        {
            BattleManager bm = BattleManager.Instance;
            BattleExecuter be = bm != null ? bm.defaultBattleExecuter : null;
            bool stopped = be == null || !be.isExecuting;
            if (stopped || Time.realtimeSinceStartup > _deadline) ToLobby();
        }

        void ToLobby()
        {
            _state = StateIdle;
            try
            {
                BattleManager.currentBattleResult = null;
                if (!SceneLoader.isLoading && SceneLoader.currentSceneName == "Battle") SceneLoader.LoadScene("Lobby");
                _ctx.Log.Info("离场：已请求回大厅");
            }
            catch (Exception e) { _ctx.Log.Error("回大厅失败", e); }
        }
    }
}
