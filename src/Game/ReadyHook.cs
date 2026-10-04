using System;
using Yx.ModSdk;
using Yx.Shared;

namespace YxArena.Game
{
	public sealed class ReadyHook
	{
		private readonly ModContext _ctx;

		private readonly OfflineHooks _hooks;

		private readonly Action _fight;

		public ReadyHook(ModContext ctx, OfflineHooks hooks, Action fight)
		{
			_ctx = ctx;
			_hooks = hooks;
			_fight = fight;
		}

		public void Install()
		{
			_hooks.Required.Prefix("ReadyCountdownButton", "Click", 0, OnReadyClick);
		}

		private bool OnReadyClick(HookContext h)
		{
			if (!ArenaSession.Active)
			{
				return true;
			}
			h.Skip(null);
			_fight();
			return false;
		}
	}
}
