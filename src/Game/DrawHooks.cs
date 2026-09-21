using System;
using System.Collections.Generic;
using System.Globalization;
using Proto;
using Yx.ModSdk;
using Yx.ModSdk.Hooks;
using Yx.Shared;
using YxArena.Draws;

namespace YxArena.Game
{
    /// <summary>
    /// 本地随机数：战斗里每次取「随机数」的那一刻，按当前是哪张牌在取，现算一个含义正确的值（见 <see cref="DrawRule"/>）。
    ///
    /// 钩子：
    ///   BattleManager.PlayBattle/1            前置：重置随机数（同一个种子 → 重播是同一场）
    ///   CardActionBase.Execute/2              前置：顶层出牌
    ///   CardActionBase.ExecuteEffect/3        前置：被别的牌打出来的牌 / 被重复的效果
    ///   BattleCharacter.GetNextRandomValue/1  前置：记下「接下来那次取值是 Value 通道」和卦象是否生效，放行原方法（它要扣卦象）
    ///   BattleCharacter.GetNextParam/0        前置：算值并跳过原方法（原方法是从服务器给的队列里取）
    /// 任何一个没挂上就整体不启用，战斗退回「0–99 的固定随机数流」（百分比判定正常，随机出牌类的牌不正常）。
    /// </summary>
    public sealed class DrawHooks
    {
        readonly ModContext _ctx;
        readonly ArenaSession _session;
        readonly Dictionary<int, CardFacts> _facts = new Dictionary<int, CardFacts>();
        DrawPlanner _planner;
        bool _valuePending;
        bool _guaXiang;
        int _installed;

        public const int HookCount = 5;

        HookGroup _group;

        public DrawHooks(ModContext ctx, ArenaSession session)
        {
            _ctx = ctx;
            _session = session;
        }

        public bool Enabled { get { return _installed == HookCount; } }

        public void Install()
        {
            Try("BattleManager", "PlayBattle", 1, OnPlayBattle);
            Try("CardActionBase", "Execute", 2, OnExecute);
            Try("CardActionBase", "ExecuteEffect", 3, OnExecuteEffect);
            Try("BattleCharacter", "GetNextRandomValue", 1, OnNextRandomValue);
            Try("BattleCharacter", "GetNextParam", 0, OnNextParam);
            if (!Enabled) _ctx.Log.Warn(_ctx.T("本地随机数没有启用：随机出牌 / 随机负面状态 / X～Y 数值这几类牌在练习场里会不正常", "Local RNG not enabled: random-play / random-debuff / X–Y value cards will misbehave in the arena"));
        }

        void Try(string type, string method, int paramCount, Func<HookContext, bool> handler)
        {
            if (_group == null) _group = _ctx.Hooks.Group("本地随机数");
            if (_group.Prefix(type, method, paramCount, handler) != null) _installed++;
        }

        public string Summary()
        {
            if (!Enabled || _planner == null) return Enabled ? "" : _ctx.T("本地随机数未启用", "Local RNG off");
            string raw = _planner.RawDraws.ToString(CultureInfo.InvariantCulture);
            string val = _planner.ValueDraws.ToString(CultureInfo.InvariantCulture);
            return _ctx.T("取值 " + raw + " / " + val, "Draws " + raw + " / " + val);
        }

        // ── 牌的配置 → 纯数据 ────────────────────────────────────────────────

        void EnsurePlanner()
        {
            if (_planner != null) return;
            List<CardConfig> configs = ConfigManager.GetCardConfigs((SeasonMechanismType)(-1), false);
            var all = new CardFacts[configs.Count];
            for (int i = 0; i < configs.Count; i++)
            {
                all[i] = ToFacts(configs[i]);
                _facts[all[i].Id] = all[i];
            }
            _planner = new DrawPlanner(all);
            _ctx.Log.Info(_ctx.T("本地随机数：已载入 " + all.Length.ToString(CultureInfo.InvariantCulture) + " 张牌的配置", "Local RNG: loaded configs for " + all.Length.ToString(CultureInfo.InvariantCulture) + " cards"));
        }

        public static CardFacts ToFacts(CardConfig c)
        {
            var f = new CardFacts();
            f.Id = c.id;
            f.Name = c.name ?? "";
            f.Desc = c.desc ?? "";
            f.Sect = (int)c.sect;
            f.Career = (int)c.career;
            f.Level = (int)c.level;
            f.Rarity = c.rarity;
            f.Subcategory = (int)c.subcategory;
            f.Owner = c.owner;
            f.Hidden = c.hidden;
            f.Obsolete = c.obsolete;
            f.ActionAgain = c.actionAgain;
            f.Attack = c.attack;
            f.RandomAttack = c.randomAttack;
            f.AttackCount = c.attackCount;
            f.Def = c.def;
            f.RandomDef = c.randomDef;
            int count = c.otherParams != null ? c.otherParams.Count : 0;
            f.OtherParams = new int[count];
            for (int i = 0; i < count; i++) f.OtherParams[i] = c.otherParams[i];
            return f;
        }

        CardFacts FactsOf(CardConfig config)
        {
            if (config == null) return null;
            CardFacts facts;
            if (_facts.TryGetValue(config.id, out facts)) return facts;
            facts = ToFacts(config);      // 不在当前赛季牌表里的牌（隐藏牌等）：现抄一份
            _facts[config.id] = facts;
            return facts;
        }

        // ── 处理器 ────────────────────────────────────────────────────────

        bool Ready()
        {
            if (!Enabled || !ArenaSession.Active) return false;
            EnsurePlanner();
            return true;
        }

        bool OnPlayBattle(HookContext h)
        {
            if (!Ready()) return true;
            _planner.Reset(_session.LastSeed);
            _valuePending = false;
            return true;
        }

        static int SectOf(object character)
        {
            BattleCharacter c = character as BattleCharacter;
            if (c == null || c.battleTempData == null || c.battleTempData.playerData == null) return 0;
            return c.battleTempData.playerData.publicData.characterId / 1000000;
        }

        bool OnExecute(HookContext h)
        {
            if (!Ready()) return true;
            CardActionBase action = h.Instance as CardActionBase;
            if (action == null) return true;
            _planner.BeginCard(FactsOf(action.cardConfig), false, SectOf(h.Args != null && h.Args.Length > 0 ? h.Args[0] : null));
            return true;
        }

        bool OnExecuteEffect(HookContext h)
        {
            if (!Ready()) return true;
            CardActionBase action = h.Instance as CardActionBase;
            if (action == null) return true;
            _planner.BeginCard(FactsOf(action.cardConfig), true, SectOf(h.Args != null && h.Args.Length > 0 ? h.Args[0] : null));
            return true;
        }

        bool OnNextRandomValue(HookContext h)
        {
            if (!Ready()) return true;
            BattleCharacter c = h.Instance as BattleCharacter;
            // 参数取不到 / 不是 bool 时按默认值 true 处理。
            bool useGuaXiang = true;
            if (h.Args != null && h.Args.Length > 0 && h.Args[0] is bool) useGuaXiang = (bool)h.Args[0];
            _guaXiang = useGuaXiang && c != null && GuaXiangActive(c);
            _valuePending = true;
            return true;
        }

        /// <summary>与原方法扣的是同一组条件：星力（紫芒星爆）、卦象、共鸣 71 耗灵气。</summary>
        static bool GuaXiangActive(BattleCharacter c)
        {
            if (c.HasBuff(BuffType.ZiMangXingBao) && c.GetBuffValue(BuffType.XingLi) > 0) return true;
            if (c.HasBuff(BuffType.GuaXiang)) return true;
            return c.IsTalentResonanceEffective(71) && c.CheckTalentResonanceTempFlag(71) && c.battleTempData.anima > 0;
        }

        bool OnNextParam(HookContext h)
        {
            if (!Ready()) return true;
            int value;
            if (_valuePending)
            {
                _valuePending = false;
                value = _planner.DrawValue(_guaXiang);
            }
            else
            {
                BattleCharacter c = h.Instance as BattleCharacter;
                value = _planner.DrawRaw(DebuffsOf(c), HandOf(c));
            }
            h.Skip(value);
            return false;
        }

        static int[] DebuffsOf(BattleCharacter c)
        {
            if (c == null) return new int[0];
            List<BuffType> list = c.GetDebuffList();
            var ids = new int[list.Count];
            for (int i = 0; i < list.Count; i++) ids[i] = (int)list[i];
            return ids;
        }

        CardFacts[] HandOf(BattleCharacter c)
        {
            if (c == null || c.battleExecuter == null) return new CardFacts[0];
            int side = c == c.battleExecuter.leftCharacter ? 0 : 1;
            int[] ids = _session.HandOf(side);
            var hand = new CardFacts[ids.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                CardFacts facts;
                hand[i] = _facts.TryGetValue(ids[i], out facts) ? facts : null;
            }
            return hand;
        }
    }
}
