using System.Collections.Generic;

namespace YxArena.Draws
{
    /// <summary>One current-season sect page, independent of the arena's battle mechanism.</summary>
    public static class QuickCards
    {
        public static bool InSeason(CardFacts card, int season)
        {
            if (card == null) return false;
            if (card.SeasonMechanics == null || card.SeasonMechanics.Length == 0) return true;
            for (int i = 0; i < card.SeasonMechanics.Length; i++)
                if (card.SeasonMechanics[i] == season) return true;
            return false;
        }

        public static int[] Ids(CardFacts[] cards, int sect, int character, int season, bool swordOwner = false)
        {
            var selected = new List<CardFacts>();
            if (cards == null || sect <= 0) return new int[0];
            for (int i = 0; i < cards.Length; i++)
            {
                CardFacts c = cards[i];
                if (c == null || c.Id <= 0 || CardIds.RarityOf(c.Id) != 0 || c.Level <= 0 || c.Obsolete) continue;
                bool sword = swordOwner && c.Id == ChengxinSetup.SwordBaseId;
                bool hiddenSecret = c.Hidden && c.Subcategory == CardFacts.SubMiShu;
                if (!sword && ((c.Hidden && !hiddenSecret) || c.Sect != sect || c.Career != 0 || (c.Owner != 0 && c.Owner != character) || !InSeason(c, season))) continue;
                bool duplicate = false;
                for (int j = 0; j < selected.Count; j++) if (selected[j].Id == c.Id) duplicate = true;
                if (duplicate) continue;
                int at = selected.Count;
                while (at > 0 && (selected[at - 1].Level > c.Level ||
                    (selected[at - 1].Level == c.Level && selected[at - 1].Id > c.Id))) at--;
                selected.Insert(at, c);
            }
            var ids = new int[selected.Count];
            for (int i = 0; i < selected.Count; i++) ids[i] = selected[i].Id;
            return ids;
        }
    }
}
