using System;
using UnityEngine;
using UnityEngine.UI;
using Yx.ModSdk;
using Yx.ModSdk.Hooks;
using Yx.Shared;

namespace YxArena.Game
{
    /// <summary>
    /// 步进与伤害统计的战斗钩子。
    ///
    ///   BattleManager.PlayBattle/1          前置：新的一场（含重播）→ 统计清零、步进复位
    ///   CardActionBase.CheckCardCost/2      前置：每张牌的必经点（同步、全游戏只有主循环里一处调用，再次行动 / 连击各经过一次）
    ///                                       → 记下「现在是谁在出哪张牌」；步进要停就在这里停
    ///   BattleCharacter.OnTurnStarted/0     前置：回合数
    ///   BattleCharacter.ApplyDamage/2       前置：Instance 是出手方，damageInfo.damage 是减防前的伤害
    ///   BattleCharacter.OnHit/1             前置：记下这一击减防后的真实伤害；后置：Instance 是受击方，返回值是实际掉血
    ///   BattleCharacterUI.AddTalentBuff/1   前置：同一个仙命占了几个槽时，战斗界面的图标表只加一次（它拿仙命 id 当字典键，
    ///                                       加第二次会抛异常，整场战斗起不来——0.10.3 / 0.11.0 实机卡住就是这个）
    ///   BattleCharacter.ModifyHp/5          后置：64 位血量池——游戏每次改完血量，把「游戏里的 hp + 池子余量」重新分配
    ///                                       （OnHit 是在 ModifyHp 返回之后才判定死亡的，所以这里把 hp 补回来就不会死）
    ///
    /// 「破限」开着时战斗公式是饱和算术：游戏里的 int 到 2147483647 就停，那条运算链的 64 位真值由 SatMath 记着。
    /// ApplyDamage 进来的伤害正好是被钉住的上限、SatMath 又有真值时，统计用真值（每张牌开始时清一次，免得串到下一张）。
    ///
    /// 暂停 / 继续一律点游戏回放面板自己的播放按钮（与玩家点是同一个入口），不直接写 Time.timeScale。
    /// 处理器里的异常自己接住：交给 SDK 的话会计入错误预算，几次就把整个 mod 熔断了。
    /// </summary>
    public sealed class BattleHooks
    {
        readonly ModContext _ctx;
        readonly DamageTally _tally;
        readonly StepController _step;
        readonly Overflow _overflow;
        readonly ArenaSession _session;
        readonly BigHpPool[] _pools = { new BigHpPool(), new BigHpPool() };
        readonly long[] _totals = new long[2];
        readonly OncePerOwner _talentIcons = new OncePerOwner();
        HookGroup _group;
        long _hitTrue;
        long _hitPoolLoss;
        bool _poolHooked;
        bool _stepHooked;
        bool _damageHooked;
        bool _structReadBroken;
        bool _warned;
        bool _pending;
        bool _reported = true;
        int _pendingSide;
        long _pendingDamage;

        public BattleHooks(ModContext ctx, DamageTally tally, StepController step, Overflow overflow, ArenaSession session)
        {
            _session = session;
            _ctx = ctx;
            _tally = tally;
            _step = step;
            _overflow = overflow;
        }

        public bool StepAvailable { get { return _stepHooked; } }

        public bool TallyAvailable { get { return _damageHooked; } }

        public void Install()
        {
            _group = _ctx.Hooks.Group("战斗");
            bool play = Try("BattleManager", "PlayBattle", 1, OnPlayBattle);
            bool card = Try("CardActionBase", "CheckCardCost", 2, OnCardAboutToPlay);
            _stepHooked = play && card;
            Try("BattleCharacter", "OnTurnStarted", 0, OnTurnStarted);
            bool apply = Try("BattleCharacter", "ApplyDamage", 2, OnApplyDamage);
            bool hit = _group.Postfix("BattleCharacter", "OnHit", 1, OnHitDone) != null;
            _damageHooked = play && card && apply && hit;
            Try("BattleCharacterUI", "AddTalentBuff", 1, OnAddTalentIcon);
            Try("BattleCharacterUI", "ResetBuffItem", 0, OnResetIcons);
            bool hitStart = Try("BattleCharacter", "OnHit", 1, OnHitStart);
            _poolHooked = _group.Postfix("BattleCharacter", "ModifyHp", 5, OnHpModified) != null && play && hitStart;
            if (!_poolHooked) _ctx.Log.Warn("64 位血量池不可用：血量超过 20 亿的部分不生效");
        }

        bool Try(string type, string method, int paramCount, Func<HookContext, bool> handler)
        {
            return _group.Prefix(type, method, paramCount, handler) != null;
        }

        void Broken(string where, Exception e)
        {
            if (!_warned) _ctx.Log.Error("战斗钩子 " + where + " 出错（之后不再报）", e);
            _warned = true;
        }

        static int SideOf(object character)
        {
            BattleCharacter c = character as BattleCharacter;
            if (c == null || c.battleExecuter == null) return -1;
            return c == c.battleExecuter.leftCharacter ? 0 : 1;
        }

        // ── 处理器 ────────────────────────────────────────────────────────

        bool OnPlayBattle(HookContext h)
        {
            if (!ArenaSession.Active) return true;
            LogReport();          // 重播：上一遍的统计先记进日志
            _reported = false;
            _tally.Reset();
            _step.OnBattleStart();
            _pending = false;
            SatMath.ClearTrue();
            _talentIcons.Reset();
            _hitTrue = 0L;
            _hitPoolLoss = 0L;
            for (int i = 0; i < 2; i++)
            {
                _totals[i] = _poolHooked ? _session.TrueTotalHp(i) : 0L;
                _pools[i].Reset(_totals[i], _session.GameTotalHp(i));
            }
            return true;
        }

        bool OnCardAboutToPlay(HookContext h)
        {
            if (!ArenaSession.Active) return true;
            try
            {
                FlushPending();
                CardActionBase action = h.Instance as CardActionBase;
                int side = SideOf(h.Args != null && h.Args.Length > 0 ? h.Args[0] : null);
                string name = action != null && action.cardConfig != null ? action.cardConfig.name : "";
                _tally.BeginCard(side, name);
                SatMath.ClearTrue();
                if (action != null && action.cardConfig != null) _overflow.OnCardAboutToPlay(action.cardConfig.id);
                if (_step.ShouldPauseBeforeCard()) SetPaused(true);
            }
            catch (Exception e) { Broken("CheckCardCost", e); }
            return true;
        }

        bool OnTurnStarted(HookContext h)
        {
            if (!ArenaSession.Active) return true;
            try
            {
                FlushPending();
                _tally.BeginTurn(SideOf(h.Instance));
            }
            catch (Exception e) { Broken("OnTurnStarted", e); }
            return true;
        }

        bool OnApplyDamage(HookContext h)
        {
            if (!ArenaSession.Active) return true;
            try
            {
                FlushPending();
                if (h.Args == null || h.Args.Length < 2) return true;
                if (object.ReferenceEquals(h.Args[0], h.Instance)) return true;     // 打自己的不算「造成的伤害」
                _pendingSide = SideOf(h.Instance);
                _pendingDamage = -1L;
                if (!_structReadBroken)
                {
                    try
                    {
                        DamageInfo info = (DamageInfo)h.Args[1];
                        _pendingDamage = TrueDamage(info.damage);
                    }
                    catch (Exception)
                    {
                        // DamageInfo 是 struct：装箱的参数读不出来就只按实际掉血统计。
                        _structReadBroken = true;
                        _ctx.Log.Warn("伤害统计：读不到减防前的伤害值，之后「总伤害」按实际掉血算");
                    }
                }
                _pending = true;
            }
            catch (Exception e) { Broken("ApplyDamage", e); }
            return true;
        }

        /// <summary>被钉在上限的伤害：SatMath 记着这条运算链的真值就用真值。</summary>
        static long TrueDamage(int damage)
        {
            long value = damage;
            if (damage == int.MaxValue && SatMath.HasTrue && SatMath.TrueValue > value) return SatMath.TrueValue;
            return value;
        }

        /// <summary>游戏清空了这个角色的图标表：去重记录跟着清。</summary>
        bool OnResetIcons(HookContext h)
        {
            _talentIcons.Forget(h.Instance);
            return true;
        }

        bool OnAddTalentIcon(HookContext h)
        {
            if (!ArenaSession.Active) return true;
            if (h.Args == null || h.Args.Length < 1 || !(h.Args[0] is int)) return true;
            if (_talentIcons.FirstTime(h.Instance, (int)h.Args[0])) return true;
            h.Skip(null);
            return false;
        }

        bool OnHitStart(HookContext h)
        {
            if (!ArenaSession.Active) return true;
            _hitTrue = 0L;
            _hitPoolLoss = 0L;
            if (_structReadBroken || h.Args == null || h.Args.Length < 1) return true;
            try
            {
                DamageInfo info = (DamageInfo)h.Args[0];
                _hitTrue = TrueDamage(info.damage);
            }
            catch (Exception) { _structReadBroken = true; }
            return true;
        }

        /// <summary>
        /// 游戏刚改完一次血量。用了血量池的一方：把余量补回游戏里的 hp（写 battleTempData.hp 并同步血条，和游戏自己
        /// 「消耗生命」那条分支的写法一样）。hp 被钉在 int.MinValue（一刀超过 hp 二十多亿）时，真实的 hp 用这一击的真实伤害算。
        /// </summary>
        void OnHpModified(HookContext h)
        {
            if (!ArenaSession.Active) return;
            try
            {
                int side = SideOf(h.Instance);
                if (side < 0 || !_pools[side].Active) return;
                BattleCharacter c = h.Instance as BattleCharacter;
                if (c == null || c.battleTempData == null) return;
                BigHpPool pool = _pools[side];
                int hpNow = c.battleTempData.hp;
                long hpTrue = hpNow;
                if (hpNow == int.MinValue)
                {
                    if (_hitTrue > 0L) hpTrue = pool.Hp - _hitTrue;
                    else if (SatMath.HasTrue && SatMath.TrueValue < hpTrue) hpTrue = SatMath.TrueValue;
                }
                int fixedHp = pool.Normalize(hpTrue);
                _hitPoolLoss += pool.LastLoss;
                if (fixedHp == hpNow) return;
                c.battleTempData.hp = fixedHp;
                if (c.characterUI != null) c.characterUI.hp = fixedHp;
            }
            catch (Exception e) { Broken("ModifyHp", e); }
        }

        /// <summary>两方的统计文本（悬浮窗和日志共用）。</summary>
        public string Report(int topCards)
        {
            var sb = new System.Text.StringBuilder();
            for (int side = 0; side < 2; side++)
            {
                if (side > 0) sb.Append((char)10).Append((char)10);
                sb.Append(_tally.Render(side, _session.SideName(side), topCards));
                string hp = HpLine(side, _session.SideName(side));
                if (hp.Length > 0) sb.Append((char)10).Append(hp);
            }
            return sb.ToString();
        }

        /// <summary>一场打完（离开斗法阶段 / 重播）时把统计记进会话日志，方便事后对数。每场只记一次。</summary>
        public void LogReport()
        {
            if (_reported) return;
            _reported = true;
            if (_tally.TotalDamage(0) + _tally.TotalDamage(1) <= 0L) return;
            string text = Report(12).Replace(((char)10).ToString(), " ｜ ");
            _ctx.Log.Info("伤害统计：" + text + " ｜ 溢出 " + SatMath.Overflows.ToString(System.Globalization.CultureInfo.InvariantCulture) + " 次（累计）");
        }

        /// <summary>用了 64 位血量池的一方的真实血量（没用上返回空串）。</summary>
        public string HpLine(int side, string name)
        {
            if (side < 0 || side > 1 || !_pools[side].Active) return "";
            long left = _pools[side].Effective;
            if (left < 0L) left = 0L;
            return name + "　真实血量 " + DamageTally.Group(left) + " / " + DamageTally.Group(_totals[side]);
        }

        void OnHitDone(HookContext h)
        {
            if (!ArenaSession.Active || !_pending) return;
            try
            {
                long hpLoss = h.Result is int ? TrueDamage((int)h.Result) : 0L;
                int victim = SideOf(h.Instance);
                if (victim >= 0 && _pools[victim].Active) hpLoss = _hitPoolLoss;
                _hitTrue = 0L;
                _tally.AddDamage(_pendingSide, _pendingDamage >= 0L ? _pendingDamage : hpLoss, hpLoss);
                _pending = false;
            }
            catch (Exception e) { Broken("OnHit", e); }
        }

        /// <summary>有一次 ApplyDamage 没有走到 OnHit（比如被完全挡掉）：按「有伤害、没掉血」记上。</summary>
        void FlushPending()
        {
            if (!_pending) return;
            _pending = false;
            if (_pendingDamage > 0L) _tally.AddDamage(_pendingSide, _pendingDamage, 0L);
        }

        // ── 暂停 / 步进：点游戏自己的播放按钮 ─────────────────────────────────

        public static bool IsPaused { get { return Time.timeScale == 0f; } }

        static Button FindPlayButton()
        {
            BattlePanel bp = ILRPanelBase.FindILRPanel<BattlePanel>();
            BattleReplayPanel rp = bp != null ? bp.FindILRSubPanel<BattleReplayPanel>() : null;
            if (rp == null || rp.panel == null || !rp.panel.isShow) return null;
            return rp.FindComponent<Button>("PlayButton");
        }

        void SetPaused(bool paused)
        {
            if (IsPaused == paused) return;
            Button play = FindPlayButton();
            if (play == null || !play.IsActive() || !play.IsInteractable()) return;
            play.onClick.Invoke();
        }

        /// <summary>「步进」按钮：在暂停就放行，下一张牌之前再停；在播放就等当前这张打完停。</summary>
        public void Step()
        {
            if (!_stepHooked) return;
            if (_step.RequestStep(IsPaused)) SetPaused(false);
        }
    }
}
