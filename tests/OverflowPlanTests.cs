using Xunit;
using YxArena;

namespace YxArena.Tests
{
    public class OverflowPlanTests
    {
        [Theory]
        [InlineData(7000035, "Card_7000035")]
        [InlineData(7010035, "Card_7000035")]
        [InlineData(20074, "Card_74")]
        [InlineData(74, "Card_74")]
        public void A_card_class_is_named_after_the_base_card(int cardId, string type)
        {
            Assert.Equal(type, OverflowPlan.CardType(cardId));
        }

        [Fact]
        public void The_first_fight_prepares_the_battle_core_and_the_cards_on_the_boards()
        {
            var plan = new OverflowPlan();
            string[] pending = plan.Pending(new[] { 7000035, 0, 7010035, 74, -1 });
            string[] core = OverflowPlan.CoreTypes();
            Assert.Equal(core.Length + 2, pending.Length);
            Assert.Equal("BattleCharacter", pending[0]);
            Assert.Equal("Card_7000035", pending[core.Length]);
            Assert.Equal("Card_74", pending[core.Length + 1]);
        }

        [Fact]
        public void What_was_prepared_is_not_prepared_again()
        {
            var plan = new OverflowPlan();
            Assert.False(plan.CoreDone);
            plan.MarkDone(plan.Pending(new[] { 74 }));
            Assert.True(plan.CoreDone);
            Assert.True(plan.IsDone("Card_74"));
            Assert.Empty(plan.Pending(new[] { 74, 10074, 20074 }));
            Assert.Equal(new[] { "Card_75" }, plan.Pending(new[] { 74, 75 }));
        }

        [Fact]
        public void A_card_prepared_on_its_first_play_counts_too()
        {
            var plan = new OverflowPlan();
            plan.MarkCardDone("Card_75");
            Assert.False(plan.CoreDone);
            Assert.True(plan.IsDone("Card_75"));
        }

        [Fact]
        public void No_cards_is_fine()
        {
            var plan = new OverflowPlan();
            Assert.Equal(OverflowPlan.CoreTypes().Length, plan.Pending(null).Length);
        }

        [Theory]
        [InlineData(1500, 2f, 3000)]
        [InlineData(1500, 8f, 1500)]
        [InlineData(1500, 30f, 750)]
        [InlineData(400, 30f, 300)]
        [InlineData(30000, 1f, 40000)]
        public void The_budget_follows_the_time_the_last_step_took(int budget, float elapsedMs, int expected)
        {
            Assert.Equal(expected, OverflowPlan.NextBudget(budget, elapsedMs));
        }

        [Theory]
        [InlineData(0, 11, "1/11")]
        [InlineData(10, 11, "11/11")]
        [InlineData(11, 11, "11/11")]
        public void Progress_counts_types(int index, int count, string text)
        {
            Assert.Equal(text, OverflowPlan.Progress(index, count));
        }
    }
}
