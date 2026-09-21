using Xunit;
using YxArena;

namespace YxArena.Tests
{
    public class ArenaSideSetupTests
    {
        [Fact]
        public void A_side_has_five_destiny_slots_that_start_empty()
        {
            var side = new ArenaSide("我", 1000001, 1, 0);
            Assert.Equal(new[] { 0, 0, 0, 0, 0 }, side.Talents);
            Assert.Equal(new[] { 0, 0, 0, 0, 0 }, side.TalentValues);
            Assert.Empty(side.ChosenTalents());
        }

        [Fact]
        public void Setting_a_destiny_resets_that_slots_counter_and_the_same_destiny_may_sit_in_several_slots()
        {
            var side = new ArenaSide("我", 1000001, 1, 0);
            side.SetTalent(0, 29);
            Assert.True(side.SetTalentValue(0, "7"));
            side.SetTalent(2, 84);
            side.SetTalent(3, 29);                       // 同一个仙命再放一个槽：两个槽都留着
            Assert.Equal(new[] { 29, 0, 84, 29, 0 }, side.Talents);
            Assert.Equal(new[] { 7, 0, 0, 7, 0 }, side.TalentValues);      // 计数是这个仙命的，不是槽的
            Assert.Equal(new[] { 29, 84, 29 }, side.ChosenTalents());
            side.SetTalent(0, 84);                       // 换掉一个槽：只清这个槽的计数
            Assert.Equal(new[] { 84, 0, 84, 29, 0 }, side.Talents);
            Assert.Equal(0, side.TalentValues[0]);
            side.SetTalent(0, 0);
            Assert.Equal(new[] { 84, 29 }, side.ChosenTalents());
            side.SetTalent(9, 5);                        // 越界的槽：忽略
            Assert.Equal(new[] { 84, 29 }, side.ChosenTalents());
        }

        [Fact]
        public void A_destiny_value_needs_a_destiny_in_the_slot()
        {
            var side = new ArenaSide("我", 1000001, 1, 0);
            Assert.False(side.SetTalentValue(1, "3"));
            side.SetTalent(1, 29);
            Assert.True(side.SetTalentValue(1, "3"));
            Assert.False(side.SetTalentValue(1, "x"));
            Assert.Equal(3, side.ValueOfTalent(29));
            Assert.Equal(0, side.ValueOfTalent(84));
            side.CaptureTalentValue(29, 6);              // 备战期游戏自己改了计数
            Assert.Equal(6, side.TalentValues[1]);
            side.SetTalent(4, 29);                       // 同一个仙命占两个槽：游戏里计数只有一份，两个槽一起跟
            side.CaptureTalentValue(29, 9);
            Assert.Equal(9, side.TalentValues[1]);
            Assert.Equal(9, side.TalentValues[4]);
            Assert.True(side.SetTalentValue(4, "2"));    // 改其中一个 = 改这个仙命的计数
            Assert.Equal(2, side.TalentValues[1]);
            Assert.Equal(2, side.ValueOfTalent(29));
        }

        [Fact]
        public void Fate_strategies_toggle_on_and_off()
        {
            var side = new ArenaSide("我", 1000001, 1, 0);
            Assert.True(side.ToggleFate(154));
            Assert.True(side.ToggleFate(63));
            Assert.False(side.ToggleFate(154));
            Assert.Equal(new[] { 63 }, side.Fates.ToArray());
            Assert.True(side.HasFate(63));
        }

        [Fact]
        public void A_side_survives_a_round_trip_through_its_saved_text()
        {
            var side = new ArenaSide("我", 3000002, 4, 250);
            side.Bonus = 5;
            side.TiPo = 12;
            side.TiPoMax = 30;
            side.SetTalent(0, 29);
            side.SetTalentValue(0, "4");
            side.SetTalent(4, 204);
            side.ToggleFate(154);
            side.ToggleFate(63);
            side.Hand.Add(1000004);
            side.Hand.Add(1020004);
            side.SetBoard(new[] { 0, 1000012, 0, 10000065 });

            var copy = new ArenaSide("我", 1000001, 1, 0);
            Assert.True(copy.Load(side.Save()));
            Assert.Equal(3000002, copy.CharacterId);
            Assert.Equal(4, copy.Level);
            Assert.Equal(250, copy.Hp);
            Assert.Equal(5, copy.Bonus);
            Assert.Equal(12, copy.TiPo);
            Assert.Equal(30, copy.TiPoMax);
            Assert.Equal(side.Talents, copy.Talents);
            Assert.Equal(side.TalentValues, copy.TalentValues);
            Assert.Equal(side.Fates.ToArray(), copy.Fates.ToArray());
            Assert.Equal(side.Hand.ToArray(), copy.Hand.ToArray());
            Assert.Equal(side.Board.ToArray(), copy.Board.ToArray());
        }

        [Fact]
        public void Changing_the_character_brings_its_own_destinies_and_resets_counters()
        {
            var side = new ArenaSide("我", 1000001, 1, 0);
            side.SetTalent(0, 29);
            side.SetTalentValue(0, "4");
            side.SetCharacter(3000002, 2, 1, new[] { 301, 302, 303, 304, 305, 306 });
            Assert.Equal(3000002, side.CharacterId);
            Assert.Equal(2, side.SkinNumber);
            Assert.Equal(1, side.SkinColor);
            Assert.Equal(new[] { 301, 302, 303, 304, 305 }, side.Talents);
            Assert.Equal(new[] { 0, 0, 0, 0, 0 }, side.TalentValues);
            Assert.True(side.HasAnyTalent());
            side.SetCharacter(0, 0, 0, null);            // 非法角色：忽略
            Assert.Equal(3000002, side.CharacterId);
        }

        [Fact]
        public void The_skin_is_saved_and_older_saves_without_it_still_load()
        {
            var side = new ArenaSide("我", 3000002, 4, 0);
            side.SkinNumber = 3;
            side.SkinColor = 2;
            var copy = new ArenaSide("我", 1000001, 1, 0);
            Assert.True(copy.Load(side.Save()));
            Assert.Equal(3, copy.SkinNumber);
            Assert.Equal(2, copy.SkinColor);

            var legacy = new ArenaSide("我", 1000001, 1, 0);
            legacy.SkinNumber = 5;
            Assert.True(legacy.Load("2000004|3|0|0|0|0|||||"));
            Assert.Equal(2000004, legacy.CharacterId);
            Assert.Equal(0, legacy.SkinNumber);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("garbage")]
        [InlineData("1|2|3")]
        [InlineData("a|b|c|d|e|f|g|h|i|j|k")]
        public void Broken_saved_text_is_rejected_and_leaves_the_side_untouched(string text)
        {
            var side = new ArenaSide("木人", 1000001, 1, 300);
            Assert.False(side.Load(text));
            Assert.Equal(300, side.Hp);
            Assert.Equal(1000001, side.CharacterId);
        }

        [Fact]
        public void Out_of_range_saved_numbers_are_clamped()
        {
            var side = new ArenaSide("我", 1000001, 1, 0);
            Assert.True(side.Load("0|9|-4|0|0|0|||||"));
            Assert.Equal(1000001, side.CharacterId);     // 角色 id 非法：保持原值
            Assert.Equal(1, side.Level);
            Assert.Equal(0, side.Hp);
        }
    }
}
