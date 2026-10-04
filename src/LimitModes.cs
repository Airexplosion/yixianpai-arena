using System.Globalization;

namespace YxArena
{
	public static class LimitModes
	{
		public const int Original = 0;

		public const int OriginalRounds = 64;

		public const int OriginalHits = 999;

		private const int Modes = 3;

		public static int Normalize(int mode)
		{
			if (mode >= 0 && mode < 3)
			{
				return mode;
			}
			return 0;
		}

		public static int Next(int mode)
		{
			int num = Normalize(mode) + 1;
			if (num < 3)
			{
				return num;
			}
			return 0;
		}

		public static bool Lifted(int mode)
		{
			return Normalize(mode) != 0;
		}

		public static int HitCap(int mode)
		{
			switch (Normalize(mode))
			{
			case 1:
				return 9999;
			case 2:
				return 99999;
			default:
				return 999;
			}
		}

		public static int RoundCap(int mode)
		{
			if (!Lifted(mode))
			{
				return 64;
			}
			return int.MaxValue;
		}

		public static string Name(int mode)
		{
			if (!Lifted(mode))
			{
				return Loc.T("上限：原版", "Caps: vanilla");
			}
			string text = HitCap(mode).ToString(CultureInfo.InvariantCulture);
			return Loc.T("解限：" + text + "段", "Uncapped: " + text + " hits");
		}
	}
}
