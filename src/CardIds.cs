namespace YxArena
{
	public static class CardIds
	{
		public const int MaxRarity = 4;

		public const int HandLimit = 50;

		private const int RarityUnit = 10000;

		public static int RarityOf(int id)
		{
			return id / 10000 % 100;
		}

		public static int BaseOf(int id)
		{
			return id - RarityOf(id) * 10000;
		}

		public static int WithRarity(int id, int rarity)
		{
			int num = ((rarity >= 0) ? ((rarity > 4) ? 4 : rarity) : 0);
			return BaseOf(id) + num * 10000;
		}

		public static int Pick(int id, int rarity, ICardCatalog catalog)
		{
			if (rarity < 0 || rarity > 4)
			{
				return 0;
			}
			int num = WithRarity(id, rarity);
			if (!catalog.Exists(num))
			{
				return 0;
			}
			return num;
		}

		public static int HighestRarity(int id, ICardCatalog catalog)
		{
			for (int num = 4; num >= 0; num--)
			{
				if (catalog.Exists(WithRarity(id, num)))
				{
					return num;
				}
			}
			return -1;
		}
	}
}
