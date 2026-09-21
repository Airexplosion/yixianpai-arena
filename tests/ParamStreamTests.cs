using Xunit;
using YxArena;

namespace YxArena.Tests
{
    public class ParamStreamTests
    {
        [Fact]
        public void Same_seed_gives_the_same_stream()
        {
            Assert.Equal(ParamStream.Generate(12345u, 64), ParamStream.Generate(12345u, 64));
        }

        [Fact]
        public void Different_seeds_give_different_streams()
        {
            Assert.NotEqual(ParamStream.Generate(1u, 64), ParamStream.Generate(2u, 64));
        }

        [Fact]
        public void Values_are_percentages_and_the_count_is_exact()
        {
            int[] values = ParamStream.Generate(20260919u, 4096);
            Assert.Equal(4096, values.Length);
            for (int i = 0; i < values.Length; i++) Assert.InRange(values[i], 0, 99);
        }

        [Fact]
        public void Seed_zero_is_replaced_because_it_is_a_fixed_point_of_xorshift()
        {
            int[] values = ParamStream.Generate(0u, 16);
            bool anyNonZero = false;
            for (int i = 0; i < values.Length; i++) if (values[i] != 0) anyNonZero = true;
            Assert.True(anyNonZero);
        }

        [Fact]
        public void A_negative_count_yields_an_empty_stream()
        {
            Assert.Empty(ParamStream.Generate(1u, -5));
        }

        [Fact]
        public void MixSeed_is_never_zero_and_changes_with_the_battle_index()
        {
            Assert.NotEqual(0u, ParamStream.MixSeed(0, 0));
            Assert.NotEqual(ParamStream.MixSeed(100, 1), ParamStream.MixSeed(100, 2));
            Assert.NotEqual(ParamStream.MixSeed(100, 1), ParamStream.MixSeed(101, 1));
        }
    }
}
