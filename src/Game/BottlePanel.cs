using System.Collections.Generic;
using Proto;
using Yx.ModSdk.Hooks;
using Yx.ModSdk.Unity;
using Yx.Shared;

namespace YxArena.Game
{
	public static class BottlePanel
	{
		public static void Install(HookGroup hooks)
		{
			hooks.Prefix("Talent199Panel", "OnShow", 0, OnShow);
			hooks.Prefix("Talent199Panel", "OnHide", 0, OnHide);
		}

		private static bool OnShow(HookContext h)
		{
			if (!ArenaSession.Active)
			{
				return true;
			}
			if (!(h.Instance is Talent199Panel talent199Panel))
			{
				return true;
			}
			BattleTalentData battleTalentData = EnsureSlots(talent199Panel);
			List<CardGridCunQuItem> cardGrids = talent199Panel.cardGridContainer.cardGrids;
			for (int i = 0; i < cardGrids.Count; i++)
			{
				cardGrids[i].gridType = CardPosition.Talent199;
				cardGrids[i].Refresh(battleTalentData.commonParams[i]);
			}
			return true;
		}

		private static bool OnHide(HookContext h)
		{
			if (!ArenaSession.Active)
			{
				return true;
			}
			if (h.Instance is Talent199Panel panel)
			{
				EnsureSlots(panel);
			}
			return true;
		}

		private static BattleTalentData EnsureSlots(Talent199Panel panel)
		{
			Dictionary<int, BattleTalentData> talentDatas = BattleManager.Instance.currentGameStatus.playerPrivateData.talentDatas;
			if (!talentDatas.TryGetValue(199, out var value) || value == null)
			{
				value = (talentDatas[199] = new BattleTalentData());
			}
			int count = panel.cardGridContainer.cardGrids.Count;
			while (value.commonParams.Count < count)
			{
				value.commonParams.Add(0);
			}
			return value;
		}

		private static Talent199Panel Find()
		{
			return ILRPanelBase.FindILRPanel<BattlePanel>()?.FindILRSubPanel<Talent199Panel>();
		}

		public static void Open()
		{
			if (ArenaSession.Active && BattleManager.Instance.currentScene == SceneType.修炼阶段)
			{
				GameStatus currentGameStatus = BattleManager.Instance.currentGameStatus;
				if (currentGameStatus == null || !currentGameStatus.GetMainPlayerData().talents.Contains(199))
				{
					Ui.Toast(Loc.T("请先选择「五行玉瓶」仙命", "Select the Five Elements Jade Bottle talent first"));
				}
				else
				{
					Talent199Panel.TogglePanel();
				}
			}
		}

		public static void Capture()
		{
			Talent199Panel talent199Panel = Find();
			if (talent199Panel != null && !(talent199Panel.panel == null) && talent199Panel.panel.isShow && !talent199Panel.hiding)
			{
				BattleTalentData battleTalentData = EnsureSlots(talent199Panel);
				List<CardGridCunQuItem> cardGrids = talent199Panel.cardGridContainer.cardGrids;
				for (int i = 0; i < cardGrids.Count; i++)
				{
					CardItem card = cardGrids[i].GetCard();
					battleTalentData.commonParams[i] = ((card != null && card.cardInfo != null) ? card.cardInfo.id : 0);
				}
			}
		}

		public static void Close()
		{
			Talent199Panel talent199Panel = Find();
			if (talent199Panel != null && talent199Panel.panel != null && talent199Panel.panel.isShow && !talent199Panel.hiding)
			{
				Capture();
				talent199Panel.Hide();
			}
		}
	}
}
