namespace YxArena
{
    /// <summary>
    /// 战斗的随机数流（BattleResult.battleParams）。平时由服务器预生成，客户端按顺序取用；
    /// 有两处取用是裸 Dequeue，空了会抛并被战斗循环吞掉，所以要给足。值域 0–99。
    /// </summary>
    public static class ParamStream
    {
        public static int[] Generate(uint seed, int count)
        {
            if (count <= 0) return new int[0];
            uint state = seed == 0u ? 1u : seed;   // 0 是 xorshift 的不动点
            var values = new int[count];
            for (int i = 0; i < count; i++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                values[i] = (int)(state % 100u);
            }
            return values;
        }

        /// <summary>每场换一个种子：帧计数器与场次混一下，保证不为 0。</summary>
        public static uint MixSeed(int frameCount, int battleIndex)
        {
            unchecked
            {
                uint mixed = (uint)frameCount * 2654435761u + (uint)battleIndex * 40503u + 1u;
                return mixed == 0u ? 1u : mixed;
            }
        }
    }
}
