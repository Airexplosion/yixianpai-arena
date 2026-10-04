using System;
using Proto;

namespace YxArena.Game
{
	public static class ReviewImport
	{
		public static void Validate(BattleResult source)
		{
			if (source == null || source.round < 1)
			{
				throw new InvalidOperationException("战绩缺少轮次");
			}
			ValidatePlayer(source.p1);
			ValidatePlayer(source.p2);
			if (source.mainViewId != source.p1.publicData.uid && source.mainViewId != source.p2.publicData.uid)
			{
				throw new InvalidOperationException("战绩缺少当前复盘视角");
			}
		}

		private static void ValidatePlayer(PlayerData player)
		{
			if (player == null || player.publicData == null || player.privateData == null || player.publicData.lastRoundData == null || player.publicData.characterId <= 0)
			{
				throw new InvalidOperationException("战绩缺少玩家数据");
			}
			int num = Slots(player);
			if (num < 1 || num > 8 || player.publicData.lastRoundData.usedCards.Count > 8)
			{
				throw new InvalidOperationException("战绩的牌位数据不完整或超出练习场支持范围");
			}
		}

		private static int Slots(PlayerData player)
		{
			int unlockGrids = player.publicData.lastRoundData.unlockGrids;
			if (unlockGrids <= 0)
			{
				return player.privateData.unlockGrids;
			}
			return unlockGrids;
		}

		public static ArenaSide Side(PlayerData player)
		{
			BattlePlayerData publicData = player.publicData;
			BattlePlayerPrivateData privateData = player.privateData;
			BattlePlayerLastRoundData lastRoundData = publicData.lastRoundData;
			ArenaSide arenaSide = new ArenaSide(publicData.username, publicData.characterId, (int)publicData.level, 0);
			arenaSide.Bonus = lastRoundData.extraMaxHp;
			arenaSide.SkinNumber = publicData.skinNumber;
			arenaSide.SkinColor = publicData.skinColor;
			arenaSide.UnlockedGrids = Slots(player);
			// 仙命与计数必须来自同一轮的快照，不能混用结算后的 publicData。
			arenaSide.ImportTalents(lastRoundData.talents, lastRoundData.talentTempDatas);
			lastRoundData.permanentBuffTempDatas.TryGetValue(10023, out arenaSide.TiPo);
			lastRoundData.permanentBuffTempDatas.TryGetValue(10024, out arenaSide.TiPoMax);
			for (int i = 0; i < lastRoundData.handCards.Count; i++)
			{
				arenaSide.Hand.Add(lastRoundData.handCards[i]);
			}
			arenaSide.SetBoard(lastRoundData.usedCards.ToArray());
			for (int j = 0; j < lastRoundData.fateStrategies.Count; j++)
			{
				arenaSide.Fates.Add(lastRoundData.fateStrategies[j]);
			}
			publicData.life = lastRoundData.life;
			publicData.exp = lastRoundData.exp;
			publicData.extraMaxHp = lastRoundData.extraMaxHp;
			publicData.talents = lastRoundData.talents;
			publicData.permanentBuffTempDatas = lastRoundData.permanentBuffTempDatas;
			publicData.talentTempDatas = lastRoundData.talentTempDatas;
			publicData.talentDatas = lastRoundData.talentDatas;
			privateData.talentDatas = lastRoundData.privateTalentDatas;
			privateData.talentResonanceData = lastRoundData.talentResonanceData;
			privateData.keYinData.usedCards = lastRoundData.usedKeYinCards;
			privateData.FZJXCareers = lastRoundData.FZJXCareers;
			TalentDetails.Read(arenaSide, publicData, privateData);
			return arenaSide;
		}
	}
}
