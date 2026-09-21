namespace YxArena
{
    /// <summary>「这张牌存不存在」的查询口；游戏里接 CardFactory.CheckCardExist，测试里用假的。</summary>
    public interface ICardCatalog
    {
        bool Exists(int id);
    }

    /// <summary>
    /// 牌 id 的运算。游戏把牌的 1 / 2 / 3 级编码在万位（代码里叫 rarity：id / 10000 % 100 = 0 / 1 / 2），
    /// 去掉它就是基础牌（图鉴里列的就是基础牌）。境界不在 id 里，要看 CardConfig.level。
    /// </summary>
    public static class CardIds
    {
        public const int MaxRarity = 2;
        public const int HandLimit = 50;
        const int RarityUnit = 10000;

        public static int RarityOf(int id)
        {
            return id / RarityUnit % 100;
        }

        public static int BaseOf(int id)
        {
            return id - RarityOf(id) * RarityUnit;
        }

        public static int WithRarity(int id, int rarity)
        {
            int clamped = rarity < 0 ? 0 : (rarity > MaxRarity ? MaxRarity : rarity);
            return BaseOf(id) + clamped * RarityUnit;
        }

        /// <summary>要发的牌：该级存在就用它，不存在退回基础牌，连基础牌都不认识返回 0。</summary>
        public static int Pick(int id, int rarity, ICardCatalog catalog)
        {
            int wanted = WithRarity(id, rarity);
            if (catalog.Exists(wanted)) return wanted;
            int baseId = BaseOf(id);
            return catalog.Exists(baseId) ? baseId : 0;
        }
    }
}
