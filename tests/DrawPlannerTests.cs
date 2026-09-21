using Xunit;
using YxArena.Draws;

namespace YxArena.Tests
{
    public class DrawPlannerTests
    {
        static CardFacts Card(int id, string name, int sect = 1, params int[] otherParams)
        {
            var c = new CardFacts();
            c.Id = id;
            c.Name = name;
            c.Sect = sect;
            c.Rarity = YxArena.CardIds.RarityOf(id);
            c.OtherParams = otherParams;
            return c;
        }

        static readonly CardFacts WuJin = Card(10000065, "无尽崩绝", 5, 3);
        static readonly CardFacts JieMai = Card(10000024, "崩拳•截脉", 5, 2);

        static CardFacts[] Catalog()
        {
            var hidden = Card(10000071, "梦·崩拳封", 5);
            hidden.Hidden = true;
            return new[]
            {
                Card(1000004, "轻剑"), Card(10000001, "崩拳•戳", 5), Card(10010001, "崩拳•戳", 5), Card(10000002, "崩拳•封", 5),
                JieMai, WuJin, hidden, Card(1000120, "狂剑•一式"), Card(1000121, "狂剑•二式"),
            };
        }

        static bool Contains(int[] values, int value)
        {
            for (int i = 0; i < values.Length; i++) if (values[i] == value) return true;
            return false;
        }

        [Fact]
        public void The_endless_punch_card_only_draws_level_one_punch_cards()
        {
            var planner = new DrawPlanner(Catalog());
            planner.Reset(7u);
            planner.BeginCard(WuJin, false, 5);
            int[] allowed = { 10000001, 10000002, 10000024 };
            for (int i = 0; i < 3; i++) Assert.True(Contains(allowed, planner.DrawRaw(new int[0], null)));
            Assert.Equal(-1, planner.DrawRaw(new int[0], null));     // 预算（otherParams[0] = 3）用完
        }

        [Fact]
        public void The_same_seed_replays_the_same_draws()
        {
            var a = new DrawPlanner(Catalog());
            var b = new DrawPlanner(Catalog());
            a.Reset(99u);
            b.Reset(99u);
            a.BeginCard(WuJin, false, 5);
            b.BeginCard(WuJin, false, 5);
            for (int i = 0; i < 3; i++) Assert.Equal(a.DrawRaw(null, null), b.DrawRaw(null, null));
        }

        [Fact]
        public void A_card_without_a_rule_gets_minus_one_on_the_raw_channel_and_a_percentage_on_the_value_channel()
        {
            var planner = new DrawPlanner(Catalog());
            planner.Reset(1u);
            planner.BeginCard(Card(1000004, "轻剑"), false, 1);
            Assert.Equal(-1, planner.DrawRaw(new[] { 100 }, null));
            for (int i = 0; i < 50; i++) Assert.InRange(planner.DrawValue(false), 0, 99);
        }

        [Fact]
        public void Removing_a_random_debuff_picks_one_the_character_has_or_minus_one()
        {
            var planner = new DrawPlanner(Catalog());
            planner.Reset(3u);
            planner.BeginCard(JieMai, false, 5);
            Assert.True(Contains(new[] { 101, 105 }, planner.DrawRaw(new[] { 101, 105 }, null)));
            planner.BeginCard(JieMai, false, 5);
            Assert.Equal(-1, planner.DrawRaw(new int[0], null));
        }

        [Fact]
        public void A_nested_card_draws_first_and_then_the_outer_card_continues()
        {
            var planner = new DrawPlanner(Catalog());
            planner.Reset(5u);
            planner.BeginCard(WuJin, false, 5);
            Assert.True(planner.DrawRaw(null, null) > 1000);              // 外层：一张崩拳的 id
            planner.BeginCard(JieMai, true, 5);                            // 假设抽到截脉：它要取 2 次负面状态
            Assert.Equal(100, planner.DrawRaw(new[] { 100 }, null));
            Assert.Equal(100, planner.DrawRaw(new[] { 100 }, null));
            Assert.True(planner.DrawRaw(new[] { 100 }, null) > 1000);      // 截脉取够了 → 回到外层，又是牌 id
        }

        [Fact]
        public void A_nested_card_that_got_minus_one_is_done()
        {
            var planner = new DrawPlanner(Catalog());
            planner.Reset(5u);
            planner.BeginCard(WuJin, false, 5);
            planner.DrawRaw(null, null);
            planner.BeginCard(JieMai, true, 5);
            Assert.Equal(-1, planner.DrawRaw(new int[0], null));           // 没有负面状态可转移
            Assert.True(planner.DrawRaw(new int[0], null) > 1000);         // 下一次取值属于外层
        }

        [Fact]
        public void A_top_level_card_clears_whatever_was_left_on_the_stack()
        {
            var planner = new DrawPlanner(Catalog());
            planner.Reset(5u);
            planner.BeginCard(WuJin, false, 5);
            planner.BeginCard(JieMai, true, 5);
            planner.BeginCard(Card(1000004, "轻剑"), false, 1);
            Assert.Equal(-1, planner.DrawRaw(new[] { 100 }, null));
        }

        [Fact]
        public void Value_ranges_come_from_the_card_and_gua_xiang_takes_the_maximum()
        {
            var thunder = Card(4000068, "御雷卦诀", 4);
            thunder.Attack = 8;
            thunder.RandomAttack = 16;
            var planner = new DrawPlanner(Catalog());
            planner.Reset(11u);
            for (int i = 0; i < 40; i++)
            {
                planner.BeginCard(thunder, false, 4);
                Assert.InRange(planner.DrawValue(false), 8, 16);
            }
            planner.BeginCard(thunder, false, 4);
            Assert.Equal(16, planner.DrawValue(true));
        }

        [Fact]
        public void A_second_value_draw_uses_the_second_range_and_later_ones_are_percentages()
        {
            var rhino = Card(4000091, "犀牛望月", 4, 3, 5);
            rhino.Attack = 10;
            rhino.RandomAttack = 20;
            var planner = new DrawPlanner(Catalog());
            planner.Reset(2u);
            planner.BeginCard(rhino, false, 4);
            Assert.Equal(20, planner.DrawValue(true));
            Assert.Equal(5, planner.DrawValue(true));
            Assert.Equal(0, planner.DrawValue(true));                      // 再往后按概率判定：卦象必中
        }

        [Fact]
        public void Cards_with_a_random_attack_in_their_config_need_no_rule()
        {
            var generic = Card(4000001, "某随机攻击牌", 4);
            generic.Attack = 3;
            generic.RandomAttack = 9;
            generic.AttackCount = 2;
            var planner = new DrawPlanner(Catalog());
            planner.Reset(2u);
            planner.BeginCard(generic, false, 4);
            Assert.Equal(9, planner.DrawValue(true));
            Assert.Equal(9, planner.DrawValue(true));
            Assert.Equal(0, planner.DrawValue(true));
        }

        [Fact]
        public void The_layer_count_drawn_on_the_value_channel_sets_how_many_debuff_types_follow()
        {
            var fuGuang = Card(193, "浮光掠影", 1, 2, 2, 9);
            var planner = new DrawPlanner(Catalog());
            planner.Reset(2u);
            planner.BeginCard(fuGuang, false, 1);
            Assert.Equal(2, planner.DrawValue(false));
            Assert.InRange(planner.DrawRaw(null, null), 100, 105);
            Assert.InRange(planner.DrawRaw(null, null), 100, 105);
            Assert.Equal(-1, planner.DrawRaw(null, null));
        }

        [Fact]
        public void Hand_based_draws_use_the_kept_hand()
        {
            var cat = Card(9, "灵猫乱剑", 1, 2);
            var cloud = Card(1000052, "残云封天剑", 1, 4);
            CardFacts[] hand = { Card(1000007, "云剑•点星"), Card(1000008, "云剑•回守"), Card(1000004, "轻剑") };
            var planner = new DrawPlanner(Catalog());
            planner.Reset(2u);
            planner.BeginCard(cat, false, 1);
            Assert.Equal(2, planner.DrawRaw(null, hand));                  // 3 张手牌，上限 2
            planner.BeginCard(cloud, false, 1);
            Assert.Equal(8, planner.DrawRaw(null, hand));                  // 2 张云剑 × 4
        }

        [Fact]
        public void Draws_with_nothing_on_the_stack_are_harmless()
        {
            var planner = new DrawPlanner(Catalog());
            planner.Reset(2u);
            Assert.Equal(-1, planner.DrawRaw(null, null));
            Assert.InRange(planner.DrawValue(false), 0, 99);
        }
    }

    public class CardPoolsTests
    {
        static CardFacts Make(int id, string name, int sect, int career, int level, int sub, int owner, int attack)
        {
            var c = new CardFacts();
            c.Id = id;
            c.Name = name;
            c.Sect = sect;
            c.Career = career;
            c.Level = level;
            c.Subcategory = sub;
            c.Owner = owner;
            c.Attack = attack;
            c.Rarity = YxArena.CardIds.RarityOf(id);
            return c;
        }

        [Fact]
        public void Pools_follow_the_card_text()
        {
            CardFacts sword = Make(1000004, "轻剑", 1, 0, 1, 0, 0, 4);
            CardFacts guard = Make(1000005, "护身灵气", 1, 0, 1, 0, 0, 0);
            CardFacts other = Make(2000004, "星弈", 2, 0, 1, 0, 0, 3);
            CardFacts pill = Make(9000001, "丹药", 0, 1, 4, 0, 0, 0);
            CardFacts secret = Make(8000001, "秘术", 0, 0, 5, CardFacts.SubMiShu, 0, 0);
            CardFacts exclusive = Make(1000090, "专属", 1, 0, 3, 0, 1000001, 5);
            CardFacts upgraded = Make(1010004, "轻剑", 1, 0, 1, 0, 0, 6);

            Assert.True(CardPools.Matches(CardPools.SectAny, sword, null, 0, 1));
            Assert.False(CardPools.Matches(CardPools.SectAny, pill, null, 0, 1));
            Assert.False(CardPools.Matches(CardPools.SectAny, exclusive, null, 0, 1));
            Assert.False(CardPools.Matches(CardPools.SectAny, upgraded, null, 0, 1));     // 牌级不符
            Assert.True(CardPools.Matches(CardPools.SectAny, upgraded, null, 1, 1));
            Assert.True(CardPools.Matches(CardPools.SectAttack, sword, null, 0, 1));
            Assert.False(CardPools.Matches(CardPools.SectAttack, guard, null, 0, 1));
            Assert.True(CardPools.Matches(CardPools.SectNoAttack, guard, null, 0, 1));
            Assert.True(CardPools.Matches(CardPools.OtherSect, other, null, 0, 1));
            Assert.False(CardPools.Matches(CardPools.OtherSect, sword, null, 0, 1));
            Assert.True(CardPools.Matches(CardPools.SectOrCareer, pill, null, 0, 1));
            Assert.True(CardPools.Matches(CardPools.CareerYuanYing, pill, null, 0, 1));
            Assert.True(CardPools.Matches(CardPools.MiShu, secret, null, 0, 1));
            Assert.True(CardPools.Matches(CardPools.TreasureYuanYingUp, secret, null, 0, 1));
            Assert.True(CardPools.Matches(CardPools.Exclusive, exclusive, null, 0, 1));
        }

        [Fact]
        public void The_card_that_asks_is_never_in_its_own_pool_and_the_result_is_sorted()
        {
            CardFacts a = Make(10000002, "崩拳•封", 5, 0, 1, 0, 0, 3);
            CardFacts b = Make(10000001, "崩拳•戳", 5, 0, 1, 0, 0, 3);
            CardFacts self = Make(10000065, "无尽崩绝•崩拳", 5, 0, 5, 0, 0, 0);
            int[] ids = CardPools.Build(CardPools.NameBengQuan, new[] { a, self, b }, self, 0, 5);
            Assert.Equal(new[] { 10000001, 10000002 }, ids);
        }

        [Fact]
        public void Every_rule_has_a_unique_card_id()
        {
            DrawRule[] rules = DrawRules.All();
            for (int i = 0; i < rules.Length; i++)
                for (int j = i + 1; j < rules.Length; j++)
                    Assert.NotEqual(rules[i].BaseId, rules[j].BaseId);
        }
    }
}
