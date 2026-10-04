namespace YxArena.Draws
{
	public static class DrawRules
	{
		private static DrawRule[] s_rules;

		public static DrawRule Find(int baseId)
		{
			DrawRule[] array = All();
			for (int i = 0; i < array.Length; i++)
			{
				if (array[i].BaseId == baseId)
				{
					return array[i];
				}
			}
			return null;
		}

		public static DrawRule[] All()
		{
			if (s_rules != null)
			{
				return s_rules;
			}
			s_rules = new DrawRule[54]
			{
				PoolRule(6000011, 1, -1, -1),
				PoolRule(64, 1, 0, -1),
				PoolRule(186, 2, 0, -1),
				PoolRule(352, 3, 0, -1),
				PoolRule(353, 4, 0, -1),
				PoolRule(354, 5, 0, -1),
				PoolRule(355, 6, 0, -1),
				PoolRule(358, 7, 0, -1),
				PoolRule(359, 8, 0, -1),
				PoolRule(360, 9, 0, -1),
				PoolRule(361, 10, 0, -1),
				PoolRule(362, 11, 0, -1),
				PoolRule(363, 12, 0, -1),
				PoolRule(364, 13, 0, -1),
				PoolRule(365, 14, 0, -1),
				PoolRule(380, 15, -1, 1),
				PoolRule(341, 16, -1, -1),
				PoolRule(10000065, 17, 0, 0),
				DebuffRule(2000004, 2, 0),
				DebuffRule(9000026, 2, -1),
				DebuffRule(100, 2, -1),
				DebuffRule(105, 2, -1),
				DebuffRule(310, 2, 0),
				Unbounded(417, 2),
				Unbounded(82, 2),
				DebuffRule(10000009, 2, -1),
				DebuffRule(10000024, 2, 0),
				DebuffRule(10000049, 2, 0),
				DebuffRule(10000092, 2, 1),
				DebuffRule(6000007, 3, -1),
				DebuffRule(73, 3, 1),
				Unbounded(4000085, 4),
				FuGuang(),
				ChoiceRule(6000014, 3, -1),
				ChoiceRule(159, 3, 0),
				HandRule(9, 6, 0, ""),
				HandRule(1000052, 7, 0, "云剑"),
				new DrawRule { BaseId = 7000111, Raw = DrawRule.RawElementHand, RawCountFixed = 2, CapParam = 0 },
				Nothing(262),
				Nothing(349),
				Nothing(395),
				Nothing(7000075),
				Nothing(7000078),
				Nothing(10000089),
				Ranges(3000001, P(0), P(1)),
				Ranges(6, P(0), P(1)),
				Ranges(4000007, P(0), P(1)),
				Ranges(4000017, P(0), P(1)),
				Ranges(4000019, P(0), P(1)),
				Ranges(4000053, 5, P(0), 5, P(1)),
				Ranges(4000063, 1, 2, 1, 2),
				Ranges(4000068, 1, 2),
				Ranges(4000088, 1, 2),
				Ranges(4000091, 1, 2, P(0), P(1))
			};
			return s_rules;
		}

		private static int P(int index)
		{
			return 100 + index;
		}

		private static DrawRule PoolRule(int baseId, int pool, int countParam, int fixedRarity)
		{
			return new DrawRule
			{
				BaseId = baseId,
				Raw = 1,
				Pool = pool,
				RawCountParam = countParam,
				FixedRarity = fixedRarity
			};
		}

		private static DrawRule DebuffRule(int baseId, int raw, int countParam)
		{
			return new DrawRule
			{
				BaseId = baseId,
				Raw = raw,
				RawCountParam = countParam
			};
		}

		private static DrawRule Unbounded(int baseId, int raw)
		{
			return new DrawRule
			{
				BaseId = baseId,
				Raw = raw,
				RawCountFixed = 99
			};
		}

		private static DrawRule FuGuang()
		{
			DrawRule drawRule = new DrawRule();
			drawRule.BaseId = 193;
			drawRule.Raw = 3;
			drawRule.RawCountFixed = 0;
			drawRule.ValueRanges = new int[2]
			{
				P(0),
				P(1)
			};
			drawRule.ValueSetsRawCount = true;
			return drawRule;
		}

		private static DrawRule ChoiceRule(int baseId, int choices, int countParam)
		{
			return new DrawRule
			{
				BaseId = baseId,
				Raw = 5,
				Choices = choices,
				RawCountParam = countParam
			};
		}

		private static DrawRule HandRule(int baseId, int raw, int capParam, string keyword)
		{
			return new DrawRule
			{
				BaseId = baseId,
				Raw = raw,
				CapParam = capParam,
				Keyword = keyword
			};
		}

		private static DrawRule Nothing(int baseId)
		{
			return new DrawRule
			{
				BaseId = baseId,
				Raw = 8,
				RawCountFixed = 99
			};
		}

		private static DrawRule Ranges(int baseId, params int[] sources)
		{
			return new DrawRule
			{
				BaseId = baseId,
				ValueRanges = sources
			};
		}
	}
}
