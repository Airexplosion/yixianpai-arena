namespace YxArena.Draws
{
    /// <summary>
    /// 「随机使用 N 张 XX 牌」的牌池。真正的牌池逻辑只在服务器上，客户端拿到的只是一个牌 id；
    /// 这里按卡面文字重建，是近似——哪条和实际不符，改这一个文件。
    /// 所有牌池共同的条件：不是隐藏 / 作废的牌、不是发起的那张牌自己、牌级符合要求。
    /// </summary>
    public static class CardPools
    {
        public const int None = 0;
        public const int SectAny = 1;            // 门派牌（神来之笔、五彩鲛珠）
        public const int NameKuangJian = 2;      // 名字含「狂剑」
        public const int SectOrCareer = 3;       // 门派牌或副职牌（天马行空）
        public const int SectAttack = 4;         // 含攻击的门派牌（金戈铁马）
        public const int SectNoAttack = 5;       // 不含攻击的门派牌（秣马厉兵）
        public const int OtherSect = 6;          // 其他门派的牌（指鹿为马）
        public const int NameZong = 7;           // 粽子牌（野马分粽）
        public const int AgainOrShenFa = 8;      // 与再次行动 / 身法有关（一马当先）
        public const int NameMa = 9;             // 名字含「马」（蛛丝马迹）
        public const int MiShu = 10;             // 秘术牌（马到成功）
        public const int Exclusive = 11;         // 角色专属牌（谈马掌）
        public const int DreamHuaShen = 12;      // 化神期梦境牌（梦·天马行空）
        public const int JiYuan = 13;            // 机缘牌（心猿意马）
        public const int LevelFanXu = 14;        // 返虚期牌（一马平川）
        public const int CareerYuanYing = 15;    // 元婴期副职牌（梦·触类旁通）
        public const int TreasureYuanYingUp = 16; // 元婴期及以上的灵宠 / 法宝 / 秘术（梦·神来之笔）
        public const int NameBengQuan = 17;      // 名字含「崩拳」（无尽崩绝）

        const int LevelYuanYing = 4;
        const int LevelHuaShen = 5;
        const int LevelFanXuValue = 6;

        /// <param name="ownerSect">出牌一方的门派（角色 id / 1000000）；0 = 不知道。</param>
        public static bool Matches(int pool, CardFacts card, CardFacts parent, int rarity, int ownerSect)
        {
            if (card == null || card.Id <= 0 || card.Hidden || card.Obsolete) return false;
            if (card.Rarity != rarity) return false;
            if (parent != null && card.BaseId == parent.BaseId) return false;
            bool plainSect = card.Sect != 0 && card.Subcategory == 0 && card.Owner == 0;
            switch (pool)
            {
                case SectAny: return plainSect;
                case NameKuangJian: return card.Name.Contains("狂剑");
                case SectOrCareer: return plainSect || (card.Career != 0 && card.Subcategory == 0);
                case SectAttack: return plainSect && card.Attack > 0;
                case SectNoAttack: return plainSect && card.Attack <= 0;
                case OtherSect: return plainSect && ownerSect != 0 && card.Sect != ownerSect;
                case NameZong: return card.Name.Contains("粽");
                case AgainOrShenFa:
                    return (plainSect || card.Career != 0) && (card.ActionAgain || card.Desc.Contains("身法") || card.Desc.Contains("再次行动"));
                case NameMa: return card.Name.Contains("马");
                case MiShu: return card.Subcategory == CardFacts.SubMiShu;
                case Exclusive: return card.Owner != 0;
                case DreamHuaShen: return card.Subcategory == CardFacts.SubDream && card.Level == LevelHuaShen;
                case JiYuan: return card.Subcategory == CardFacts.SubJiYuan;
                case LevelFanXu: return card.Level == LevelFanXuValue && (plainSect || card.Career != 0);
                case CareerYuanYing: return card.Career != 0 && card.Subcategory == 0 && card.Level == LevelYuanYing;
                case TreasureYuanYingUp:
                    return card.Level >= LevelYuanYing
                           && (card.Subcategory == CardFacts.SubPet || card.Subcategory == CardFacts.SubArtifact || card.Subcategory == CardFacts.SubMiShu);
                case NameBengQuan: return card.Name.Contains("崩拳");
                default: return false;
            }
        }

        /// <summary>牌池里所有牌的 id（按 id 升序，保证同一个种子抽到同一张）。</summary>
        public static int[] Build(int pool, CardFacts[] all, CardFacts parent, int rarity, int ownerSect)
        {
            int count = 0;
            for (int i = 0; i < all.Length; i++) if (Matches(pool, all[i], parent, rarity, ownerSect)) count++;
            var ids = new int[count];
            int n = 0;
            for (int i = 0; i < all.Length; i++) if (Matches(pool, all[i], parent, rarity, ownerSect)) ids[n++] = all[i].Id;
            for (int i = 1; i < ids.Length; i++)
            {
                int value = ids[i];
                int j = i - 1;
                while (j >= 0 && ids[j] > value) { ids[j + 1] = ids[j]; j--; }
                ids[j + 1] = value;
            }
            return ids;
        }
    }
}
