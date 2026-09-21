using System.Text;

namespace YxArena.Draws
{
    /// <summary>按名字搜牌：名字里含有输入的文字就算；分隔用的圆点和空格不计（牌名里「•」「·」混用）。只列 1 级牌。</summary>
    public static class CardSearch
    {
        static string Normalize(string text)
        {
            if (text == null) return "";
            var sb = new StringBuilder();
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == ' ' || c == '　' || c == '•' || c == '·' || c == '・' || c == '.') continue;
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        /// <summary>所有匹配的 1 级牌的 id，升序。</summary>
        public static int[] Find(string query, CardFacts[] all)
        {
            string wanted = Normalize(query);
            if (wanted.Length == 0 || all == null) return new int[0];
            var hit = new bool[all.Length];
            int count = 0;
            for (int i = 0; i < all.Length; i++)
            {
                CardFacts c = all[i];
                if (c == null || c.Id <= 0 || c.Obsolete || c.Rarity != 0) continue;
                if (!Normalize(c.Name).Contains(wanted)) continue;
                hit[i] = true;
                count++;
            }
            var ids = new int[count];
            int n = 0;
            for (int i = 0; i < all.Length; i++) if (hit[i]) ids[n++] = all[i].Id;
            for (int i = 1; i < ids.Length; i++)
            {
                int value = ids[i];
                int j = i - 1;
                while (j >= 0 && ids[j] > value) { ids[j + 1] = ids[j]; j--; }
                ids[j + 1] = value;
            }
            return ids;
        }

        /// <summary>其中游戏的卡牌面板能显示的（有境界的）。</summary>
        public static int[] Showable(int[] ids, CardFacts[] all)
        {
            int count = 0;
            for (int i = 0; i < ids.Length; i++) if (HasRealm(ids[i], all)) count++;
            var result = new int[count];
            int n = 0;
            for (int i = 0; i < ids.Length; i++) if (HasRealm(ids[i], all)) result[n++] = ids[i];
            return result;
        }

        static bool HasRealm(int id, CardFacts[] all)
        {
            for (int i = 0; i < all.Length; i++) if (all[i] != null && all[i].Id == id) return all[i].Level > 0;
            return false;
        }
    }
}
