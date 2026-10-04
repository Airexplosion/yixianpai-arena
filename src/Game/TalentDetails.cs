using System.Collections.Generic;
using Proto;

namespace YxArena.Game
{
	public static class TalentDetails
	{
		public const int Bottle = 199;

		public const int Learning = 189;

		public static void Read(ArenaSide side, BattlePlayerData pub, BattlePlayerPrivateData priv)
		{
			BattleTalentData value;
			side.SetLearnedCards((pub != null && pub.talentDatas != null && pub.talentDatas.TryGetValue(189, out value) && value != null) ? value.commonParams : null);
			side.SetBottleCards((priv != null && priv.talentDatas != null && priv.talentDatas.TryGetValue(199, out value) && value != null) ? value.commonParams : null);
		}

		public static void WritePublic(ArenaSide side, BattlePlayerData pub)
		{
			if (pub.talentDatas == null) pub.talentDatas = new Dictionary<int, BattleTalentData>();
			BattleTalentData battleTalentData = new BattleTalentData();
			for (int i = 0; i < side.LearnedCards.Count; i++)
			{
				battleTalentData.commonParams.Add(side.LearnedCards[i]);
			}
			pub.talentDatas[189] = battleTalentData;
		}

		public static void WritePrivate(ArenaSide side, BattlePlayerPrivateData priv)
		{
			if (priv.talentDatas == null) priv.talentDatas = new Dictionary<int, BattleTalentData>();
			BattleTalentData battleTalentData = new BattleTalentData();
			for (int i = 0; i < side.BottleCards.Count; i++)
			{
				battleTalentData.commonParams.Add(side.BottleCards[i]);
			}
			while (battleTalentData.commonParams.Count < 2)
			{
				battleTalentData.commonParams.Add(0);
			}
			priv.talentDatas[199] = battleTalentData;
		}
	}
}
