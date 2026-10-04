namespace YxArena.Draws
{
	public sealed class DrawPlanner
	{
		private sealed class Frame
		{
			public CardFacts Card;

			public DrawRule Rule;

			public int RawLeft;

			public int ValueIndex;

			public int RawIndex;

			public int OwnerSect;
		}

		private const int MaxDepth = 8;

		private static readonly int[] BasicDebuffs = new int[6] { 100, 101, 102, 103, 104, 105 };

		private static readonly int[] ThreeDebuffs = new int[3] { 100, 101, 105 };

		private readonly CardFacts[] _all;

		private readonly Frame[] _stack = new Frame[8];

		private int _depth;

		private uint _state = 1u;

		private int _poolKey = -1;

		private int[] _poolIds = new int[0];

		public int RawDraws;

		public int ValueDraws;

		public DrawPlanner(CardFacts[] all)
		{
			_all = all ?? new CardFacts[0];
		}

		public void Reset(uint seed)
		{
			_state = ((seed == 0) ? 1u : seed);
			_depth = 0;
		}

		public void BeginCard(CardFacts card, bool nested, int ownerSect)
		{
			if (!nested)
			{
				_depth = 0;
			}
			if (card != null && _depth < 8)
			{
				Frame frame = new Frame();
				frame.Card = card;
				frame.Rule = DrawRules.Find(card.BaseId);
				frame.RawLeft = ((frame.Rule != null) ? frame.Rule.RawBudget(card) : 0);
				frame.OwnerSect = ownerSect;
				_stack[_depth] = frame;
				_depth++;
			}
		}

		private int Next(int exclusiveMax)
		{
			_state ^= _state << 13;
			_state ^= _state >> 17;
			_state ^= _state << 5;
			if (exclusiveMax > 1)
			{
				return (int)(_state % (uint)exclusiveMax);
			}
			return 0;
		}

		public int DrawValue(bool guaXiang)
		{
			ValueDraws++;
			Frame frame = ((_depth > 0) ? _stack[_depth - 1] : null);
			if (frame == null)
			{
				return Percent(guaXiang);
			}
			int valueIndex = frame.ValueIndex;
			frame.ValueIndex++;
			if (!RangeFor(frame, valueIndex, out var min, out var max))
			{
				return Percent(guaXiang);
			}
			if (max < min)
			{
				max = min;
			}
			int num = (guaXiang ? max : (min + Next(max - min + 1)));
			if (frame.Rule != null && frame.Rule.ValueSetsRawCount && valueIndex == 0)
			{
				frame.RawLeft = num;
			}
			return num;
		}

		private int Percent(bool guaXiang)
		{
			if (!guaXiang)
			{
				return Next(100);
			}
			return 0;
		}

		private static bool RangeFor(Frame frame, int index, out int min, out int max)
		{
			min = 0;
			max = 0;
			CardFacts card = frame.Card;
			DrawRule rule = frame.Rule;
			if (rule != null && rule.ValueRanges.Length != 0)
			{
				if (index * 2 + 1 >= rule.ValueRanges.Length)
				{
					return false;
				}
				min = Source(card, rule.ValueRanges[index * 2]);
				max = Source(card, rule.ValueRanges[index * 2 + 1]);
				return true;
			}
			int num = ((card.RandomAttack > card.Attack) ? ((card.AttackCount <= 0) ? 1 : card.AttackCount) : 0);
			if (index < num)
			{
				min = card.Attack;
				max = card.RandomAttack;
				return true;
			}
			if (card.RandomDef > card.Def && index == num)
			{
				min = card.Def;
				max = card.RandomDef;
				return true;
			}
			return false;
		}

		private static int Source(CardFacts card, int source)
		{
			if (source >= 100)
			{
				return card.Param(source - 100);
			}
			switch (source)
			{
			case 1:
				return card.Attack;
			case 2:
				return card.RandomAttack;
			case 3:
				return card.Def;
			case 4:
				return card.RandomDef;
			default:
				return 0;
			}
		}

		public int DrawRaw(int[] ownDebuffs, CardFacts[] hand)
		{
			RawDraws++;
			while (_depth > 1 && _stack[_depth - 1].RawLeft <= 0)
			{
				_depth--;
			}
			Frame frame = ((_depth > 0) ? _stack[_depth - 1] : null);
			if (frame == null || frame.Rule == null || frame.RawLeft <= 0)
			{
				return -1;
			}
			frame.RawLeft--;
			int num = RawValue(frame, ownDebuffs, hand);
			// The flag's seal draw is independent of its formation draw. An
			// empty formation pool must not suppress the even-slot seal.
			if (num == -1 && frame.Rule.Raw != DrawRule.RawElementHand)
			{
				frame.RawLeft = 0;
			}
			return num;
		}

		private int RawValue(Frame frame, int[] ownDebuffs, CardFacts[] hand)
		{
			DrawRule rule = frame.Rule;
			CardFacts card = frame.Card;
			int rawIndex = frame.RawIndex++;
			switch (rule.Raw)
			{
			case 1:
			{
				int[] array = PoolFor(frame);
				if (array.Length != 0)
				{
					return array[Next(array.Length)];
				}
				return -1;
			}
			case 2:
				if (ownDebuffs != null && ownDebuffs.Length != 0)
				{
					return ownDebuffs[Next(ownDebuffs.Length)];
				}
				return -1;
			case 3:
				return BasicDebuffs[Next(BasicDebuffs.Length)];
			case 4:
				return ThreeDebuffs[Next(ThreeDebuffs.Length)];
			case 5:
				return Next(rule.Choices);
			case 6:
			{
				int num2 = ((hand != null) ? hand.Length : 0);
				int num3 = card.Param(rule.CapParam);
				if (num3 <= 0 || num2 <= num3)
				{
					return num2;
				}
				return num3;
			}
			case 7:
			{
				int num = 0;
				if (hand != null)
				{
					for (int i = 0; i < hand.Length; i++)
					{
						if (hand[i] != null && hand[i].Name.Contains(rule.Keyword))
						{
							num++;
						}
					}
				}
				return num * card.Param(rule.CapParam);
			}
			case DrawRule.RawElementHand:
				return ElementHand(frame, hand, rawIndex == 1);
			default:
				return -1;
			}
		}

		private int ElementHand(Frame frame, CardFacts[] hand, bool seal)
		{
			if (hand == null) return -1;
			int[] candidates = new int[hand.Length];
			int count = 0;
			for (int i = 0; i < hand.Length; i++)
			{
				int id = ElementHandCards.CopyId(hand[i], seal, frame.Card.Param(frame.Rule.CapParam));
				if (id <= 0) continue;
				// A level cap can change the ID; only return an existing config.
				bool exists = false;
				for (int j = 0; j < _all.Length; j++)
					if (_all[j] != null && _all[j].Id == id) { exists = true; break; }
				if (exists) candidates[count++] = id;
			}
			return count == 0 ? -1 : candidates[Next(count)];
		}

		private int[] PoolFor(Frame frame)
		{
			DrawRule rule = frame.Rule;
			int num = ((rule.FixedRarity >= 0) ? rule.FixedRarity : frame.Card.Rarity);
			int num2 = ((rule.Pool * 4 + num) * 8 + frame.OwnerSect) * 100000 + frame.Card.BaseId % 100000;
			if (num2 != _poolKey)
			{
				_poolIds = CardPools.Build(rule.Pool, _all, frame.Card, num, frame.OwnerSect);
				_poolKey = num2;
			}
			return _poolIds;
		}
	}
}
