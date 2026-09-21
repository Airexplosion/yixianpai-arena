namespace YxArena
{
    /// <summary>自绘的选择列表（仙命、天衍仙命）共用的筛选与分页：按组、按名字里的一段文字。纯逻辑。</summary>
    public static class ListFilter
    {
        public const int AnyGroup = -1;

        /// <summary>符合条件的条目下标，保持原顺序。</summary>
        public static int[] Match(string[] names, int[] groups, int group, string text)
        {
            string wanted = text == null ? "" : text.Trim();
            int count = 0;
            for (int i = 0; i < names.Length; i++) if (Hit(names, groups, i, group, wanted)) count++;
            var result = new int[count];
            int n = 0;
            for (int i = 0; i < names.Length; i++) if (Hit(names, groups, i, group, wanted)) result[n++] = i;
            return result;
        }

        static bool Hit(string[] names, int[] groups, int index, int group, string wanted)
        {
            if (group != AnyGroup && (groups == null || index >= groups.Length || groups[index] != group)) return false;
            return wanted.Length == 0 || (names[index] != null && names[index].Contains(wanted));
        }

        public static int PageCount(int count, int pageSize)
        {
            if (pageSize <= 0) return 1;
            int pages = (count + pageSize - 1) / pageSize;
            return pages < 1 ? 1 : pages;
        }

        public static int ClampPage(int count, int pageSize, int page)
        {
            int pages = PageCount(count, pageSize);
            if (page >= pages) return pages - 1;
            return page < 0 ? 0 : page;
        }
    }
}
