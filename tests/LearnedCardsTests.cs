using System.Collections.Generic;
using Xunit;

namespace YxArena.Tests
{
    public class LearnedCardsTests
    {
        static ArenaSide Side() { return new ArenaSide("我", 1000001, 5, 0); }

        [Fact]
        public void Toggling_another_upgrade_removes_the_same_learned_card()
        {
            var side = Side();
            side.ToggleLearnedCard(20101);
            Assert.Equal(new[] { 101 }, side.LearnedCards);
            side.ToggleLearnedCard(10101);
            Assert.Empty(side.LearnedCards);
        }

        [Fact]
        public void Imported_records_are_normalized_and_deduplicated_including_large_base_ids()
        {
            var side = Side();
            side.SetLearnedCards(new List<int> { 101, 10101, 20101, 1040001, 1000001, 0, -1 });
            Assert.Equal(new[] { 101, 1000001 }, side.LearnedCards);
        }

        [Fact]
        public void Selection_and_clear_are_saved_and_do_not_change_other_side_cards_or_talents()
        {
            var side = Side();
            var opponent = Side();
            side.SetTalent(0, 189);
            side.SetTalentValue(0, "5");
            side.Hand.Add(10101);
            opponent.ToggleLearnedCard(102);
            side.ToggleLearnedCard(101);
            side.ToggleLearnedCard(103);
            var copy = Side();
            Assert.True(copy.Load(side.Save()));
            Assert.Equal(new[] { 101, 103 }, copy.LearnedCards);
            Assert.Equal(new[] { 10101 }, copy.Hand);
            Assert.Equal(5, copy.ValueOfTalent(189));
            copy.SetLearnedCards(null);
            var cleared = Side();
            Assert.True(cleared.Load(copy.Save()));
            Assert.Empty(cleared.LearnedCards);
            Assert.Equal(new[] { 102 }, opponent.LearnedCards);
            Assert.Equal(new[] { 101, 103 }, side.LearnedCards);
        }
    }
}
