using Xunit;
using YxArena;

namespace YxArena.Tests
{
    public class ArenaSideTests
    {
        [Fact]
        public void A_new_side_has_eight_empty_grids_and_an_empty_hand()
        {
            var side = new ArenaSide("我", 1000001, 1, 0);
            Assert.Empty(side.Hand);
            Assert.Equal(new[] { 0, 0, 0, 0, 0, 0, 0, 0 }, side.Board.ToArray());
            Assert.Equal(0, side.PlacedCount());
        }

        [Fact]
        public void Hp_zero_means_follow_the_realm_base_hp()
        {
            var side = new ArenaSide("我", 1000001, 1, 0);
            Assert.Equal(60, side.TotalHp(60));
            Assert.Equal(0, side.ExtraMaxHp(60));
        }

        [Fact]
        public void An_explicit_hp_is_turned_into_extra_max_hp_relative_to_the_base()
        {
            var side = new ArenaSide("木人", 1000001, 1, 300);
            Assert.Equal(300, side.TotalHp(60));
            Assert.Equal(240, side.ExtraMaxHp(60));
            Assert.Equal(-20, new ArenaSide("木人", 1000001, 1, 40).ExtraMaxHp(60));
        }

        [Fact]
        public void Gains_made_in_the_ready_phase_are_kept_as_a_bonus_on_top()
        {
            var side = new ArenaSide("我", 1000001, 1, 0);
            side.CaptureExtra(60, 5);              // 炼化等让 extraMaxHp 从 0 变成了 5
            Assert.Equal(5, side.Bonus);
            Assert.Equal(65, side.TotalHp(60));
            Assert.Equal(85, side.TotalHp(80));    // 换境界：基础血量跟着变，加成保留
            Assert.Equal(5, side.ExtraMaxHp(80));

            var dummy = new ArenaSide("木人", 1000001, 1, 300);
            dummy.CaptureExtra(60, 243);           // 我们给的是 240，多出来的 3 是加成
            Assert.Equal(3, dummy.Bonus);
            Assert.Equal(303, dummy.TotalHp(60));
        }

        [Fact]
        public void Typing_a_new_hp_drops_the_bonus()
        {
            var side = new ArenaSide("我", 1000001, 1, 0);
            side.CaptureExtra(60, 5);
            Assert.True(side.SetHp("100"));
            Assert.Equal(0, side.Bonus);
            Assert.Equal(100, side.TotalHp(60));
        }

        [Theory]
        [InlineData("250", 250)]
        [InlineData(" 99 ", 99)]
        [InlineData("0", 0)]
        public void SetHp_parses_and_accepts(string text, int expected)
        {
            var side = new ArenaSide("木人", 1000001, 1, 300);
            Assert.True(side.SetHp(text));
            Assert.Equal(expected, side.Hp);
        }

        [Theory]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("-5")]
        [InlineData("9999999999999999999")]
        public void SetHp_rejects_garbage_and_keeps_the_old_value(string text)
        {
            var side = new ArenaSide("木人", 1000001, 1, 300);
            Assert.False(side.SetHp(text));
            Assert.Equal(300, side.Hp);
        }

        [Fact]
        public void Hp_above_the_old_six_digit_limit_is_just_a_bigger_number()
        {
            var side = new ArenaSide("木人", 1000001, 1, 300);
            Assert.True(side.SetHp("1500000000"));
            Assert.Equal(1500000000, side.Hp);
            Assert.Equal(0L, side.BigHp);
            Assert.Equal(1500000000, side.TotalHp(60));
            Assert.Equal(1500000000L, side.TrueTotalHp(60));
        }

        [Fact]
        public void Hp_beyond_what_the_game_can_hold_goes_into_the_big_pool()
        {
            var side = new ArenaSide("木人", 1000001, 1, 300);
            Assert.True(side.SetHp("1000000000000"));
            Assert.Equal(1000000000000L, side.BigHp);
            Assert.Equal(BigHpPool.Cap, side.TotalHp(60));          // 喂给游戏的
            Assert.Equal(BigHpPool.Cap - 60, side.ExtraMaxHp(60));
            Assert.Equal(1000000000000L, side.TrueTotalHp(60));     // 显示 / 血量池用的
            side.CaptureExtra(60, BigHpPool.Cap - 60 + 7);           // 备战期的加成在这个量级上不算
            Assert.Equal(0, side.Bonus);
            Assert.True(side.SetHp("300"));
            Assert.Equal(0L, side.BigHp);
            Assert.Equal(300, side.TotalHp(60));
        }

        [Fact]
        public void A_bonus_never_pushes_the_game_value_past_the_cap()
        {
            var side = new ArenaSide("木人", 1000001, 1, 300);
            Assert.True(side.SetHp("2000000000"));
            side.CaptureExtra(60, 2000000000 - 60 + 500);
            Assert.Equal(BigHpPool.Cap, side.TotalHp(60));
        }

        [Fact]
        public void Big_hp_survives_saving()
        {
            var side = new ArenaSide("木人", 1000001, 1, 300);
            side.SetHp("1000000000000");
            var copy = new ArenaSide("木人", 1000001, 1, 300);
            Assert.True(copy.Load(side.Save()));
            Assert.Equal(1000000000000L, copy.BigHp);
            Assert.Equal(BigHpPool.Cap, copy.Hp);
            var old = new ArenaSide("木人", 1000001, 1, 300);
            Assert.True(old.Load("2000004|3|250|0|0|0||||||0|0"));   // 0.8–0.11 的存档：13 项，没有大血量
            Assert.Equal(250, old.Hp);
            Assert.Equal(0L, old.BigHp);
        }

        [Fact]
        public void Physique_is_clamped_to_its_cap_and_the_cap_follows_upwards()
        {
            var side = new ArenaSide("我", 1000001, 1, 0);
            Assert.True(side.SetTiPoMax("20"));
            Assert.True(side.SetTiPo("12"));
            Assert.Equal(12, side.TiPo);
            Assert.True(side.SetTiPo("50"));       // 超过上限：上限跟着抬
            Assert.Equal(50, side.TiPo);
            Assert.Equal(50, side.TiPoMax);
            Assert.True(side.SetTiPoMax("30"));    // 上限压到体魄之下：体魄跟着降
            Assert.Equal(30, side.TiPo);
            Assert.False(side.SetTiPo("x"));
        }

        [Fact]
        public void SetBoard_pads_and_truncates_to_the_grid_count()
        {
            var side = new ArenaSide("我", 1000001, 1, 0);
            side.SetBoard(new[] { 5, 0, 7 });
            Assert.Equal(new[] { 5, 0, 7, 0, 0, 0, 0, 0 }, side.Board.ToArray());
            Assert.Equal(2, side.PlacedCount());
            side.SetBoard(new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 });
            Assert.Equal(8, side.Board.Count);
        }
    }
}
