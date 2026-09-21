using System.Globalization;

namespace YxArena
{
    /// <summary>
    /// 「上限」按钮的几档。游戏自己的两个上限：战斗最多 64 个单方回合（双方各 32 回合），一次攻击最多 999 段。
    /// 0 = 原版；其余各档都取消回合上限，攻击段数上限放宽到这一档的数。纯逻辑。
    /// 段数没有做成无限：每一段至少占一帧，99999 段在 60 帧下就要将近半小时。
    /// </summary>
    public static class LimitModes
    {
        public const int Original = 0;
        public const int OriginalRounds = 64;
        public const int OriginalHits = 999;
        const int Modes = 3;

        public static int Normalize(int mode)
        {
            return mode < 0 || mode >= Modes ? Original : mode;
        }

        public static int Next(int mode)
        {
            int next = Normalize(mode) + 1;
            return next >= Modes ? Original : next;
        }

        public static bool Lifted(int mode) { return Normalize(mode) != Original; }

        public static int HitCap(int mode)
        {
            int m = Normalize(mode);
            if (m == 1) return 9999;
            if (m == 2) return 99999;
            return OriginalHits;
        }

        public static int RoundCap(int mode) { return Lifted(mode) ? int.MaxValue : OriginalRounds; }

        public static string Name(int mode)
        {
            if (!Lifted(mode)) return "上限：原版";
            return "解限：" + HitCap(mode).ToString(CultureInfo.InvariantCulture) + "段";
        }
    }
}
