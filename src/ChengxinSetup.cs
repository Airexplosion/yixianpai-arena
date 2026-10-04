using System.Globalization;

namespace YxArena
{
    /// <summary>Lu Jianxin's sword is controlled by native talents 92–96.</summary>
    public sealed class ChengxinSetup
    {
        public const int GrindingTalent = 92;
        public const int SwordBaseId = 19;
        static readonly int[][] Branches = {
            new[] { 0, 10093, 20093 },
            new[] { 0, 10094, 20094, 30094 },
            new[] { 0, 10095, 20095, 30095 },
            new[] { 0, 10096, 20096, 30096 }
        };
        public readonly int[] Selected = new int[4];
        public int Grinding;

        public ChengxinSetup(ArenaSide side)
        {
            Grinding = side.ValueOfTalent(GrindingTalent);
            int[] talents = side.ChosenTalents();
            for (int row = 0; row < Selected.Length; row++)
                for (int i = 0; i < talents.Length; i++)
                    for (int j = 1; j < Branches[row].Length; j++)
                        if (talents[i] == Branches[row][j]) Selected[row] = talents[i];
        }

        public void Next(int row)
        {
            if (row < 0 || row >= Selected.Length) return;
            int next = 0;
            for (int i = 0; i < Branches[row].Length; i++)
                if (Branches[row][i] == Selected[row]) next = (i + 1) % Branches[row].Length;
            Selected[row] = Branches[row][next];
        }

        public bool Apply(ArenaSide side, string grinding)
        {
            int value;
            if (!int.TryParse(grinding, NumberStyles.Integer, CultureInfo.InvariantCulture, out value) || value < 0 || value > ArenaSide.MaxNumber) return false;
            Grinding = value;
            side.ReplaceSwordTalent(0, 92, 92, value);
            for (int i = 0; i < Selected.Length; i++) side.ReplaceSwordTalent(i + 1, 93 + i, Selected[i], 0);
            return true;
        }

        public static int CardId(int realm)
        {
            int stage = realm < 1 ? 1 : (realm > 5 ? 5 : realm);
            return SwordBaseId + (stage - 1) * 10000;
        }
    }
}
