using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
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
        List<CardConfig> _requestedCards;
        object _specificPanel;
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
            if (!Installed) _ctx.Log.Warn(_ctx.T("图鉴点牌钩子没挂上：发牌用不了", "Gallery click hook not installed: dealing is unavailable"));
            var specific = _ctx.Hooks.Group("特殊牌列表显示");
            specific.Prefix("SpecificCardIllustrationPanel", "Show", 1, OnSpecificShow);
            specific.Prefix("SpecificCardIllustrationPanel", "Refresh", 0, BeginSpecificRefresh);
            if (!specific.Complete)
            {
                specific.CancelAll();
                _ctx.Log.Warn(_ctx.T("特殊牌列表修复未启用，澄心剑请用专属面板直接发牌", "Special list fix unavailable; deal the sword from its dedicated panel"));
            }
            var gallery = _ctx.Hooks.Group("发牌图鉴当前赛季");
            gallery.Prefix("CardIllustrationPanel", "OnSetType", 2, BeginGalleryQuery);
            if (!gallery.Complete)
            {
                gallery.CancelAll();
                _ctx.Log.Warn(_ctx.T("图鉴赛季修复未启用，请用快捷发牌或搜索。", "Gallery season fix unavailable; use Quick deal or Search."));
            }
        }

        bool BeginGalleryQuery(HookContext h)
        {
            if (!ArenaSession.Active || !_session.InPlacement || h.Args == null || h.Args.Length != 2) return true;
            CardIllustrationPanel panel = h.Instance as CardIllustrationPanel;
            if (panel == null) return true;
            try
            {
                int type = (int)(CardIllustrationType)h.Args[0];
                int detail = (int)h.Args[1];
                int season = (int)OpenManager.seasonMec;
                CardFacts[] facts = AllFacts();
                var cards = new List<CardConfig>();
                for (int i = 0; i < facts.Length; i++)
                    if (GalleryCards.Matches(facts[i], type, detail, season))
                    {
                        CardConfig config = CardFactory.FindCardConfig(facts[i].Id);
                        if (config != null) cards.Add(config);
                    }
                var scroll = panel.FindComponent<UnityEngine.UI.ScrollRect>("ScrollView");
                if (scroll != null) { scroll.content.anchoredPosition = UnityEngine.Vector2.zero; scroll.StopMovement(); }
                if (!RenderRows(panel, cards)) return true;
                _ctx.Log.Info(_ctx.T("发牌图鉴：", "Card gallery: ") + type + "/" + detail + " season=" + season + " cards=" + cards.Count);
                h.Skip(null);
                return false;
            }
            catch (Exception e)
            {
                _ctx.Log.Warn(_ctx.T("发牌图鉴刷新失败：", "Card gallery refresh failed: ") + e.Message);
                return true;
            }
        }

        public void Tick()
        {
            if (!_session.InPlacement) { _requestedCards = null; _specificPanel = null; }
        }

        bool OnSpecificShow(HookContext h)
        {
            _requestedCards = null;
            _specificPanel = null;
            if (!ArenaSession.Active || !_session.InPlacement || h.Args == null || h.Args.Length != 1) return true;
            List<int> ids = h.Args[0] as List<int>;
            if (ids == null) return true;
            _specificPanel = h.Instance;
            _requestedCards = new List<CardConfig>();
            for (int i = 0; i < ids.Count; i++)
            {
                CardConfig config = CardFactory.FindCardConfig(ids[i]);
                if (config != null) _requestedCards.Add(config);
            }
            return true;
        }

        bool BeginSpecificRefresh(HookContext h)
        {
            // OnStart can refresh again on the next frame after Show returns.
            // Keep the requested list, but scope replacement to this panel.
            if (!ArenaSession.Active || !_session.InPlacement || !object.ReferenceEquals(h.Instance, _specificPanel) || _requestedCards == null) return true;
            try
            {
                if (!RenderRows(h.Instance, _requestedCards)) return true;
                h.Skip(null);
                return false;
            }
            catch (Exception e)
            {
                _ctx.Log.Warn(_ctx.T("特殊牌列表刷新失败：", "Specific card list refresh failed: ") + e.Message);
                return true;
            }
        }

        static bool RenderRows(object panel, List<CardConfig> cards)
        {
            // SetData is async void and now has FOUR parameters. Do not rewrite
            // its async kickoff (or ConfigManager's shared predicate method).
            // Use the game's rows directly from synchronous panel hooks, with
            // no scaled-time delay that could stall while practice is paused.
            FieldInfo field = panel.GetType().GetField("m_LevelCardsItems", BindingFlags.Instance | BindingFlags.NonPublic);
            var rows = field == null ? null : field.GetValue(panel) as List<IllustrationLevelCardsItem>;
            if (rows == null || rows.Count == 0) return false;
            for (int i = 0; i < rows.Count; i++)
            {
                Level realm = (Level)(i + 1);
                var row = new List<CardConfig>();
                for (int j = 0; j < cards.Count; j++)
                    if (cards[j] != null && cards[j].level == realm) row.Add(cards[j]);
                rows[i].SetData(realm, row, 0, null);
            }
            return true;
        }

        public void OpenQuick()
        {
            if (!_session.InPlacement) { Ui.Toast(_ctx.T("只能在备战界面发牌", "Cards can only be dealt on the setup screen")); return; }
            CharacterConfig character = ConfigManager.GetCharacterConfig(_session.Editing.CharacterId);
            if (character == null) { Ui.Toast(_ctx.T("无法读取当前角色的宗门", "Couldn't read this character's sect")); return; }
            int season = (int)OpenManager.seasonMec;
            int[] ids = QuickCards.Ids(AllFacts(), (int)character.sect, character.id, season, IsSwordOwner());
            if (ids.Length == 0) { Ui.Toast(_ctx.T("当前宗门没有可显示的赛季牌", "No current-season cards for this sect")); return; }
            DealMode = true;
            _ctx.Log.Info(_ctx.T("快捷发牌：当前赛季 / ", "Quick deal: current season / ") +
                TranslateUtil.GetSectTranslate((int)character.sect) + " / " + ids.Length.ToString(CultureInfo.InvariantCulture));
            ShowCards(ids);
        }

        public bool IsSwordOwner()
        {
            CharacterConfig c = ConfigManager.GetCharacterConfig(_session.Editing.CharacterId);
            return c != null && c.talents != null && c.talents.Contains(ChengxinSetup.GrindingTalent);
        }

        int PickForDeal(int id, string name)
        {
            int chosen = CardIds.BaseOf(id) == ChengxinSetup.SwordBaseId
                ? ChengxinSetup.CardId(_session.Editing.Level) : CardIds.WithRarity(id, _cfg.Rarity);
            if (_catalog.Exists(chosen)) return chosen;
            Ui.Toast(_ctx.T(name + "没有所选等级，无法发牌", name + ": selected level is unavailable"));
            return 0;
        }

        public void DealSword()
        {
            if (!_session.InPlacement || !IsSwordOwner()) return;
            int id = PickForDeal(ChengxinSetup.SwordBaseId, "澄心剑胚");
            if (id == 0 || !_session.Deal(id)) return;
            Dealt++;
            _notify(_ctx.T("发牌 → ", "Deal → ") + _session.Editing.Name + _ctx.T("：澄心剑胚", ": Chengxin Sword"));
        }

        bool OnCardClick(HookContext h)
        {
            if (!ArenaSession.Active || !DealMode) return true;
            if (!_session.InPlacement) return true;
            IllustrationCardItem item = h.Instance as IllustrationCardItem;
            if (item == null || item.cardItem == null || item.cardItem.cardConfig == null) return true;
            CardConfig config = item.cardItem.cardConfig;
            int id = PickForDeal(config.id, config.name);
            if (id == 0) { h.Skip(null); return false; }
            if (_session.Deal(id))
            {
                Dealt++;
                _notify(_ctx.T("发牌 → ", "Deal → ") + _session.Editing.Name + _ctx.T("：", ": ") + config.name + " " + ArenaConfig.RarityName(CardIds.RarityOf(id)));
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
            if (!_session.InPlacement) { Ui.Toast(_ctx.T("只能在备战界面发牌", "Cards can only be dealt on the setup screen")); return; }
            try
            {
                CardFacts[] all = AllFacts();
                int[] ids = SpecialCards.Ids(SpecialCategory, all);
                int hiddenByPanel = SpecialCards.CountWithoutRealm(SpecialCategory, all);
                string name = SpecialCards.Name(SpecialCategory);
                string shown = ids.Length.ToString(CultureInfo.InvariantCulture);
                string hidden = hiddenByPanel.ToString(CultureInfo.InvariantCulture);
                _ctx.Log.Info(_ctx.T("特殊牌「" + name + "」：" + shown + " 张；另有 " + hidden + " 张没有境界，游戏的面板显示不了",
                              "Special cards \"" + name + "\": " + shown + "; another " + hidden + " have no realm and can't be shown by the game's panel"));
                if (ids.Length == 0) { Ui.Toast(_ctx.T("「" + name + "」这一类里没有能显示的牌", "No showable cards in the \"" + name + "\" category")); return; }
                ShowCards(ids);
            }
            catch (Exception e) { _ctx.Log.Error(_ctx.T("打开特殊牌面板失败", "Open special cards panel failed"), e); }
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
            if (panel == null) { Ui.Toast(_ctx.T("没能打开卡牌面板", "Couldn't open the card panel")); return false; }
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
            if (!_session.InPlacement) { Ui.Toast(_ctx.T("只能在备战界面发牌", "Cards can only be dealt on the setup screen")); return; }
            try
            {
                string wanted = query.Trim();
                CardFacts[] all = AllFacts();
                int[] found = CardSearch.Find(wanted, all);
                if (found.Length == 0) { Ui.Toast(_ctx.T("没有名字含「" + wanted + "」的牌", "No cards with a name containing \"" + wanted + "\"")); return; }
                int[] showable = CardSearch.Showable(found, all);
                string foundN = found.Length.ToString(CultureInfo.InvariantCulture);
                string showN = showable.Length.ToString(CultureInfo.InvariantCulture);
                _ctx.Log.Info(_ctx.T("搜牌「" + wanted + "」：" + foundN + " 张，能显示 " + showN + " 张",
                              "Search \"" + wanted + "\": " + foundN + " found, " + showN + " showable"));
                if (showable.Length > 0) { ShowCards(showable); return; }
                int id = PickForDeal(found[0], NameOf(found[0], all));
                if (id == 0 || !_session.Deal(id)) return;
                Dealt++;
                _notify(_ctx.T("发牌 → ", "Deal → ") + _session.Editing.Name + _ctx.T("：", ": ") + NameOf(id, all) + _ctx.T("（没有境界，面板显示不了，直接发了）", " (no realm; can't show in panel, dealt directly)"));
            }
            catch (Exception e) { _ctx.Log.Error(_ctx.T("搜牌失败", "Card search failed"), e); }
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
            if (!_session.InPlacement) { Ui.Toast(_ctx.T("只能在备战界面发牌", "Cards can only be dealt on the setup screen")); return; }
            try
            {
                BattlePanel bp = ILRPanelBase.FindILRPanel<BattlePanel>();
                if (bp == null || bp.readyLayer == null) return;
                CardIllustrationPanel gallery = bp.FindILRSubPanelRuntime<CardIllustrationPanel>(bp.readyLayer.subPanelContainer);
                if (gallery != null) gallery.ShowNormal(false);
            }
            catch (Exception e) { _ctx.Log.Error(_ctx.T("打开图鉴失败", "Open gallery failed"), e); }
        }
    }
}
