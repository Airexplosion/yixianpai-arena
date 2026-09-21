using Xunit;
using YxArena.Draws;

namespace YxArena.Tests
{
    public class SpecialCardsTests
    {
        static CardFacts Make(int id, int sect, int career, int sub, bool hidden, int level = 1)
        {
            var c = new CardFacts();
            c.Id = id;
            c.Sect = sect;
            c.Career = career;
            c.Subcategory = sub;
            c.Hidden = hidden;
            c.Level = level;
            c.Rarity = YxArena.CardIds.RarityOf(id);
            return c;
        }

        [Fact]
        public void Cards_the_game_gallery_already_lists_are_not_special()
        {
            Assert.Equal(SpecialCards.None, SpecialCards.CategoryOf(Make(1000004, 1, 0, 0, false)));                       // 公开的门派牌
            Assert.Equal(SpecialCards.None, SpecialCards.CategoryOf(Make(9000001, 0, 1, 0, false)));                       // 公开的副职牌
            Assert.Equal(SpecialCards.None, SpecialCards.CategoryOf(Make(8000001, 0, 0, CardFacts.SubArtifact, true)));    // 法宝
            Assert.Equal(SpecialCards.None, SpecialCards.CategoryOf(Make(8000002, 0, 0, CardFacts.SubPet, true)));         // 灵宠
            Assert.Equal(SpecialCards.None, SpecialCards.CategoryOf(Make(8000003, 2, 0, CardFacts.SubMiShu, true)));       // 门派秘术
        }

        [Fact]
        public void Dream_mirage_and_horse_cards_get_their_own_category()
        {
            Assert.Equal(SpecialCards.Dream, SpecialCards.CategoryOf(Make(10000071, 5, 0, CardFacts.SubDream, true)));
            Assert.Equal(SpecialCards.Mirage, SpecialCards.CategoryOf(Make(308, 5, 0, CardFacts.SubMirage, true)));
            Assert.Equal(SpecialCards.Horse, SpecialCards.CategoryOf(Make(352, 0, 0, CardFacts.SubMa, false)));
        }

        [Fact]
        public void Everything_else_the_gallery_does_not_show_is_other()
        {
            Assert.Equal(SpecialCards.Other, SpecialCards.CategoryOf(Make(7000041, 3, 0, 0, true)));                       // 隐藏的门派衍生牌
            Assert.Equal(SpecialCards.Other, SpecialCards.CategoryOf(Make(100, 0, 0, 0, false)));                          // 无门派无副职的公开牌（粽子等）
            Assert.Equal(SpecialCards.Other, SpecialCards.CategoryOf(Make(8000004, 0, 0, CardFacts.SubMiShu, true)));      // 没有门派的秘术
        }

        [Fact]
        public void Only_base_level_cards_that_are_not_obsolete_are_listed_sorted_by_id()
        {
            CardFacts obsolete = Make(10000080, 5, 0, CardFacts.SubDream, true);
            obsolete.Obsolete = true;
            CardFacts[] all =
            {
                Make(10000074, 5, 0, CardFacts.SubDream, true), Make(10010071, 5, 0, CardFacts.SubDream, true),
                Make(10000071, 5, 0, CardFacts.SubDream, true), obsolete, Make(1000004, 1, 0, 0, false),
            };
            Assert.Equal(new[] { 10000071, 10000074 }, SpecialCards.Ids(SpecialCards.Dream, all));
        }

        [Fact]
        public void Cards_without_a_realm_cannot_be_shown_by_the_game_panel_and_are_counted_separately()
        {
            CardFacts[] all = { Make(10000071, 5, 0, CardFacts.SubDream, true, 0), Make(10000074, 5, 0, CardFacts.SubDream, true, 3) };
            Assert.Equal(new[] { 10000074 }, SpecialCards.Ids(SpecialCards.Dream, all));
            Assert.Equal(1, SpecialCards.CountWithoutRealm(SpecialCards.Dream, all));
        }

        [Fact]
        public void Categories_cycle_and_have_names()
        {
            Assert.Equal(SpecialCards.Mirage, SpecialCards.Next(SpecialCards.Dream));
            Assert.Equal(SpecialCards.Dream, SpecialCards.Next(SpecialCards.Other));
            Assert.Equal("梦境", SpecialCards.Name(SpecialCards.Dream));
            Assert.Equal("其他", SpecialCards.Name(SpecialCards.Other));
        }
    }
}
