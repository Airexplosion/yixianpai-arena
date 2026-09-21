using Xunit;
using YxArena;

namespace YxArena.Tests
{
    public class CardIdsTests
    {
        sealed class FakeCatalog : ICardCatalog
        {
            readonly int[] _ids;
            public FakeCatalog(params int[] ids) { _ids = ids; }
            public bool Exists(int id)
            {
                for (int i = 0; i < _ids.Length; i++) if (_ids[i] == id) return true;
                return false;
            }
        }

        [Theory]
        [InlineData(1000004, 0)]
        [InlineData(1010004, 1)]
        [InlineData(1020004, 2)]
        [InlineData(3020013, 2)]
        public void RarityOf_reads_the_ten_thousands_digits(int id, int rarity)
        {
            Assert.Equal(rarity, CardIds.RarityOf(id));
        }

        [Theory]
        [InlineData(1020004, 1000004)]
        [InlineData(1000004, 1000004)]
        [InlineData(3020013, 3000013)]
        public void BaseOf_strips_the_rarity(int id, int baseId)
        {
            Assert.Equal(baseId, CardIds.BaseOf(id));
        }

        [Theory]
        [InlineData(1000004, 2, 1020004)]
        [InlineData(1010004, 0, 1000004)]
        [InlineData(1000004, 7, 1020004)]
        [InlineData(1000004, -1, 1000004)]
        public void WithRarity_rebuilds_the_id_and_clamps_the_rarity(int id, int rarity, int expected)
        {
            Assert.Equal(expected, CardIds.WithRarity(id, rarity));
        }

        [Fact]
        public void Pick_uses_the_requested_rarity_when_it_exists()
        {
            Assert.Equal(1020004, CardIds.Pick(1000004, 2, new FakeCatalog(1000004, 1020004)));
        }

        [Fact]
        public void Pick_falls_back_to_the_base_card_when_that_rarity_does_not_exist()
        {
            Assert.Equal(1000004, CardIds.Pick(1000004, 1, new FakeCatalog(1000004, 1020004)));
        }

        [Fact]
        public void Pick_returns_zero_when_even_the_base_card_is_unknown()
        {
            Assert.Equal(0, CardIds.Pick(1000004, 1, new FakeCatalog()));
        }

        [Fact]
        public void Pick_accepts_an_upgraded_id_as_input()
        {
            Assert.Equal(1000004, CardIds.Pick(1020004, 0, new FakeCatalog(1000004, 1020004)));
        }
    }
}
