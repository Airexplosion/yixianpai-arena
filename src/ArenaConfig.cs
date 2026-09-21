namespace YxArena
{
    /// <summary>练习场的设置。纯数据 + 取值范围；读写存档在入口类里做。</summary>
    public sealed class ArenaConfig
    {
        public const int DefaultCharacterId = 1000001;
        public const int MinLevel = 1;
        public const int MaxLevel = 6;
        public const int DefaultDummyHp = 300;

        static readonly string[] LevelNames = { "炼气", "筑基", "金丹", "元婴", "化神", "返虚" };

        public int CharacterId = DefaultCharacterId;
        public int Level = MinLevel;
        /// <summary>对手（木人）的总血量；0 = 跟随境界。</summary>
        public int DummyHp = DefaultDummyHp;
        public bool PlayerFirst = true;
        public int Rarity;

        public int NextLevel()
        {
            Level = Level >= MaxLevel || Level < MinLevel ? MinLevel : Level + 1;
            return Level;
        }

        public int NextRarity()
        {
            Rarity = Rarity >= CardIds.MaxRarity || Rarity < 0 ? 0 : Rarity + 1;
            return Rarity;
        }

        /// <summary>读存档之后调用：越界的值放回默认。</summary>
        public void Normalize()
        {
            if (CharacterId <= 0) CharacterId = DefaultCharacterId;
            if (Level < MinLevel || Level > MaxLevel) Level = MinLevel;
            if (Rarity < 0 || Rarity > CardIds.MaxRarity) Rarity = 0;
            if (DummyHp < 0 || DummyHp > ArenaSide.MaxNumber) DummyHp = DefaultDummyHp;
        }

        public static string LevelName(int level)
        {
            return level >= MinLevel && level <= MaxLevel ? LevelNames[level - 1] : "?";
        }

        public static string RarityName(int rarity)
        {
            switch (rarity)
            {
                case 0: return "1 级";
                case 1: return "2 级";
                case 2: return "3 级";
                default: return "?";
            }
        }
    }
}
