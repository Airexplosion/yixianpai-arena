using System;
using Yx.ModSdk;
using Yx.ModSdk.Hooks;
using Yx.Shared;

namespace YxArena.Game
{
	public sealed class Limits
	{
		private readonly ModContext _ctx;

		private ConstOverride _rounds;

		private ConstOverride _hits;

		private bool _tried;

		private bool _failed;

		public int Mode;

		public Limits(ModContext ctx)
		{
			_ctx = ctx;
		}

		public void Install()
		{
			if (_ctx.Hooks.TryPrefix("BattleManager", "PlayBattle", 1, OnPlayBattle) == null)
			{
				_failed = true;
				_ctx.Log.Warn(_ctx.T("解限不可用：上限保持原版", "Uncap unavailable: caps stay vanilla"));
			}
		}

		public void Prepare()
		{
			if (_tried || _failed || !LimitModes.Lifted(Mode))
			{
				return;
			}
			_tried = true;
			try
			{
				_rounds = _ctx.Hooks.OverrideConstant("BattleExecuter", "Execute", 3, 64, "MAX_HUI_HE_COUNT");
				_hits = _ctx.Hooks.OverrideConstant("BattleCharacter", "Attack", 5, 999, "attackCount");
				Describe(_ctx.T("回合上限", "round cap"), _rounds);
				Describe(_ctx.T("攻击段数上限", "attack-hit cap"), _hits);
			}
			catch (Exception exception)
			{
				_failed = true;
				_ctx.Log.Error(_ctx.T("解限出错（上限保持原版）", "Uncap failed (caps stay vanilla)"), exception);
			}
		}

		private void Describe(string what, ConstOverride site)
		{
			if (site.Applied)
			{
				_ctx.Log.Info(_ctx.T("解限：", "Uncap: ") + what + _ctx.T("已可覆盖（", " is now overridable (") + site.Report.Describe() + _ctx.T("）", ")"));
			}
			else
			{
				_failed = true;
				_ctx.Log.Warn(_ctx.T("解限：", "Uncap: ") + what + _ctx.T("没改成，保持原版（", " unchanged, stays vanilla (") + site.Report.Describe() + _ctx.T("）", ")"));
			}
		}

		private bool OnPlayBattle(HookContext h)
		{
			if (ArenaSession.Active && LimitModes.Lifted(Mode))
			{
				Arm();
			}
			else
			{
				Disarm();
			}
			return true;
		}

		private void Arm()
		{
			if (_rounds != null)
			{
				_rounds.Set(LimitModes.RoundCap(Mode));
			}
			if (_hits != null)
			{
				_hits.Set(LimitModes.HitCap(Mode));
			}
		}

		public void Disarm()
		{
			if (_rounds != null)
			{
				_rounds.Disable();
			}
			if (_hits != null)
			{
				_hits.Disable();
			}
		}

		public string Status()
		{
			if (!_failed)
			{
				return "";
			}
			return _ctx.T("解限失败（见日志）", "Uncap failed (see log)");
		}
	}
}
