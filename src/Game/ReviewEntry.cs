using System;
using Proto;
using Yx.ModSdk;
using Yx.ModSdk.Unity;
using Yx.Shared;

namespace YxArena.Game
{
	public sealed class ReviewEntry
	{
		private readonly ModContext _ctx;

		private readonly ArenaSession _session;

		public ReviewEntry(ModContext ctx, ArenaSession session)
		{
			_ctx = ctx;
			_session = session;
		}

		public void Install()
		{
			// Intercept the click before the game's confirmation callback and async
			// server-room workflow. Keep Review as a fallback for older entry paths.
			bool clickInstalled = _ctx.Hooks.TryPrefix("RecordDetailPanel", "OnReviewBtnClick", 0, OnReview) != null;
			bool reviewInstalled = _ctx.Hooks.TryPrefix("RecordDetailPanel", "Review", 0, OnReview) != null;
			if (!clickInstalled && !reviewInstalled)
			{
				_ctx.Log.Warn("复盘转练习场入口不可用：复盘按钮和 Review 均未挂上");
			}
		}

		private bool OnReview(HookContext h)
		{
			h.Skip(null);
			string stage = "读取战绩";
			try
			{
				_ctx.Log.Info("收到：战绩复盘导入练习场");
				RecordDetailPanel recordDetailPanel = h.Instance as RecordDetailPanel;
				BattleResult battleResult = RecordDetailPanel.battleResult;
				if (battleResult == null)
				{
					Ui.Toast("没有可导入的复盘数据");
					return false;
				}
				stage = "读取赛季";
				SeasonMechanismType seasonMec = battleResult.seasonMec;
				if (!OpenManager.IsOpen(OpenType.OpenBattleResultEx) && recordDetailPanel != null && recordDetailPanel.recordItem != null && recordDetailPanel.recordItem.recordData != null && recordDetailPanel.recordItem.recordData.info != null)
				{
					seasonMec = recordDetailPanel.recordItem.recordData.info.seasonMec;
				}
				stage = "导入战绩";
				_session.EnterReview(battleResult, seasonMec);
			}
			catch (Exception exception)
			{
				if (stage == "导入战绩") stage = _session.ReviewStage;
				// Older loaders can throw again while formatting ILRuntime exceptions
				// in Log.Error(message, exception), hiding both the cause and the toast.
				string reason = "异常消息不可读";
				try
				{
					if (exception != null && !string.IsNullOrEmpty(exception.Message)) reason = exception.Message;
				}
				catch (Exception) { }
				string message = "复盘导入失败（" + stage + "）：" + reason;
				_ctx.Log.Warn(message);
				Ui.Toast(message);
			}
			return false;
		}
	}
}
