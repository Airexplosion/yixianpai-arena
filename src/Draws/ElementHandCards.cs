namespace YxArena.Draws
{
    public static class ElementHandCards
    {
        // Match the game's spirit formation/seal name families, including
        // dream, mirage and extreme versions. Other formations do not qualify.
        public static bool Matches(CardFacts card, bool seal)
        {
            return card != null && card.Id > 0 && card.Sect == 3 &&
                card.Name != null && card.Name.Contains(seal ? "灵印" : "灵阵");
        }

        public static int CopyId(CardFacts card, bool seal, int maxLevel)
        {
            if (!Matches(card, seal)) return -1;
            if (seal) return card.Id;
            if (maxLevel < 1) return -1;
            int rarity = CardIds.RarityOf(card.Id);
            if (rarity >= maxLevel) rarity = maxLevel - 1;
            return CardIds.WithRarity(card.Id, rarity);
        }
    }
}
