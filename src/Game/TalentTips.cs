using System;
using System.Collections.Generic;
using System.Text;
using Proto;
using UnityEngine;
using Yx.ModSdk;
using Yx.ModSdk.Hooks;
using Yx.Shared;

namespace YxArena.Game
{
	public sealed class TalentTips
	{
		private const int MaxCells = 128;

		private readonly ModContext _ctx;

		private readonly List<SelectInfoCell> _cells = new List<SelectInfoCell>();

		private readonly List<int> _talents = new List<int>();

		private bool _broken;

		public TalentTips(ModContext ctx)
		{
			_ctx = ctx;
		}

		public void Install()
		{
			HookGroup hookGroup = _ctx.Hooks.Group("仙命悬停说明");
			hookGroup.Prefix("SelectInfoCell", "UpdateContent", 1, OnUpdateContent);
			hookGroup.Postfix("SelectInfoCell", "OnPointerEnter", 1, OnEnter);
			hookGroup.Postfix("SelectInfoCell", "OnPointerExit", 1, OnExit);
			if (!hookGroup.Complete)
			{
				hookGroup.CancelAll();
				_broken = true;
				_ctx.Log.Warn(_ctx.T("仙命选择框的悬停说明不可用", "Talent picker hover tooltips unavailable"));
			}
		}

		private void Fail(string where, Exception e)
		{
			if (!_broken)
			{
				_ctx.Log.Error(_ctx.T("仙命悬停说明 " + where + " 出错（之后不再显示说明）", "Talent hover tooltip " + where + " failed (tooltips disabled from now on)"), e);
			}
			_broken = true;
		}

		private int IndexOf(SelectInfoCell cell)
		{
			for (int i = 0; i < _cells.Count; i++)
			{
				if (_cells[i] == cell)
				{
					return i;
				}
			}
			return -1;
		}

		private bool OnUpdateContent(HookContext h)
		{
			if (_broken)
			{
				return true;
			}
			try
			{
				SelectInfoCell selectInfoCell = h.Instance as SelectInfoCell;
				SelectInfoCell.CellData cellData = ((h.Args != null && h.Args.Length != 0) ? (h.Args[0] as SelectInfoCell.CellData) : null);
				if (selectInfoCell == null || cellData == null)
				{
					return true;
				}
				int num = ((cellData.infoType == SelectInfoType.仙命 && cellData.param > 0) ? cellData.param : 0);
				int num2 = IndexOf(selectInfoCell);
				if (num2 >= 0)
				{
					_talents[num2] = num;
					return true;
				}
				if (_cells.Count >= 128)
				{
					_cells.Clear();
					_talents.Clear();
				}
				_cells.Add(selectInfoCell);
				_talents.Add(num);
			}
			catch (Exception e)
			{
				Fail("UpdateContent", e);
			}
			return true;
		}

		private static TalentDescriptionPanel FindPanel()
		{
			return ILRPanelBase.FindILRPanel<TooltipsPanel>()?.FindILRSubPanel<TalentDescriptionPanel>();
		}

		private void OnEnter(HookContext h)
		{
			if (_broken)
			{
				return;
			}
			try
			{
				SelectInfoCell selectInfoCell = h.Instance as SelectInfoCell;
				int num = ((selectInfoCell != null) ? IndexOf(selectInfoCell) : (-1));
				int num2 = ((num >= 0) ? _talents[num] : 0);
				if (num2 > 0)
				{
					TalentConfig talentConfig = ConfigManager.GetTalentConfig(num2);
					TalentDescriptionPanel talentDescriptionPanel = FindPanel();
					if (talentConfig != null && talentDescriptionPanel != null)
					{
						StringBuilder stringBuilder = new StringBuilder();
						stringBuilder.Append(talentConfig.GetName()).Append('\n').Append(TranslateUtil.GetLevelTranslate(talentConfig.level));
						RectTransform targetRT = FrameOf(selectInfoCell.transform) ?? (selectInfoCell.transform as RectTransform);
						talentDescriptionPanel.ShowBox(targetRT, stringBuilder.ToString(), ConfigExtension.ParseDescription(talentConfig, GameMode.InvalidGameMode), num2);
						SetBlocking(talentDescriptionPanel, blocking: false);
					}
				}
			}
			catch (Exception e)
			{
				Fail("OnPointerEnter", e);
			}
		}

		private void OnExit(HookContext h)
		{
			Hide();
		}

		private static RectTransform FrameOf(Transform cell)
		{
			Transform transform = cell;
			for (int i = 0; i < 12; i++)
			{
				if (!(transform != null))
				{
					break;
				}
				if (transform.name == "Box")
				{
					return transform as RectTransform;
				}
				transform = transform.parent;
			}
			return null;
		}

		private static void SetBlocking(TalentDescriptionPanel panel, bool blocking)
		{
			if (panel == null || panel.transform == null)
			{
				return;
			}
			GameObject gameObject = panel.transform.gameObject;
			CanvasGroup canvasGroup = gameObject.GetComponent(typeof(CanvasGroup)) as CanvasGroup;
			if (canvasGroup == null)
			{
				if (blocking)
				{
					return;
				}
				canvasGroup = gameObject.AddComponent(typeof(CanvasGroup)) as CanvasGroup;
			}
			canvasGroup.blocksRaycasts = blocking;
		}

		public void Hide()
		{
			if (_broken)
			{
				return;
			}
			try
			{
				TalentDescriptionPanel talentDescriptionPanel = FindPanel();
				if (talentDescriptionPanel != null)
				{
					SetBlocking(talentDescriptionPanel, blocking: true);
					if (talentDescriptionPanel.panel != null && talentDescriptionPanel.panel.isShow)
					{
						talentDescriptionPanel.Hide();
					}
				}
			}
			catch (Exception e)
			{
				Fail("Hide", e);
			}
		}
	}
}
