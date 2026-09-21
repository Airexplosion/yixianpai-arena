using Xunit;
using YxArena;

namespace YxArena.Tests
{
    public class ArenaConfigTests
    {
        [Fact]
        public void Defaults_are_a_level_one_sword_character_against_a_300_hp_dummy()
        {
            var cfg = new ArenaConfig();
            Assert.Equal(1000001, cfg.CharacterId);
            Assert.Equal(1, cfg.Level);
            Assert.Equal(300, cfg.DummyHp);
            Assert.True(cfg.PlayerFirst);
            Assert.Equal(0, cfg.Rarity);
        }

        [Fact]
        public void NextLevel_cycles_through_the_six_realms()
        {
            var cfg = new ArenaConfig();
            int[] seen = new int[7];
            for (int i = 0; i < 7; i++) seen[i] = cfg.NextLevel();
            Assert.Equal(new[] { 2, 3, 4, 5, 6, 1, 2 }, seen);
        }

        [Fact]
        public void NextRarity_cycles_zero_one_two()
        {
            var cfg = new ArenaConfig();
            Assert.Equal(1, cfg.NextRarity());
            Assert.Equal(2, cfg.NextRarity());
            Assert.Equal(0, cfg.NextRarity());
        }

        [Fact]
        public void Normalize_puts_out_of_range_values_back_to_defaults()
        {
            var cfg = new ArenaConfig();
            cfg.CharacterId = -3;
            cfg.Level = 9;
            cfg.DummyHp = -1;
            cfg.Rarity = 5;
            cfg.Normalize();
            Assert.Equal(1000001, cfg.CharacterId);
            Assert.Equal(1, cfg.Level);
            Assert.Equal(300, cfg.DummyHp);
            Assert.Equal(0, cfg.Rarity);
        }

        [Fact]
        public void Normalize_keeps_valid_values()
        {
            var cfg = new ArenaConfig();
            cfg.CharacterId = 3000002;
            cfg.Level = 5;
            cfg.DummyHp = 9999;
            cfg.Rarity = 2;
            cfg.Normalize();
            Assert.Equal(3000002, cfg.CharacterId);
            Assert.Equal(5, cfg.Level);
            Assert.Equal(9999, cfg.DummyHp);
            Assert.Equal(2, cfg.Rarity);
        }

        [Theory]
        [InlineData(1, "炼气")]
        [InlineData(6, "返虚")]
        [InlineData(0, "?")]
        public void LevelName_is_a_lookup_table(int level, string name)
        {
            Assert.Equal(name, ArenaConfig.LevelName(level));
        }

        [Theory]
        [InlineData(0, "1 级")]
        [InlineData(2, "3 级")]
        public void RarityName_is_one_based(int rarity, string name)
        {
            Assert.Equal(name, ArenaConfig.RarityName(rarity));
        }
    }
}
