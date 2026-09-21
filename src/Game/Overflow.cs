using System;
using System.Globalization;
using UnityEngine;
using Yx.ModSdk;
using Yx.ModSdk.Hooks;
using Yx.Shared;

namespace YxArena.Game
{
    /// <summary>
    /// 「破限」：把战斗公式里的 32 位整数运算改写成到顶就停的版本（SDK 的 ctx.Hooks.BeginSaturate / Saturate），
    /// 溢出的那条运算链的 64 位真值由 SatMath 记着，伤害统计拿它显示真实的总伤害 / 单次伤害。
    /// 设计见 docs/superpowers/specs/2026-09-19-saturating-arithmetic-design.md。
    ///
    /// 只在练习场里发生：正常对局的战斗结果由服务器算，本地改了只会让画面和服务器不一致。
    ///   · 战斗核心（BattleCharacter 等）很大：第一次开打前分帧处理，处理完再开打；
    ///   · 卡牌类：场上的牌和核心一起处理；战斗里冒出来的牌（生成的、对手的）第一次被打出时现场处理。
    /// 改写不可撤销（本次游戏进程内一直有效，重启游戏恢复），所以关掉开关只是「之后不再处理新的类型」。
    /// </summary>
    public sealed class Overflow
    {
        readonly ModContext _ctx;
        readonly OverflowPlan _plan = new OverflowPlan();
        SaturateJob _job;
        string[] _jobTypes;
        int _budget = OverflowPlan.StartBudget;
        bool _broken;
        int _sites;

        public Overflow(ModContext ctx)
        {
            _ctx = ctx;
        }

        public bool Enabled;

        public bool Busy { get { return _job != null; } }

        /// <summary>开打前调：没有要处理的返回 true（可以直接开打）；否则建好分帧作业、返回 false，之后每帧调 <see cref="Tick"/>。</summary>
        public bool Prepare(int[] boardCards)
        {
            if (!Enabled || _broken) return true;
            if (_job != null) return false;
            try
            {
                string[] pending = _plan.Pending(boardCards);
                if (pending.Length == 0) return true;
                _jobTypes = pending;
                _job = _ctx.Hooks.BeginSaturate(pending);
                return false;
            }
            catch (Exception e)
            {
                Fail(_ctx.T("准备", "prepare"), e);
                return true;
            }
        }

        /// <summary>推进一帧。返回 true = 做完了（或者失败了，照常开打）。</summary>
        public bool Tick()
        {
            if (_job == null) return true;
            try
            {
                float start = Time.realtimeSinceStartup;
                bool done = _ctx.Hooks.StepSaturate(_job, _budget);
                float elapsedMs = (Time.realtimeSinceStartup - start) * 1000f;
                _budget = OverflowPlan.NextBudget(_budget, elapsedMs);
                if (!done) return false;
                Finish();
                return true;
            }
            catch (Exception e)
            {
                Fail(_ctx.T("改写", "rewrite"), e);
                return true;
            }
        }

        void Finish()
        {
            SaturateReport total = _job.Total;
            _job = null;
            if (!total.Ok)
            {
                _broken = true;
                _ctx.Log.Warn(_ctx.T("破限没做成（战斗照常，数值到 21 亿仍会溢出）：", "Overflow prep didn't complete (fight continues; values still overflow past ~2.1 billion): ") + total.Describe());
                return;
            }
            _plan.MarkDone(_jobTypes);
            _sites += total.Sites;
            _ctx.Log.Info(_ctx.T("破限：", "Overflow: ") + total.Describe());
        }

        void Fail(string where, Exception e)
        {
            _broken = true;
            _job = null;
            _ctx.Log.Error(_ctx.T("破限", "Overflow ") + where + _ctx.T("出错（之后不再尝试，战斗照常）", " failed (won't retry; fight continues)"), e);
        }

        /// <summary>一张牌即将被打出（CheckCardCost 的前置里，牌自己的代码还没开始跑）：它的类没处理过就现场处理。</summary>
        public void OnCardAboutToPlay(int cardId)
        {
            if (!Enabled || _broken || cardId <= 0 || !_plan.CoreDone) return;
            string type = OverflowPlan.CardType(cardId);
            if (_plan.IsDone(type)) return;
            _plan.MarkCardDone(type);          // 先记上：不管成不成都只试一次
            try
            {
                SaturateReport report = _ctx.Hooks.Saturate(type);
                // 没有自己的类的牌走 FallbackCardAction（核心里已经处理过），这里会报「找不到热更类型」，不算错。
                if (report.Ok) _sites += report.Sites;
            }
            catch (Exception e) { Fail(_ctx.T("处理 " + type + " ", "processing " + type + " "), e); }
        }

        public string Progress()
        {
            if (_job == null) return "";
            return _ctx.T("破限准备中 ", "Preparing overflow ") + OverflowPlan.Progress(_job.TypeIndex, _job.Types.Length);
        }

        /// <summary>控制栏状态行上的一小段。</summary>
        public string Status()
        {
            if (_broken) return _ctx.T("破限：失败（见日志）", "Overflow: failed (see log)");
            if (_job != null) return Progress();
            if (!_plan.CoreDone) return Enabled ? _ctx.T("破限：首次开打时准备", "Overflow: prepared on first fight") : _ctx.T("破限：关", "Overflow: off");
            string sites = _sites.ToString(CultureInfo.InvariantCulture);
            string text = _ctx.T("破限：" + sites + " 处", "Overflow: " + sites + " sites");
            long overflows = SatMath.Overflows;
            if (overflows > 0L) { string n = overflows.ToString(CultureInfo.InvariantCulture); text += _ctx.T("，已拦下溢出 " + n + " 次", ", caught " + n + " overflows"); }
            if (!Enabled) text += _ctx.T("（已关：不再处理新牌）", " (off: no new cards processed)");
            return text;
        }
    }
}
