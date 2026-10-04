namespace YxArena.Draws
{
	public sealed class DrawRule
	{
		public const int RawNone = 0;

		public const int RawPool = 1;

		public const int RawOwnDebuff = 2;

		public const int RawBasicDebuff = 3;

		public const int RawThreeDebuff = 4;

		public const int RawChoice = 5;

		public const int RawHandCount = 6;

		public const int RawHandNameBonus = 7;

		public const int RawNothing = 8;

		public const int RawElementHand = 9;

		public const int SrcAttack = 1;

		public const int SrcRandomAttack = 2;

		public const int SrcDef = 3;

		public const int SrcRandomDef = 4;

		public const int SrcZero = 5;

		public const int SrcParam = 100;

		public const int Unbounded = 99;

		public int BaseId;

		public int Raw;

		public int Pool;

		public int FixedRarity = -1;

		public int RawCountParam = -1;

		public int RawCountFixed = 1;

		public int Choices;

		public int CapParam = -1;

		public string Keyword = "";

		public int[] ValueRanges = new int[0];

		public bool ValueSetsRawCount;

		public int RawBudget(CardFacts card)
		{
			int num = ((RawCountParam >= 0) ? card.Param(RawCountParam) : RawCountFixed);
			if (num >= 0)
			{
				return num;
			}
			return 0;
		}
	}
}
