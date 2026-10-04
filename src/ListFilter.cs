namespace YxArena
{
	public static class ListFilter
	{
		public const int AnyGroup = -1;

		public static int[] Match(string[] names, int[] groups, int group, string text)
		{
			string wanted = ((text == null) ? "" : text.Trim());
			int num = 0;
			for (int i = 0; i < names.Length; i++)
			{
				if (Hit(names, groups, i, group, wanted))
				{
					num++;
				}
			}
			int[] array = new int[num];
			int num2 = 0;
			for (int j = 0; j < names.Length; j++)
			{
				if (Hit(names, groups, j, group, wanted))
				{
					array[num2++] = j;
				}
			}
			return array;
		}

		private static bool Hit(string[] names, int[] groups, int index, int group, string wanted)
		{
			if (group != -1 && (groups == null || index >= groups.Length || groups[index] != group))
			{
				return false;
			}
			if (wanted.Length != 0)
			{
				if (names[index] != null)
				{
					return names[index].Contains(wanted);
				}
				return false;
			}
			return true;
		}

		public static int PageCount(int count, int pageSize)
		{
			if (pageSize <= 0)
			{
				return 1;
			}
			int num = (count + pageSize - 1) / pageSize;
			if (num >= 1)
			{
				return num;
			}
			return 1;
		}

		public static int ClampPage(int count, int pageSize, int page)
		{
			int num = PageCount(count, pageSize);
			if (page >= num)
			{
				return num - 1;
			}
			if (page >= 0)
			{
				return page;
			}
			return 0;
		}
	}
}
