using System;
using System.Collections.Generic;
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
			if (player.publicData.lastRoundData.usedCards == null)
			{
				throw new InvalidOperationException("战绩缺少本轮场上卡牌快照");
			}
			if (player.publicData.lastRoundData.talents == null)
			{
				throw new InvalidOperationException("战绩缺少本轮仙命快照");
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
			return Side(player, null);
		}

		public static ArenaSide Side(PlayerData player, Action<string> progress)
		{
			Step(progress, "检查玩家快照");
			ValidatePlayer(player);
			BattlePlayerData publicData = player.publicData;
			BattlePlayerPrivateData privateData = player.privateData;
			BattlePlayerLastRoundData lastRoundData = publicData.lastRoundData;
			Step(progress, "补齐可缺省记录" + MissingOptionalData(lastRoundData, privateData));
			NormalizeOptionalData(lastRoundData, privateData);
			Step(progress, "读取角色和境界");
			int level = (int)lastRoundData.level;
			if (level <= 0) level = (int)publicData.level;
			ArenaSide arenaSide = new ArenaSide(publicData.username, publicData.characterId, level, 0);
			arenaSide.Bonus = lastRoundData.extraMaxHp;
			arenaSide.SkinNumber = publicData.skinNumber;
			arenaSide.SkinColor = publicData.skinColor;
			arenaSide.UnlockedGrids = Slots(player);
			// 仙命与计数必须来自同一轮的快照，不能混用结算后的 publicData。
			Step(progress, "读取本轮仙命与永久状态");
			arenaSide.ImportTalents(lastRoundData.talents, lastRoundData.talentTempDatas);
			lastRoundData.permanentBuffTempDatas.TryGetValue(10023, out arenaSide.TiPo);
			lastRoundData.permanentBuffTempDatas.TryGetValue(10024, out arenaSide.TiPoMax);
			Step(progress, "读取本轮手牌和场上卡牌");
			for (int i = 0; i < lastRoundData.handCards.Count; i++)
			{
				arenaSide.Hand.Add(lastRoundData.handCards[i]);
			}
			arenaSide.SetBoard(lastRoundData.usedCards.ToArray());
			Step(progress, "读取天衍仙命");
			for (int j = 0; j < lastRoundData.fateStrategies.Count; j++)
			{
				arenaSide.Fates.Add(lastRoundData.fateStrategies[j]);
			}
			Step(progress, "同步本轮公共数据");
			publicData.level = (Level)level;
			publicData.life = lastRoundData.life;
			publicData.exp = lastRoundData.exp;
			publicData.extraMaxHp = lastRoundData.extraMaxHp;
			publicData.talents = lastRoundData.talents;
			publicData.permanentBuffTempDatas = lastRoundData.permanentBuffTempDatas;
			publicData.talentTempDatas = lastRoundData.talentTempDatas;
			publicData.talentDatas = lastRoundData.talentDatas;
			Step(progress, "同步共鸣、刻印和副职记录");
			privateData.talentDatas = lastRoundData.privateTalentDatas;
			privateData.talentResonanceData = lastRoundData.talentResonanceData;
			privateData.keYinData.usedCards = lastRoundData.usedKeYinCards;
			privateData.FZJXCareers = lastRoundData.FZJXCareers;
			Step(progress, "读取悟剑和玉瓶记录");
			TalentDetails.Read(arenaSide, publicData, privateData);
			return arenaSide;
		}

		private static void Step(Action<string> progress, string step)
		{
			if (progress != null) progress(step);
		}

		private static string MissingOptionalData(BattlePlayerLastRoundData snapshot, BattlePlayerPrivateData priv)
		{
			var missing = new List<string>();
			if (snapshot.handCards == null) missing.Add("handCards");
			if (snapshot.fateStrategies == null) missing.Add("fateStrategies");
			if (snapshot.usedKeYinCards == null) missing.Add("usedKeYinCards");
			if (snapshot.talentTempDatas == null) missing.Add("talentTempDatas");
			if (snapshot.permanentBuffTempDatas == null) missing.Add("permanentBuffTempDatas");
			if (snapshot.talentDatas == null) missing.Add("talentDatas");
			if (snapshot.privateTalentDatas == null) missing.Add("privateTalentDatas");
			if (snapshot.FZJXCareers == null) missing.Add("FZJXCareers");
			if (snapshot.talentResonanceData == null) missing.Add("talentResonanceData");
			if (priv.keYinData == null) missing.Add("privateData.keYinData");
			return missing.Count == 0 ? "" : "（缺项：" + string.Join(",", missing.ToArray()) + "）";
		}

		private static void NormalizeOptionalData(BattlePlayerLastRoundData snapshot, BattlePlayerPrivateData priv)
		{
			// Optional records may be explicitly null after the game's JSON clone.
			// An absent record means no extra state; never substitute post-round
			// public/private state into the selected round's snapshot.
			if (snapshot.handCards == null) snapshot.handCards = new List<int>();
			if (snapshot.fateStrategies == null) snapshot.fateStrategies = new List<int>();
			if (snapshot.usedKeYinCards == null) snapshot.usedKeYinCards = new List<int>();
			if (snapshot.talentTempDatas == null) snapshot.talentTempDatas = new Dictionary<int, int>();
			if (snapshot.permanentBuffTempDatas == null) snapshot.permanentBuffTempDatas = new Dictionary<int, int>();
			if (snapshot.talentDatas == null) snapshot.talentDatas = new Dictionary<int, BattleTalentData>();
			if (snapshot.privateTalentDatas == null) snapshot.privateTalentDatas = new Dictionary<int, BattleTalentData>();
			if (snapshot.FZJXCareers == null) snapshot.FZJXCareers = new Dictionary<int, int>();
			if (snapshot.talentResonanceData == null) snapshot.talentResonanceData = new TalentResonanceData();
			if (priv.keYinData == null) priv.keYinData = new KeYinData();
		}
	}
}
