namespace YxArena.Draws
{
	public static class CardPools
	{
		public const int None = 0;

		public const int SectAny = 1;

		public const int NameKuangJian = 2;

		public const int SectOrCareer = 3;

		public const int SectAttack = 4;

		public const int SectNoAttack = 5;

		public const int OtherSect = 6;

		public const int NameZong = 7;

		public const int AgainOrShenFa = 8;

		public const int NameMa = 9;

		public const int MiShu = 10;

		public const int Exclusive = 11;

		public const int DreamHuaShen = 12;

		public const int JiYuan = 13;

		public const int LevelFanXu = 14;

		public const int CareerYuanYing = 15;

		public const int TreasureYuanYingUp = 16;

		public const int NameBengQuan = 17;

		private const int LevelYuanYing = 4;

		private const int LevelHuaShen = 5;

		private const int LevelFanXuValue = 6;

		public static bool Matches(int pool, CardFacts card, CardFacts parent, int rarity, int ownerSect)
		{
			if (card == null || card.Id <= 0 || card.Hidden || card.Obsolete)
			{
				return false;
			}
			if (card.Rarity != rarity)
			{
				return false;
			}
			if (parent != null && card.BaseId == parent.BaseId)
			{
				return false;
			}
			bool flag = card.Sect != 0 && card.Subcategory == 0 && card.Owner == 0;
			switch (pool)
			{
			case 1:
				return flag;
			case 2:
				return card.Name.Contains("狂剑");
			case 3:
				if (!flag)
				{
					if (card.Career != 0)
					{
						return card.Subcategory == 0;
					}
					return false;
				}
				return true;
			case 4:
				if (flag)
				{
					return card.Attack > 0;
				}
				return false;
			case 5:
				if (flag)
				{
					return card.Attack <= 0;
				}
				return false;
			case 6:
				if (flag && ownerSect != 0)
				{
					return card.Sect != ownerSect;
				}
				return false;
			case 7:
				return card.Name.Contains("粽");
			case 8:
				if (flag || card.Career != 0)
				{
					if (!card.ActionAgain && !card.Desc.Contains("身法"))
					{
						return card.Desc.Contains("再次行动");
					}
					return true;
				}
				return false;
			case 9:
				return card.Name.Contains("马");
			case 10:
				return card.Subcategory == 4;
			case 11:
				return card.Owner != 0;
			case 12:
				if (card.Subcategory == 14)
				{
					return card.Level == 5;
				}
				return false;
			case 13:
				return card.Subcategory == 1;
			case 14:
				if (card.Level == 6)
				{
					if (!flag)
					{
						return card.Career != 0;
					}
					return true;
				}
				return false;
			case 15:
				if (card.Career != 0 && card.Subcategory == 0)
				{
					return card.Level == 4;
				}
				return false;
			case 16:
				if (card.Level >= 4)
				{
					if (card.Subcategory != 3 && card.Subcategory != 2)
					{
						return card.Subcategory == 4;
					}
					return true;
				}
				return false;
			case 17:
				return card.Name.Contains("崩拳");
			default:
				return false;
			}
		}

		public static int[] Build(int pool, CardFacts[] all, CardFacts parent, int rarity, int ownerSect)
		{
			int num = 0;
			for (int i = 0; i < all.Length; i++)
			{
				if (Matches(pool, all[i], parent, rarity, ownerSect))
				{
					num++;
				}
			}
			int[] array = new int[num];
			int num2 = 0;
			for (int j = 0; j < all.Length; j++)
			{
				if (Matches(pool, all[j], parent, rarity, ownerSect))
				{
					array[num2++] = all[j].Id;
				}
			}
			for (int k = 1; k < array.Length; k++)
			{
				int num3 = array[k];
				int num4 = k - 1;
				while (num4 >= 0 && array[num4] > num3)
				{
					array[num4 + 1] = array[num4];
					num4--;
				}
				array[num4 + 1] = num3;
			}
			return array;
		}
	}
}
