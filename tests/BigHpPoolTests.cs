using Xunit;
using YxArena;

namespace YxArena.Tests
{
    public class BigHpPoolTests
    {
        const long Trillion = 1000000000000L;

        [Fact]
        public void An_ordinary_amount_of_hp_needs_no_pool()
        {
            var pool = new BigHpPool();
            pool.Reset(300L, 300);
            Assert.False(pool.Active);
            Assert.Equal(250, pool.Normalize(250L));
            Assert.Equal(-40, pool.Normalize(-40L));
            Assert.Equal(0L, pool.Reserve);
        }

        [Fact]
        public void A_big_total_keeps_the_game_hp_at_the_cap_and_the_rest_in_reserve()
        {
            var pool = new BigHpPool();
            pool.Reset(Trillion, BigHpPool.Cap);
            Assert.True(pool.Active);
            Assert.Equal(Trillion - BigHpPool.Cap, pool.Reserve);
            Assert.Equal(Trillion, pool.Effective);
        }

        [Fact]
        public void A_hit_is_paid_from_the_reserve()
        {
            var pool = new BigHpPool();
            pool.Reset(Trillion, BigHpPool.Cap);
            int hp = pool.Normalize(BigHpPool.Cap - 500000000L);
            Assert.Equal(BigHpPool.Cap, hp);
            Assert.Equal(Trillion - 500000000L, pool.Effective);
            Assert.Equal(500000000L, pool.LastLoss);
        }

        [Fact]
        public void A_hit_bigger_than_the_game_hp_does_not_kill_while_the_reserve_covers_it()
        {
            var pool = new BigHpPool();
            pool.Reset(Trillion, BigHpPool.Cap);
            int hp = pool.Normalize(BigHpPool.Cap - 50000000000L);      // 500 亿的一刀：游戏里的 hp 已经是负的了
            Assert.Equal(BigHpPool.Cap, hp);
            Assert.Equal(Trillion - 50000000000L, pool.Effective);
            Assert.Equal(50000000000L, pool.LastLoss);
        }

        [Fact]
        public void The_last_stretch_drains_the_game_hp_itself()
        {
            var pool = new BigHpPool();
            pool.Reset(BigHpPool.Cap + 100L, BigHpPool.Cap);
            Assert.Equal(1000, pool.Normalize(BigHpPool.Cap - (BigHpPool.Cap - 900L)));   // 还剩 900 + 100
            Assert.Equal(0L, pool.Reserve);
            Assert.Equal(400, pool.Normalize(400L));
            Assert.Equal(600L, pool.LastLoss);
        }

        [Fact]
        public void A_hit_bigger_than_everything_is_lethal()
        {
            var pool = new BigHpPool();
            pool.Reset(Trillion, BigHpPool.Cap);
            int hp = pool.Normalize(BigHpPool.Cap - 2L * Trillion);
            Assert.True(hp <= 0);
            Assert.Equal(0L, pool.Reserve);
            Assert.True(pool.Effective <= 0L);
        }

        [Fact]
        public void Healing_shows_no_loss()
        {
            var pool = new BigHpPool();
            pool.Reset(BigHpPool.Cap + 100L, BigHpPool.Cap);
            pool.Normalize(500L);
            pool.Normalize(800L);
            Assert.Equal(0L, pool.LastLoss);
            Assert.Equal(800L, pool.Effective);
        }

        [Fact]
        public void A_replay_starts_over()
        {
            var pool = new BigHpPool();
            pool.Reset(Trillion, BigHpPool.Cap);
            pool.Normalize(BigHpPool.Cap - 50000000000L);
            pool.Reset(Trillion, BigHpPool.Cap);
            Assert.Equal(Trillion, pool.Effective);
            Assert.Equal(0L, pool.LastLoss);
        }
    }
}
