namespace YxArena
{
    /// <summary>Stable keys shared by the in-game settings and manager configuration.</summary>
    public sealed class ControlVisibility
    {
        public const int Gallery = 0, Quick = 1, DealMode = 2, Rarity = 3, Level = 4,
            First = 5, ClearHand = 6, Side = 7, Pause = 8, Overflow = 9, Limits = 10,
            Fight = 11, Hp = 12, Body = 13, BodyMax = 14, Special = 15, Category = 16,
            Talents = 17, Search = 18, Step = 19, BattlePause = 20, Tally = 21,
            Status = 22, Feed = 23, Sword = 24, Leave = 25, BattleLeave = 26, Learned = 27, Count = 28;

        static readonly string[] Keys = { "gallery", "quickDeal", "dealMode", "rarity", "realm",
            "first", "clearHand", "switchSide", "pauseAtStart", "overflow", "limits", "fight",
            "hp", "body", "bodyMax", "special", "specialCategory", "talents", "search",
            "battleStep", "battlePause", "battleTally", "status", "dealFeed", "chengxinSword", "leave", "battleLeave", "learnedCards" };
        static readonly string[] Names = { "发牌图鉴", "快捷发牌", "点牌发牌 / 详情", "牌级", "境界",
            "先手", "清空手牌", "切换编辑方", "开场暂停", "数值破限", "战斗上限", "开打",
            "血量", "体魄", "体魄上限", "特殊牌", "特殊牌类别", "仙命", "搜牌名",
            "战斗：步进", "战斗：开场暂停", "战斗：伤害统计按钮", "控制栏状态文字", "发牌记录", "澄心剑专属设置", "回大厅", "战斗：回到摆牌", "悟剑记录编辑" };
        static readonly string[] NamesEn = { "Card gallery", "Quick deal", "Click: deal / detail", "Card level", "Realm",
            "First player", "Clear hand", "Switch side", "Pause at start", "Overflow", "Battle limits", "Fight",
            "HP", "Body", "Body max", "Special cards", "Special category", "Talents", "Search",
            "Battle: step", "Battle: pause at start", "Battle: damage button", "Status text", "Deal feed", "Chengxin Sword", "Back to lobby", "Battle: back to setup", "Learned card editor" };
        readonly bool[] _visible = new bool[Count];

        public ControlVisibility() { Reset(); }
        public bool Shows(int index) { return index >= 0 && index < Count && _visible[index]; }
        public void Set(int index, bool visible) { if (index >= 0 && index < Count) _visible[index] = visible; }
        public void Reset() { for (int i = 0; i < Count; i++) _visible[i] = true; }
        public static string Key(int index) { return Keys[index]; }
        public static string Name(int index) { return Loc.T(Names[index], NamesEn[index]); }
    }
}
