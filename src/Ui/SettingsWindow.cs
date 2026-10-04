using System;
using UnityEngine;
using Yx.ModSdk.Unity;

namespace YxArena.Views
{
    public sealed class SettingsWindow
    {
        sealed class Choice
        {
            readonly SettingsWindow _owner;
            readonly int _index;
            public UiButton Button;
            public Choice(SettingsWindow owner, int index) { _owner = owner; _index = index; }
            public void Click() { _owner._toggle(_index); _owner.Refresh(); }
        }
        readonly UiKit _ui;
        readonly ControlVisibility _visibility;
        readonly Action<int> _toggle;
        readonly Action _reset;
        readonly Choice[] _choices = new Choice[ControlVisibility.Count];
        GameObject _root;
        public SettingsWindow(UiKit ui, ControlVisibility visibility, Action<int> toggle, Action reset)
        { _ui = ui; _visibility = visibility; _toggle = toggle; _reset = reset; }

        public void Open()
        {
            Close();
            var center = new Vector2(0.5f, 0.5f);
            float height = 182f + ((ControlVisibility.Count + 2) / 3) * 42f;
            _root = _ui.Panel("YxArenaSettings", center, center, Vector2.zero, new Vector2(1000f, height), new Color(0.06f, 0.08f, 0.12f, 0.97f));
            if (_root == null) return;
            _ui.Label(_root.transform, "title", Loc.T("练习场设置 · 显示哪些控件", "Arena settings · Visible controls"), new Vector2(20f, -16f), new Vector2(600f, 40f), 25f);
            _ui.TextButton(_root.transform, "close", Loc.T("关闭", "Close"), new Vector2(872f, -14f), new Vector2(108f, 40f), Close);
            _ui.Label(_root.transform, "hint", Loc.T("勾选即显示，立即生效并保存。设置与展开入口始终保留。", "Checked controls are visible. Saved immediately. Settings and Expand stay accessible."), new Vector2(20f, -66f), new Vector2(780f, 52f), 19f);
            for (int i = 0; i < ControlVisibility.Count; i++)
            {
                var choice = new Choice(this, i);
                _choices[i] = choice;
                float x = 20f + (i % 3) * 324f;
                float y = -120f - (i / 3) * 42f;
                choice.Button = _ui.TextButton(_root.transform, ControlVisibility.Key(i), "", new Vector2(x, y), new Vector2(312f, 36f), choice.Click);
            }
            _ui.TextButton(_root.transform, "reset", Loc.T("恢复全部显示", "Show all controls"), new Vector2(20f, -height + 46f), new Vector2(250f, 36f), Reset);
            Refresh();
        }

        void Reset() { _reset(); Refresh(); }
        public void Refresh()
        {
            if (_root == null) return;
            _root.transform.SetAsLastSibling();
            for (int i = 0; i < _choices.Length; i++)
                if (_choices[i] != null && _choices[i].Button != null)
                    _choices[i].Button.SetText((_visibility.Shows(i) ? "[✓]  " : "[  ]  ") + ControlVisibility.Name(i));
        }
        public void Close() { if (_root != null) _ui.Destroy(_root); _root = null; }
    }
}
