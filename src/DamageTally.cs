using System.Globalization;
using System.Text;

namespace YxArena
{
	public sealed class DamageTally
	{
		private sealed class SideTally
		{
			public long Damage;

			public long HpLoss;

			public int Turns;

			public long TurnDamage;

			public string LastCard = "";

			public long LastCardDamage;

			public long MaxHit;

			public string MaxHitCard = "";

			public readonly string[] Names = new string[64];

			public readonly long[] Totals = new long[64];

			public readonly int[] Plays = new int[64];

			public int Count;

			public int Current = -1;
		}

		private const int MaxCards = 64;

		private readonly SideTally[] _sides = new SideTally[2]
		{
			new SideTally(),
			new SideTally()
		};

		private static string Placeholder => Loc.T("（开场 / 其他）", "(start / other)");

		private static string N(int value)
		{
			return value.ToString(CultureInfo.InvariantCulture);
		}

		public static string Group(long value)
		{
			string text = value.ToString(CultureInfo.InvariantCulture);
			bool flag = text.Length > 0 && text[0] == '-';
			if (flag)
			{
				text = text.Substring(1);
			}
			StringBuilder stringBuilder = new StringBuilder();
			if (flag)
			{
				stringBuilder.Append('-');
			}
			int num = text.Length % 3;
			if (num > 0)
			{
				stringBuilder.Append(text.Substring(0, num));
			}
			for (int i = num; i < text.Length; i += 3)
			{
				if (stringBuilder.Length > (flag ? 1 : 0))
				{
					stringBuilder.Append(',');
				}
				stringBuilder.Append(text.Substring(i, 3));
			}
			return stringBuilder.ToString();
		}

		public void Reset()
		{
			_sides[0] = new SideTally();
			_sides[1] = new SideTally();
		}

		private SideTally Side(int side)
		{
			if (side != 0 && side != 1)
			{
				return null;
			}
			return _sides[side];
		}

		public void BeginTurn(int side)
		{
			SideTally sideTally = Side(side);
			if (sideTally != null)
			{
				sideTally.Turns++;
				sideTally.TurnDamage = 0L;
			}
		}

		private static int Slot(SideTally s, string name)
		{
			for (int i = 0; i < s.Count; i++)
			{
				if (s.Names[i] == name)
				{
					return i;
				}
			}
			if (s.Count >= 64)
			{
				return 63;
			}
			s.Names[s.Count] = name;
			s.Count++;
			return s.Count - 1;
		}

		public void BeginCard(int side, string cardName)
		{
			SideTally sideTally = Side(side);
			if (sideTally != null)
			{
				string text = (string.IsNullOrEmpty(cardName) ? Placeholder : cardName);
				sideTally.Current = Slot(sideTally, text);
				sideTally.Plays[sideTally.Current]++;
				sideTally.LastCard = text;
				sideTally.LastCardDamage = 0L;
			}
		}

		public void AddDamage(int side, long damage, long hpLoss)
		{
			SideTally sideTally = Side(side);
			if (sideTally != null)
			{
				if (sideTally.Current < 0)
				{
					sideTally.Current = Slot(sideTally, Placeholder);
				}
				long num = ((damage < 0) ? 0 : damage);
				sideTally.Damage += num;
				sideTally.HpLoss += ((hpLoss < 0) ? 0 : hpLoss);
				if (num > sideTally.MaxHit)
				{
					sideTally.MaxHit = num;
					sideTally.MaxHitCard = sideTally.Names[sideTally.Current];
				}
				sideTally.TurnDamage += num;
				sideTally.LastCardDamage += num;
				sideTally.Totals[sideTally.Current] += num;
			}
		}

		public long TotalDamage(int side)
		{
			return Side(side)?.Damage ?? 0;
		}

		public long TotalHpLoss(int side)
		{
			return Side(side)?.HpLoss ?? 0;
		}

		public long MaxHit(int side)
		{
			return Side(side)?.MaxHit ?? 0;
		}

		public string MaxHitCard(int side)
		{
			SideTally sideTally = Side(side);
			if (sideTally == null)
			{
				return "";
			}
			return sideTally.MaxHitCard;
		}

		public int Turns(int side)
		{
			return Side(side)?.Turns ?? 0;
		}

		public long TurnDamage(int side)
		{
			return Side(side)?.TurnDamage ?? 0;
		}

		public string LastCard(int side)
		{
			SideTally sideTally = Side(side);
			if (sideTally == null)
			{
				return "";
			}
			return sideTally.LastCard;
		}

		public long LastCardDamage(int side)
		{
			return Side(side)?.LastCardDamage ?? 0;
		}

		public string Render(int side, string sideName, int topCards)
		{
			SideTally sideTally = Side(side);
			if (sideTally == null)
			{
				return "";
			}
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(sideName).Append(Loc.T("\u3000总伤害 ", "  Total dmg ")).Append(Group(sideTally.Damage))
				.Append(Loc.T("（掉血 ", " (HP lost "))
				.Append(Group(sideTally.HpLoss))
				.Append(Loc.T("）", ")"));
			if (sideTally.MaxHit > 0)
			{
				stringBuilder.Append('\n').Append(Loc.T("最大一击 ", "Max hit ")).Append(Group(sideTally.MaxHit))
					.Append(Loc.T("（", " ("))
					.Append(sideTally.MaxHitCard)
					.Append(Loc.T("）", ")"));
			}
			stringBuilder.Append('\n').Append(Loc.T("第 ", "Round ")).Append(N(sideTally.Turns))
				.Append(Loc.T(" 回合 ", " dmg "))
				.Append(Group(sideTally.TurnDamage));
			if (sideTally.LastCard.Length > 0)
			{
				stringBuilder.Append(Loc.T("\u3000上一张 ", "  Last ")).Append(sideTally.LastCard).Append(" ")
					.Append(Group(sideTally.LastCardDamage));
			}
			int[] array = new int[sideTally.Count];
			for (int i = 0; i < sideTally.Count; i++)
			{
				array[i] = i;
			}
			for (int j = 1; j < array.Length; j++)
			{
				int num = array[j];
				int num2 = j - 1;
				while (num2 >= 0 && sideTally.Totals[array[num2]] < sideTally.Totals[num])
				{
					array[num2 + 1] = array[num2];
					num2--;
				}
				array[num2 + 1] = num;
			}
			int num3 = 0;
			for (int k = 0; k < array.Length; k++)
			{
				if (num3 >= topCards)
				{
					break;
				}
				int num4 = array[k];
				if (sideTally.Totals[num4] > 0)
				{
					stringBuilder.Append('\n').Append("  ").Append(sideTally.Names[num4]);
					if (sideTally.Plays[num4] > 1)
					{
						stringBuilder.Append(" ×").Append(N(sideTally.Plays[num4]));
					}
					stringBuilder.Append(Loc.T("\u3000", "  ")).Append(Group(sideTally.Totals[num4]));
					num3++;
				}
			}
			return stringBuilder.ToString();
		}
	}
}
