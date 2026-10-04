using System.Collections.Generic;
using Xunit;
using YxArena.Draws;

namespace YxArena.Tests
{
    public class ChengxinSetupTests
    {
        static ArenaSide Side() { return new ArenaSide("陆剑心", 1000001, 5, 0); }

        [Fact]
        public void Hidden_sword_with_native_rarity_two_is_searchable_and_only_added_to_its_owners_quick_page()
        {
            var sword = new CardFacts { Id = 19, Name = "澄心剑胚", Rarity = 2, Level = 1, Hidden = true };
            var upgrade = new CardFacts { Id = 10019, Name = "澄心剑胚", Rarity = 2, Level = 2, Hidden = true };
            var cards = new[] { sword, upgrade };
            Assert.Equal(new[] { 19 }, CardSearch.Find("澄心剑", cards));
            Assert.Equal(new[] { 19 }, SpecialCards.Ids(SpecialCards.Other, cards));
            Assert.Empty(QuickCards.Ids(cards, 1, 1000001, 10));
            Assert.Equal(new[] { 19 }, QuickCards.Ids(cards, 1, 1000001, 10, true));
        }

        [Theory]
        [InlineData(0, 19)]
        [InlineData(1, 19)]
        [InlineData(2, 10019)]
        [InlineData(3, 20019)]
        [InlineData(4, 30019)]
        [InlineData(5, 40019)]
        [InlineData(6, 40019)]
        public void Sword_card_variant_follows_realm_instead_of_selected_card_rarity(int realm, int id)
        { Assert.Equal(id, ChengxinSetup.CardId(realm)); }

        [Fact]
        public void Grinding_and_all_four_branch_families_round_trip_through_save()
        {
            var side = Side();
            var draft = new ChengxinSetup(side);
            draft.Selected[0] = 20093;
            draft.Selected[1] = 30094;
            draft.Selected[2] = 30095;
            draft.Selected[3] = 30096;
            Assert.True(draft.Apply(side, "123"));
            var copy = Side();
            Assert.True(copy.Load(side.Save()));
            var loaded = new ChengxinSetup(copy);
            Assert.Equal(123, loaded.Grinding);
            Assert.Equal(draft.Selected, loaded.Selected);
        }

        [Fact]
        public void Replacing_review_branches_removes_conflicting_extra_talents_and_keeps_unrelated_talents()
        {
            var side = Side();
            side.ImportTalents(new List<int> { 7, 8, 9, 10, 11, 92, 10093, 10094, 20095, 10096, 20096 },
                new Dictionary<int, int> { { 7, 12 }, { 92, 22 } });
            var draft = new ChengxinSetup(side);
            draft.Selected[0] = 20093;
            draft.Selected[1] = 0;
            draft.Selected[2] = 30095;
            draft.Selected[3] = 30096;
            Assert.True(draft.Apply(side, "31"));
            int[] talents = side.ChosenTalents();
            foreach (int id in new[] { 7, 8, 9, 10, 11, 92, 20093, 30095, 30096 }) Assert.Contains(id, talents);
            foreach (int id in new[] { 10093, 10094, 20095, 10096, 20096 }) Assert.DoesNotContain(id, talents);
            Assert.Equal(12, side.ValueOfTalent(7));
            Assert.Equal(31, side.ValueOfTalent(92));
            var copy = Side();
            Assert.True(copy.Load(side.Save()));
            Assert.Equal(talents, copy.ChosenTalents());
            Assert.Equal(12, copy.ValueOfTalent(7));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("-1")]
        [InlineData("1000000")]
        [InlineData("1.5")]
        public void Invalid_count_cannot_partially_change_talents(string count)
        {
            var side = Side();
            side.SetTalent(0, 92);
            side.SetTalentValue(0, "7");
            string before = side.Save();
            var draft = new ChengxinSetup(side);
            draft.Selected[0] = 10093;
            Assert.False(draft.Apply(side, count));
            Assert.Equal(before, side.Save());
        }

        [Fact]
        public void Each_realm_cycles_through_every_native_branch_and_none()
        {
            var draft = new ChengxinSetup(Side());
            int[][] expected = { new[] { 10093, 20093 }, new[] { 10094, 20094, 30094 },
                new[] { 10095, 20095, 30095 }, new[] { 10096, 20096, 30096 } };
            for (int row = 0; row < 4; row++)
            {
                for (int i = 0; i < expected[row].Length; i++)
                {
                    draft.Next(row);
                    Assert.Equal(expected[row][i], draft.Selected[row]);
                }
                draft.Next(row);
                Assert.Equal(0, draft.Selected[row]);
            }
        }
    }
}
