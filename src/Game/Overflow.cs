using System;
using System.Globalization;
using UnityEngine;
using Yx.ModSdk;
using Yx.ModSdk.Hooks;
using Yx.Shared;

namespace YxArena.Game
{
	public sealed class Overflow
	{
		private readonly ModContext _ctx;

		private readonly OverflowPlan _plan = new OverflowPlan();

		private SaturateJob _job;

		private string[] _jobTypes;

		private int _budget = 1500;

		private bool _broken;

		private int _sites;

		public bool Enabled;

		public bool Busy => _job != null;

		public Overflow(ModContext ctx)
		{
			_ctx = ctx;
		}

		public bool Prepare(int[] boardCards)
		{
			if (!Enabled || _broken)
			{
				return true;
			}
			if (_job != null)
			{
				return false;
			}
			try
			{
				string[] array = _plan.Pending(boardCards);
				if (array.Length == 0)
				{
					return true;
				}
				_jobTypes = array;
				_job = _ctx.Hooks.BeginSaturate(array);
				return false;
			}
			catch (Exception e)
			{
				Fail(_ctx.T("准备", "prepare"), e);
				return true;
			}
		}

		public bool Tick()
		{
			if (_job == null)
			{
				return true;
			}
			try
			{
				float realtimeSinceStartup = Time.realtimeSinceStartup;
				bool num = _ctx.Hooks.StepSaturate(_job, _budget);
				_budget = OverflowPlan.NextBudget(elapsedMs: (Time.realtimeSinceStartup - realtimeSinceStartup) * 1000f, budget: _budget);
				if (!num)
				{
					return false;
				}
				Finish();
				return true;
			}
			catch (Exception e)
			{
				Fail(_ctx.T("改写", "rewrite"), e);
				return true;
			}
		}

		private void Finish()
		{
			SaturateReport total = _job.Total;
			_job = null;
			if (!total.Ok)
			{
				_broken = true;
				_ctx.Log.Warn(_ctx.T("破限没做成（战斗照常，数值到 21 亿仍会溢出）：", "Overflow prep didn't complete (fight continues; values still overflow past ~2.1 billion): ") + total.Describe());
			}
			else
			{
				_plan.MarkDone(_jobTypes);
				_sites += total.Sites;
				_ctx.Log.Info(_ctx.T("破限：", "Overflow: ") + total.Describe());
			}
		}

		private void Fail(string where, Exception e)
		{
			_broken = true;
			_job = null;
			_ctx.Log.Error(_ctx.T("破限", "Overflow ") + where + _ctx.T("出错（之后不再尝试，战斗照常）", " failed (won't retry; fight continues)"), e);
		}

		public void OnCardAboutToPlay(int cardId)
		{
			if (!Enabled || _broken || cardId <= 0 || !_plan.CoreDone)
			{
				return;
			}
			string text = OverflowPlan.CardType(cardId);
			if (_plan.IsDone(text))
			{
				return;
			}
			_plan.MarkCardDone(text);
			try
			{
				SaturateReport saturateReport = _ctx.Hooks.Saturate(text);
				if (saturateReport.Ok)
				{
					_sites += saturateReport.Sites;
				}
			}
			catch (Exception e)
			{
				Fail(_ctx.T("处理 " + text + " ", "processing " + text + " "), e);
			}
		}

		public string Progress()
		{
			if (_job == null)
			{
				return "";
			}
			return _ctx.T("破限准备中 ", "Preparing overflow ") + OverflowPlan.Progress(_job.TypeIndex, _job.Types.Length);
		}

		public string Status()
		{
			if (_broken)
			{
				return _ctx.T("破限：失败（见日志）", "Overflow: failed (see log)");
			}
			if (_job != null)
			{
				return Progress();
			}
			if (!_plan.CoreDone)
			{
				if (!Enabled)
				{
					return _ctx.T("破限：关", "Overflow: off");
				}
				return _ctx.T("破限：首次开打时准备", "Overflow: prepared on first fight");
			}
			string text = _sites.ToString(CultureInfo.InvariantCulture);
			string text2 = _ctx.T("破限：" + text + " 处", "Overflow: " + text + " sites");
			long overflows = SatMath.Overflows;
			if (overflows > 0)
			{
				string text3 = overflows.ToString(CultureInfo.InvariantCulture);
				text2 += _ctx.T("，已拦下溢出 " + text3 + " 次", ", caught " + text3 + " overflows");
			}
			if (!Enabled)
			{
				text2 += _ctx.T("（已关：不再处理新牌）", " (off: no new cards processed)");
			}
			return text2;
		}
	}
}
