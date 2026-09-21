using System;
using UnityEngine;
using Yx.ModSdk;
using Yx.ModSdk.Unity;
using YxArena.Game;
using YxArena.Views;

namespace YxArena
{
    /// <summary>
    /// 练习场：完全本地的练习对局（不进房间、不发包）。模式选择 → 单人模式 →「练习场」（或 CTRL+ALT+1）进场；备战界面上边缘有控制栏。
    /// 设计见 docs/superpowers/specs/2026-09-19-practice-arena-design.md。
    /// </summary>
    public sealed class ArenaMod : YxMod
    {
        const int SlowTickFrames = 30;
        const int BattleTickFrames = 10;
        const int AutosaveFrames = 300;

        readonly ArenaConfig _cfg = new ArenaConfig();
        OfflineHooks _hooks;
        ArenaSession _session;
        DealHook _deal;
        ReadyHook _ready;
        DrawHooks _draws;
        LobbyEntry _entry;
        TalentTips _tips;
        BoxSearch _boxSearch;
        UiKit _ui;
        BattleHooks _battle;
        Overflow _overflow;
        Limits _limits;
        NativeSwitch _nativeSwitch;
        ConfigEntry<int> _switchOffsetX;
        ConfigEntry<int> _switchOffsetY;
        bool _fightPending;
        bool _tallyOpen = true;
        bool _wasBattle;
        BattleBar _battleBar;
        SetupWindow _setup;
        ArenaRoom _room;
        RoomStrip _strip;
        readonly DamageTally _tally = new DamageTally();
        readonly StepController _step = new StepController();
        ControlBar _bar;
        readonly DealFeed _feed = new DealFeed();
        readonly ControlBarState _barState = new ControlBarState();
        bool _inLobby;
        bool _roomWasOpen;
        string _lastQuery = "";
        int _frame;

        public override void OnLoad(ModContext ctx)
        {
            Loc.En = ctx.Lang == "en";      // 没有 ModContext 的界面 / 静态帮助类经 Loc.T 取当前语言的文案
            // SDK 的界面工具：面板 / 按钮 / 输入框；回调自动兜底（异常记在本 mod 头上）。所有界面类共用这一个。
            _ui = new UiKit(ctx);
            LoadConfig(ctx);
            _hooks = new OfflineHooks(ctx);
            _hooks.Install();
            _session = new ArenaSession(ctx, _cfg, _hooks);
            _deal = new DealHook(ctx, _cfg, _session, _feed.Add);
            _deal.Install();
            _ready = new ReadyHook(ctx, _hooks, DoFight);
            _ready.Install();
            _draws = new DrawHooks(ctx, _session);
            _draws.Install();
            _nativeSwitch = new NativeSwitch(ctx, DoSwitchSide);
            _nativeSwitch.Install();
            _switchOffsetX = ctx.Config.Bind("layout", "switchOffsetX", 24, ctx.T("游戏自带的「切换」按钮：离「修为 / 血量」右边缘多远（往右为正）", "Game's own Switch button: distance right of the Exp / HP row's right edge (positive = right)"));
            _switchOffsetY = ctx.Config.Bind("layout", "switchOffsetY", 0, ctx.T("游戏自带的「切换」按钮：相对「修为 / 血量」那一行的上下偏移（往上为正）", "Game's own Switch button: vertical offset from the Exp / HP row (positive = up)"));
            _limits = new Limits(ctx);
            _limits.Mode = LimitModes.Normalize(ctx.Data.Get<int>("limitMode", LimitModes.Original));
            _limits.Install();
            _overflow = new Overflow(ctx);
            _overflow.Enabled = ctx.Data.Get<bool>("overflow", true);
            _battle = new BattleHooks(ctx, _tally, _step, _overflow, _session);
            _battle.Install();
            _battleBar = new BattleBar(_ui, OnStep, OnTogglePauseAtStart, OnToggleTally);
            _tallyOpen = ctx.Data.Get<bool>("tallyOpen", true);
            _tips = new TalentTips(ctx);
            _tips.Install();
            _boxSearch = new BoxSearch(_ui, ctx);
            _boxSearch.Install();
            _setup = new SetupWindow(_ui, ctx, _session, OnSetupChanged, _tips.Hide, _boxSearch);
            _room = new ArenaRoom(ctx, _session, DoEnter, OnRoomPickTalent);
            _room.Install();
            _strip = new RoomStrip(_ui, OnRoomBack, OnRoomSwitchSide, OnRoomTalents, OnRoomFates);
            if (ctx.Hooks.TryPrefix("BattleTalentIconItem", "OnPointerClick", 1, OnReadyTalentIconClick) == null)
                ctx.Log.Warn(ctx.T("备战界面的仙命图标点不了：用控制栏的「仙命…」", "Talent icons on the setup screen aren't clickable: use \"Talents…\" on the control bar"));
            _entry = new LobbyEntry(ctx, OpenSetup);
            _entry.Install();
            _step.PauseAtStart = ctx.Data.Get<bool>("pauseAtStart", false);

            var handlers = new ControlBarHandlers();
            handlers.ToggleCollapsed = OnToggleCollapsed;
            handlers.SwitchSide = OnSwitchSide;
            handlers.SetHp = OnSetHp;
            handlers.SetTiPo = OnSetTiPo;
            handlers.SetTiPoMax = OnSetTiPoMax;
            handlers.Search = OnSearch;
            handlers.OpenGallery = OnOpenGallery;
            handlers.OpenSpecial = OnOpenSpecial;
            handlers.OpenTalents = OnOpenTalents;
            handlers.NextSpecial = OnNextSpecial;
            handlers.ToggleDealMode = OnToggleDealMode;
            handlers.NextRarity = OnNextRarity;
            handlers.NextLevel = OnNextLevel;
            handlers.ToggleFirst = OnToggleFirst;
            handlers.ClearHand = OnClearHand;
            handlers.Fight = OnFight;
            handlers.TogglePauseAtStart = OnTogglePauseAtStart;
            handlers.ToggleOverflow = OnToggleOverflow;
            handlers.NextLimitMode = OnNextLimitMode;
            _bar = new ControlBar(_ui, handlers);

            // 热键是按钮的后备（自绘按钮不灵时仍能用）。用数字键：CTRL+ALT+字母在中文桌面上常被 QQ / 输入法之类的全局热键
            // 吃掉（0.1.0 的 CTRL+ALT+P 实机按了没反应），CTRL+ALT+数字探针实机用过。id 换了名字，免得沿用存档里的旧绑定。
            ctx.Input.RegisterHotkey("arena-enter", "CTRL+ALT+1", OnOpenSetup);
            ctx.Input.RegisterHotkey("arena-gallery", "CTRL+ALT+2", OnOpenGallery);
            ctx.Input.RegisterHotkey("arena-fight", "CTRL+ALT+3", OnFight);
            ctx.Input.RegisterHotkey("arena-leave", "CTRL+ALT+4", OnLeave);
            ctx.MainThread.EveryFrame(Tick);

            if (_hooks.Complete) ctx.Log.Info(ctx.T("练习场就绪：模式选择 → 单人模式 →「练习场」，或 CTRL+ALT+1", "Arena ready: Mode Select → Single Player → \"Arena\", or CTRL+ALT+1"));
            else ctx.Log.Warn(ctx.T("离线保护不完整，练习场禁止进场。没挂上：", "Offline protection incomplete; entering the arena is blocked. Missing: ") + _hooks.Missing);
        }

        // ── 存档 ──────────────────────────────────────────────────────────

        void LoadConfig(ModContext ctx)
        {
            _cfg.CharacterId = ctx.Data.Get<int>("char", ArenaConfig.DefaultCharacterId);
            _cfg.Level = ctx.Data.Get<int>("level", ArenaConfig.MinLevel);
            _cfg.DummyHp = ctx.Data.Get<int>("dummyHp", ArenaConfig.DefaultDummyHp);
            _cfg.PlayerFirst = ctx.Data.Get<bool>("playerFirst", true);
            _cfg.Rarity = ctx.Data.Get<int>("rarity", 0);
            _cfg.Normalize();
        }

        void SaveConfig()
        {
            Context.Data.Set("char", _cfg.CharacterId);
            Context.Data.Set("level", _cfg.Level);
            Context.Data.Set("dummyHp", _cfg.DummyHp);
            Context.Data.Set("playerFirst", _cfg.PlayerFirst);
            Context.Data.Set("rarity", _cfg.Rarity);
        }

        // ── 每帧 ──────────────────────────────────────────────────────────

        void Tick()
        {
            _hooks.Tick();
            _session.Tick();
            _feed.Tick();
            _setup.Tick();
            _frame++;
            if (_fightPending) TickOverflow();
            if (_frame % BattleTickFrames == 0) TickBattleBar();
            if (_frame % AutosaveFrames == 0) _session.Autosave();
            if (_frame % BattleTickFrames == 0) TickRoom();
            if (_frame % SlowTickFrames != 0) return;
            TickLobbyEntry();
            bool show = _session.InPlacement;
            if (show)
            {
                _nativeSwitch.OffsetX = _switchOffsetX.Value;
                _nativeSwitch.OffsetY = _switchOffsetY.Value;
                _nativeSwitch.Ensure(_session.EditingOpponent);
            }
            // 换边用游戏自己的「切换」按钮；借不到（钩子不全 / 控件加载失败）时控制栏才画自己的那个。状态变了就重建控制栏。
            if (_bar.NativeSide != _nativeSwitch.Available)
            {
                _bar.Destroy();
                _bar.NativeSide = _nativeSwitch.Available;
            }
            _bar.SetVisible(show);
            if (!show)
            {
                if (_setup.IsOpen && !_room.IsOpen) _setup.Close();
                return;
            }
            _session.HideReplaceArea();
            RefreshBar();
        }

        // ── 战斗里的步进条与伤害统计 ─────────────────────────────────────────

        void TickBattleBar()
        {
            bool show = _session.InBattlePhase;
            _battleBar.SetVisible(show);
            if (!show && _wasBattle) _battle.LogReport();
            _wasBattle = show;
            if (!show) return;
            string text = _battle.TallyAvailable ? _battle.Report(6) : "";
            _battleBar.Refresh(_battle.StepAvailable, _step.PauseAtStart, BattleHooks.IsPaused, _tallyOpen, text);
        }

        void OnStep() { Context.Guard(Context.T("步进", "Step"), _battle.Step); }

        void OnToggleTally()
        {
            _tallyOpen = !_tallyOpen;
            Context.Data.Set("tallyOpen", _tallyOpen);
            Context.Guard(Context.T("伤害统计窗", "Damage window"), TickBattleBar);
        }

        void OnTogglePauseAtStart()
        {
            _step.PauseAtStart = !_step.PauseAtStart;
            Context.Data.Set("pauseAtStart", _step.PauseAtStart);
            RefreshBar();
        }

        // ── 单人模式页里的入口 ─────────────────────────────────────────────
        // 大厅主界面上原来还有一个自绘的后备按钮，用户 2026-09-20 要求去掉：入口只剩单人模式页里的「练习场」和 CTRL+ALT+1。

        void TickLobbyEntry()
        {
            bool show = _inLobby && _session.State == ArenaSession.StateIdle && !SceneLoader.isLoading
                        && SceneLoader.currentSceneName == "Lobby" && RoomGuard.Reason() == null;
            if (show) _entry.Tick();
        }

        public override void OnSceneChanged(GameScene scene)
        {
            _inLobby = scene == GameScene.Lobby;
            if (!_inLobby)
            {
                _entry.OnLeftLobby();
                CloseRoomUi();
            }
            if (scene == GameScene.Lobby)
            {
                _limits.Disarm();
                _session.OnLobby();
                _bar.Destroy();
                _feed.Destroy();
                _battleBar.Destroy();
            }
        }

        public override void OnDisable()
        {
            if (_limits != null) _limits.Disarm();
            _feed.Destroy();
            if (_battleBar != null) _battleBar.Destroy();
            if (_room != null && _room.IsOpen) _room.Close();
            CloseRoomUi();
            if (_bar != null) _bar.Destroy();
            if (_session != null && _session.State != ArenaSession.StateIdle) _session.Leave();
            if (_ui != null) _ui.DestroyAll();          // 兜底：UiKit 建过的面板一个不留
        }

        // ── 动作（按钮与热键共用）。Context.Guard：出错记在本 mod 头上，不往游戏里漏；UiKit 的按钮回调本身也兜了一层 ──────────

        void RefreshBar()
        {
            if (!_bar.IsAlive) return;
            ArenaSide side = _session.Editing;
            _barState.DealMode = _deal.DealMode;
            _barState.Rarity = _cfg.Rarity;
            _barState.Level = side.Level;
            _barState.PlayerFirst = _cfg.PlayerFirst;
            _barState.EditingOpponent = _session.EditingOpponent;
            _barState.SideName = side.Name;
            _barState.TotalHp = _session.EditingTotalHp();
            _barState.TiPo = side.TiPo;
            _barState.TiPoMax = side.TiPoMax;
            _barState.PauseAtStart = _step.PauseAtStart;
            _barState.Overflow = _overflow.Enabled;
            _barState.LimitMode = _limits.Mode;
            _barState.SpecialName = YxArena.Draws.SpecialCards.Name(_deal.SpecialCategory);
            _barState.Status = Context.T("正在编辑：", "Editing: ") + side.Name + "　" + _hooks.Summary() + "　" + _draws.Summary() + "　" + _overflow.Status() + "　" + _limits.Status();
            _bar.Refresh(_barState);
        }

        static string N(int value) { return value.ToString(System.Globalization.CultureInfo.InvariantCulture); }

        void OnOpenSetup() { Context.Guard(Context.T("打开设置窗", "Open setup window"), OpenSetup); }

        /// <summary>入口 / 大厅按钮 / 热键都先到选英雄界面（游戏自己的单人房间）；点它的「开始」才真的进场。</summary>
        void OpenSetup()
        {
            if (!_inLobby || SceneLoader.isLoading || SceneLoader.currentSceneName != "Lobby") { Ui.Toast(Context.T("只能在大厅里打开练习场", "The arena can only be opened from the lobby")); return; }
            if (_room.IsOpen) return;
            if (!_room.Available) { DoEnter(); return; }      // 钩子没挂上：退回「直接用上次的角色进场」
            _room.Open();
        }

        // ── 选英雄界面上的小条 ───────────────────────────────────────────────

        void TickRoom()
        {
            _room.Tick();
            bool open = _room.IsOpen;
            _strip.SetVisible(open && !_setup.IsOpen);
            // 选英雄界面刚关掉：连带关掉从它上面打开的小窗。（不能写成「没开就关」——备战界面里也会开这个小窗。）
            if (!open)
            {
                if (_roomWasOpen && _setup.IsOpen) _setup.Close();
                _roomWasOpen = false;
                return;
            }
            _roomWasOpen = true;
            _strip.Refresh(_room.Side == 1, _session.SideAt(_room.Side).Fates.Count);
        }

        void CloseRoomUi()
        {
            if (_strip != null) _strip.Destroy();
            if (_setup != null) _setup.Close();
        }

        void OnRoomBack() { Context.Guard(Context.T("返回", "Back"), _room.Close); }

        void OnRoomSwitchSide() { Context.Guard(Context.T("切换我方 / 对手", "Switch mine / foe"), _room.SwitchSide); }

        void OnRoomFates() { Context.Guard(Context.T("打开天衍仙命", "Open fates"), OpenFates); }

        void OpenFates() { _setup.OpenFates(_room.Side); }

        void OnRoomTalents() { Context.Guard(Context.T("打开仙命", "Open talents"), OpenRoomTalents); }

        void OpenRoomTalents() { _setup.OpenSlots(_room.Side, false); }

        /// <summary>选英雄界面上点了角色旁第 slot 个仙命图标：直接弹游戏的全仙命选择框，选完就结束。</summary>
        void OnRoomPickTalent(int slot, RectTransform icon) { _setup.PickTalentDirect(_room.Side, slot, icon); }

        // ── 备战界面里改仙命 ─────────────────────────────────────────────────

        void OnOpenTalents() { Context.Guard(Context.T("打开仙命", "Open talents"), OpenArenaTalents); }

        void OpenArenaTalents()
        {
            if (!_session.InPlacement) { Ui.Toast(Context.T("只能在备战界面改仙命", "Talents can only be changed on the setup screen")); return; }
            _session.Autosave();      // 先把界面上的现状（牌、仙命计数）读回设置
            _setup.OpenSlots(_session.EditingIndex, true);
        }

        /// <summary>备战界面上点仙命图标：练习场里改成打开仙命小窗（原逻辑是看说明 / 向服务器选仙命）。</summary>
        bool OnReadyTalentIconClick(Yx.Shared.HookContext h)
        {
            if (!ArenaSession.Active || !_session.InPlacement) return true;
            h.Skip(null);
            try
            {
                // 点的是哪个仙命 → 它在正在编辑的一方的第几个槽 → 直接弹选择框换掉它。对不上（比如点的是对手那排）就开小窗。
                BattleTalentIconItem icon = h.Instance as BattleTalentIconItem;
                _session.Autosave();
                ArenaSide side = _session.Editing;
                int slot = -1;
                if (icon != null && icon.talentId > 0)
                    for (int i = 0; i < ArenaSide.TalentSlots; i++) if (side.Talents[i] == icon.talentId) slot = i;
                if (slot >= 0) _setup.PickTalentDirect(_session.EditingIndex, slot, icon.transform as RectTransform);
                else _setup.OpenSlots(_session.EditingIndex, true);
            }
            catch (Exception e) { Context.Log.Error(Context.T("点仙命图标 出错", "Talent icon click failed"), e); }
            return false;
        }

        /// <summary>小窗里改了仙命 / 天衍仙命 / 计数：在备战界面里就重新喂一次对局状态（大厅里由选英雄界面自己对图标）。</summary>
        void OnSetupChanged()
        {
            if (_session.InPlacement) _session.ReapplySettings();
        }

        void DoEnter()
        {
            Context.Log.Info(Context.T("收到：进场", "Received: enter"));
            _session.Enter();
        }

        void OnOpenGallery() { Context.Guard(Context.T("打开图鉴", "Open gallery"), _deal.OpenGallery); }

        void OnOpenSpecial() { Context.Guard(Context.T("打开特殊牌", "Open special cards"), _deal.OpenSpecial); }

        // 输入框失焦也会触发 onEndEdit（比如去点搜出来的牌）：只有按了回车、或者文字变了才重新搜，免得面板被反复刷新。
        void OnSearch(string text)
        {
            bool enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
            string query = text == null ? "" : text.Trim();
            if (!enter && query == _lastQuery) return;
            _lastQuery = query;
            try { _deal.Search(query); }
            catch (Exception e) { Context.Log.Error(Context.T("搜牌 出错", "Card search failed"), e); }
        }

        void OnNextSpecial()
        {
            _deal.SpecialCategory = YxArena.Draws.SpecialCards.Next(_deal.SpecialCategory);
            RefreshBar();
        }

        void OnClearHand() { Context.Guard(Context.T("清空手牌", "Clear hand"), _session.ClearHand); }

        void OnFight() { Context.Guard(Context.T("开打", "Fight"), DoFight); }

        /// <summary>
        /// 开打。「破限」开着时，第一次开打（和场上有没处理过的牌时）要先把战斗代码改写成饱和算术——分帧做，做完自动开打。
        /// </summary>
        void DoFight()
        {
            if (_fightPending) return;
            if (_session.InPlacement) _limits.Prepare();
            if (_session.InPlacement && _overflow.Enabled)
            {
                _session.Autosave();      // 先把场上的牌读回设置，才知道要处理哪些牌的类
                if (!_overflow.Prepare(BoardCards()))
                {
                    _fightPending = true;
                    Ui.Toast(Context.T("正在准备破限（只有第一次开打和上了新牌时需要），好了自动开打", "Preparing overflow (needed only on first fight and when new cards are added); will start automatically when ready"));
                    RefreshBar();
                    return;
                }
            }
            StartFight();
        }

        void StartFight()
        {
            _bar.SetVisible(false);
            _session.Fight();
        }

        int[] BoardCards()
        {
            ArenaSide mine = _session.SideAt(0);
            ArenaSide theirs = _session.SideAt(1);
            var cards = new int[mine.Board.Count + theirs.Board.Count];
            for (int i = 0; i < mine.Board.Count; i++) cards[i] = mine.Board[i];
            for (int i = 0; i < theirs.Board.Count; i++) cards[mine.Board.Count + i] = theirs.Board[i];
            return cards;
        }

        void TickOverflow()
        {
            if (!_overflow.Tick())
            {
                if (_frame % BattleTickFrames == 0) RefreshBar();
                return;
            }
            _fightPending = false;
            RefreshBar();
            if (_session.InPlacement) Context.Guard(Context.T("开打", "Fight"), StartFight);
        }

        void OnNextLimitMode()
        {
            _limits.Mode = LimitModes.Next(_limits.Mode);
            Context.Data.Set("limitMode", _limits.Mode);
            RefreshBar();
        }

        void OnToggleOverflow()
        {
            _overflow.Enabled = !_overflow.Enabled;
            Context.Data.Set("overflow", _overflow.Enabled);
            RefreshBar();
        }

        void OnLeave() { Context.Guard(Context.T("回大厅", "Back to lobby"), DoLeave); }

        void DoLeave()
        {
            Context.Log.Info(Context.T("收到：回大厅", "Received: back to lobby"));
            _bar.Destroy();
            _nativeSwitch.Hide();
            _session.Leave();
        }

        void OnToggleDealMode()
        {
            _deal.DealMode = !_deal.DealMode;
            RefreshBar();
        }

        void OnNextRarity()
        {
            _cfg.NextRarity();
            SaveConfig();
            RefreshBar();
        }

        void OnToggleFirst()
        {
            _cfg.PlayerFirst = !_cfg.PlayerFirst;
            SaveConfig();
            RefreshBar();
        }

        void OnNextLevel() { Context.Guard(Context.T("切换境界", "Switch realm"), DoNextLevel); }

        void DoNextLevel()
        {
            _session.NextLevel();
            SaveConfig();
            RefreshBar();
        }

        void OnToggleCollapsed() { Context.Guard(Context.T("收起 / 展开", "Collapse / expand"), _bar.ToggleCollapsed); }

        void OnSwitchSide() { Context.Guard(Context.T("切换编辑方", "Switch editing side"), DoSwitchSide); }

        void DoSwitchSide()
        {
            _session.SwitchSide();
            RefreshBar();
        }

        // 输入框失焦也会触发 onEndEdit：值没变就什么都不做，免得白白重喂一次状态（牌会闪一下）。

        void OnSetHp(string text)
        {
            try
            {
                if (text == null || text.Trim() == _session.EditingTotalHp().ToString(System.Globalization.CultureInfo.InvariantCulture)) return;
                _session.SetHp(text);
                SaveConfig();
                RefreshBar();
            }
            catch (Exception e) { Context.Log.Error(Context.T("改血量 出错", "Set HP failed"), e); }
        }

        void OnSetTiPo(string text)
        {
            try
            {
                if (text == null || text.Trim() == N(_session.Editing.TiPo)) return;
                _session.SetTiPo(text);
                RefreshBar();
            }
            catch (Exception e) { Context.Log.Error(Context.T("改体魄 出错", "Set body failed"), e); }
        }

        void OnSetTiPoMax(string text)
        {
            try
            {
                if (text == null || text.Trim() == N(_session.Editing.TiPoMax)) return;
                _session.SetTiPoMax(text);
                RefreshBar();
            }
            catch (Exception e) { Context.Log.Error(Context.T("改体魄上限 出错", "Set body max failed"), e); }
        }
    }
}
