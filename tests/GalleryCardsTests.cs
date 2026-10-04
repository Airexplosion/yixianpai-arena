using Xunit;
using YxArena.Draws;

namespace YxArena.Tests
{
    public class GalleryCardsTests
    {
        [Fact]
        public void Current_season_career_cards_are_visible_even_when_practice_uses_an_old_mechanism()
        {
            var pill = new CardFacts { Id = 9000001, Name = "丹药", Level = 1, Career = 1, SeasonMechanics = new[] { 8 } };
            Assert.True(GalleryCards.Matches(pill, 1, 1, 8));
            Assert.False(GalleryCards.Matches(pill, 1, 1, 0));
            Assert.False(GalleryCards.Matches(pill, 1, 2, 8));
        }

        [Fact]
        public void Sect_and_secret_tabs_keep_their_separate_filters()
        {
            var regular = new CardFacts { Id = 7000111, Sect = 3, Level = 4 };
            var secret = new CardFacts { Id = 12, Sect = 3, Level = 2, Hidden = true, Subcategory = CardFacts.SubMiShu };
            Assert.True(GalleryCards.Matches(regular, 0, 3, 8));
            Assert.False(GalleryCards.Matches(regular, 0, 4, 8));
            Assert.False(GalleryCards.Matches(secret, 0, 3, 8));
            Assert.True(GalleryCards.Matches(secret, 3, 3, 8));
            Assert.False(GalleryCards.Matches(secret, 2, CardFacts.SubMiShu, 8));
        }

        [Fact]
        public void Opportunity_tab_includes_hidden_cards_of_the_requested_category()
        {
            var pet = new CardFacts { Id = 9, Level = 4, Hidden = true, Subcategory = CardFacts.SubPet };
            Assert.True(GalleryCards.Matches(pet, 2, CardFacts.SubPet, 8));
            Assert.False(GalleryCards.Matches(pet, 2, CardFacts.SubArtifact, 8));
        }

        [Fact]
        public void Gallery_shows_only_the_base_card_once_and_leaves_card_facts_unchanged()
        {
            var high = new CardFacts { Id = 7010111, Rarity = 1, Level = 4, Sect = 3 };
            Assert.False(GalleryCards.Matches(high, 0, 3, 8));
            Assert.False(GalleryCards.Matches(null, 0, 3, 8));
            Assert.Equal(1, high.Rarity);
        }
    }
}
