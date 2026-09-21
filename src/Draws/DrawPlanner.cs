namespace YxArena.Draws
{
    /// <summary>
    /// 在取值的那一刻算出一个含义正确的值。纯逻辑：谁在出牌、身上有什么负面状态、保留了哪些手牌，都由调用方告诉它。
    ///
    /// 「当前是哪张牌在取」用一个栈记：顶层出牌（CardActionBase.Execute）清栈后入栈；被别的牌随机打出来的牌
    /// （CardActionBase.ExecuteEffect）再入一层。嵌套的牌什么时候执行完没有事件可挂（它是 async 的），所以用预算判断：
    /// 每条规则知道自己要 Raw 取几次，取够了（或取到了 -1）就轮到外层的牌。Value 通道的取值总是算在栈顶。
    /// </summary>
    public sealed class DrawPlanner
    {
        const int MaxDepth = 8;
        static readonly int[] BasicDebuffs = { 100, 101, 102, 103, 104, 105 };   // 内伤 虚弱 破绽 减攻 困缚 外伤
        static readonly int[] ThreeDebuffs = { 100, 101, 105 };                   // 内伤 虚弱 外伤

        sealed class Frame
        {
            public CardFacts Card;
            public DrawRule Rule;
            public int RawLeft;
            public int ValueIndex;
            public int OwnerSect;
        }

        readonly CardFacts[] _all;
        readonly Frame[] _stack = new Frame[MaxDepth];
        int _depth;
        uint _state = 1u;
        int _poolKey = -1;
        int[] _poolIds = new int[0];

        public int RawDraws;
        public int ValueDraws;

        public DrawPlanner(CardFacts[] all)
        {
            _all = all ?? new CardFacts[0];
        }

        /// <summary>每场战斗开始（含重播）时调用：同一个种子 → 同一场战斗。</summary>
        public void Reset(uint seed)
        {
            _state = seed == 0u ? 1u : seed;
            _depth = 0;
        }

        /// <param name="nested">true = 被别的牌随机打出来的（ExecuteEffect）；false = 顶层出牌。</param>
        public void BeginCard(CardFacts card, bool nested, int ownerSect)
        {
            if (!nested) _depth = 0;
            if (card == null || _depth >= MaxDepth) return;
            var frame = new Frame();
            frame.Card = card;
            frame.Rule = DrawRules.Find(card.BaseId);
            frame.RawLeft = frame.Rule != null ? frame.Rule.RawBudget(card) : 0;
            frame.OwnerSect = ownerSect;
            _stack[_depth] = frame;
            _depth++;
        }

        int Next(int exclusiveMax)
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            return exclusiveMax <= 1 ? 0 : (int)(_state % (uint)exclusiveMax);
        }

        // ── Value 通道 ────────────────────────────────────────────────────

        /// <param name="guaXiang">这次取值有卦象（或同类效果）生效：区间取最大，概率判定必中。</param>
        public int DrawValue(bool guaXiang)
        {
            ValueDraws++;
            Frame frame = _depth > 0 ? _stack[_depth - 1] : null;
            if (frame == null) return Percent(guaXiang);
            int index = frame.ValueIndex;
            frame.ValueIndex++;
            int min;
            int max;
            if (!RangeFor(frame, index, out min, out max)) return Percent(guaXiang);
            if (max < min) max = min;
            int value = guaXiang ? max : min + Next(max - min + 1);
            if (frame.Rule != null && frame.Rule.ValueSetsRawCount && index == 0) frame.RawLeft = value;
            return value;
        }

        int Percent(bool guaXiang)
        {
            return guaXiang ? 0 : Next(100);
        }

        static bool RangeFor(Frame frame, int index, out int min, out int max)
        {
            min = 0;
            max = 0;
            CardFacts card = frame.Card;
            DrawRule rule = frame.Rule;
            if (rule != null && rule.ValueRanges.Length > 0)
            {
                if (index * 2 + 1 >= rule.ValueRanges.Length) return false;
                min = Source(card, rule.ValueRanges[index * 2]);
                max = Source(card, rule.ValueRanges[index * 2 + 1]);
                return true;
            }
            // 没有专门规则的牌：配置里带随机攻击 / 随机防的，前几次取值就是它们（通用的出牌流程先攻后防）。
            int attacks = card.RandomAttack > card.Attack ? (card.AttackCount > 0 ? card.AttackCount : 1) : 0;
            if (index < attacks)
            {
                min = card.Attack;
                max = card.RandomAttack;
                return true;
            }
            if (card.RandomDef > card.Def && index == attacks)
            {
                min = card.Def;
                max = card.RandomDef;
                return true;
            }
            return false;
        }

        static int Source(CardFacts card, int source)
        {
            if (source >= DrawRule.SrcParam) return card.Param(source - DrawRule.SrcParam);
            switch (source)
            {
                case DrawRule.SrcAttack: return card.Attack;
                case DrawRule.SrcRandomAttack: return card.RandomAttack;
                case DrawRule.SrcDef: return card.Def;
                case DrawRule.SrcRandomDef: return card.RandomDef;
                default: return 0;
            }
        }

        // ── Raw 通道 ──────────────────────────────────────────────────────

        /// <param name="ownDebuffs">取值的角色身上现有的负面状态类型号（升序）。</param>
        /// <param name="hand">出牌一方保留的手牌。</param>
        public int DrawRaw(int[] ownDebuffs, CardFacts[] hand)
        {
            RawDraws++;
            // 栈顶的牌取够了 → 轮到外层的牌。
            while (_depth > 1 && _stack[_depth - 1].RawLeft <= 0) _depth--;
            Frame frame = _depth > 0 ? _stack[_depth - 1] : null;
            if (frame == null || frame.Rule == null || frame.RawLeft <= 0) return -1;
            frame.RawLeft--;
            int value = RawValue(frame, ownDebuffs, hand);
            if (value == -1) frame.RawLeft = 0;
            return value;
        }

        int RawValue(Frame frame, int[] ownDebuffs, CardFacts[] hand)
        {
            DrawRule rule = frame.Rule;
            CardFacts card = frame.Card;
            switch (rule.Raw)
            {
                case DrawRule.RawPool:
                    int[] pool = PoolFor(frame);
                    return pool.Length == 0 ? -1 : pool[Next(pool.Length)];
                case DrawRule.RawOwnDebuff:
                    return ownDebuffs == null || ownDebuffs.Length == 0 ? -1 : ownDebuffs[Next(ownDebuffs.Length)];
                case DrawRule.RawBasicDebuff:
                    return BasicDebuffs[Next(BasicDebuffs.Length)];
                case DrawRule.RawThreeDebuff:
                    return ThreeDebuffs[Next(ThreeDebuffs.Length)];
                case DrawRule.RawChoice:
                    return Next(rule.Choices);
                case DrawRule.RawHandCount:
                {
                    int count = hand != null ? hand.Length : 0;
                    int cap = card.Param(rule.CapParam);
                    return cap > 0 && count > cap ? cap : count;
                }
                case DrawRule.RawHandNameBonus:
                {
                    int matches = 0;
                    if (hand != null)
                        for (int i = 0; i < hand.Length; i++)
                            if (hand[i] != null && hand[i].Name.Contains(rule.Keyword)) matches++;
                    return matches * card.Param(rule.CapParam);
                }
                default:
                    return -1;
            }
        }

        int[] PoolFor(Frame frame)
        {
            DrawRule rule = frame.Rule;
            int rarity = rule.FixedRarity >= 0 ? rule.FixedRarity : frame.Card.Rarity;
            int key = ((rule.Pool * 4 + rarity) * 8 + frame.OwnerSect) * 100000 + frame.Card.BaseId % 100000;
            if (key != _poolKey)
            {
                _poolIds = CardPools.Build(rule.Pool, _all, frame.Card, rarity, frame.OwnerSect);
                _poolKey = key;
            }
            return _poolIds;
        }
    }
}
