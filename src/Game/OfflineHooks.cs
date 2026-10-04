using System.Globalization;
using Proto;
using Yx.ModSdk;
using Yx.ModSdk.Hooks;
using Yx.Shared;

namespace YxArena.Game
{
	public sealed class OfflineHooks
	{
		private readonly ModContext _ctx;

		private HookGroup _required;

		private int _muteDepth;

		public int Moves;

		public int Refines;

		public int BlockedRequests;

		public bool Complete
		{
			get
			{
				if (_required != null)
				{
					return _required.Complete;
				}
				return false;
			}
		}

		public string Missing
		{
			get
			{
				if (_required == null)
				{
					return "（还没安装）";
				}
				return _required.Missing;
			}
		}

		public HookGroup Required => _required;

		public OfflineHooks(ModContext ctx)
		{
			_ctx = ctx;
		}

		public void Install()
		{
			_required = _ctx.Hooks.Group("离线保护");
			Move("MoveToGrid", 2);
			Move("MoveToHand", 1);
			Move("TryUpgradeHandCard", 2);
			Move("TryFuseHandCard", 3);
			Move("InsertCard", 1);
			_required.Prefix("CardGridCunQuItem", "MoveCardToGrid", 1, OnMovePrefix);
			_required.Postfix("CardGridCunQuItem", "MoveCardToGrid", 1, OnBottleMovePostfix);
			_required.Prefix("CardGridCunQuItem", "MoveCardToHand", 2, OnMovePrefix);
			_required.Postfix("CardGridCunQuItem", "MoveCardToHand", 2, OnBottleMovePostfix);
			BottlePanel.Install(_required);
			_required.Prefix("CardPanel", "RefineCardAsync", 2, OnRefinePrefix);
			_required.Prefix("BattleManager", "GameStatusReq", 2, OnBlockedRequest);
			_required.Prefix("BattleManager", "SelfInfoReq", 0, OnBlockedRequest);
			_required.Prefix("BattleManager", "SettleResultReq", 0, OnBlockedRequest);
			_required.Prefix("BattleManager", "OnReciveGameStatusPositiveAction", 1, OnBlockedRequest);
			_required.Prefix("CardPanel", "ReloadCardOrder", 0, OnBlockedRequest);
			_ctx.Hooks.TryPrefix("CardPanel", "ReplaceCardAsync", 3, OnReplacePrefix);
		}

		private void Move(string method, int paramCount)
		{
			_required.Prefix("CardPanel", method, paramCount, OnMovePrefix);
			_required.Postfix("CardPanel", method, paramCount, OnMovePostfix);
		}

		private static ReadyLayer FindReadyLayer()
		{
			return ILRPanelBase.FindILRPanel<BattlePanel>()?.readyLayer;
		}

		private static void SetMuted(bool muted)
		{
			ReadyLayer readyLayer = FindReadyLayer();
			if (readyLayer != null && readyLayer.isRealtimeSpectating != muted)
			{
				readyLayer.isRealtimeSpectating = muted;
			}
		}

		private bool OnMovePrefix(HookContext h)
		{
			if (!ArenaSession.Active)
			{
				return true;
			}
			_muteDepth++;
			SetMuted(muted: true);
			Moves++;
			return true;
		}

		private void OnMovePostfix(HookContext h)
		{
			if (_muteDepth > 0)
			{
				_muteDepth--;
				if (_muteDepth == 0)
				{
					SetMuted(muted: false);
				}
			}
		}

		private void OnBottleMovePostfix(HookContext h)
		{
			OnMovePostfix(h);
			if (ArenaSession.Active)
			{
				BottlePanel.Capture();
			}
		}

		public void Tick()
		{
			if (ArenaSession.Active)
			{
				_muteDepth = 0;
				ReadyLayer readyLayer = FindReadyLayer();
				if (readyLayer != null && readyLayer.isRealtimeSpectating)
				{
					readyLayer.isRealtimeSpectating = false;
				}
			}
		}

		private bool OnRefinePrefix(HookContext h)
		{
			if (!ArenaSession.Active)
			{
				return true;
			}
			if (h.Args == null || h.Args.Length < 2 || h.Args[1] != null)
			{
				return true;
			}
			if (!(h.Args[0] is CardItem cardItem))
			{
				return true;
			}
			RefineCardResp refineCardResp = new RefineCardResp();
			refineCardResp.result = true;
			refineCardResp.targetCard = cardItem.cardInfo;
			h.Args[1] = refineCardResp;
			Refines++;
			return true;
		}

		private bool OnBlockedRequest(HookContext h)
		{
			if (!ArenaSession.Active)
			{
				return true;
			}
			BlockedRequests++;
			h.Skip(null);
			return false;
		}

		private bool OnReplacePrefix(HookContext h)
		{
			if (!ArenaSession.Active)
			{
				return true;
			}
			_ctx.Log.Warn(_ctx.T("练习场里走到了换牌（ReplaceCardAsync）——换牌区应该已经隐藏，这一次会向服务器发包并失败", "Reached card replace (ReplaceCardAsync) in the arena — the replace area should have been hidden; this will send a packet to the server and fail"));
			return true;
		}

		public string Summary()
		{
			string text = Moves.ToString(CultureInfo.InvariantCulture);
			string text2 = Refines.ToString(CultureInfo.InvariantCulture);
			string text3 = BlockedRequests.ToString(CultureInfo.InvariantCulture);
			return _ctx.T("移牌 " + text + "  炼化 " + text2 + "  拦下的请求 " + text3, "Moves " + text + "  Refines " + text2 + "  Blocked " + text3);
		}
	}
}
