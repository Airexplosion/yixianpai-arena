using System.Globalization;
using System.Text;

namespace YxArena
{
    /// <summary>
    /// 伤害统计。两方各一份：总伤害（减防前）、实际掉血、本回合伤害、上一张牌、按牌名汇总。
    /// 伤害记在「当前正在出的那张牌」头上（被它触发的连带效果也算它的）。纯逻辑。
    /// </summary>
    public sealed class DamageTally
    {
        const int MaxCards = 64;
        static string Placeholder { get { return Loc.T("（开场 / 其他）", "(start / other)"); } }

        sealed class SideTally
        {
            public long Damage;
            public long HpLoss;
            public int Turns;
            public long TurnDamage;
            public string LastCard = "";
            public long LastCardDamage;
            public long MaxHit;
            public string MaxHitCard = "";
            public readonly string[] Names = new string[MaxCards];
            public readonly long[] Totals = new long[MaxCards];
            public readonly int[] Plays = new int[MaxCards];
            public int Count;
            public int Current = -1;
        }

        readonly SideTally[] _sides = { new SideTally(), new SideTally() };

        static string N(int value) { return value.ToString(CultureInfo.InvariantCulture); }

        /// <summary>千位分组（统计会超过 21 亿，一长串数字不分组没法读）。自己拼，不依赖格式串。</summary>
        public static string Group(long value)
        {
            string digits = value.ToString(CultureInfo.InvariantCulture);
            bool negative = digits.Length > 0 && digits[0] == (char)45;
            if (negative) digits = digits.Substring(1);
            var sb = new StringBuilder();
            if (negative) sb.Append((char)45);
            int lead = digits.Length % 3;
            if (lead > 0) sb.Append(digits.Substring(0, lead));
            for (int i = lead; i < digits.Length; i += 3)
            {
                if (sb.Length > (negative ? 1 : 0)) sb.Append((char)44);
                sb.Append(digits.Substring(i, 3));
            }
            return sb.ToString();
        }

        public void Reset()
        {
            _sides[0] = new SideTally();
            _sides[1] = new SideTally();
        }

        SideTally Side(int side)
        {
            return side == 0 || side == 1 ? _sides[side] : null;
        }

        public void BeginTurn(int side)
        {
            SideTally s = Side(side);
            if (s == null) return;
            s.Turns++;
            s.TurnDamage = 0;
        }

        static int Slot(SideTally s, string name)
        {
            for (int i = 0; i < s.Count; i++) if (s.Names[i] == name) return i;
            if (s.Count >= MaxCards) return MaxCards - 1;
            s.Names[s.Count] = name;
            s.Count++;
            return s.Count - 1;
        }

        public void BeginCard(int side, string cardName)
        {
            SideTally s = Side(side);
            if (s == null) return;
            string name = string.IsNullOrEmpty(cardName) ? Placeholder : cardName;
            s.Current = Slot(s, name);
            s.Plays[s.Current]++;
            s.LastCard = name;
            s.LastCardDamage = 0;
        }

        /// <param name="damage">减防前的伤害（64 位：溢出的那一击用 SatMath 记下的真值）。</param>
        /// <param name="hpLoss">实际掉的血。</param>
        public void AddDamage(int side, long damage, long hpLoss)
        {
            SideTally s = Side(side);
            if (s == null) return;
            if (s.Current < 0) s.Current = Slot(s, Placeholder);
            long d = damage < 0L ? 0L : damage;
            s.Damage += d;
            s.HpLoss += hpLoss < 0L ? 0L : hpLoss;
            if (d > s.MaxHit)
            {
                s.MaxHit = d;
                s.MaxHitCard = s.Names[s.Current];
            }
            s.TurnDamage += d;
            s.LastCardDamage += d;
            s.Totals[s.Current] += d;
        }

        public long TotalDamage(int side) { SideTally s = Side(side); return s != null ? s.Damage : 0L; }

        public long TotalHpLoss(int side) { SideTally s = Side(side); return s != null ? s.HpLoss : 0L; }

        public long MaxHit(int side) { SideTally s = Side(side); return s != null ? s.MaxHit : 0L; }

        public string MaxHitCard(int side) { SideTally s = Side(side); return s != null ? s.MaxHitCard : ""; }

        public int Turns(int side) { SideTally s = Side(side); return s != null ? s.Turns : 0; }

        public long TurnDamage(int side) { SideTally s = Side(side); return s != null ? s.TurnDamage : 0L; }

        public string LastCard(int side) { SideTally s = Side(side); return s != null ? s.LastCard : ""; }

        public long LastCardDamage(int side) { SideTally s = Side(side); return s != null ? s.LastCardDamage : 0L; }

        /// <summary>一方的统计文本：总计 + 伤害最高的若干张牌。</summary>
        public string Render(int side, string sideName, int topCards)
        {
            SideTally s = Side(side);
            if (s == null) return "";
            var sb = new StringBuilder();
            sb.Append(sideName).Append(Loc.T("　总伤害 ", "  Total dmg ")).Append(Group(s.Damage)).Append(Loc.T("（掉血 ", " (HP lost ")).Append(Group(s.HpLoss)).Append(Loc.T("）", ")"));
            if (s.MaxHit > 0L) sb.Append((char)10).Append(Loc.T("最大一击 ", "Max hit ")).Append(Group(s.MaxHit)).Append(Loc.T("（", " (")).Append(s.MaxHitCard).Append(Loc.T("）", ")"));
            sb.Append((char)10).Append(Loc.T("第 ", "Round ")).Append(N(s.Turns)).Append(Loc.T(" 回合 ", " dmg ")).Append(Group(s.TurnDamage));
            if (s.LastCard.Length > 0) sb.Append(Loc.T("　上一张 ", "  Last ")).Append(s.LastCard).Append(" ").Append(Group(s.LastCardDamage));

            var order = new int[s.Count];
            for (int i = 0; i < s.Count; i++) order[i] = i;
            for (int i = 1; i < order.Length; i++)
            {
                int value = order[i];
                int j = i - 1;
                while (j >= 0 && s.Totals[order[j]] < s.Totals[value]) { order[j + 1] = order[j]; j--; }
                order[j + 1] = value;
            }
            int shown = 0;
            for (int i = 0; i < order.Length && shown < topCards; i++)
            {
                int slot = order[i];
                if (s.Totals[slot] <= 0L) continue;
                sb.Append((char)10).Append("  ").Append(s.Names[slot]);
                if (s.Plays[slot] > 1) sb.Append(" ×").Append(N(s.Plays[slot]));
                sb.Append(Loc.T("　", "  ")).Append(Group(s.Totals[slot]));
                shown++;
            }
            return sb.ToString();
        }
    }
}
