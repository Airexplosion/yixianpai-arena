using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace YxArena
{
    /// <summary>
    /// 练习场里的一方（我 / 对手）：角色、境界、血量、体魄、手牌、场上的牌。纯数据，不碰游戏。
    /// 血量 = （显式设定的总血量，0 表示跟随境界的基础血量）+ 备战期攒下的加成（炼化等）。
    /// 游戏里的总血量 = 境界基础血量 + extraMaxHp，所以喂给游戏的是 <see cref="ExtraMaxHp"/>。
    /// </summary>
    public sealed class ArenaSide
    {
        public const int Grids = 8;
        public const int MaxNumber = 999999;
        public const int TalentSlots = 5;
        /// <summary>血量最多填 18 位（long 装得下）。</summary>
        public const long MaxHp = 999999999999999999L;
        const int SavedFields = 14;
        const int SkinSavedFields = 13;     // 0.8–0.11 的存档没有大血量
        const int LegacySavedFields = 11;   // 0.7.x 的存档没有皮肤两项
        const char FieldSeparator = (char)124;   // |
        const char ItemSeparator = (char)44;     // ,

        public readonly string Name;
        public int CharacterId;
        /// <summary>皮肤（游戏的房间界面里选的，保证是拥有的）；0 号每个角色都有。</summary>
        public int SkinNumber;
        public int SkinColor;
        public int Level;
        /// <summary>显式设定的总血量；0 = 跟随境界。</summary>
        public int Hp;
        /// <summary>
        /// 超过游戏装得下的血量（&gt; <see cref="BigHpPool.Cap"/>）：真实总血量记在这里，<see cref="Hp"/> 停在 Cap，
        /// 战斗里由 <see cref="BigHpPool"/> 把余量一点点补给游戏里的 hp。0 = 没用上。
        /// </summary>
        public long BigHp;
        /// <summary>备战期攒下的血量上限加成。</summary>
        public int Bonus;
        public int TiPo;
        public int TiPoMax;
        /// <summary>仙命槽（炼气…化神各一个，0 = 空）。存的是 TalentConfig.id。</summary>
        public readonly int[] Talents = new int[TalentSlots];
        /// <summary>每个槽里那个仙命的计数（游戏的 talentTempDatas：比如清血苦炼攒了几次）。</summary>
        public readonly int[] TalentValues = new int[TalentSlots];
        /// <summary>天衍仙命（FateStrategyConfig.id）。</summary>
        public readonly List<int> Fates = new List<int>();
        public readonly List<int> Hand = new List<int>();
        public readonly List<int> Board = new List<int>();

        public ArenaSide(string name, int characterId, int level, int hp)
        {
            Name = name;
            CharacterId = characterId;
            Level = level;
            Hp = hp;
            for (int i = 0; i < Grids; i++) Board.Add(0);
        }

        /// <summary>喂给游戏的总血量（最多到 <see cref="BigHpPool.Cap"/>）。</summary>
        public int TotalHp(int baseHp)
        {
            if (BigHp > 0L) return BigHpPool.Cap;
            long total = (long)(Hp > 0 ? Hp : baseHp) + Bonus;
            return total > BigHpPool.Cap ? BigHpPool.Cap : (int)total;
        }

        /// <summary>真实的总血量（显示、血量池用）。</summary>
        public long TrueTotalHp(int baseHp)
        {
            return BigHp > 0L ? BigHp : TotalHp(baseHp);
        }

        /// <summary>要写进 extraMaxHp 的值（可以为负：总血量低于境界基础血量）。</summary>
        public int ExtraMaxHp(int baseHp)
        {
            return TotalHp(baseHp) - baseHp;
        }

        /// <summary>把界面上当前的 extraMaxHp 读回来：超出我们给的那部分就是备战期的加成。</summary>
        public void CaptureExtra(int baseHp, int liveExtraMaxHp)
        {
            if (BigHp > 0L) { Bonus = 0; return; }
            int given = Hp > 0 ? Hp - baseHp : 0;
            Bonus = liveExtraMaxHp - given;
        }

        public bool SetHp(string text)
        {
            long value;
            if (text == null || !long.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out value)) return false;
            if (value < 0L || value > MaxHp) return false;
            BigHp = value > BigHpPool.Cap ? value : 0L;
            Hp = value > BigHpPool.Cap ? BigHpPool.Cap : (int)value;
            Bonus = 0;
            return true;
        }

        /// <summary>体魄超过上限时上限跟着抬。</summary>
        public bool SetTiPo(string text)
        {
            int value;
            if (!TryNumber(text, out value)) return false;
            TiPo = value;
            if (TiPoMax < TiPo) TiPoMax = TiPo;
            return true;
        }

        /// <summary>上限压到体魄之下时体魄跟着降。</summary>
        public bool SetTiPoMax(string text)
        {
            int value;
            if (!TryNumber(text, out value)) return false;
            TiPoMax = value;
            if (TiPo > TiPoMax) TiPo = TiPoMax;
            return true;
        }

        public void SetBoard(int[] ids)
        {
            for (int i = 0; i < Grids; i++) Board[i] = ids != null && i < ids.Length ? ids[i] : 0;
        }

        public int PlacedCount()
        {
            int placed = 0;
            for (int i = 0; i < Board.Count; i++) if (Board[i] != 0) placed++;
            return placed;
        }

        // ── 仙命 / 天衍仙命 ────────────────────────────────────────────────

        public void SetTalent(int slot, int talentId)
        {
            // 同一个仙命可以占好几个槽（用户要求）。游戏里一个仙命的计数只有一份（talentTempDatas 按仙命 id 存），
            // 所以新放进来的槽沿用这个仙命已有的计数。
            if (slot < 0 || slot >= TalentSlots) return;
            int id = talentId > 0 ? talentId : 0;
            int shared = 0;
            for (int i = 0; i < TalentSlots; i++) if (i != slot && id != 0 && Talents[i] == id) shared = TalentValues[i];
            Talents[slot] = id;
            TalentValues[slot] = shared;
        }

        public bool SetTalentValue(int slot, string text)
        {
            if (slot < 0 || slot >= TalentSlots || Talents[slot] == 0) return false;
            int value;
            if (!TryNumber(text, out value)) return false;
            for (int i = 0; i < TalentSlots; i++) if (Talents[i] == Talents[slot]) TalentValues[i] = value;
            return true;
        }

        public int ValueOfTalent(int talentId)
        {
            for (int i = 0; i < TalentSlots; i++) if (Talents[i] == talentId && talentId != 0) return TalentValues[i];
            return 0;
        }

        /// <summary>备战期游戏自己改了某个仙命的计数：读回来。</summary>
        public void CaptureTalentValue(int talentId, int value)
        {
            for (int i = 0; i < TalentSlots; i++) if (Talents[i] == talentId && talentId != 0) TalentValues[i] = value < 0 ? 0 : value;
        }

        /// <summary>按槽的顺序，选了的仙命。</summary>
        public int[] ChosenTalents()
        {
            int count = 0;
            for (int i = 0; i < TalentSlots; i++) if (Talents[i] != 0) count++;
            var chosen = new int[count];
            int n = 0;
            for (int i = 0; i < TalentSlots; i++) if (Talents[i] != 0) chosen[n++] = Talents[i];
            return chosen;
        }

        /// <summary>换角色：皮肤回到给定的，仙命槽换成这个角色自带的那几个（计数清零）。</summary>
        public void SetCharacter(int characterId, int skinNumber, int skinColor, int[] innateTalents)
        {
            if (characterId <= 0) return;
            CharacterId = characterId;
            SkinNumber = skinNumber < 0 ? 0 : skinNumber;
            SkinColor = skinColor < 0 ? 0 : skinColor;
            for (int i = 0; i < TalentSlots; i++)
            {
                Talents[i] = innateTalents != null && i < innateTalents.Length && innateTalents[i] > 0 ? innateTalents[i] : 0;
                TalentValues[i] = 0;
            }
        }

        public bool HasAnyTalent()
        {
            for (int i = 0; i < TalentSlots; i++) if (Talents[i] != 0) return true;
            return false;
        }

        public bool HasFate(int fateId)
        {
            for (int i = 0; i < Fates.Count; i++) if (Fates[i] == fateId) return true;
            return false;
        }

        /// <summary>选上 / 取消一个天衍仙命；返回现在是否选着。</summary>
        public bool ToggleFate(int fateId)
        {
            if (fateId <= 0) return false;
            for (int i = 0; i < Fates.Count; i++)
            {
                if (Fates[i] != fateId) continue;
                Fates.RemoveAt(i);
                return false;
            }
            Fates.Add(fateId);
            return true;
        }

        // ── 存档 ──────────────────────────────────────────────────────────
        // 一行文本：角色|境界|血量|加成|体魄|体魄上限|仙命槽|仙命计数|天衍仙命|手牌|场上|皮肤|皮肤配色|大血量，列表用逗号分隔。

        public string Save()
        {
            var sb = new StringBuilder();
            sb.Append(Num(CharacterId)).Append(FieldSeparator).Append(Num(Level)).Append(FieldSeparator).Append(Num(Hp)).Append(FieldSeparator);
            sb.Append(Num(Bonus)).Append(FieldSeparator).Append(Num(TiPo)).Append(FieldSeparator).Append(Num(TiPoMax)).Append(FieldSeparator);
            sb.Append(Join(Talents)).Append(FieldSeparator).Append(Join(TalentValues)).Append(FieldSeparator).Append(JoinList(Fates)).Append(FieldSeparator);
            sb.Append(JoinList(Hand)).Append(FieldSeparator).Append(JoinList(Board)).Append(FieldSeparator);
            sb.Append(Num(SkinNumber)).Append(FieldSeparator).Append(Num(SkinColor)).Append(FieldSeparator);
            sb.Append(BigHp.ToString(CultureInfo.InvariantCulture));
            return sb.ToString();
        }

        /// <summary>读不出来就什么都不改，返回 false。</summary>
        public bool Load(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            string[] fields = text.Split(FieldSeparator);
            if (fields.Length != SavedFields && fields.Length != SkinSavedFields && fields.Length != LegacySavedFields) return false;
            int skinNumber = 0;
            int skinColor = 0;
            if (fields.Length >= SkinSavedFields && (!TryInt(fields[11], out skinNumber) || !TryInt(fields[12], out skinColor))) return false;
            long bigHp = 0L;
            if (fields.Length == SavedFields && !long.TryParse(fields[13], NumberStyles.None, CultureInfo.InvariantCulture, out bigHp)) return false;
            var numbers = new int[6];
            for (int i = 0; i < 6; i++)
            {
                int parsed;
                if (!TryInt(fields[i], out parsed)) return false;
                numbers[i] = parsed;
            }
            int[] talents;
            int[] values;
            int[] fates;
            int[] hand;
            int[] board;
            if (!TryList(fields[6], out talents) || !TryList(fields[7], out values) || !TryList(fields[8], out fates)
                || !TryList(fields[9], out hand) || !TryList(fields[10], out board)) return false;

            if (numbers[0] > 0) CharacterId = numbers[0];
            SkinNumber = skinNumber < 0 ? 0 : skinNumber;
            SkinColor = skinColor < 0 ? 0 : skinColor;
            Level = numbers[1] >= 1 && numbers[1] <= 6 ? numbers[1] : 1;
            Hp = numbers[2] < 0 || numbers[2] > BigHpPool.Cap ? 0 : numbers[2];
            BigHp = bigHp > BigHpPool.Cap && bigHp <= MaxHp ? bigHp : 0L;
            if (BigHp > 0L) Hp = BigHpPool.Cap;
            Bonus = numbers[3] < -MaxNumber || numbers[3] > MaxNumber ? 0 : numbers[3];
            TiPo = Clamp(numbers[4]);
            TiPoMax = Clamp(numbers[5]);
            if (TiPoMax < TiPo) TiPoMax = TiPo;
            for (int i = 0; i < TalentSlots; i++)
            {
                Talents[i] = i < talents.Length && talents[i] > 0 ? talents[i] : 0;
                TalentValues[i] = Talents[i] != 0 && i < values.Length ? Clamp(values[i]) : 0;
            }
            Fates.Clear();
            for (int i = 0; i < fates.Length; i++) if (fates[i] > 0 && !HasFate(fates[i])) Fates.Add(fates[i]);
            Hand.Clear();
            for (int i = 0; i < hand.Length; i++) if (hand[i] > 0) Hand.Add(hand[i]);
            SetBoard(board);
            return true;
        }

        static int Clamp(int value)
        {
            return value < 0 || value > MaxNumber ? 0 : value;
        }

        static string Num(int value) { return value.ToString(CultureInfo.InvariantCulture); }

        static string Join(int[] values)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < values.Length; i++)
            {
                if (i > 0) sb.Append(ItemSeparator);
                sb.Append(Num(values[i]));
            }
            return sb.ToString();
        }

        static string JoinList(List<int> values)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) sb.Append(ItemSeparator);
                sb.Append(Num(values[i]));
            }
            return sb.ToString();
        }

        static bool TryInt(string text, out int value)
        {
            return int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out value);
        }

        static bool TryList(string text, out int[] values)
        {
            values = new int[0];
            if (string.IsNullOrEmpty(text)) return true;
            string[] parts = text.Split(ItemSeparator);
            var parsed = new int[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                int item;
                if (!TryInt(parts[i], out item)) return false;
                parsed[i] = item;
            }
            values = parsed;
            return true;
        }

        static bool TryNumber(string text, out int value)
        {
            value = 0;
            if (text == null) return false;
            int parsed;
            if (!int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out parsed)) return false;
            if (parsed < 0 || parsed > MaxNumber) return false;
            value = parsed;
            return true;
        }
    }
}
