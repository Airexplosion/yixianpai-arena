namespace YxArena.Draws
{
    /// <summary>
    /// 一张牌从「随机数流」里取值的规则。
    ///
    /// 背景（反编译实证）：战斗里的随机不是客户端现算的，而是顺序取服务器预先算好的一条流，而且每个值的含义随
    /// 调用点不同——有的是百分比判定（0–99），有的直接是牌的 id、负面状态的类型号、随机攻击的数值。牌池之类的
    /// 逻辑只在服务器上。练习场没有服务器，所以在取值的那一刻按「当前是哪张牌在取」现算一个含义正确的值。
    ///
    /// 两条取值通道：
    ///   Raw   BattleCharacter.GetNextParam() 直接取：牌 id / 负面状态类型 / 选项序号 / 与手牌有关的数。
    ///   Value BattleCharacter.GetNextRandomValue() 取：百分比判定，或一个区间里的数值（受卦象影响：卦象生效取最大 / 必中）。
    /// </summary>
    public sealed class DrawRule
    {
        // Raw 通道的含义
        public const int RawNone = 0;          // 没有 Raw 取值
        public const int RawPool = 1;          // 牌池里随机一张牌的 id
        public const int RawOwnDebuff = 2;     // 取值的角色身上现有的一种负面状态（没有则 -1）
        public const int RawBasicDebuff = 3;   // 六种基础负面状态里随机一种
        public const int RawThreeDebuff = 4;   // 内伤 / 虚弱 / 外伤 里随机一种
        public const int RawChoice = 5;        // 0..Choices-1
        public const int RawHandCount = 6;     // 保留的手牌数（上限 otherParams[CapParam]）
        public const int RawHandNameBonus = 7; // 手牌里名字含 Keyword 的张数 × otherParams[CapParam]
        public const int RawNothing = 8;       // 依赖服务器才知道的上下文：给 -1（牌自己会跳过这段效果）

        // Value 通道的区间端点从哪里来
        public const int SrcAttack = 1;
        public const int SrcRandomAttack = 2;
        public const int SrcDef = 3;
        public const int SrcRandomDef = 4;
        public const int SrcZero = 5;
        public const int SrcParam = 100;       // SrcParam + i = otherParams[i]

        /// <summary>Raw 取值次数不定时用的预算（只在被别的牌随机打出来、需要判断「它取完了没有」时才有意义）。</summary>
        public const int Unbounded = 99;

        public int BaseId;
        public int Raw = RawNone;
        public int Pool = CardPools.None;
        public int FixedRarity = -1;
        /// <summary>Raw 取几次：otherParams 的下标；-1 = 用 <see cref="RawCountFixed"/>。</summary>
        public int RawCountParam = -1;
        public int RawCountFixed = 1;
        public int Choices;
        public int CapParam = -1;
        public string Keyword = "";
        /// <summary>Value 通道依次取到的区间：[min 来源, max 来源, min, max, …]；取完了的后续取值按百分比。</summary>
        public int[] ValueRanges = new int[0];
        /// <summary>第一次 Value 取值的结果就是接下来 Raw 取几次（浮光掠影）。</summary>
        public bool ValueSetsRawCount;

        public int RawBudget(CardFacts card)
        {
            int count = RawCountParam >= 0 ? card.Param(RawCountParam) : RawCountFixed;
            return count < 0 ? 0 : count;
        }
    }

    /// <summary>规则表。键是基础牌 id（1 / 2 / 3 级共用一条）。</summary>
    public static class DrawRules
    {
        static DrawRule[] s_rules;

        public static DrawRule Find(int baseId)
        {
            DrawRule[] rules = All();
            for (int i = 0; i < rules.Length; i++) if (rules[i].BaseId == baseId) return rules[i];
            return null;
        }

        public static DrawRule[] All()
        {
            if (s_rules != null) return s_rules;
            s_rules = new[]
            {
                // ── 随机使用 N 张 XX 牌 ──
                PoolRule(6000011, CardPools.SectAny, -1, -1),           // 神来之笔
                PoolRule(64, CardPools.SectAny, 0, -1),                 // 五彩鲛珠
                PoolRule(186, CardPools.NameKuangJian, 0, -1),          // 狂剑•降神
                PoolRule(352, CardPools.SectOrCareer, 0, -1),           // 天马行空
                PoolRule(353, CardPools.SectAttack, 0, -1),             // 金戈铁马
                PoolRule(354, CardPools.SectNoAttack, 0, -1),           // 秣马厉兵
                PoolRule(355, CardPools.OtherSect, 0, -1),              // 指鹿为马
                PoolRule(358, CardPools.NameZong, 0, -1),               // 野马分粽
                PoolRule(359, CardPools.AgainOrShenFa, 0, -1),          // 一马当先
                PoolRule(360, CardPools.NameMa, 0, -1),                 // 蛛丝马迹
                PoolRule(361, CardPools.MiShu, 0, -1),                  // 马到成功
                PoolRule(362, CardPools.Exclusive, 0, -1),              // 谈马掌
                PoolRule(363, CardPools.DreamHuaShen, 0, -1),           // 梦·天马行空
                PoolRule(364, CardPools.JiYuan, 0, -1),                 // 心猿意马
                PoolRule(365, CardPools.LevelFanXu, 0, -1),             // 一马平川
                PoolRule(380, CardPools.CareerYuanYing, -1, 1),         // 梦·触类旁通：2 级的元婴期副职牌
                PoolRule(341, CardPools.TreasureYuanYingUp, -1, -1),    // 梦·神来之笔
                PoolRule(10000065, CardPools.NameBengQuan, 0, 0),       // 无尽崩绝：崩拳的 1 级效果

                // ── 随机减 / 转移自身的负面状态 ──
                DebuffRule(2000004, DrawRule.RawOwnDebuff, 0),          // 驱邪丹
                DebuffRule(9000026, DrawRule.RawOwnDebuff, -1),         // 清肠紫蕨
                DebuffRule(100, DrawRule.RawOwnDebuff, -1),             // 鲜肉粽
                DebuffRule(105, DrawRule.RawOwnDebuff, -1),             // 板栗粽
                DebuffRule(310, DrawRule.RawOwnDebuff, 0),              // 幻•浩然正气
                Unbounded(417, DrawRule.RawOwnDebuff),
                Unbounded(82, DrawRule.RawOwnDebuff),                   // 万玄破魔掌
                DebuffRule(10000009, DrawRule.RawOwnDebuff, -1),        // 千斤坠
                DebuffRule(10000024, DrawRule.RawOwnDebuff, 0),         // 崩拳•截脉
                DebuffRule(10000049, DrawRule.RawOwnDebuff, 0),         // 万魂破军
                DebuffRule(10000092, DrawRule.RawOwnDebuff, 1),         // 凌空飞扫

                // ── 施加随机负面状态 ──
                DebuffRule(6000007, DrawRule.RawBasicDebuff, -1),       // 挥毫泼墨
                DebuffRule(73, DrawRule.RawBasicDebuff, 1),             // 疯魔
                Unbounded(4000085, DrawRule.RawThreeDebuff),            // 梦•白蛇吐信（有后招时次数更多）
                FuGuang(),                                              // 浮光掠影：先取层数，再逐层取类型

                // ── 随机一种效果 ──
                ChoiceRule(6000014, 3, -1),                             // 落纸云烟
                ChoiceRule(159, 3, 0),                                  // 玄冥云烟

                // ── 与保留的手牌有关 ──
                HandRule(9, DrawRule.RawHandCount, 0, ""),              // 灵猫乱剑：每保留 1 张手牌追加 1 次攻击
                HandRule(1000052, DrawRule.RawHandNameBonus, 0, "云剑"), // 残云封天剑：手牌中每留 1 张「云剑」加攻

                // ── 依赖服务器才知道的上下文：本地给 -1，这段效果不发生 ──
                Nothing(262), Nothing(349), Nothing(395), Nothing(7000075), Nothing(7000078), Nothing(10000089),

                // ── X～Y 的数值 ──
                Ranges(3000001, P(0), P(1)),                            // 奔雷符
                Ranges(6, P(0), P(1)),                                  // 灯焰飞舞
                Ranges(4000007, P(0), P(1)),                            // 星弈•长
                Ranges(4000017, P(0), P(1)),                            // 气疗术
                Ranges(4000019, P(0), P(1)),                            // 斩草除根
                Ranges(4000053, DrawRule.SrcZero, P(0), DrawRule.SrcZero, P(1)),   // 投石问路
                Ranges(4000063, DrawRule.SrcAttack, DrawRule.SrcRandomAttack, DrawRule.SrcAttack, DrawRule.SrcRandomAttack), // 轰雷掣电
                Ranges(4000068, DrawRule.SrcAttack, DrawRule.SrcRandomAttack),     // 御雷卦诀
                Ranges(4000088, DrawRule.SrcAttack, DrawRule.SrcRandomAttack),     // 梦•御雷卦诀
                Ranges(4000091, DrawRule.SrcAttack, DrawRule.SrcRandomAttack, P(0), P(1)), // 犀牛望月
            };
            return s_rules;
        }

        static int P(int index) { return DrawRule.SrcParam + index; }

        static DrawRule PoolRule(int baseId, int pool, int countParam, int fixedRarity)
        {
            var rule = new DrawRule();
            rule.BaseId = baseId;
            rule.Raw = DrawRule.RawPool;
            rule.Pool = pool;
            rule.RawCountParam = countParam;
            rule.FixedRarity = fixedRarity;
            return rule;
        }

        static DrawRule DebuffRule(int baseId, int raw, int countParam)
        {
            var rule = new DrawRule();
            rule.BaseId = baseId;
            rule.Raw = raw;
            rule.RawCountParam = countParam;
            return rule;
        }

        static DrawRule Unbounded(int baseId, int raw)
        {
            var rule = new DrawRule();
            rule.BaseId = baseId;
            rule.Raw = raw;
            rule.RawCountFixed = DrawRule.Unbounded;
            return rule;
        }

        static DrawRule FuGuang()
        {
            var rule = new DrawRule();
            rule.BaseId = 193;
            rule.Raw = DrawRule.RawBasicDebuff;
            rule.RawCountFixed = 0;
            rule.ValueRanges = new[] { P(0), P(1) };
            rule.ValueSetsRawCount = true;
            return rule;
        }

        static DrawRule ChoiceRule(int baseId, int choices, int countParam)
        {
            var rule = new DrawRule();
            rule.BaseId = baseId;
            rule.Raw = DrawRule.RawChoice;
            rule.Choices = choices;
            rule.RawCountParam = countParam;
            return rule;
        }

        static DrawRule HandRule(int baseId, int raw, int capParam, string keyword)
        {
            var rule = new DrawRule();
            rule.BaseId = baseId;
            rule.Raw = raw;
            rule.CapParam = capParam;
            rule.Keyword = keyword;
            return rule;
        }

        static DrawRule Nothing(int baseId)
        {
            var rule = new DrawRule();
            rule.BaseId = baseId;
            rule.Raw = DrawRule.RawNothing;
            rule.RawCountFixed = DrawRule.Unbounded;
            return rule;
        }

        static DrawRule Ranges(int baseId, params int[] sources)
        {
            var rule = new DrawRule();
            rule.BaseId = baseId;
            rule.ValueRanges = sources;
            return rule;
        }
    }
}
