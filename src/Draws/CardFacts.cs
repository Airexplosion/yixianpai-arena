namespace YxArena.Draws
{
    /// <summary>
    /// 一张牌的配置里，「本地随机数」要用到的那部分（从游戏的 CardConfig 抄出来的纯数据，不碰游戏类型）。
    /// 枚举都存成 int：门派 0 = 无；副职 0 = 无；境界 1–6；Subcategory 见下面的常量。
    /// </summary>
    public sealed class CardFacts
    {
        public const int SubJiYuan = 1;
        public const int SubArtifact = 2;
        public const int SubPet = 3;
        public const int SubMiShu = 4;
        public const int SubMirage = 12;
        public const int SubDream = 14;
        public const int SubMa = 15;

        public int Id;
        public string Name = "";
        public string Desc = "";
        public int Sect;
        public int Career;
        public int Level;
        public int Rarity;
        public int Subcategory;
        public int Owner;
        public bool Hidden;
        public bool Obsolete;
        public bool ActionAgain;
        public int Attack;
        public int RandomAttack;
        public int AttackCount;
        public int Def;
        public int RandomDef;
        public int[] OtherParams = new int[0];

        public int BaseId { get { return CardIds.BaseOf(Id); } }

        public int Param(int index)
        {
            return index >= 0 && index < OtherParams.Length ? OtherParams[index] : 0;
        }
    }
}
