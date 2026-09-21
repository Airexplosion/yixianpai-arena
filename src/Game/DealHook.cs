using System;
using System.Collections.Generic;
using System.Globalization;
using Proto;
using Yx.ModSdk;
using Yx.ModSdk.Unity;
using Yx.Shared;
using YxArena.Draws;

namespace YxArena.Game
{
    /// <summary>CardFactory.CheckCardExist 接到 <see cref="ICardCatalog"/> 上。</summary>
    public sealed class GameCatalog : ICardCatalog
    {
        public bool Exists(int id)
        {
            return id > 0 && CardFactory.CheckCardExist(id);
        }
    }

    /// <summary>
    /// 「图鉴里点牌 = 发牌」。游戏自己的图鉴（CardIllustrationPanel）在战斗场景里点一张牌默认只是弹卡牌详情
    /// （IllustrationCardItem.OnPointerClick）；练习场里、发牌模式开着时改成把这张牌发进手牌。
    /// 发牌模式关掉、或不在练习场里，一律放行原逻辑。
    /// </summary>
    public sealed class DealHook
    {
        readonly ModContext _ctx;
        readonly ArenaConfig _cfg;
        readonly ArenaSession _session;
        readonly GameCatalog _catalog = new GameCatalog();
        readonly Action<string> _notify;

        public bool DealMode = true;
        public int SpecialCategory = SpecialCards.Dream;
        CardFacts[] _allFacts;
        public bool Installed;
        public int Dealt;

        /// <param name="notify">发出一张牌时的提示（入口类接到向上滚动的发牌记录上）。</param>
        public DealHook(ModContext ctx, ArenaConfig cfg, ArenaSession session, Action<string> notify)
        {
            _notify = notify;
            _ctx = ctx;
            _cfg = cfg;
            _session = session;
        }

        public void Install()
        {
            Installed = _ctx.Hooks.TryPrefix("IllustrationCardItem", "OnPointerClick", 1, OnCardClick) != null;
            if (!Installed) _ctx.Log.Warn("图鉴点牌钩子没挂上：发牌用不了");
        }

        bool OnCardClick(HookContext h)
        {
            if (!ArenaSession.Active || !DealMode) return true;
            if (!_session.InPlacement) return true;
            IllustrationCardItem item = h.Instance as IllustrationCardItem;
            if (item == null || item.cardItem == null || item.cardItem.cardConfig == null) return true;
            CardConfig config = item.cardItem.cardConfig;
            int id = CardIds.Pick(config.id, _cfg.Rarity, _catalog);
            if (id == 0) return true;
            if (_session.Deal(id))
            {
                Dealt++;
                string note = CardIds.RarityOf(id) == _cfg.Rarity ? "" : "（没有" + ArenaConfig.RarityName(_cfg.Rarity) + "，发了 1 级）";
                _notify("发牌 → " + _session.Editing.Name + "：" + config.name + " " + ArenaConfig.RarityName(CardIds.RarityOf(id)) + note);
            }
            h.Skip(null);
            return false;
        }

        /// <summary>
        /// 游戏的图鉴不列梦境牌 / 幻境牌 / 马牌 / 各种衍生牌。用游戏自己的另一个面板显示它们：
        /// SpecificCardIllustrationPanel.Show(List&lt;int&gt; cardIds)——给一串 id，按境界分行显示卡面；
        /// 里面的牌同样是 IllustrationCardItem，点牌走的还是上面那个钩子。
        /// </summary>
        public void OpenSpecial()
        {
            if (!_session.InPlacement) { Ui.Toast("只能在备战界面发牌"); return; }
            try
            {
                CardFacts[] all = AllFacts();
                int[] ids = SpecialCards.Ids(SpecialCategory, all);
                int hiddenByPanel = SpecialCards.CountWithoutRealm(SpecialCategory, all);
                string name = SpecialCards.Name(SpecialCategory);
                _ctx.Log.Info("特殊牌「" + name + "」：" + ids.Length.ToString(CultureInfo.InvariantCulture) + " 张；另有 "
                              + hiddenByPanel.ToString(CultureInfo.InvariantCulture) + " 张没有境界，游戏的面板显示不了");
                if (ids.Length == 0) { Ui.Toast("「" + name + "」这一类里没有能显示的牌"); return; }
                ShowCards(ids);
            }
            catch (Exception e) { _ctx.Log.Error("打开特殊牌面板失败", e); }
        }

        CardFacts[] AllFacts()
        {
            if (_allFacts != null) return _allFacts;
            // 不按赛季过滤：练习场里什么牌都可以拿来试。
            List<CardConfig> configs = ConfigManager.GetCardConfigs((SeasonMechanismType)(-1), true);
            _allFacts = new CardFacts[configs.Count];
            for (int i = 0; i < configs.Count; i++) _allFacts[i] = DrawHooks.ToFacts(configs[i]);
            return _allFacts;
        }

        bool ShowCards(int[] ids)
        {
            BattlePanel bp = ILRPanelBase.FindILRPanel<BattlePanel>();
            if (bp == null || bp.readyLayer == null) return false;
            SpecificCardIllustrationPanel panel = bp.FindILRSubPanelRuntime<SpecificCardIllustrationPanel>(bp.readyLayer.subPanelContainer);
            if (panel == null) { Ui.Toast("没能打开卡牌面板"); return false; }
            var list = new List<int>();
            for (int i = 0; i < ids.Length; i++) list.Add(ids[i]);
            panel.Show(list);
            return true;
        }

        /// <summary>
        /// 按名字搜牌（含梦境 / 幻境 / 衍生牌，不分赛季），结果用同一个卡牌面板显示，点牌 = 发牌。
        /// 没有境界的牌面板显示不了：只搜到这种牌时，直接把第一张发出去。
        /// </summary>
        public void Search(string query)
        {
            if (query == null || query.Trim().Length == 0) return;
            if (!_session.InPlacement) { Ui.Toast("只能在备战界面发牌"); return; }
            try
            {
                string wanted = query.Trim();
                CardFacts[] all = AllFacts();
                int[] found = CardSearch.Find(wanted, all);
                if (found.Length == 0) { Ui.Toast("没有名字含「" + wanted + "」的牌"); return; }
                int[] showable = CardSearch.Showable(found, all);
                _ctx.Log.Info("搜牌「" + wanted + "」：" + found.Length.ToString(CultureInfo.InvariantCulture) + " 张，能显示 "
                              + showable.Length.ToString(CultureInfo.InvariantCulture) + " 张");
                if (showable.Length > 0) { ShowCards(showable); return; }
                int id = CardIds.Pick(found[0], _cfg.Rarity, _catalog);
                if (id == 0 || !_session.Deal(id)) return;
                Dealt++;
                _notify("发牌 → " + _session.Editing.Name + "：" + NameOf(id, all) + "（没有境界，面板显示不了，直接发了）");
            }
            catch (Exception e) { _ctx.Log.Error("搜牌失败", e); }
        }

        static string NameOf(int id, CardFacts[] all)
        {
            int baseId = CardIds.BaseOf(id);
            for (int i = 0; i < all.Length; i++) if (all[i] != null && all[i].Id == baseId) return all[i].Name;
            return id.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>打开游戏自己的图鉴（备战界面的图鉴按钮做的就是这一句）。</summary>
        public void OpenGallery()
        {
            if (!_session.InPlacement) { Ui.Toast("只能在备战界面发牌"); return; }
            try
            {
                BattlePanel bp = ILRPanelBase.FindILRPanel<BattlePanel>();
                if (bp == null || bp.readyLayer == null) return;
                CardIllustrationPanel gallery = bp.FindILRSubPanelRuntime<CardIllustrationPanel>(bp.readyLayer.subPanelContainer);
                if (gallery != null) gallery.ShowNormal(false);
            }
            catch (Exception e) { _ctx.Log.Error("打开图鉴失败", e); }
        }
    }
}
