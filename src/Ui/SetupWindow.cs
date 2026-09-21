using System;
using System.Collections.Generic;
using System.Globalization;
using Proto;
using UnityEngine;
using Yx.ModSdk;
using Yx.ModSdk.Unity;
using YxArena.Game;

namespace YxArena.Views
{
    /// <summary>
    /// 仙命 / 天衍仙命的小窗，大厅的选英雄界面和练习场的备战界面都能打开：
    ///   · 仙命槽页：五个槽（炼气…化神），点名字选仙命，× 清空，「计数」是这个仙命攒的层数 / 次数；备战界面里还能换角色；
    ///   · 选仙命 / 选角色用游戏自己的 SelectInfoPanel（原生、带图标、所有仙命不限角色）；它打不开时退回自绘的全仙命表
    ///     （按 全部 / 通用 / 各门派 / 角色专属 筛选，按名字搜索，分页）；
    ///   · 天衍仙命表：同样的列表，点一下选上 / 取消，可多选。
    /// 每次改动都整窗重建（控件不多，省掉逐个刷新的代码）。改完通知外面（备战界面里要重新喂一次对局状态）。
    /// </summary>
    public sealed class SetupWindow
    {
        const float Width = 900f;
        const float Height = 560f;
        const float Row = 44f;
        const float Pad = 16f;
        const int Columns = 4;
        const int Rows = 8;
        const float CellWidth = 212f;

        const int PageSlots = 0;
        const int PageTalents = 1;
        const int PageFates = 2;

        const int KindPickSlot = 1;
        const int KindClearSlot = 2;
        const int KindSlotValue = 3;
        const int KindChooseTalent = 4;
        const int KindToggleFate = 5;
        const int KindGroup = 6;
        const int KindPickCharacter = 7;

        const int GroupGeneral = 0;      // 1–4 = 门派
        const int GroupExclusive = 9;

        static readonly string[] SlotNames = { "炼气", "筑基", "金丹", "元婴", "化神" };
        static readonly string[] SlotNamesEn = { "Qi Refining", "Foundation", "Golden Core", "Nascent Soul", "Spirit Severing" };
        static readonly string[] LevelTags = { "", "炼", "筑", "金", "元", "化", "返" };
        static readonly string[] LevelTagsEn = { "", "Q", "F", "G", "N", "S", "V" };

        /// <summary>带着一个序号的回调（按钮 / 输入框的委托不带参数，用它把「第几个槽」「哪个 id」带进去）。</summary>
        sealed class Bound
        {
            public SetupWindow Owner;
            public int Kind;
            public int Value;

            public void Run() { Owner.OnBound(Kind, Value, null); }

            public void RunText(string text) { Owner.OnBound(Kind, Value, text); }
        }

        readonly ModContext _ctx;
        readonly ArenaSession _session;
        readonly Action _changed;
        readonly List<Bound> _bounds = new List<Bound>();
        GameObject _root;
        int _side;
        int _page;
        int _slot;
        int _pageIndex;
        int _group = ListFilter.AnyGroup;
        string _filter = "";
        int[] _talentIds;
        string[] _talentNames;
        int[] _talentGroups;
        int[] _fateIds;
        string[] _fateNames;
        bool _allowCharacter;
        bool _direct;
        readonly Action _hideTips;
        readonly BoxSearch _search;
        bool _nativeBroken;
        SelectInfoPanel _waiting;
        bool _returnToWindow;
        int _waitingFrames;

        /// <param name="changed">仙命 / 天衍仙命 / 计数改了之后调用。</param>
        /// <param name="hideTips">原生选择框关掉时收起悬停说明。</param>
        /// <param name="search">给游戏的选择框挂搜索框的那个部件。</param>
        readonly UiKit _ui;

        public SetupWindow(UiKit ui, ModContext ctx, ArenaSession session, Action changed, Action hideTips, BoxSearch search)
        {
            _ui = ui;
            _search = search;
            _hideTips = hideTips;
            _ctx = ctx;
            _session = session;
            _changed = changed;
        }

        public bool IsOpen { get { return _root != null || _waiting != null; } }

        static string N(int value) { return value.ToString(CultureInfo.InvariantCulture); }

        /// <summary>打开仙命槽页。</summary>
        /// <param name="allowCharacter">能不能在这里换角色。大厅里不行（选英雄界面自己管角色，会把这里的改动盖掉）。</param>
        public void OpenSlots(int side, bool allowCharacter)
        {
            _direct = false;
            _allowCharacter = allowCharacter;
            _side = side == 1 ? 1 : 0;
            _page = PageSlots;
            Rebuild();
        }

        /// <summary>
        /// 点了某个仙命图标（选英雄界面上角色旁的、备战界面上的）：直接弹游戏的全仙命选择框，选完就结束，
        /// 不弹这个小窗（0.10.0 选完会把小窗弹出来，用户不要）。原生框打不开才退回自绘的全仙命表，选完同样直接关掉。
        /// </summary>
        public void PickTalentDirect(int side, int slot, RectTransform anchor)
        {
            Close();
            _allowCharacter = false;
            _side = side == 1 ? 1 : 0;
            _slot = slot < 0 || slot >= ArenaSide.TalentSlots ? 0 : slot;
            _direct = true;
            if (anchor != null && OpenNativeBox(SelectInfoType.仙命, OnNativeTalent, anchor, false)) return;
            GoToList(PageTalents);
        }

        public void OpenFates(int side)
        {
            _direct = false;
            _side = side == 1 ? 1 : 0;
            GoToList(PageFates);
        }

        void GoToList(int page)
        {
            _page = page;
            _pageIndex = 0;
            _filter = "";
            _group = ListFilter.AnyGroup;
            Rebuild();
        }

        public void Close()
        {
            _waiting = null;
            if (_root != null) _ui.Destroy(_root);
            _root = null;
            _bounds.Clear();
        }

        ArenaSide Side { get { return _session.SideAt(_side); } }

        // ── 数据 ──────────────────────────────────────────────────────────

        static string TalentName(int id)
        {
            if (id <= 0) return Loc.T("（空）", "(empty)");
            try { return TranslateUtil.GetTalentTranslate(id); }
            catch (Exception) { return N(id); }
        }

        void EnsureTalents()
        {
            if (_talentIds != null) return;
            List<TalentConfig> configs = ConfigManager.talentConfigs;
            _talentIds = new int[configs.Count];
            _talentNames = new string[configs.Count];
            _talentGroups = new int[configs.Count];
            for (int i = 0; i < configs.Count; i++)
            {
                TalentConfig c = configs[i];
                _talentIds[i] = c.id;
                int level = (int)c.level;
                string tag = level > 0 && level < LevelTags.Length ? Loc.T("［" + LevelTags[level] + "］", " [" + LevelTagsEn[level] + "]") : "";
                _talentNames[i] = TalentName(c.id) + tag;
                _talentGroups[i] = c.charId != 0 ? GroupExclusive : (int)c.sect;
            }
        }

        void EnsureFates()
        {
            if (_fateIds != null) return;
            List<FateStrategyConfig> configs = ConfigManager.fateStrategyConfigs;
            _fateIds = new int[configs.Count];
            _fateNames = new string[configs.Count];
            for (int i = 0; i < configs.Count; i++)
            {
                _fateIds[i] = configs[i].id;
                string name;
                try { name = TranslateUtil.GetFateStrategyNameTranslate(configs[i].id, null); }
                catch (Exception) { name = ""; }
                _fateNames[i] = string.IsNullOrEmpty(name) ? N(configs[i].id) : name;
            }
        }

        static string GroupName(int group)
        {
            if (group == ListFilter.AnyGroup) return Loc.T("全部", "All");
            if (group == GroupGeneral) return Loc.T("通用", "General");
            if (group == GroupExclusive) return Loc.T("角色专属", "Exclusive");
            try { return TranslateUtil.GetSectTranslate(group); }
            catch (Exception) { return N(group); }
        }

        // ── 搭窗口 ────────────────────────────────────────────────────────

        Bound Bind(int kind, int value)
        {
            var b = new Bound();
            b.Owner = this;
            b.Kind = kind;
            b.Value = value;
            _bounds.Add(b);
            return b;
        }

        void Rebuild()
        {
            Close();
            var center = new Vector2(0.5f, 0.5f);
            _root = _ui.Panel("YxArenaSetup", center, center, Vector2.zero, new Vector2(Width, Height), new Color(0.05f, 0.04f, 0.03f, 0.95f));
            if (_root == null) return;
            if (_page == PageSlots) BuildSlotsPage();
            else BuildListPage();
        }

        void BuildSlotsPage()
        {
            Transform t = _root.transform;
            ArenaSide side = Side;
            float y = -Pad;
            _ui.Label(t, "title", Loc.T("仙命（" + side.Name + "）", "Talents (" + side.Name + ")"), new Vector2(Pad, y), new Vector2(420f, Row), 26f);
            _ui.TextButton(t, "done", Loc.T("完成", "Done"), new Vector2(Width - Pad - 110f, y), new Vector2(110f, 40f), Close);
            y -= Row + 4f;
            _ui.Label(t, "hint", Loc.T("点名字选仙命（所有仙命，不限角色）。「计数」是这个仙命攒的层数 / 次数在开打时已经有多少，不需要就留 0。",
                "Click a name to pick a talent (all talents, any character). \"Count\" is how many stacks / triggers it already has when the fight starts; leave 0 if not needed."),
                new Vector2(Pad, y), new Vector2(Width - Pad * 2f, Row), 17f);
            y -= Row;
            if (_allowCharacter)
            {
                _ui.Label(t, "charCaption", Loc.T("角色", "Character"), new Vector2(Pad, y - 6f), new Vector2(90f, Row), 22f);
                _ui.TextButton(t, "char", CharacterName(side.CharacterId), new Vector2(Pad + 96f, y), new Vector2(380f, 40f), Bind(KindPickCharacter, 0).Run);
                _ui.Label(t, "charHint", Loc.T("换角色会换成它自带的仙命，皮肤回到默认", "Switching character loads its innate talents; skin resets to default"), new Vector2(Pad + 490f, y - 8f), new Vector2(380f, Row), 16f);
                y -= Row + 4f;
            }
            for (int i = 0; i < ArenaSide.TalentSlots; i++)
            {
                _ui.Label(t, "slot", Loc.T(SlotNames[i], SlotNamesEn[i]), new Vector2(Pad, y - 6f), new Vector2(90f, Row), 22f);
                _ui.TextButton(t, "talent", TalentName(side.Talents[i]), new Vector2(Pad + 96f, y), new Vector2(380f, 40f), Bind(KindPickSlot, i).Run);
                _ui.TextButton(t, "clear", "×", new Vector2(Pad + 484f, y), new Vector2(44f, 40f), Bind(KindClearSlot, i).Run);
                if (side.Talents[i] != 0)
                {
                    _ui.Label(t, "valueCaption", Loc.T("计数", "Count"), new Vector2(Pad + 552f, y - 6f), new Vector2(54f, Row), 20f);
                    UiInput value = _ui.NumberInput(t, "value", new Vector2(Pad + 610f, y), new Vector2(130f, 40f), 6, Bind(KindSlotValue, i).RunText);
                    value.Show(N(side.TalentValues[i]));
                }
                y -= Row;
            }
            y -= 10f;
            _ui.Label(t, "fateCaption", Loc.T("天衍仙命", "Fates"), new Vector2(Pad, y - 6f), new Vector2(96f, Row), 22f);
            string fateCount = N(side.Fates.Count);
            _ui.TextButton(t, "fates", Loc.T("选择…（已选 " + fateCount + "）", "Choose… (" + fateCount + " picked)"), new Vector2(Pad + 96f, y), new Vector2(380f, 40f), OnOpenFates);
            EnsureFates();
            var chosen = new System.Text.StringBuilder();
            for (int i = 0; i < side.Fates.Count; i++)
            {
                if (i > 0) chosen.Append(Loc.T("、", ", "));
                chosen.Append(FateName(side.Fates[i]));
            }
            _ui.Label(t, "fateList", chosen.ToString(), new Vector2(Pad, y - Row), new Vector2(Width - Pad * 2f, 80f), 18f);
        }

        string FateName(int id)
        {
            for (int i = 0; i < _fateIds.Length; i++) if (_fateIds[i] == id) return _fateNames[i];
            return N(id);
        }

        void BuildListPage()
        {
            bool talents = _page == PageTalents;
            if (talents) EnsureTalents();
            else EnsureFates();
            int[] ids = talents ? _talentIds : _fateIds;
            string[] names = talents ? _talentNames : _fateNames;
            int[] groups = talents ? _talentGroups : null;
            Transform t = _root.transform;
            ArenaSide side = Side;
            float y = -Pad;
            string title = talents
                ? Loc.T("全仙命表 → " + SlotNames[_slot] + "（" + side.Name + "）", "All talents → " + SlotNamesEn[_slot] + " (" + side.Name + ")")
                : Loc.T("天衍仙命（" + side.Name + "）　已选 " + N(side.Fates.Count), "Fates (" + side.Name + ")  " + N(side.Fates.Count) + " picked");
            _ui.Label(t, "title", title, new Vector2(Pad, y), new Vector2(430f, Row), 24f);
            _ui.Label(t, "searchCaption", Loc.T("搜名字", "Search"), new Vector2(Pad + 440f, y - 4f), new Vector2(70f, Row), 20f);
            UiInput search = _ui.TextInput(t, "search", new Vector2(Pad + 514f, y), new Vector2(220f, 40f), 16, OnFilter);
            search.Show(_filter);
            _ui.TextButton(t, "back", Loc.T("返回", "Back"), new Vector2(Width - Pad - 110f, y), new Vector2(110f, 40f), OnBackToSlots);
            y -= Row + 4f;

            if (talents)
            {
                int[] tabs = { ListFilter.AnyGroup, GroupGeneral, 1, 2, 3, 4, GroupExclusive };
                float x = Pad;
                for (int i = 0; i < tabs.Length; i++)
                {
                    string text = tabs[i] == _group ? "● " + GroupName(tabs[i]) : GroupName(tabs[i]);
                    _ui.TextButton(t, "tab", text, new Vector2(x, y), new Vector2(118f, 38f), Bind(KindGroup, tabs[i]).Run);
                    x += 122f;
                }
                y -= Row;
            }

            int[] matches = ListFilter.Match(names, groups, talents ? _group : ListFilter.AnyGroup, _filter);
            int pageSize = Columns * (talents ? Rows - 1 : Rows);
            _pageIndex = ListFilter.ClampPage(matches.Length, pageSize, _pageIndex);
            int start = _pageIndex * pageSize;
            for (int n = 0; n < pageSize && start + n < matches.Length; n++)
            {
                int index = matches[start + n];
                int column = n % Columns;
                int row = n / Columns;
                bool on = talents ? side.Talents[_slot] == ids[index] : side.HasFate(ids[index]);
                string text = on ? "✔ " + names[index] : names[index];
                _ui.TextButton(t, "item", text, new Vector2(Pad + column * (CellWidth + 6f), y - row * Row),
                    new Vector2(CellWidth, 40f), Bind(talents ? KindChooseTalent : KindToggleFate, ids[index]).Run);
            }
            float bottom = -(Height - Pad - 44f);
            _ui.TextButton(t, "prev", Loc.T("◀ 上一页", "◀ Prev"), new Vector2(Pad, bottom), new Vector2(130f, 44f), OnPrev);
            string pageNums = N(_pageIndex + 1) + " / " + N(ListFilter.PageCount(matches.Length, pageSize));
            string total = N(matches.Length);
            string pageText = Loc.T(pageNums + "　共 " + total + " 个", pageNums + "  " + total + " total");
            _ui.Label(t, "page", pageText, new Vector2(Pad + 146f, bottom - 8f), new Vector2(260f, Row), 20f);
            _ui.TextButton(t, "next", Loc.T("下一页 ▶", "Next ▶"), new Vector2(Pad + 410f, bottom), new Vector2(130f, 44f), OnNext);
        }

        // ── 游戏自己的选择框（SelectInfoPanel）───────────────────────────────────
        // 原生的、带图标的「全仙命」/「全角色」选择框：页签 全部 / 角色专属 / 通用 / 各门派，每格有图标、名字、境界标签，
        // 点框外自动关，回调给一个 id（选「空」给 -999999）。游戏自己已经不用它了（门派秘传编辑器换成了只列本角色的那一套），
        // 但预制体还随游戏发布（资源目录里有这个键）。打不开就退回自绘的全仙命表。

        const int NativeEmpty = -999999;

        static SelectInfoPanel FindNativeBox()
        {
            if (SceneLoader.currentSceneName == "Battle")
            {
                BattlePanel battle = ILRPanelBase.FindILRPanel<BattlePanel>();
                if (battle == null || battle.readyLayer == null) return null;
                return battle.FindILRSubPanelRuntime<SelectInfoPanel>(battle.readyLayer.subPanelContainer);
            }
            LobbyPanel lobby = ILRPanelBase.FindILRPanel<LobbyPanel>();
            return lobby != null ? lobby.FindILRSubPanelRuntime<SelectInfoPanel>() : null;
        }

        /// <summary>从小窗里打开原生选择框：开着的时候把小窗藏起来（不然可能挡住它），它关了小窗再回来。</summary>
        bool OpenNativeBox(SelectInfoType type, Action<int> onPicked)
        {
            if (_root == null) return false;
            return OpenNativeBox(type, onPicked, _root.transform as RectTransform, true);
        }

        /// <param name="returnToWindow">true = 从小窗打开的，关了回小窗；false = 点图标直接打开的，关了就结束。</param>
        bool OpenNativeBox(SelectInfoType type, Action<int> onPicked, RectTransform anchor, bool returnToWindow)
        {
            if (_nativeBroken || anchor == null) return false;
            try
            {
                SelectInfoPanel box = FindNativeBox();
                if (box == null) { _nativeBroken = true; return false; }
                box.ShowBox(anchor, onPicked, type);
                if (_search != null) _search.Attach(box, type == SelectInfoType.仙命);
                _waiting = box;
                _waitingFrames = 0;
                _returnToWindow = returnToWindow;
                if (_root != null) _root.SetActive(false);       // 只藏不销毁：选择框拿它当定位锚点
                return true;
            }
            catch (Exception e)
            {
                _nativeBroken = true;
                _ctx.Log.Warn(Loc.T("游戏自己的选择框打不开，改用自绘的列表：", "Game's own picker won't open; falling back to the custom list: ") + e.Message);
                return false;
            }
        }

        /// <summary>每帧调：原生选择框关了（选了或点了框外）就把自己的窗重建出来。</summary>
        public void Tick()
        {
            if (_waiting == null) return;
            _waitingFrames++;
            bool open = _waiting.panel != null && _waiting.panel.isShow;
            if (open || _waitingFrames < 2) return;
            _waiting = null;
            if (_search != null) _search.Detach();
            if (_hideTips != null) _hideTips();
            if (_returnToWindow && _root != null) Rebuild();
        }

        void OnNativeTalent(int id)
        {
            try
            {
                if (id == NativeEmpty || id <= 0) Side.SetTalent(_slot, 0);
                else if (ConfigManager.GetTalentConfig(id) != null) ChooseTalent(id);
                else return;
                Notify();
            }
            catch (Exception e) { _ctx.Log.Error(Loc.T("仙命小窗：选仙命出错", "Talent window: pick talent failed"), e); }
        }

        void OnNativeCharacter(int id)
        {
            try
            {
                if (id <= 0 || ConfigManager.GetCharacterConfig(id) == null) return;
                if (id == Side.CharacterId) return;
                // 皮肤回到 0 号（每个角色都有；别的皮肤只有大厅的选英雄界面能保证是拥有的）。
                Side.SetCharacter(id, 0, 0, ArenaRoom.InnateTalents(id));
                Notify();
            }
            catch (Exception e) { _ctx.Log.Error(Loc.T("仙命小窗：换角色出错", "Talent window: switch character failed"), e); }
        }

        void PickSlot(int slot)
        {
            _slot = slot;
            if (OpenNativeBox(SelectInfoType.仙命, OnNativeTalent)) return;
            GoToList(PageTalents);
        }

        static string CharacterName(int id)
        {
            try { return TranslateUtil.GetCharacterNameTranslate(id); }
            catch (Exception) { return N(id); }
        }

        // ── 回调 ──────────────────────────────────────────────────────────

        void Notify()
        {
            _session.Persist();
            if (_changed != null) _changed();
        }

        void OnBound(int kind, int value, string text)
        {
            try
            {
                switch (kind)
                {
                    case KindPickSlot:
                        PickSlot(value);
                        return;
                    case KindPickCharacter:
                        if (!OpenNativeBox(SelectInfoType.角色, OnNativeCharacter)) Ui.Toast(Loc.T("游戏的角色选择框打不开；换角色请回大厅的选英雄界面", "Game's character picker won't open; switch characters from the lobby's hero-select screen"));
                        return;
                    case KindClearSlot:
                        Side.SetTalent(value, 0);
                        Notify();
                        break;
                    case KindSlotValue:
                        if (text == null || text.Trim() == N(Side.TalentValues[value])) return;
                        if (!Side.SetTalentValue(value, text)) Ui.Toast(Loc.T("计数要填 0–999999 的整数", "Count must be an integer 0–999999"));
                        Notify();
                        break;
                    case KindChooseTalent:
                        ChooseTalent(value);
                        _page = PageSlots;
                        Notify();
                        if (_direct) { Close(); return; }
                        break;
                    case KindToggleFate:
                        Side.ToggleFate(value);
                        Notify();
                        break;
                    case KindGroup:
                        _group = value;
                        _pageIndex = 0;
                        break;
                }
                Rebuild();
            }
            catch (Exception e)
            {
                _ctx.Log.Error(Loc.T("仙命小窗：操作出错", "Talent window: operation failed"), e);
                Ui.Toast(Loc.T("操作失败，详见日志", "Operation failed; see log"));
            }
        }

        /// <summary>同一个仙命可以放进好几个槽（0.10.x 会自动和已有的那个槽对调，用户要的是能重复选）。</summary>
        void ChooseTalent(int id)
        {
            Side.SetTalent(_slot, id);
        }

        void OnOpenFates() { GoToList(PageFates); }

        void OnBackToSlots()
        {
            if (_direct) { Close(); return; }
            _page = PageSlots;
            Rebuild();
        }

        void OnPrev() { _pageIndex--; Rebuild(); }

        void OnNext() { _pageIndex++; Rebuild(); }

        void OnFilter(string text)
        {
            string wanted = text == null ? "" : text.Trim();
            if (wanted == _filter) return;
            _filter = wanted;
            _pageIndex = 0;
            Rebuild();
        }
    }
}
