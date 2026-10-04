using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yx.ModSdk;
using Yx.ModSdk.Hooks;
using Yx.Shared;

namespace YxArena.Game
{
	public sealed class NativeSwitch
	{
		private readonly ModContext _ctx;

		private readonly Action _onSwitch;

		private bool _hooked;

		private bool _failed;

		private bool _shown;

		private bool _detached;

		public int OffsetX = 24;

		public int OffsetY;

		public bool Available
		{
			get
			{
				if (_hooked)
				{
					return !_failed;
				}
				return false;
			}
		}

		public NativeSwitch(ModContext ctx, Action onSwitch)
		{
			_ctx = ctx;
			_onSwitch = onSwitch;
		}

		public void Install()
		{
			HookGroup hookGroup = _ctx.Hooks.Group("原生切换按钮");
			hookGroup.Prefix("ReadyLayerRecoveryingCI", "OnSwitchButtonClick", 0, OnSwitchClick);
			hookGroup.Prefix("ReadyLayerRecoveryingCI", "OnRecoveryButtonClick", 0, OnBlockedClick);
			hookGroup.Prefix("ReadyLayerRecoveryingCI", "OnDealCardButtonClick", 0, OnBlockedClick);
			_hooked = hookGroup.Complete;
			if (!_hooked)
			{
				hookGroup.CancelAll();
				_ctx.Log.Warn(_ctx.T("游戏自己的「切换」按钮借不了（换边用控制栏上的按钮）", "Can't borrow the game's own Switch button (use the control bar to switch sides)"));
			}
		}

		private bool OnSwitchClick(HookContext h)
		{
			if (!ArenaSession.Active)
			{
				return true;
			}
			h.Skip(null);
			try
			{
				_onSwitch();
			}
			catch (Exception exception)
			{
				_ctx.Log.Error(_ctx.T("换边出错", "Switch side failed"), exception);
			}
			return false;
		}

		private bool OnBlockedClick(HookContext h)
		{
			if (!ArenaSession.Active)
			{
				return true;
			}
			h.Skip(null);
			return false;
		}

		public void Ensure(bool editingOpponent)
		{
			if (!Available)
			{
				return;
			}
			try
			{
				ReadyLayerRecoveryingCI readyLayerRecoveryingCI = Find();
				if (readyLayerRecoveryingCI == null)
				{
					Fail(_ctx.T("这组控件加载不出来", "couldn't load its controls"), null);
					return;
				}
				if (!readyLayerRecoveryingCI.showing)
				{
					readyLayerRecoveryingCI.Refresh();
				}
				_shown = true;
				Button button = readyLayerRecoveryingCI.FindComponent<Button>("SwitchButton");
				Show(button, visible: true);
				Show(readyLayerRecoveryingCI.FindComponent<Button>("RecoveryButton"), visible: false);
				Show(readyLayerRecoveryingCI.FindComponent<Button>("DealCardButton"), visible: false);
				TextMeshProUGUI textMeshProUGUI = readyLayerRecoveryingCI.FindComponent<TextMeshProUGUI>("ReviewLabel");
				if (textMeshProUGUI != null)
				{
					string text = (editingOpponent ? Loc.T("练习场 · 对手", "Arena · Foe") : Loc.T("练习场 · 我方", "Arena · Mine"));
					if (textMeshProUGUI.text != text)
					{
						textMeshProUGUI.text = text;
					}
				}
				PlaceOnStatsRow(button, textMeshProUGUI);
			}
			catch (Exception e)
			{
				Fail(_ctx.T("摆放出错", "layout failed"), e);
			}
		}

		public void Hide()
		{
			if (!_shown)
			{
				return;
			}
			_shown = false;
			_detached = false;
			try
			{
				ReadyLayerRecoveryingCI readyLayerRecoveryingCI = Find();
				if (readyLayerRecoveryingCI != null && readyLayerRecoveryingCI.showing)
				{
					readyLayerRecoveryingCI.Hide();
				}
			}
			catch (Exception)
			{
			}
		}

		private static ReadyLayerRecoveryingCI Find()
		{
			BattlePanel battlePanel = ILRPanelBase.FindILRPanel<BattlePanel>();
			if (battlePanel == null || battlePanel.readyLayer == null || battlePanel.readyLayer.containerCI == null)
			{
				return null;
			}
			return battlePanel.readyLayer.containerCI.FindComponentItem<ReadyLayerRecoveryingCI>();
		}

		private void PlaceOnStatsRow(Button button, TextMeshProUGUI label)
		{
			if (button == null)
			{
				return;
			}
			CardPanel cardPanel = ILRPanelBase.FindILRPanel<BattlePanel>()?.FindILRSubPanel<CardPanel>();
			if (cardPanel == null || cardPanel.cardScroll == null)
			{
				return;
			}
			RectTransform rectTransform = RightmostShown(cardPanel.cardScroll.attrLayout);
			if (rectTransform == null)
			{
				return;
			}
			RectTransform rectTransform2 = button.transform as RectTransform;
			if (rectTransform2 == null)
			{
				return;
			}
			if (!_detached)
			{
				IgnoreLayout(rectTransform2.gameObject);
				if (label != null)
				{
					IgnoreLayout(label.gameObject);
				}
				_detached = true;
			}
			float x = rectTransform.lossyScale.x;
			float leftEdge = RowLayout.RightEdge(rectTransform.position.x, rectTransform.rect.xMax, x) + (float)OffsetX * x;
			float centerY = RowLayout.CenterY(rectTransform.position.y, rectTransform.rect.center.y, rectTransform.lossyScale.y) + (float)OffsetY * rectTransform.lossyScale.y;
			MoveTo(rectTransform2, leftEdge, centerY);
			if (!(label == null))
			{
				float leftEdge2 = RowLayout.RightEdge(rectTransform2.position.x, rectTransform2.rect.xMax, rectTransform2.lossyScale.x) + 10f * x;
				MoveTo(label.rectTransform, leftEdge2, centerY);
			}
		}

		private static RectTransform RightmostShown(Transform row)
		{
			if (row == null || !row.gameObject.activeInHierarchy)
			{
				return null;
			}
			RectTransform rectTransform = null;
			float num = 0f;
			for (int i = 0; i < row.childCount; i++)
			{
				RectTransform rectTransform2 = row.GetChild(i) as RectTransform;
				if (!(rectTransform2 == null) && rectTransform2.gameObject.activeInHierarchy)
				{
					float num2 = RowLayout.RightEdge(rectTransform2.position.x, rectTransform2.rect.xMax, rectTransform2.lossyScale.x);
					if (!(rectTransform != null) || !(num2 <= num))
					{
						rectTransform = rectTransform2;
						num = num2;
					}
				}
			}
			return rectTransform;
		}

		private static void MoveTo(RectTransform rt, float leftEdge, float centerY)
		{
			float num = RowLayout.PivotXForLeftEdge(leftEdge, rt.rect.xMin, rt.lossyScale.x);
			float num2 = RowLayout.PivotYForCenter(centerY, rt.rect.center.y, rt.lossyScale.y);
			Vector3 position = rt.position;
			float num3 = 0.5f * Mathf.Abs(rt.lossyScale.x);
			if (!(Mathf.Abs(position.x - num) < num3) || !(Mathf.Abs(position.y - num2) < num3))
			{
				rt.position = new Vector3(num, num2, position.z);
			}
		}

		private static void IgnoreLayout(GameObject go)
		{
			LayoutElement layoutElement = go.GetComponent(typeof(LayoutElement)) as LayoutElement;
			if (layoutElement == null)
			{
				layoutElement = go.AddComponent(typeof(LayoutElement)) as LayoutElement;
			}
			if (layoutElement != null)
			{
				layoutElement.ignoreLayout = true;
			}
		}

		private static void Show(Button button, bool visible)
		{
			if (!(button == null) && button.gameObject.activeSelf != visible)
			{
				button.gameObject.SetActive(visible);
			}
		}

		private void Fail(string what, Exception e)
		{
			_failed = true;
			string text = _ctx.T("游戏自己的「切换」按钮", "Game's own Switch button: ");
			string text2 = _ctx.T("（之后用控制栏上的按钮换边）", " (switch sides from the control bar instead)");
			if (e != null)
			{
				_ctx.Log.Error(text + what + text2, e);
			}
			else
			{
				_ctx.Log.Warn(text + what + text2);
			}
		}
	}
}
