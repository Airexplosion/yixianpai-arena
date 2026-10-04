using Xunit;
using YxArena.Draws;

namespace YxArena.Tests
{
    public class QuickCardsTests
    {
        static CardFacts Card(int id, int sect = 1, int level = 1, params int[] seasons)
        { return new CardFacts { Id = id, Sect = sect, Level = level, SeasonMechanics = seasons }; }

        [Fact]
        public void Current_rotation_is_included_even_when_battle_uses_none_or_an_old_mechanism()
        {
            var current = Card(101, 1, 1, 10);
            var old = Card(102, 1, 1, 9);
            var shared = Card(103, 1, 1, 9, 10);
            var evergreen = Card(104);
            var cards = new[] { current, old, shared, evergreen };
            Assert.False(QuickCards.InSeason(current, 0));
            Assert.False(QuickCards.InSeason(current, 9));
            Assert.Equal(new[] { 101, 103, 104 }, QuickCards.Ids(cards, 1, 1000001, 10));
            Assert.Equal(new[] { 102, 103, 104 }, QuickCards.Ids(cards, 1, 1000001, 9));
        }

        [Fact]
        public void Switching_edited_side_or_character_changes_the_sect_page()
        {
            var cards = new[] { Card(101), Card(201, 2), Card(301, 3), Card(401, 4) };
            for (int sect = 1; sect <= 4; sect++)
                Assert.Equal(new[] { sect * 100 + 1 }, QuickCards.Ids(cards, sect, sect * 1000000 + 1, 10));
            Assert.Empty(QuickCards.Ids(cards, 0, 0, 10));
        }

        [Fact]
        public void Page_excludes_hidden_obsolete_upgraded_and_other_characters_exclusive_cards()
        {
            var hidden = Card(102); hidden.Hidden = true;
            var obsolete = Card(103); obsolete.Obsolete = true;
            var upgraded = Card(10104); upgraded.Rarity = 1;
            var exclusive = Card(105); exclusive.Owner = 1000002;
            var career = Card(106); career.Career = 1;
            var cards = new[] { Card(101), hidden, obsolete, upgraded, exclusive, career, Card(107, 1, 0), null };
            Assert.Equal(new[] { 101 }, QuickCards.Ids(cards, 1, 1000001, 10));
            Assert.Equal(new[] { 101, 105 }, QuickCards.Ids(cards, 1, 1000002, 10));
        }

        [Fact]
        public void Cards_are_deduplicated_and_ordered_by_realm_then_id()
        {
            var cards = new[] { Card(110, 1, 5), Card(102), Card(101), Card(101), Card(109, 1, 3) };
            Assert.Equal(new[] { 101, 102, 109, 110 }, QuickCards.Ids(cards, 1, 1000001, 10));
        }

        [Fact]
        public void Sect_secrets_share_the_page_but_hidden_dream_cards_do_not()
        {
            var secret = Card(102, 1, 4, 10); secret.Hidden = true; secret.Subcategory = CardFacts.SubMiShu;
            var otherSect = Card(202, 2, 4, 10); otherSect.Hidden = true; otherSect.Subcategory = CardFacts.SubMiShu;
            var oldSecret = Card(103, 1, 4, 9); oldSecret.Hidden = true; oldSecret.Subcategory = CardFacts.SubMiShu;
            var dream = Card(104); dream.Hidden = true; dream.Subcategory = CardFacts.SubDream;
            Assert.Equal(new[] { 101, 102 }, QuickCards.Ids(new[] { Card(101), secret, otherSect, oldSecret, dream }, 1, 1000001, 10));
        }

        [Fact]
        public void Missing_season_list_means_evergreen_like_the_native_catalog()
        {
            var c = Card(101); c.SeasonMechanics = null;
            Assert.True(QuickCards.InSeason(c, 10));
            Assert.Empty(QuickCards.Ids(null, 1, 1000001, 10));
        }
    }
}
