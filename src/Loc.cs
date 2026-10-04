namespace YxArena
{
	public static class Loc
	{
		public static bool En;

		public static string T(string zh, string en)
		{
			if (!En)
			{
				return zh;
			}
			return en;
		}
	}
}
