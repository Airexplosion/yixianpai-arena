namespace YxArena.Draws
{
    /// <summary>
    /// 游戏自己的图鉴不列的牌（梦境牌、幻境牌、马牌、各种衍生 / 活动牌），按类别分给「特殊牌」入口。
    /// 图鉴列的是：公开的门派牌 / 副职牌、法宝、灵宠、各门派的秘术（CardIllustrationPanel.OnSetType 的条件）。
    /// </summary>
    public static class SpecialCards
    {
        public const int None = 0;
        public const int Dream = 1;
        public const int Mirage = 2;
        public const int Horse = 3;
        public const int Other = 4;

        public static int Next(int category)
        {
            return category >= Other || category < Dream ? Dream : category + 1;
        }

        public static string Name(int category)
        {
            switch (category)
            {
                case Dream: return Loc.T("梦境", "Dream");
                case Mirage: return Loc.T("幻境", "Mirage");
                case Horse: return Loc.T("马牌", "Horse");
                case Other: return Loc.T("其他", "Other");
                default: return "?";
            }
        }

        static bool InGameGallery(CardFacts c)
        {
            if (!c.Hidden) return c.Sect != 0 || c.Career != 0;
            if (c.Subcategory == CardFacts.SubArtifact || c.Subcategory == CardFacts.SubPet) return true;
            return c.Subcategory == CardFacts.SubMiShu && c.Sect != 0;
        }

        public static int CategoryOf(CardFacts c)
        {
            if (c == null || c.Id <= 0) return None;
            if (c.Subcategory == CardFacts.SubDream) return Dream;
            if (c.Subcategory == CardFacts.SubMirage) return Mirage;
            if (c.Subcategory == CardFacts.SubMa) return Horse;
            return InGameGallery(c) ? None : Other;
        }

        static bool Listed(int category, CardFacts c)
        {
            return c != null && !c.Obsolete && c.Rarity == 0 && CategoryOf(c) == category;
        }

        /// <summary>
        /// 这个类别里能显示的牌（1 级牌、有境界），按 id 升序。游戏的面板按境界分行，没有境界的牌它显示不了。
        /// </summary>
        public static int[] Ids(int category, CardFacts[] all)
        {
            int count = 0;
            for (int i = 0; i < all.Length; i++) if (Listed(category, all[i]) && all[i].Level > 0) count++;
            var ids = new int[count];
            int n = 0;
            for (int i = 0; i < all.Length; i++) if (Listed(category, all[i]) && all[i].Level > 0) ids[n++] = all[i].Id;
            for (int i = 1; i < ids.Length; i++)
            {
                int value = ids[i];
                int j = i - 1;
                while (j >= 0 && ids[j] > value) { ids[j + 1] = ids[j]; j--; }
                ids[j + 1] = value;
            }
            return ids;
        }

        /// <summary>这个类别里因为没有境界而显示不出来的牌数（记日志用）。</summary>
        public static int CountWithoutRealm(int category, CardFacts[] all)
        {
            int count = 0;
            for (int i = 0; i < all.Length; i++) if (Listed(category, all[i]) && all[i].Level <= 0) count++;
            return count;
        }
    }
}
