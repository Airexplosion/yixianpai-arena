using Xunit;
using YxArena.Draws;

namespace YxArena.Tests
{
    public class ElementHandCardsTests
    {
        static CardFacts Card(int id, string name, int sect = 3, params int[] parameters)
        {
            return new CardFacts { Id = id, Name = name, Sect = sect,
                Rarity = CardIds.RarityOf(id), OtherParams = parameters };
        }

        static readonly CardFacts[] Cards = {
            Card(7000016, "金灵阵"), Card(7010016, "金灵阵"), Card(7020016, "金灵阵"),
            Card(7000003, "木灵印"), Card(7020003, "木灵印"),
            Card(7000111, "五行阵旗", 3, 1, 10),
            Card(7010111, "五行阵旗", 3, 2, 10), Card(7020111, "五行阵旗", 3, 3, 10)
        };

        [Theory]
        [InlineData(7000111, 7000016)]
        [InlineData(7010111, 7010016)]
        [InlineData(7020111, 7020016)]
        public void Flag_copies_a_retained_formation_at_its_level_cap(int flag, int expected)
        {
            var planner = new DrawPlanner(Cards);
            planner.Reset(7);
            planner.BeginCard(Cards[flag == 7000111 ? 5 : flag == 7010111 ? 6 : 7], false, 3);
            Assert.Equal(expected, planner.DrawRaw(null, new[] { Cards[2] }));
        }

        [Fact]
        public void Even_slot_seal_keeps_its_own_level_after_a_nested_formation()
        {
            var planner = new DrawPlanner(Cards);
            planner.BeginCard(Cards[5], false, 3);
            var hand = new[] { Cards[2], Cards[4] };
            Assert.Equal(7000016, planner.DrawRaw(null, hand));
            planner.BeginCard(Cards[0], true, 3);
            Assert.Equal(7020003, planner.DrawRaw(null, hand));
            Assert.Equal(-1, planner.DrawRaw(null, hand));
            Assert.Equal(7020016, hand[0].Id); // Copying does not consume/downgrade the hand.
        }

        [Fact]
        public void Missing_formation_does_not_suppress_the_even_slot_seal()
        {
            var planner = new DrawPlanner(Cards);
            planner.BeginCard(Cards[5], false, 3);
            Assert.Equal(-1, planner.DrawRaw(null, new[] { Cards[4] }));
            Assert.Equal(7020003, planner.DrawRaw(null, new[] { Cards[4] }));
        }

        [Fact]
        public void Empty_hand_and_unrelated_formations_do_not_create_cards_from_the_catalog()
        {
            var planner = new DrawPlanner(Cards);
            planner.BeginCard(Cards[5], false, 3);
            Assert.Equal(-1, planner.DrawRaw(null, new[] { Card(7000058, "混元无极阵"), Card(2000020, "回响阵", 2) }));
            Assert.Equal(-1, planner.DrawRaw(null, null));
        }

        [Theory]
        [InlineData(7000074, "梦•金灵阵")]
        [InlineData(7000104, "极•水灵阵")]
        [InlineData(317, "幻•土灵阵")]
        public void Formation_variants_are_recognized(int id, string name)
        {
            Assert.True(ElementHandCards.Matches(Card(id, name), false));
            Assert.False(ElementHandCards.Matches(Card(id, name), true));
        }

        [Fact]
        public void Missing_capped_version_is_not_returned()
        {
            var high = Card(7040090, "梦•火灵阵");
            var planner = new DrawPlanner(new[] { high, Cards[5] });
            planner.BeginCard(Cards[5], false, 3);
            Assert.Equal(-1, planner.DrawRaw(null, new[] { high }));
        }

        [Fact]
        public void New_card_resets_the_odd_slot_flags_unused_seal_draw()
        {
            var planner = new DrawPlanner(Cards);
            planner.BeginCard(Cards[5], false, 3);
            Assert.Equal(7000016, planner.DrawRaw(null, new[] { Cards[0] }));
            planner.BeginCard(Card(7000032, "木灵•疏影"), false, 3);
            Assert.Equal(-1, planner.DrawRaw(null, new[] { Cards[4] }));
        }

        [Fact]
        public void Replaying_the_seed_repeats_selection_from_multiple_hand_cards()
        {
            var second = Card(7000029, "水灵阵");
            var planner = new DrawPlanner(new[] { Cards[0], second });
            var hand = new[] { Cards[0], second };
            int[] result = new int[20];
            for (int pass = 0; pass < 2; pass++)
            {
                planner.Reset(91);
                for (int i = 0; i < result.Length; i++)
                {
                    planner.BeginCard(Cards[5], false, 3);
                    int id = planner.DrawRaw(null, hand);
                    Assert.Contains(id, new[] { 7000016, 7000029 });
                    if (pass == 0) result[i] = id;
                    else Assert.Equal(result[i], id);
                }
            }
        }
    }
}
