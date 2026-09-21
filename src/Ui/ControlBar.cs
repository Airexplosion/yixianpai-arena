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
        const int ButtonCount = 12;
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
        UiInput _hp;
        UiInput _tiPo;
        UiInput _tiPoMax;
        TextMeshProUGUI _status;
        bool _collapsed;
        bool _visible;
        float _x;

        readonly UiKit _ui;

        public ControlBar(UiKit ui, ControlBarHandlers actions)
        {
            _ui = ui;
            _actions = actions;
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
        }

        void Build()
        {
            Destroy();
            var top = new Vector2(0.5f, 1f);
            var background = new Color(0f, 0f, 0f, 0.6f);
            int buttons = NativeSide ? ButtonCount - 1 : ButtonCount;
            float width = Pad * 2f + buttons * ButtonWidth + (buttons - 1) * Gap;
            float height = Pad * 2f + RowHeight * 2f + Gap * 2f + StatusHeight;
            _root = _ui.Panel("YxArenaControlBar", top, top, new Vector2(0f, -4f), new Vector2(width, height), background);
            _tab = _ui.Panel("YxArenaControlTab", top, top, new Vector2(0f, -4f), new Vector2(ButtonWidth + Pad * 2f, RowHeight + Pad * 2f), background);
            if (_root == null || _tab == null) { Destroy(); return; }
            _ui.TextButton(_tab.transform, "expand", "练习场 ▼", new Vector2(Pad, -Pad), new Vector2(ButtonWidth, RowHeight), _actions.ToggleCollapsed);

            _x = Pad;
            Button("收起 ▲", _actions.ToggleCollapsed);
            Button("发牌", _actions.OpenGallery);
            _dealMode = Button("", _actions.ToggleDealMode);
            _rarity = Button("", _actions.NextRarity);
            _level = Button("", _actions.NextLevel);
            _first = Button("", _actions.ToggleFirst);
            Button("清空手牌", _actions.ClearHand);
            _side = NativeSide ? null : Button("", _actions.SwitchSide);
            _pauseAtStart = Button("", _actions.TogglePauseAtStart);
            _overflow = Button("", _actions.ToggleOverflow);
            _limits = Button("", _actions.NextLimitMode);
            Button("开打", _actions.Fight);

            float y = -(Pad + RowHeight + Gap);
            float x = Pad;
            x = Caption("血量", x, y, 50f);
            _hp = _ui.NumberInput(_root.transform, "hp", new Vector2(x, y), new Vector2(HpWidth, RowHeight), 18, _actions.SetHp);
            x += HpWidth + Gap * 3f;
            x = Caption("体魄", x, y, 50f);
            _tiPo = _ui.NumberInput(_root.transform, "tipo", new Vector2(x, y), new Vector2(90f, RowHeight), 6, _actions.SetTiPo);
            x += 90f + Gap * 3f;
            x = Caption("体魄上限", x, y, 92f);
            _tiPoMax = _ui.NumberInput(_root.transform, "tipoMax", new Vector2(x, y), new Vector2(90f, RowHeight), 6, _actions.SetTiPoMax);
            x += 90f + Gap * 3f;
            _ui.TextButton(_root.transform, "special", "特殊牌", new Vector2(x, y), new Vector2(ButtonWidth, RowHeight), _actions.OpenSpecial);
            x += ButtonWidth + Gap;
            _special = _ui.TextButton(_root.transform, "specialKind", "", new Vector2(x, y), new Vector2(ButtonWidth + 20f, RowHeight), _actions.NextSpecial);
            x += ButtonWidth + 20f + Gap * 3f;
            _ui.TextButton(_root.transform, "talents", "仙命…", new Vector2(x, y), new Vector2(96f, RowHeight), _actions.OpenTalents);
            x += 96f + Gap * 3f;
            x = Caption("搜牌名", x, y, 72f);
            _ui.TextInput(_root.transform, "search", new Vector2(x, y), new Vector2(width - x - Pad, RowHeight), 16, _actions.Search);
            _status = _ui.Label(_root.transform, "status", "", new Vector2(Pad, y - RowHeight - Gap), new Vector2(width - Pad * 2f, StatusHeight), 16f);
            Apply();
        }

        UiButton Button(string text, Action onClick)
        {
            UiButton button = _ui.TextButton(_root.transform, "button", text, new Vector2(_x, -Pad), new Vector2(ButtonWidth, RowHeight), onClick);
            _x += ButtonWidth + Gap;
            return button;
        }

        float Caption(string text, float x, float y, float width)
        {
            _ui.Label(_root.transform, "caption", text, new Vector2(x, y - 6f), new Vector2(width, RowHeight), 20f);
            return x + width;
        }

        public void Refresh(ControlBarState state)
        {
            if (!IsAlive) return;
            _dealMode.SetText(state.DealMode ? "点牌：发牌" : "点牌：详情");
            _rarity.SetText("牌级：" + ArenaConfig.RarityName(state.Rarity));
            _level.SetText("境界：" + ArenaConfig.LevelName(state.Level));
            _first.SetText(state.PlayerFirst ? "先手：我" : "先手：对手");
            if (_side != null) _side.SetText(state.EditingOpponent ? "编辑：对手 ⇄" : "编辑：我方 ⇄");
            _special.SetText("类别：" + state.SpecialName);
            _pauseAtStart.SetText(state.PauseAtStart ? "开场暂停：开" : "开场暂停：关");
            _overflow.SetText(state.Overflow ? "破限：开" : "破限：关");
            _limits.SetText(LimitModes.Name(state.LimitMode));
            _hp.Show(state.TotalHp.ToString(CultureInfo.InvariantCulture));
            _tiPo.Show(state.TiPo.ToString(CultureInfo.InvariantCulture));
            _tiPoMax.Show(state.TiPoMax.ToString(CultureInfo.InvariantCulture));
            if (_status != null) _status.text = state.Status;
        }
    }
}
