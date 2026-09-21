using System;
using System.Globalization;
using Proto;
using Yx.ModSdk;
using Yx.ModSdk.Hooks;
using Yx.Shared;

namespace YxArena.Game
{
    /// <summary>
    /// 让备战界面的操作完全离线的三组钩子（探针 0.2.0 / 0.3.0 实机验证过的做法）。
    /// 任一组没挂全，<see cref="Complete"/> 为 false，练习场拒绝进场——宁可不能用，也不把包发出去。
    /// 处理器在练习场之外第一句就放行，不读不改任何东西。
    ///
    ///   move   CardPanel 的 5 个移牌方法：发包处包在 if (!isRealtimeSpectating) 里，且在方法的同步段里。
    ///          前置把 readyLayer.isRealtimeSpectating 置 true，后置（async 外壳返回 = 同步段跑完）放回 false。
    ///          这个标志同时是 CardItem「能不能拖」的闸，所以不能常开；每帧兜底复位。
    ///   refine CardPanel.RefineCardAsync(CardItem, RefineCardResp simulationResp = null)：游戏自己留的本地模拟口子，
    ///          给了 simulationResp 就不发包，后半段（加修为、炼化牌的 OnRefined……）全是游戏自己的代码。
    ///   block  BattleManager.GameStatusReq / SelfInfoReq、CardPanel.ReloadCardOrder：个别仙命 / 牌在移牌、炼化收尾时
    ///          会向服务器要状态。练习场里直接跳过。
    /// </summary>
    public sealed class OfflineHooks
    {
        readonly ModContext _ctx;
        // 「不挂全就不许进场」的那一组钩子：登记不抛异常，挂没挂上看 Complete / Missing（SDK 的 HookGroup）。
        HookGroup _required;
        int _muteDepth;

        public int Moves;
        public int Refines;
        public int BlockedRequests;

        public OfflineHooks(ModContext ctx)
        {
            _ctx = ctx;
        }

        /// <summary>三组必需的钩子是否全部挂上。</summary>
        public bool Complete { get { return _required != null && _required.Complete; } }

        /// <summary>没挂上的站点，逗号分隔；全挂上为空串。</summary>
        public string Missing { get { return _required != null ? _required.Missing : "（还没安装）"; } }

        /// <summary>别处登记的、同样属于「不挂上就不许进场」的钩子也登记到这一组里（比如「准备」按钮）。</summary>
        public HookGroup Required { get { return _required; } }

        public void Install()
        {
            _required = _ctx.Hooks.Group("离线保护");
            Move("MoveToGrid", 2);
            Move("MoveToHand", 1);
            Move("TryUpgradeHandCard", 2);
            Move("TryFuseHandCard", 3);
            Move("InsertCard", 1);
            _required.Prefix("CardPanel", "RefineCardAsync", 2, OnRefinePrefix);
            _required.Prefix("BattleManager", "GameStatusReq", 2, OnBlockedRequest);
            _required.Prefix("BattleManager", "SelfInfoReq", 0, OnBlockedRequest);
            _required.Prefix("CardPanel", "ReloadCardOrder", 0, OnBlockedRequest);
            // 换牌只是观察：挂不上不影响使用，所以不进「必需」那一组。
            _ctx.Hooks.TryPrefix("CardPanel", "ReplaceCardAsync", 3, OnReplacePrefix);
        }

        void Move(string method, int paramCount)
        {
            _required.Prefix("CardPanel", method, paramCount, OnMovePrefix);
            _required.Postfix("CardPanel", method, paramCount, OnMovePostfix);
        }

        // ── move ──────────────────────────────────────────────────────────

        static ReadyLayer FindReadyLayer()
        {
            BattlePanel bp = ILRPanelBase.FindILRPanel<BattlePanel>();
            return bp != null ? bp.readyLayer : null;
        }

        static void SetMuted(bool muted)
        {
            ReadyLayer layer = FindReadyLayer();
            if (layer == null) return;
            if (layer.isRealtimeSpectating != muted) layer.isRealtimeSpectating = muted;
        }

        bool OnMovePrefix(HookContext h)
        {
            if (!ArenaSession.Active) return true;
            _muteDepth++;
            SetMuted(true);
            Moves++;
            return true;
        }

        void OnMovePostfix(HookContext h)
        {
            if (_muteDepth <= 0) return;
            _muteDepth--;
            if (_muteDepth == 0) SetMuted(false);
        }

        /// <summary>每帧兜底：原方法同步段抛异常时后置不会跑，闸会卡在开着（牌就拖不动了）。</summary>
        public void Tick()
        {
            if (!ArenaSession.Active) return;
            _muteDepth = 0;
            ReadyLayer layer = FindReadyLayer();
            if (layer != null && layer.isRealtimeSpectating) layer.isRealtimeSpectating = false;
        }

        // ── refine ────────────────────────────────────────────────────────

        bool OnRefinePrefix(HookContext h)
        {
            if (!ArenaSession.Active) return true;
            if (h.Args == null || h.Args.Length < 2 || h.Args[1] != null) return true;
            CardItem card = h.Args[0] as CardItem;
            if (card == null) return true;
            var resp = new RefineCardResp();
            resp.result = true;
            resp.targetCard = card.cardInfo;
            h.Args[1] = resp;
            Refines++;
            return true;
        }

        // ── block ─────────────────────────────────────────────────────────

        bool OnBlockedRequest(HookContext h)
        {
            if (!ArenaSession.Active) return true;
            BlockedRequests++;
            h.Skip(null);
            return false;
        }

        bool OnReplacePrefix(HookContext h)
        {
            if (!ArenaSession.Active) return true;
            _ctx.Log.Warn("练习场里走到了换牌（ReplaceCardAsync）——换牌区应该已经隐藏，这一次会向服务器发包并失败");
            return true;
        }

        public string Summary()
        {
            return "移牌 " + Moves.ToString(CultureInfo.InvariantCulture)
                   + "  炼化 " + Refines.ToString(CultureInfo.InvariantCulture)
                   + "  拦下的请求 " + BlockedRequests.ToString(CultureInfo.InvariantCulture);
        }
    }
}
