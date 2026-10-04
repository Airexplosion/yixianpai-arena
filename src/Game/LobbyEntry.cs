using System;
using TMPro;
using UnityEngine;
using Yx.ModSdk;
using Yx.Shared;

namespace YxArena.Game
{
	public sealed class LobbyEntry
	{
		private const string PanelName = "GameEntrancePanel";

		private const string PartName = "SinglePlayerModePart";

		private const string ItemScript = "SinglePlayerModeItem";

		private const string CloneName = "YxArenaEntry";

		private readonly ModContext _ctx;

		private readonly Action _enter;

		private GameObject _clone;

		private bool _hooked;

		private bool _gaveUp;

		private bool _announced;

		private static string Title => Loc.T("练习场", "Arena");

		private static string Hint => Loc.T("本地练习：自选卡牌，打木人", "Local practice: pick cards, fight a dummy");

		public bool Injected
		{
			get
			{
				if (_hooked)
				{
					return _clone != null;
				}
				return false;
			}
		}

		public LobbyEntry(ModContext ctx, Action enter)
		{
			_ctx = ctx;
			_enter = enter;
		}

		public void Install()
		{
			_hooked = _ctx.Hooks.TryPrefix("SinglePlayerModeItem", "OnClick", 1, OnItemClick) != null;
			if (!_hooked)
			{
				_ctx.Log.Warn(_ctx.T("单人模式页里的入口不可用：用 CTRL+ALT+1 进练习场", "The Single Player entry is unavailable: use CTRL+ALT+1 to enter the arena"));
			}
		}

		private bool OnItemClick(HookContext h)
		{
			if (_clone == null)
			{
				return true;
			}
			if (!(h.Instance is SinglePlayerModeItem singlePlayerModeItem) || singlePlayerModeItem.transform != _clone.transform)
			{
				return true;
			}
			h.Skip(null);
			_enter();
			return false;
		}

		public void Tick()
		{
			if (!_hooked || _gaveUp)
			{
				return;
			}
			try
			{
				if (_clone != null)
				{
					KeepTexts();
					return;
				}
				Transform transform = FindShownPart();
				if (!(transform == null))
				{
					Inject(transform);
				}
			}
			catch (Exception ex)
			{
				_gaveUp = true;
				_ctx.Log.Warn(_ctx.T("往单人模式页加入口失败，不再重试（用 CTRL+ALT+1 进练习场）：", "Failed to add the entry to the Single Player page; won't retry (use CTRL+ALT+1 to enter the arena): ") + ex.Message);
			}
		}

		private static Transform FindShownPart()
		{
			LobbyPanel lobbyPanel = ILRPanelBase.FindILRPanel<LobbyPanel>();
			if (lobbyPanel == null || lobbyPanel.panel == null)
			{
				return null;
			}
			UISubPanelBase uISubPanelBase = lobbyPanel.panel.FindSubPanel("GameEntrancePanel");
			if (uISubPanelBase == null || !uISubPanelBase.isShow)
			{
				return null;
			}
			Component[] componentsInChildren = uISubPanelBase.GetComponentsInChildren(typeof(Transform), includeInactive: false);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				Transform transform = componentsInChildren[i] as Transform;
				if (transform != null && transform.name == "SinglePlayerModePart")
				{
					return transform;
				}
			}
			return null;
		}

		private void Inject(Transform part)
		{
			Component[] componentsInChildren = part.GetComponentsInChildren(typeof(ILRComponentBridge), includeInactive: false);
			GameObject gameObject = null;
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				ILRComponentBridge iLRComponentBridge = componentsInChildren[i] as ILRComponentBridge;
				if (!(iLRComponentBridge == null) && !(iLRComponentBridge.scriptName != "SinglePlayerModeItem"))
				{
					if (iLRComponentBridge.gameObject.name == "YxArenaEntry")
					{
						_clone = iLRComponentBridge.gameObject;
						return;
					}
					if (gameObject == null)
					{
						gameObject = iLRComponentBridge.gameObject;
					}
				}
			}
			if (!(gameObject == null))
			{
				_clone = UnityEngine.Object.Instantiate((UnityEngine.Object)gameObject, gameObject.transform.parent) as GameObject;
				if (_clone == null)
				{
					throw new InvalidOperationException("Instantiate 返回了空");
				}
				_clone.name = "YxArenaEntry";
				_clone.SetActive(value: true);
				_clone.transform.SetAsLastSibling();
				KeepTexts();
				if (!_announced)
				{
					_ctx.Log.Info(_ctx.T("单人模式页里已加入「练习场」入口（克隆自 ", "Added the \"Arena\" entry to the Single Player page (cloned from ") + gameObject.name + _ctx.T("）", ")"));
				}
				_announced = true;
			}
		}

		private void KeepTexts()
		{
			Component[] componentsInChildren = _clone.GetComponentsInChildren(typeof(TextMeshProUGUI), includeInactive: true);
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				TextMeshProUGUI textMeshProUGUI = componentsInChildren[i] as TextMeshProUGUI;
				if (!(textMeshProUGUI == null))
				{
					if (textMeshProUGUI.gameObject.name == "TitleLabel" && textMeshProUGUI.text != Title)
					{
						textMeshProUGUI.text = Title;
					}
					else if (textMeshProUGUI.gameObject.name == "HintLabel" && textMeshProUGUI.text != Hint)
					{
						textMeshProUGUI.text = Hint;
					}
				}
			}
		}

		public void OnLeftLobby()
		{
			_clone = null;
		}
	}
}
