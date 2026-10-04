using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace YxArena
{
	public sealed class ArenaSide
	{
		public const int Grids = 8;

		public const int MaxNumber = 999999;

		public const int TalentSlots = 5;

		public const long MaxHp = 999999999999999999L;

		private const int SavedFields = 16;

		private const int BigHpSavedFields = 14;

		private const int SkinSavedFields = 13;

		private const int LegacySavedFields = 11;

		private const char FieldSeparator = '|';

		private const char ItemSeparator = ',';

		public readonly string Name;

		public int CharacterId;

		public int SkinNumber;

		public int SkinColor;

		public int Level;

		public int Hp;

		public long BigHp;

		public int Bonus;

		public int TiPo;

		public int TiPoMax;

		public readonly int[] Talents = new int[5];

		public readonly int[] TalentValues = new int[5];

		public readonly List<int> Fates = new List<int>();

		public readonly List<int> Hand = new List<int>();

		public readonly List<int> Board = new List<int>();

		public readonly List<int> BottleCards = new List<int>();

		public readonly List<int> LearnedCards = new List<int>();

		public int UnlockedGrids = 8;

		private readonly List<int> _extraTalents = new List<int>();

		private readonly Dictionary<int, int> _extraTalentValues = new Dictionary<int, int>();

		public void SetBottleCards(List<int> cards)
		{
			BottleCards.Clear();
			if (cards != null)
			{
				for (int i = 0; i < cards.Count; i++)
				{
					BottleCards.Add((cards[i] > 0) ? cards[i] : 0);
				}
			}
		}

		public void SetLearnedCards(List<int> cards)
		{
			LearnedCards.Clear();
			if (cards == null)
			{
				return;
			}
			for (int i = 0; i < cards.Count; i++)
			{
				int num = cards[i] - cards[i] / 10000 % 100 * 10000;
				if (num > 0 && !LearnedCards.Contains(num))
				{
					LearnedCards.Add(num);
				}
			}
		}

		public void ToggleLearnedCard(int id)
		{
			id -= id / 10000 % 100 * 10000;
			if (id > 0)
			{
				if (LearnedCards.Contains(id))
				{
					LearnedCards.Remove(id);
				}
				else
				{
					LearnedCards.Add(id);
				}
			}
		}

		public void ImportTalents(List<int> talents, Dictionary<int, int> values)
		{
			_extraTalents.Clear();
			_extraTalentValues.Clear();
			for (int i = 0; i < 5; i++)
			{
				Talents[i] = 0;
				TalentValues[i] = 0;
			}
			if (talents == null)
			{
				return;
			}
			for (int j = 0; j < talents.Count; j++)
			{
				int value = 0;
				values?.TryGetValue(talents[j], out value);
				if (j < 5)
				{
					Talents[j] = talents[j];
					TalentValues[j] = value;
				}
				else
				{
					_extraTalents.Add(talents[j]);
					_extraTalentValues[talents[j]] = value;
				}
			}
		}

		public ArenaSide(string name, int characterId, int level, int hp)
		{
			Name = name;
			CharacterId = characterId;
			Level = level;
			Hp = hp;
			for (int i = 0; i < 8; i++)
			{
				Board.Add(0);
			}
		}

		public int TotalHp(int baseHp)
		{
			if (BigHp > 0)
			{
				return 2000000000;
			}
			long num = (long)((Hp > 0) ? Hp : baseHp) + (long)Bonus;
			if (num <= 2000000000)
			{
				return (int)num;
			}
			return 2000000000;
		}

		public long TrueTotalHp(int baseHp)
		{
			if (BigHp <= 0)
			{
				return TotalHp(baseHp);
			}
			return BigHp;
		}

		public int ExtraMaxHp(int baseHp)
		{
			return TotalHp(baseHp) - baseHp;
		}

		public void CaptureExtra(int baseHp, int liveExtraMaxHp)
		{
			if (BigHp > 0)
			{
				Bonus = 0;
				return;
			}
			int num = ((Hp > 0) ? (Hp - baseHp) : 0);
			Bonus = liveExtraMaxHp - num;
		}

		public bool SetHp(string text)
		{
			if (text == null || !long.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var result))
			{
				return false;
			}
			if (result < 0 || result > 999999999999999999L)
			{
				return false;
			}
			BigHp = ((result > 2000000000) ? result : 0);
			Hp = (int)((result > 2000000000) ? 2000000000 : result);
			Bonus = 0;
			return true;
		}

		public bool SetTiPo(string text)
		{
			if (!TryNumber(text, out var value))
			{
				return false;
			}
			TiPo = value;
			if (TiPoMax < TiPo)
			{
				TiPoMax = TiPo;
			}
			return true;
		}

		public bool SetTiPoMax(string text)
		{
			if (!TryNumber(text, out var value))
			{
				return false;
			}
			TiPoMax = value;
			if (TiPo > TiPoMax)
			{
				TiPo = TiPoMax;
			}
			return true;
		}

		public void SetBoard(int[] ids)
		{
			for (int i = 0; i < 8; i++)
			{
				Board[i] = ((ids != null && i < ids.Length) ? ids[i] : 0);
			}
		}

		public int PlacedCount()
		{
			int num = 0;
			for (int i = 0; i < Board.Count; i++)
			{
				if (Board[i] != 0)
				{
					num++;
				}
			}
			return num;
		}

		public void SetTalent(int slot, int talentId)
		{
			if (slot < 0 || slot >= 5)
			{
				return;
			}
			int num = ((talentId > 0) ? talentId : 0);
			int num2 = 0;
			for (int i = 0; i < 5; i++)
			{
				if (i != slot && num != 0 && Talents[i] == num)
				{
					num2 = TalentValues[i];
				}
			}
			Talents[slot] = num;
			TalentValues[slot] = num2;
		}

        public void ReplaceSwordTalent(int slot, int family, int talentId, int count)
        {
            for (int i = 0; i < Talents.Length; i++)
                if (Talents[i] % 10000 == family) { Talents[i] = 0; TalentValues[i] = 0; }
            for (int i = _extraTalents.Count - 1; i >= 0; i--)
                if (_extraTalents[i] % 10000 == family)
                {
                    _extraTalentValues.Remove(_extraTalents[i]);
                    _extraTalents.RemoveAt(i);
                }
            if (talentId <= 0) return;
            // Imported reviews can carry unrelated talents in these five slots.
            // Keep them as extra talents instead of deleting them when editing the sword.
            int previous = Talents[slot];
            if (previous > 0 && previous % 10000 != family && !_extraTalents.Contains(previous))
            {
                _extraTalents.Add(previous);
                _extraTalentValues[previous] = TalentValues[slot];
            }
            SetTalent(slot, talentId);
            TalentValues[slot] = count;
        }

		public bool SetTalentValue(int slot, string text)
		{
			if (slot < 0 || slot >= 5 || Talents[slot] == 0)
			{
				return false;
			}
			if (!TryNumber(text, out var value))
			{
				return false;
			}
			for (int i = 0; i < 5; i++)
			{
				if (Talents[i] == Talents[slot])
				{
					TalentValues[i] = value;
				}
			}
			if (_extraTalentValues.ContainsKey(Talents[slot]))
			{
				_extraTalentValues[Talents[slot]] = value;
			}
			return true;
		}

		public int ValueOfTalent(int talentId)
		{
			for (int i = 0; i < 5; i++)
			{
				if (Talents[i] == talentId && talentId != 0)
				{
					return TalentValues[i];
				}
			}
			if (!_extraTalentValues.TryGetValue(talentId, out var value))
			{
				return 0;
			}
			return value;
		}

		public void CaptureTalentValue(int talentId, int value)
		{
			for (int i = 0; i < 5; i++)
			{
				if (Talents[i] == talentId && talentId != 0)
				{
					TalentValues[i] = ((value >= 0) ? value : 0);
				}
			}
			if (_extraTalentValues.ContainsKey(talentId))
			{
				_extraTalentValues[talentId] = value;
			}
		}

		public int[] ChosenTalents()
		{
			int num = 0;
			for (int i = 0; i < 5; i++)
			{
				if (Talents[i] != 0)
				{
					num++;
				}
			}
			int[] array = new int[num + _extraTalents.Count];
			int num2 = 0;
			for (int j = 0; j < 5; j++)
			{
				if (Talents[j] != 0)
				{
					array[num2++] = Talents[j];
				}
			}
			for (int k = 0; k < _extraTalents.Count; k++)
			{
				array[num2++] = _extraTalents[k];
			}
			return array;
		}

		public void SetCharacter(int characterId, int skinNumber, int skinColor, int[] innateTalents)
		{
			if (characterId > 0)
			{
				_extraTalents.Clear();
				_extraTalentValues.Clear();
				CharacterId = characterId;
				BottleCards.Clear();
				LearnedCards.Clear();
				SkinNumber = ((skinNumber >= 0) ? skinNumber : 0);
				SkinColor = ((skinColor >= 0) ? skinColor : 0);
				for (int i = 0; i < 5; i++)
				{
					Talents[i] = ((innateTalents != null && i < innateTalents.Length && innateTalents[i] > 0) ? innateTalents[i] : 0);
					TalentValues[i] = 0;
				}
			}
		}

		public bool HasAnyTalent()
		{
			for (int i = 0; i < 5; i++)
			{
				if (Talents[i] != 0)
				{
					return true;
				}
			}
			return false;
		}

		public bool HasFate(int fateId)
		{
			for (int i = 0; i < Fates.Count; i++)
			{
				if (Fates[i] == fateId)
				{
					return true;
				}
			}
			return false;
		}

		public bool ToggleFate(int fateId)
		{
			if (fateId <= 0)
			{
				return false;
			}
			for (int i = 0; i < Fates.Count; i++)
			{
				if (Fates[i] == fateId)
				{
					Fates.RemoveAt(i);
					return false;
				}
			}
			Fates.Add(fateId);
			return true;
		}

		public string Save()
		{
			StringBuilder stringBuilder = new StringBuilder();
			stringBuilder.Append(Num(CharacterId)).Append('|').Append(Num(Level))
				.Append('|')
				.Append(Num(Hp))
				.Append('|');
			stringBuilder.Append(Num(Bonus)).Append('|').Append(Num(TiPo))
				.Append('|')
				.Append(Num(TiPoMax))
				.Append('|');
			stringBuilder.Append(Join(Talents)).Append('|').Append(Join(TalentValues))
				.Append('|')
				.Append(JoinList(Fates))
				.Append('|');
			stringBuilder.Append(JoinList(Hand)).Append('|').Append(JoinList(Board))
				.Append('|');
			stringBuilder.Append(Num(SkinNumber)).Append('|').Append(Num(SkinColor))
				.Append('|');
			stringBuilder.Append(BigHp.ToString(CultureInfo.InvariantCulture));
			stringBuilder.Append('|').Append(JoinList(BottleCards)).Append('|')
				.Append(JoinList(LearnedCards));
            if (_extraTalents.Count > 0)
            {
                int[] extraValues = new int[_extraTalents.Count];
                for (int i = 0; i < extraValues.Length; i++) extraValues[i] = ValueOfTalent(_extraTalents[i]);
                stringBuilder.Append('|').Append(JoinList(_extraTalents)).Append('|').Append(Join(extraValues));
            }
			return stringBuilder.ToString();
		}

		public bool Load(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return false;
			}
			string[] array = text.Split('|');
			if (array.Length != 18 && array.Length != 16 && array.Length != 14 && array.Length != 13 && array.Length != 11)
			{
				return false;
			}
			int value = 0;
			int value2 = 0;
			if (array.Length >= 13 && (!TryInt(array[11], out value) || !TryInt(array[12], out value2)))
			{
				return false;
			}
			long result = 0L;
			if (array.Length >= 14 && !long.TryParse(array[13], NumberStyles.None, CultureInfo.InvariantCulture, out result))
			{
				return false;
			}
			int[] values = new int[0];
			int[] values2 = new int[0];
			if (array.Length >= 16 && (!TryList(array[14], out values) || !TryList(array[15], out values2)))
			{
				return false;
			}
            int[] extraIds = new int[0], extraValues = new int[0];
            if (array.Length == 18 && (!TryList(array[16], out extraIds) || !TryList(array[17], out extraValues) || extraIds.Length != extraValues.Length)) return false;
			int[] array2 = new int[6];
			for (int i = 0; i < 6; i++)
			{
				if (!TryInt(array[i], out var value3))
				{
					return false;
				}
				array2[i] = value3;
			}
			if (!TryList(array[6], out var values3) || !TryList(array[7], out var values4) || !TryList(array[8], out var values5) || !TryList(array[9], out var values6) || !TryList(array[10], out var values7))
			{
				return false;
			}
			if (array2[0] > 0)
			{
				CharacterId = array2[0];
			}
			SkinNumber = ((value >= 0) ? value : 0);
			SkinColor = ((value2 >= 0) ? value2 : 0);
			Level = ((array2[1] < 1 || array2[1] > 6) ? 1 : array2[1]);
			Hp = ((array2[2] >= 0 && array2[2] <= 2000000000) ? array2[2] : 0);
			BigHp = ((result > 2000000000 && result <= 999999999999999999L) ? result : 0);
			if (BigHp > 0)
			{
				Hp = 2000000000;
			}
			Bonus = ((array2[3] >= -999999 && array2[3] <= 999999) ? array2[3] : 0);
			TiPo = Clamp(array2[4]);
			TiPoMax = Clamp(array2[5]);
			if (TiPoMax < TiPo)
			{
				TiPoMax = TiPo;
			}
			for (int j = 0; j < 5; j++)
			{
				Talents[j] = ((j < values3.Length && values3[j] > 0) ? values3[j] : 0);
				TalentValues[j] = ((Talents[j] != 0 && j < values4.Length) ? Clamp(values4[j]) : 0);
			}
			Fates.Clear();
			for (int k = 0; k < values5.Length; k++)
			{
				if (values5[k] > 0 && !HasFate(values5[k]))
				{
					Fates.Add(values5[k]);
				}
			}
			Hand.Clear();
			for (int l = 0; l < values6.Length; l++)
			{
				if (values6[l] > 0)
				{
					Hand.Add(values6[l]);
				}
			}
			SetBoard(values7);
			SetBottleCards(new List<int>(values));
			SetLearnedCards(new List<int>(values2));
            _extraTalents.Clear();
            _extraTalentValues.Clear();
            for (int i = 0; i < extraIds.Length; i++)
                if (extraIds[i] > 0 && !_extraTalents.Contains(extraIds[i]))
                {
                    bool inSlot = false;
                    for (int j = 0; j < Talents.Length; j++) if (Talents[j] == extraIds[i]) inSlot = true;
                    if (inSlot) continue;
                    _extraTalents.Add(extraIds[i]);
                    _extraTalentValues[extraIds[i]] = Clamp(extraValues[i]);
                }
			return true;
		}

		private static int Clamp(int value)
		{
			if (value >= 0 && value <= 999999)
			{
				return value;
			}
			return 0;
		}

		private static string Num(int value)
		{
			return value.ToString(CultureInfo.InvariantCulture);
		}

		private static string Join(int[] values)
		{
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < values.Length; i++)
			{
				if (i > 0)
				{
					stringBuilder.Append(',');
				}
				stringBuilder.Append(Num(values[i]));
			}
			return stringBuilder.ToString();
		}

		private static string JoinList(List<int> values)
		{
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < values.Count; i++)
			{
				if (i > 0)
				{
					stringBuilder.Append(',');
				}
				stringBuilder.Append(Num(values[i]));
			}
			return stringBuilder.ToString();
		}

		private static bool TryInt(string text, out int value)
		{
			return int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
		}

		private static bool TryList(string text, out int[] values)
		{
			values = new int[0];
			if (string.IsNullOrEmpty(text))
			{
				return true;
			}
			string[] array = text.Split(',');
			int[] array2 = new int[array.Length];
			for (int i = 0; i < array.Length; i++)
			{
				if (!TryInt(array[i], out var value))
				{
					return false;
				}
				array2[i] = value;
			}
			values = array2;
			return true;
		}

		private static bool TryNumber(string text, out int value)
		{
			value = 0;
			if (text == null)
			{
				return false;
			}
			if (!int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var result))
			{
				return false;
			}
			if (result < 0 || result > 999999)
			{
				return false;
			}
			value = result;
			return true;
		}
	}
}
