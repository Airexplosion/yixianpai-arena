using Xunit;
using YxArena;

namespace YxArena.Tests
{
    public class LimitModesTests
    {
        [Fact]
        public void The_button_cycles_through_the_modes_and_back()
        {
            Assert.Equal(1, LimitModes.Next(0));
            Assert.Equal(2, LimitModes.Next(1));
            Assert.Equal(0, LimitModes.Next(2));
            Assert.Equal(1, LimitModes.Next(-7));
        }

        [Fact]
        public void The_original_mode_keeps_the_games_own_numbers()
        {
            Assert.False(LimitModes.Lifted(0));
            Assert.Equal(64, LimitModes.RoundCap(0));
            Assert.Equal(999, LimitModes.HitCap(0));
            Assert.Equal("上限：原版", LimitModes.Name(0));
        }

        [Theory]
        [InlineData(1, 9999, "解限：9999段")]
        [InlineData(2, 99999, "解限：99999段")]
        public void Lifted_modes_have_no_round_cap_and_a_wider_hit_cap(int mode, int hits, string name)
        {
            Assert.True(LimitModes.Lifted(mode));
            Assert.Equal(int.MaxValue, LimitModes.RoundCap(mode));
            Assert.Equal(hits, LimitModes.HitCap(mode));
            Assert.Equal(name, LimitModes.Name(mode));
        }

        [Fact]
        public void A_bad_saved_value_falls_back_to_the_original()
        {
            Assert.Equal(0, LimitModes.Normalize(99));
            Assert.Equal(999, LimitModes.HitCap(99));
        }
    }
}
