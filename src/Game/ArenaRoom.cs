using System;
using Proto;
using UnityEngine;
using Yx.ModSdk;
using Yx.ModSdk.Hooks;
using Yx.ModSdk.Unity;
using Yx.Shared;

namespace YxArena.Game
{
	public sealed class ArenaRoom
	{
		private readonly ModContext _ctx;

		private readonly ArenaSession _session;

		private readonly Action _enter;

		private readonly Action<int, RectTransform> _pickTalent;

		private SinglePlayerRoomPanel _panel;

		private RogueModePart _part;

		private bool _open;

		private bool _hooked;

		private bool _seenShown;

		private int _side;

		private int _savedLastCharacter;

		public bool Available => _hooked;

		public bool IsOpen => _open;

		public int Side => _side;

		public ArenaRoom(ModContext ctx, ArenaSession session, Action enter, Action<int, RectTransform> pickTalent)
		{
			_pickTalent = pickTalent;
			_ctx = ctx;
			_session = session;
			_enter = enter;
		}

		public void Install()
		{
			HookGroup hookGroup = _ctx.Hooks.Group("选英雄界面");
			hookGroup.Prefix("RogueModePart", "OnShow", 0, OnRoguePartShow);
			hookGroup.Prefix("SinglePlayerRoomPanel", "OnGameButtonClick", 0, OnStartClick);
			_hooked = hookGroup.Complete;
			if (!_hooked)
			{
				hookGroup.CancelAll();
				_ctx.Log.Warn(_ctx.T("选英雄界面的钩子不全（", "Hero-select hooks incomplete (") + hookGroup.Missing + _ctx.T("），练习场直接用上次的角色进场", "); the arena enters with the last-used character"));
			}
			else if (_ctx.Hooks.TryPrefix("TalentItem", "OnClick", 1, OnTalentClick) == null)
			{
				_ctx.Log.Warn(_ctx.T("仙命图标的点击钩子没挂上：选英雄界面上的仙命只能用角色自带的", "Talent icon click hook not installed: talents on the hero-select screen are limited to the character's innate ones"));
			}
		}

		public void Open()
		{
			if (!_hooked || _open)
			{
				return;
			}
			LobbyPanel lobbyPanel = ILRPanelBase.FindILRPanel<LobbyPanel>();
			if (lobbyPanel == null)
			{
				return;
			}
			_panel = lobbyPanel.FindILRSubPanelRuntime<SinglePlayerRoomPanel>();
			if (_panel == null)
			{
				Ui.Toast(_ctx.T("没能打开选英雄界面", "Couldn't open the hero-select screen"));
				return;
			}
			_savedLastCharacter = SettingsManager.GetLastCharacterId(GameMode.RogueMode);
			_open = true;
			_seenShown = false;
			_side = 0;
			try
			{
				UISubPanelBase uISubPanelBase = ((lobbyPanel.panel != null) ? lobbyPanel.panel.FindSubPanel("GameEntrancePanel") : null);
				if (uISubPanelBase != null && uISubPanelBase.isShow)
				{
					_panel.SetFromPanel(lobbyPanel.FindILRSubPanel<GameEntrancePanel>());
				}
				_panel.ShowPart<RogueModePart>();
				_part = _panel.FindPart<RogueModePart>();
				SetPartVisible(visible: false);
				ShowSide();
			}
			catch (Exception)
			{
				Closed();
				throw;
			}
		}

		private void SetPartVisible(bool visible)
		{
			if (_part != null && !(_part.transform == null))
			{
				GameObject gameObject = _part.transform.gameObject;
				CanvasGroup canvasGroup = gameObject.GetComponent(typeof(CanvasGroup)) as CanvasGroup;
				if (canvasGroup == null)
				{
					canvasGroup = gameObject.AddComponent(typeof(CanvasGroup)) as CanvasGroup;
				}
				canvasGroup.alpha = (visible ? 1f : 0f);
				canvasGroup.blocksRaycasts = visible;
				canvasGroup.interactable = visible;
			}
		}

		public void Close()
		{
			if (!_open)
			{
				return;
			}
			try
			{
				if (_panel != null && _panel.panel != null && _panel.panel.isShow)
				{
					_panel.Hide();
				}
			}
			catch (Exception ex)
			{
				_ctx.Log.Warn(_ctx.T("关闭选英雄界面出错：", "Error closing the hero-select screen: ") + ex.Message);
			}
			Closed();
		}

		private void Closed()
		{
			_open = false;
			try
			{
				SetPartVisible(visible: true);
				if (_savedLastCharacter != 0)
				{
					SettingsManager.SetLastCharacterId(_savedLastCharacter, GameMode.RogueMode);
				}
			}
			catch (Exception ex)
			{
				_ctx.Log.Warn(_ctx.T("还原选英雄界面出错：", "Error restoring the hero-select screen: ") + ex.Message);
			}
			_session.Persist();
		}

		public void Tick()
		{
			if (!_open || _panel == null)
			{
				return;
			}
			try
			{
				bool flag = _panel.panel != null && _panel.panel.isShow;
				if (flag)
				{
					_seenShown = true;
				}
				else if (_seenShown)
				{
					Closed();
					return;
				}
				if (flag)
				{
					SyncFromRoom();
					PaintTalents();
				}
			}
			catch (Exception exception)
			{
				_ctx.Log.Error(_ctx.T("选英雄界面同步出错，已关闭", "Hero-select sync failed; closed"), exception);
				Close();
			}
		}

		public static int[] InnateTalents(int characterId)
		{
			CharacterConfig characterConfig = ConfigManager.GetCharacterConfig(characterId);
			if (characterConfig == null || characterConfig.talents == null)
			{
				return new int[0];
			}
			int[] array = new int[characterConfig.talents.Count];
			for (int i = 0; i < array.Length; i++)
			{
				array[i] = characterConfig.talents[i];
			}
			return array;
		}

		private void SyncFromRoom()
		{
			RoomCharacterSelectionPart characterSelectionPart = _panel.characterSelectionPart;
			if (characterSelectionPart != null && !characterSelectionPart.isRandom && characterSelectionPart.characterId > 0)
			{
				ArenaSide arenaSide = _session.SideAt(_side);
				if (arenaSide.CharacterId != characterSelectionPart.characterId || !arenaSide.HasAnyTalent())
				{
					arenaSide.SetCharacter(characterSelectionPart.characterId, characterSelectionPart.skinNumber, characterSelectionPart.skinColor, InnateTalents(characterSelectionPart.characterId));
					return;
				}
				arenaSide.SkinNumber = characterSelectionPart.skinNumber;
				arenaSide.SkinColor = characterSelectionPart.skinColor;
			}
		}

		private void ShowSide()
		{
			ArenaSide arenaSide = _session.SideAt(_side);
			if (!arenaSide.HasAnyTalent())
			{
				arenaSide.SetCharacter(arenaSide.CharacterId, arenaSide.SkinNumber, arenaSide.SkinColor, InnateTalents(arenaSide.CharacterId));
			}
			_panel.characterSelectionPart.SetCharacterInfo(arenaSide.CharacterId, arenaSide.SkinNumber, arenaSide.SkinColor, forceRefresh: true);
		}

		public void SwitchSide()
		{
			if (_open)
			{
				SyncFromRoom();
				_side = 1 - _side;
				ShowSide();
				_session.Persist();
			}
		}

		private void PaintTalents()
		{
			Transform transform = TalentContainer();
			if (transform == null)
			{
				return;
			}
			ArenaSide arenaSide = _session.SideAt(_side);
			for (int i = 0; i < transform.childCount && i < 5; i++)
			{
				TalentItem talentItem = ItemAt(transform, i);
				if (talentItem != null && arenaSide.Talents[i] != 0 && talentItem.talentId != arenaSide.Talents[i])
				{
					talentItem.talentId = arenaSide.Talents[i];
				}
			}
		}

		private Transform TalentContainer()
		{
			return _panel.roomCharItem?.FindComponent<RectTransform>("Talents");
		}

		private static TalentItem ItemAt(Transform container, int index)
		{
			ILRComponentBridge iLRComponentBridge = container.GetChild(index).GetComponent(typeof(ILRComponentBridge)) as ILRComponentBridge;
			if (!(iLRComponentBridge != null))
			{
				return null;
			}
			return iLRComponentBridge.GetILRObject<TalentItem>();
		}

		private bool OnRoguePartShow(HookContext h)
		{
			if (!_open)
			{
				return true;
			}
			h.Skip(null);
			return false;
		}

		private bool OnStartClick(HookContext h)
		{
			if (!_open)
			{
				return true;
			}
			h.Skip(null);
			try
			{
				SyncFromRoom();
				Close();
				_enter();
			}
			catch (Exception exception)
			{
				_ctx.Log.Error(_ctx.T("从选英雄界面进场出错", "Entering from the hero-select screen failed"), exception);
			}
			return false;
		}

		private bool OnTalentClick(HookContext h)
		{
			if (!_open || _panel == null)
			{
				return true;
			}
			TalentItem talentItem = h.Instance as TalentItem;
			Transform transform = TalentContainer();
			if (talentItem == null || transform == null || talentItem.transform.parent != transform)
			{
				return true;
			}
			int siblingIndex = talentItem.transform.GetSiblingIndex();
			if (siblingIndex < 0 || siblingIndex >= 5)
			{
				return true;
			}
			h.Skip(null);
			try
			{
				_pickTalent(siblingIndex, talentItem.transform as RectTransform);
			}
			catch (Exception exception)
			{
				_ctx.Log.Error(_ctx.T("打开全仙命表出错", "Opening the all-talents list failed"), exception);
			}
			return false;
		}
	}
}
