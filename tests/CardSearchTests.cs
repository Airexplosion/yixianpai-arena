using Xunit;
using YxArena.Draws;

namespace YxArena.Tests
{
    public class CardSearchTests
    {
        static CardFacts Make(int id, string name, int level = 1)
        {
            var c = new CardFacts();
            c.Id = id;
            c.Name = name;
            c.Level = level;
            c.Rarity = YxArena.CardIds.RarityOf(id);
            return c;
        }

        static CardFacts[] Catalog()
        {
            CardFacts obsolete = Make(10000099, "崩拳•旧");
            obsolete.Obsolete = true;
            return new[]
            {
                Make(10000002, "崩拳•封"), Make(10000001, "崩拳•戳"), Make(10010001, "崩拳•戳"), Make(74, "崩拳·冥夜"),
                Make(10000071, "梦·崩拳封"), Make(1000004, "轻剑"), obsolete, Make(10000065, "无尽崩绝", 0),
            };
        }

        [Fact]
        public void Matches_any_part_of_the_name_and_lists_base_level_cards_sorted_by_id()
        {
            Assert.Equal(new[] { 74, 10000001, 10000002, 10000071 }, CardSearch.Find("崩拳", Catalog()));
        }

        [Fact]
        public void Separator_dots_and_spaces_do_not_matter()
        {
            Assert.Equal(new[] { 10000002, 10000071 }, CardSearch.Find("崩拳封", Catalog()));
            Assert.Equal(new[] { 74 }, CardSearch.Find(" 崩拳•冥夜 ", Catalog()));
            Assert.Equal(new[] { 74 }, CardSearch.Find("崩拳 冥夜", Catalog()));
        }

        [Fact]
        public void An_empty_query_matches_nothing()
        {
            Assert.Empty(CardSearch.Find("", Catalog()));
            Assert.Empty(CardSearch.Find("  ", Catalog()));
            Assert.Empty(CardSearch.Find(null, Catalog()));
        }

        [Fact]
        public void Obsolete_cards_are_left_out()
        {
            Assert.Empty(CardSearch.Find("旧", Catalog()));
        }

        [Fact]
        public void Cards_without_a_realm_are_found_but_reported_separately_because_the_panel_cannot_show_them()
        {
            int[] all = CardSearch.Find("无尽", Catalog());
            Assert.Equal(new[] { 10000065 }, all);
            Assert.Empty(CardSearch.Showable(all, Catalog()));
            Assert.Equal(new[] { 74 }, CardSearch.Showable(CardSearch.Find("冥夜", Catalog()), Catalog()));
        }
    }
}
