using System.Collections.Generic;
using System.Globalization;

namespace YxArena
{
    /// <summary>
    /// 「破限」（饱和算术改写）要处理哪些热更类型、处理过哪些。纯逻辑。
    /// 战斗核心只在第一次开打前处理一次；卡牌类（Card_基础牌号，游戏的 CardFactory 就是按这个名字找类的）用到哪张处理哪张。
    /// 改写在本次游戏进程内不可撤销，所以「处理过」只增不减。
    /// </summary>
    public sealed class OverflowPlan
    {
        public const int MinBudget = 300;
        public const int MaxBudget = 40000;
        public const int StartBudget = 1500;
        /// <summary>每帧愿意花在改写上的时间（毫秒）。</summary>
        public const float TargetMs = 10f;

        const string Core = "*core";

        // 值是占位：只当集合用（热更代码里避免值类型参数的 CLR 泛型集合）。
        readonly Dictionary<string, string> _done = new Dictionary<string, string>();

        /// <summary>战斗核心：数值公式所在的类型。卡牌类不在这里（九百多个，按需处理）。</summary>
        public static string[] CoreTypes()
        {
            return new string[]
            {
                "BattleCharacter", "CardActionBase", "BattleExecuter", "FallbackCardAction", "RefineCardActionBase",
                "DanYaoCardActionBase", "CardReadyLayerActionBase", "FateStrategyFunctions", "KeYinCardFunctions",
                "PlayerSelfInfoItem", "BattleCharacterUI"
            };
        }

        public static string CardType(int cardId)
        {
            return "Card_" + CardIds.BaseOf(cardId).ToString(CultureInfo.InvariantCulture);
        }

        public bool CoreDone { get { return _done.ContainsKey(Core); } }

        public bool IsDone(string type) { return _done.ContainsKey(type); }

        /// <summary>这一场开打前还要处理的类型：战斗核心（没处理过的话）+ 场上这些牌里没处理过的。去重，保持顺序。</summary>
        public string[] Pending(int[] cardIds)
        {
            var list = new List<string>();
            var seen = new Dictionary<string, string>();
            if (!CoreDone)
            {
                string[] core = CoreTypes();
                for (int i = 0; i < core.Length; i++) list.Add(core[i]);
            }
            if (cardIds != null)
            {
                for (int i = 0; i < cardIds.Length; i++)
                {
                    if (cardIds[i] <= 0) continue;
                    string type = CardType(cardIds[i]);
                    if (_done.ContainsKey(type) || seen.ContainsKey(type)) continue;
                    seen[type] = "";
                    list.Add(type);
                }
            }
            return list.ToArray();
        }

        /// <summary>一批类型处理完了（Pending 给出去的那一批）。</summary>
        public void MarkDone(string[] types)
        {
            if (types == null) return;
            string[] core = CoreTypes();
            for (int i = 0; i < types.Length; i++)
            {
                _done[types[i]] = "";
                if (types[i] == core[0]) _done[Core] = "";
            }
        }

        public void MarkCardDone(string type) { _done[type] = ""; }

        /// <summary>按上一步实际花的时间调整下一步的指令预算：太快就加倍，太慢就减半。</summary>
        public static int NextBudget(int budget, float elapsedMs)
        {
            int next = budget;
            if (elapsedMs < TargetMs / 2f) next = budget * 2;
            else if (elapsedMs > TargetMs * 2f) next = budget / 2;
            if (next < MinBudget) next = MinBudget;
            if (next > MaxBudget) next = MaxBudget;
            return next;
        }

        /// <summary>进度文字：处理到第几个类型。</summary>
        public static string Progress(int typeIndex, int typeCount)
        {
            int shown = typeIndex + 1 > typeCount ? typeCount : typeIndex + 1;
            return shown.ToString(CultureInfo.InvariantCulture) + "/" + typeCount.ToString(CultureInfo.InvariantCulture);
        }
    }
}
