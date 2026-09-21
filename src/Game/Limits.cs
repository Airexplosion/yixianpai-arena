using System;
using Yx.ModSdk;
using Yx.ModSdk.Hooks;
using Yx.Shared;

namespace YxArena.Game
{
    /// <summary>
    /// 解除战斗的回合上限和攻击段数上限（用户 2026-09-19 要求）。两个上限都是游戏方法中间的常量：
    ///   BattleExecuter.Execute/3    局部变量 MAX_HUI_HE_COUNT = 64（单方回合数；async 方法，实际在状态机里）
    ///   BattleCharacter.Attack/5    if (attackCount > 999) attackCount = 999
    /// 钩子够不着，用 SDK 的 ctx.Hooks.OverrideConstant 把它们改成「可在运行时覆盖」的常量：改写本身不改变行为，
    /// 只有练习场开打时才把覆盖值打开，回到正常对局（任何一次不在练习场里的 PlayBattle、回大厅、停用 mod）就关回原值
    /// ——正常对局的战斗结果由服务器算，本地的上限必须和它一致。
    /// </summary>
    public sealed class Limits
    {
        readonly ModContext _ctx;
        ConstOverride _rounds;
        ConstOverride _hits;
        bool _tried;
        bool _failed;

        public Limits(ModContext ctx)
        {
            _ctx = ctx;
        }

        public int Mode;

        public void Install()
        {
            if (_ctx.Hooks.TryPrefix("BattleManager", "PlayBattle", 1, OnPlayBattle) != null) return;
            _failed = true;
            _ctx.Log.Warn("解限不可用：上限保持原版");
        }

        /// <summary>开打前调（备战界面里，战斗代码此刻没有在跑）：第一次用到时改写那两个方法。</summary>
        public void Prepare()
        {
            if (_tried || _failed || !LimitModes.Lifted(Mode)) return;
            _tried = true;
            try
            {
                _rounds = _ctx.Hooks.OverrideConstant("BattleExecuter", "Execute", 3, LimitModes.OriginalRounds, "MAX_HUI_HE_COUNT");
                _hits = _ctx.Hooks.OverrideConstant("BattleCharacter", "Attack", 5, LimitModes.OriginalHits, "attackCount");
                Describe("回合上限", _rounds);
                Describe("攻击段数上限", _hits);
            }
            catch (Exception e)
            {
                _failed = true;
                _ctx.Log.Error("解限出错（上限保持原版）", e);
            }
        }

        void Describe(string what, ConstOverride site)
        {
            if (site.Applied) _ctx.Log.Info("解限：" + what + "已可覆盖（" + site.Report.Describe() + "）");
            else
            {
                _failed = true;
                _ctx.Log.Warn("解限：" + what + "没改成，保持原版（" + site.Report.Describe() + "）");
            }
        }

        bool OnPlayBattle(HookContext h)
        {
            if (ArenaSession.Active && LimitModes.Lifted(Mode)) Arm();
            else Disarm();
            return true;
        }

        void Arm()
        {
            if (_rounds != null) _rounds.Set(LimitModes.RoundCap(Mode));
            if (_hits != null) _hits.Set(LimitModes.HitCap(Mode));
        }

        /// <summary>关回游戏自己的上限。</summary>
        public void Disarm()
        {
            if (_rounds != null) _rounds.Disable();
            if (_hits != null) _hits.Disable();
        }

        /// <summary>状态行上的一小段；一切正常时为空。</summary>
        public string Status()
        {
            return _failed ? "解限失败（见日志）" : "";
        }
    }
}
