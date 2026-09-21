using Xunit;
using YxArena;

namespace YxArena.Tests
{
    public class StepControllerTests
    {
        [Fact]
        public void Nothing_pauses_when_stepping_was_never_asked_for()
        {
            var step = new StepController();
            step.OnBattleStart();
            Assert.False(step.ShouldPauseBeforeCard());
            Assert.False(step.ShouldPauseBeforeCard());
        }

        [Fact]
        public void Pause_at_start_stops_before_the_first_card_only()
        {
            var step = new StepController();
            step.PauseAtStart = true;
            step.OnBattleStart();
            Assert.True(step.ShouldPauseBeforeCard());
            Assert.False(step.ShouldPauseBeforeCard());
        }

        [Fact]
        public void A_step_while_paused_resumes_and_stops_before_the_next_card()
        {
            var step = new StepController();
            step.PauseAtStart = true;
            step.OnBattleStart();
            Assert.True(step.ShouldPauseBeforeCard());         // 停在第 1 张牌之前
            Assert.True(step.RequestStep(true));               // 在暂停 → 要放行
            Assert.True(step.ShouldPauseBeforeCard());         // 第 1 张打完，第 2 张之前停
            Assert.False(step.ShouldPauseBeforeCard());        // 没再按步进就不停
        }

        [Fact]
        public void A_step_while_running_does_not_resume_anything_but_stops_before_the_next_card()
        {
            var step = new StepController();
            step.OnBattleStart();
            Assert.False(step.RequestStep(false));
            Assert.True(step.ShouldPauseBeforeCard());
        }

        [Fact]
        public void A_new_battle_forgets_a_pending_step()
        {
            var step = new StepController();
            step.OnBattleStart();
            step.RequestStep(false);
            step.OnBattleStart();
            Assert.False(step.ShouldPauseBeforeCard());
        }
    }

    public class DamageTallyTests
    {
        [Fact]
        public void Damage_is_attributed_to_the_card_being_played_and_summed_per_side()
        {
            var tally = new DamageTally();
            tally.Reset();
            tally.BeginTurn(0);
            tally.BeginCard(0, "轻剑");
            tally.AddDamage(0, 4, 4);
            tally.BeginCard(0, "飞牙剑");
            tally.AddDamage(0, 6, 2);                 // 6 点伤害，4 点被防挡掉
            tally.AddDamage(0, 6, 6);
            Assert.Equal(16, tally.TotalDamage(0));
            Assert.Equal(12, tally.TotalHpLoss(0));
            Assert.Equal(0, tally.TotalDamage(1));
            Assert.Equal("飞牙剑", tally.LastCard(0));
            Assert.Equal(12, tally.LastCardDamage(0));
        }

        [Fact]
        public void The_same_card_name_is_pooled_and_the_breakdown_is_sorted_by_damage()
        {
            var tally = new DamageTally();
            tally.Reset();
            tally.BeginCard(0, "轻剑");
            tally.AddDamage(0, 4, 4);
            tally.BeginCard(0, "飞牙剑");
            tally.AddDamage(0, 9, 9);
            tally.BeginCard(0, "轻剑");
            tally.AddDamage(0, 4, 4);
            string text = tally.Render(0, "我", 5);
            int heavy = text.IndexOf("飞牙剑");
            int light = text.IndexOf("轻剑 ×2");
            Assert.True(heavy >= 0 && light > heavy);
            Assert.Contains("总伤害 17（", text);
        }

        [Fact]
        public void Turn_damage_restarts_each_turn_of_that_side()
        {
            var tally = new DamageTally();
            tally.Reset();
            tally.BeginTurn(0);
            tally.BeginCard(0, "轻剑");
            tally.AddDamage(0, 4, 4);
            tally.BeginTurn(1);
            tally.BeginTurn(0);
            Assert.Equal(2, tally.Turns(0));
            Assert.Equal(0, tally.TurnDamage(0));
            tally.AddDamage(0, 3, 3);
            Assert.Equal(3, tally.TurnDamage(0));
        }

        [Fact]
        public void Damage_before_any_card_goes_to_a_placeholder_and_bad_sides_are_ignored()
        {
            var tally = new DamageTally();
            tally.Reset();
            tally.AddDamage(1, 5, 5);
            tally.AddDamage(7, 5, 5);
            Assert.Equal(5, tally.TotalDamage(1));
            Assert.Contains("（开场 / 其他）", tally.Render(1, "木人", 5));
        }

        [Fact]
        public void Totals_go_past_the_32_bit_limit()
        {
            var tally = new DamageTally();
            tally.Reset();
            tally.BeginCard(0, "无尽崩绝");
            tally.AddDamage(0, 50000000000L, 2147483647L);
            tally.AddDamage(0, 50000000000L, 2147483647L);
            Assert.Equal(100000000000L, tally.TotalDamage(0));
            Assert.Equal(4294967294L, tally.TotalHpLoss(0));
            Assert.Contains("总伤害 100,000,000,000", tally.Render(0, "我", 5));
        }

        [Fact]
        public void The_biggest_single_hit_is_remembered_with_its_card()
        {
            var tally = new DamageTally();
            tally.Reset();
            tally.BeginCard(0, "轻剑");
            tally.AddDamage(0, 4, 4);
            tally.BeginCard(0, "无尽崩绝");
            tally.AddDamage(0, 75000000000L, 2147483647L);
            tally.BeginCard(0, "飞牙剑");
            tally.AddDamage(0, 9, 9);
            Assert.Equal(75000000000L, tally.MaxHit(0));
            Assert.Equal("无尽崩绝", tally.MaxHitCard(0));
            Assert.Contains("最大一击 75,000,000,000（无尽崩绝）", tally.Render(0, "我", 5));
            Assert.Equal(0L, tally.MaxHit(1));
        }

        [Theory]
        [InlineData(0L, "0")]
        [InlineData(999L, "999")]
        [InlineData(1000L, "1,000")]
        [InlineData(2147483647L, "2,147,483,647")]
        [InlineData(-1234567L, "-1,234,567")]
        [InlineData(9223372036854775807L, "9,223,372,036,854,775,807")]
        public void Numbers_are_grouped_by_thousands(long value, string text)
        {
            Assert.Equal(text, DamageTally.Group(value));
        }

        [Fact]
        public void Reset_clears_everything()
        {
            var tally = new DamageTally();
            tally.BeginCard(0, "轻剑");
            tally.AddDamage(0, 4, 4);
            tally.Reset();
            Assert.Equal(0, tally.TotalDamage(0));
            Assert.Equal("", tally.LastCard(0));
        }
    }
}
