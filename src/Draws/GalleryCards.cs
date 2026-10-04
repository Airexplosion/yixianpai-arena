namespace YxArena.Draws
{
    public static class GalleryCards
    {
        // CardIllustrationType: Sect=0, Career=1, JiYuan=2, MiShu=3, Special=5.
        public static bool Matches(CardFacts card, int type, int detail, int season)
        {
            if (card == null || card.Id <= 0 || card.Level <= 0 ||
                card.Rarity != 0 || !QuickCards.InSeason(card, season)) return false;
            switch (type)
            {
                case 0: return card.Sect == detail && !card.Hidden;
                case 1: return card.Career == detail && !card.Hidden;
                case 2: return card.Subcategory == detail && detail != CardFacts.SubMiShu && card.Hidden;
                case 3: return card.Sect == detail && card.Hidden && card.Subcategory == CardFacts.SubMiShu;
                case 5: return card.Sect == 0 && card.Hidden && card.Subcategory == 0;
                default: return false;
            }
        }
    }
}
