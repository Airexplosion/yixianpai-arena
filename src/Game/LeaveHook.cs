using System;
using Yx.ModSdk.Hooks;
using Yx.Shared;

namespace YxArena.Game
{
	public sealed class LeaveHook
	{
		private readonly Action _leave;

		public LeaveHook(HookGroup hooks, Action leave)
		{
			_leave = leave;
			hooks.Prefix("SettingsPanel", "OnReturnButtonClick", 0, OnReturn);
		}

		private bool OnReturn(HookContext h)
		{
			if (!ArenaSession.Active)
			{
				return true;
			}
			h.Skip(null);
			if (h.Instance is SettingsPanel settingsPanel && settingsPanel.panel != null)
			{
				settingsPanel.panel.HideSelf();
			}
			_leave();
			return false;
		}
	}
}
