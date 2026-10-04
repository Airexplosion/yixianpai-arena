using System;
using Yx.ModSdk.Unity;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace YxArena.Views
{
    /// <summary>
    /// 控制栏各按钮 / 输入框的回调。用「实例方法 → Action」的字段而不是接口：热更里从接口方法取委托（ldvirtftn）
    /// 没有实机验证过，实例方法委托已经实机用过。交给 Unity 的那一层（UnityAction + 异常兜底）由 SDK 的 UiKit 负责。
    /// </summary>
    public sealed class ControlBarHandlers
    {
        public Action ToggleCollapsed;
        public Action OpenGallery;
        public Action OpenQuick;
        public Action OpenSettings;
        public Action OpenSword;
        public Action OpenLearned;
        public Action Leave;
        public Action OpenSpecial;
        public Action OpenTalents;
        public Action NextSpecial;
        public Action ToggleDealMode;
        public Action NextRarity;
        public Action NextLevel;
        public Action ToggleFirst;
        public Action ClearHand;
        public Action SwitchSide;
        public Action Fight;
        public Action TogglePauseAtStart;
        public Action ToggleOverflow;
        public Action NextLimitMode;
        public Action<string> SetHp;
        public Action<string> SetTiPo;
        public Action<string> SetTiPoMax;
        public Action<string> Search;
    }

    /// <summary>控制栏要显示的当前值。</summary>
    public sealed class ControlBarState
    {
        public bool DealMode;
        public int Rarity;
        public int Level;
        public bool PlayerFirst;
        public bool EditingOpponent;
        public bool PauseAtStart;
        public bool Overflow;
        public bool SwordOwner;
        public int LimitMode;
        public string SideName = "";
        public long TotalHp;
        public int TiPo;
        public int TiPoMax;
        public string SpecialName = "";
        public string Status = "";
    }

    /// <summary>
    /// 屏幕上边缘居中的悬浮控制栏：一行按钮，一行数值输入 + 特殊牌 + 搜索，最下面一行状态文字。可以收起成一个小标签
    /// （0.1.x 的左侧竖栏挡住了第一个格子，牌放不进去）。只在练习场的备战界面显示。
    /// </summary>
    public sealed class ControlBar
    {
        const float ButtonWidth = 118f;
        const float RowHeight = 40f;
        const float Gap = 5f;
        const float Pad = 6f;
        const float StatusHeight = 22f;
        const float HpWidth = 210f;      // 18 位数字

        readonly ControlBarHandlers _actions;
        GameObject _root;
        GameObject _tab;
        UiButton _dealMode;
        UiButton _rarity;
        UiButton _level;
        UiButton _first;
        UiButton _side;
        UiButton _special;
        UiButton _pauseAtStart;
        UiButton _overflow;
        UiButton _limits;
        UiButton _sword;
        UiInput _hp;
        UiInput _tiPo;
        UiInput _tiPoMax;
        TextMeshProUGUI _status;
        bool _collapsed;
        bool _visible;
        ControlLayout _layout;
        readonly ControlVisibility _visibility;

        readonly UiKit _ui;

        public ControlBar(UiKit ui, ControlBarHandlers actions, ControlVisibility visibility)
        {
            _ui = ui;
            _actions = actions;
            _visibility = visibility;
        }

        /// <summary>换边由游戏自己的「切换」按钮负责：控制栏就不画「编辑：我方 / 对手」了。改了要 Destroy 后重建才生效。</summary>
        public bool NativeSide;

        public bool IsAlive { get { return _root != null && _tab != null; } }

        public bool Collapsed { get { return _collapsed; } }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (visible && !IsAlive) Build();
            Apply();
        }

        public void ToggleCollapsed()
        {
            _collapsed = !_collapsed;
            Apply();
        }

        void Apply()
        {
            if (!IsAlive) return;
            bool bar = _visible && !_collapsed;
            bool tab = _visible && _collapsed;
            if (_root.activeSelf != bar) _root.SetActive(bar);
            if (_tab.activeSelf != tab) _tab.SetActive(tab);
        }

        public void Destroy()
        {
            if (_root != null) _ui.Destroy(_root);
            if (_tab != null) _ui.Destroy(_tab);
            _root = null;
            _tab = null;
            _dealMode = _rarity = _level = _first = _side = _special = _pauseAtStart = _overflow = _limits = null;
            _sword = null;
            _hp = _tiPo = _tiPoMax = null;
            _status = null;
        }

        void Build()
        {
            Destroy();
            var top = new Vector2(0.5f, 1f);
            var background = new Color(0f, 0f, 0f, 0.6f);
            float width = 1450f;
            _root = _ui.Panel("YxArenaControlBar", top, top, new Vector2(0f, -4f), new Vector2(width, RowHeight + Pad * 2f), background);
            _tab = _ui.Panel("YxArenaControlTab", top, top, new Vector2(0f, -4f), new Vector2(ButtonWidth + Pad * 2f, RowHeight + Pad * 2f), background);
            if (_root == null || _tab == null) { Destroy(); return; }
            _ui.TextButton(_tab.transform, "expand", Loc.T("练习场 ▼", "Arena ▼"), new Vector2(Pad, -Pad), new Vector2(ButtonWidth, RowHeight), _actions.ToggleCollapsed);

            RectTransform parent = _root.transform.parent as RectTransform;
            if (parent != null && parent.rect.width > 360f) width = Mathf.Min(width, parent.rect.width - 16f);
            _layout = new ControlLayout(width);
            Button(Loc.T("收起 ▲", "Collapse ▲"), _actions.ToggleCollapsed);
            Button(Loc.T("设置", "Settings"), _actions.OpenSettings);
            Control(ControlVisibility.Gallery, Loc.T("发牌", "Deal"), _actions.OpenGallery);
            Control(ControlVisibility.Quick, Loc.T("快捷发牌", "Quick deal"), _actions.OpenQuick);
            _sword = Control(ControlVisibility.Sword, Loc.T("澄心剑…", "Chengxin…"), _actions.OpenSword);
            _dealMode = Control(ControlVisibility.DealMode, "", _actions.ToggleDealMode);
            _rarity = Control(ControlVisibility.Rarity, "", _actions.NextRarity);
            _level = Control(ControlVisibility.Level, "", _actions.NextLevel);
            _first = Control(ControlVisibility.First, "", _actions.ToggleFirst);
            Control(ControlVisibility.ClearHand, Loc.T("清空手牌", "Clear hand"), _actions.ClearHand);
            _side = NativeSide ? null : Control(ControlVisibility.Side, "", _actions.SwitchSide);
            _pauseAtStart = Control(ControlVisibility.Pause, "", _actions.TogglePauseAtStart);
            _overflow = Control(ControlVisibility.Overflow, "", _actions.ToggleOverflow);
            _limits = Control(ControlVisibility.Limits, "", _actions.NextLimitMode);
            Control(ControlVisibility.Fight, Loc.T("开打", "Fight"), _actions.Fight);
            Control(ControlVisibility.Leave, Loc.T("回大厅", "Back to lobby"), _actions.Leave);
            _hp = Number(ControlVisibility.Hp, "hp", Loc.T("血量", "HP"), 50f, HpWidth, 18, _actions.SetHp);
            _tiPo = Number(ControlVisibility.Body, "tipo", Loc.T("体魄", "Body"), 50f, 90f, 6, _actions.SetTiPo);
            _tiPoMax = Number(ControlVisibility.BodyMax, "tipoMax", Loc.T("体魄上限", "Body max"), 92f, 90f, 6, _actions.SetTiPoMax);
            Control(ControlVisibility.Special, Loc.T("特殊牌", "Special"), _actions.OpenSpecial);
            _special = Control(ControlVisibility.Category, "", _actions.NextSpecial);
            Control(ControlVisibility.Talents, Loc.T("仙命…", "Talents…"), _actions.OpenTalents);
            Control(ControlVisibility.Learned, Loc.T("悟剑…", "Learned…"), _actions.OpenLearned);
            if (_visibility.Shows(ControlVisibility.Search))
            {
                ControlSlot slot = _layout.Add(272f);
                float x = Caption(Loc.T("搜牌名", "Search"), slot.X, slot.Y, 72f);
                _ui.TextInput(_root.transform, "search", new Vector2(x, slot.Y), new Vector2(200f, RowHeight), 16, _actions.Search);
            }
            float height = -_layout.Bottom;
            if (_visibility.Shows(ControlVisibility.Status))
            {
                _status = _ui.Label(_root.transform, "status", "", new Vector2(Pad, _layout.Bottom - Gap), new Vector2(_layout.Width - Pad * 2f, StatusHeight), 16f);
                height += Gap + StatusHeight;
            }
            RectTransform rect = _root.transform as RectTransform;
            if (rect != null) rect.sizeDelta = new Vector2(_layout.Width, height);
            Apply();
        }

        UiButton Button(string text, Action onClick)
        {
            ControlSlot slot = _layout.Add(ButtonWidth);
            return _ui.TextButton(_root.transform, "button", text, new Vector2(slot.X, slot.Y), new Vector2(ButtonWidth, RowHeight), onClick);
        }

        UiButton Control(int key, string text, Action onClick)
        { return _visibility.Shows(key) ? Button(text, onClick) : null; }

        UiInput Number(int key, string name, string caption, float captionWidth, float inputWidth, int digits, Action<string> action)
        {
            if (!_visibility.Shows(key)) return null;
            ControlSlot slot = _layout.Add(captionWidth + inputWidth + Gap * 2f);
            float x = Caption(caption, slot.X, slot.Y, captionWidth);
            return _ui.NumberInput(_root.transform, name, new Vector2(x, slot.Y), new Vector2(inputWidth, RowHeight), digits, action);
        }

        float Caption(string text, float x, float y, float width)
        {
            _ui.Label(_root.transform, "caption", text, new Vector2(x, y - 6f), new Vector2(width, RowHeight), 20f);
            return x + width;
        }

        public void Refresh(ControlBarState state)
        {
            if (!IsAlive) return;
            if (_sword != null) _sword.SetInteractable(state.SwordOwner);
            if (_dealMode != null) _dealMode.SetText(state.DealMode ? Loc.T("点牌：发牌", "Click: deal") : Loc.T("点牌：详情", "Click: detail"));
            if (_rarity != null) _rarity.SetText(Loc.T("牌级：", "Rarity: ") + ArenaConfig.RarityName(state.Rarity));
            if (_level != null) _level.SetText(Loc.T("境界：", "Realm: ") + ArenaConfig.LevelName(state.Level));
            if (_first != null) _first.SetText(state.PlayerFirst ? Loc.T("先手：我", "First: me") : Loc.T("先手：对手", "First: foe"));
            if (_side != null) _side.SetText(state.EditingOpponent ? Loc.T("编辑：对手 ⇄", "Edit: foe ⇄") : Loc.T("编辑：我方 ⇄", "Edit: mine ⇄"));
            if (_special != null) _special.SetText(Loc.T("类别：", "Kind: ") + state.SpecialName);
            if (_pauseAtStart != null) _pauseAtStart.SetText(state.PauseAtStart ? Loc.T("开场暂停：开", "Pause at start: on") : Loc.T("开场暂停：关", "Pause at start: off"));
            if (_overflow != null) _overflow.SetText(state.Overflow ? Loc.T("破限：开", "Overflow: on") : Loc.T("破限：关", "Overflow: off"));
            if (_limits != null) _limits.SetText(LimitModes.Name(state.LimitMode));
            if (_hp != null) _hp.Show(state.TotalHp.ToString(CultureInfo.InvariantCulture));
            if (_tiPo != null) _tiPo.Show(state.TiPo.ToString(CultureInfo.InvariantCulture));
            if (_tiPoMax != null) _tiPoMax.Show(state.TiPoMax.ToString(CultureInfo.InvariantCulture));
            if (_status != null) _status.text = state.Status;
        }
    }
}
