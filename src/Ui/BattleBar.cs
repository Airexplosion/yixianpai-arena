using System;
using Yx.ModSdk.Unity;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace YxArena.Views
{
    /// <summary>
    /// 战斗中的小控制条（屏幕上边缘居中：步进、开场暂停开关、伤害统计开关）和伤害统计窗（屏幕右侧、带底板的悬浮窗，
    /// 可以用按钮开关；底板不挡点击，底下的游戏界面照常能点）。只在练习场的斗法阶段显示。
    /// 0.11.1 及以前统计是左侧的描边文字，没有底、在亮的场景上看不清（用户 2026-09-19 要求改到右侧、带底、可开关）。
    /// </summary>
    public sealed class BattleBar
    {
        const float ButtonWidth = 150f;
        const float RowHeight = 40f;
        const float Gap = 6f;
        const float Pad = 6f;
        const float WindowWidth = 470f;
        const float WindowHeight = 600f;
        const float WindowPad = 12f;

        readonly Action _onStep;
        readonly Action _onTogglePauseAtStart;
        readonly Action _onToggleTally;
        GameObject _root;
        GameObject _window;
        UiButton _step;
        UiButton _pauseAtStart;
        UiButton _tallyToggle;
        TextMeshProUGUI _tally;
        bool _visible;
        bool _tallyOpen;

        readonly UiKit _ui;

        public BattleBar(UiKit ui, Action onStep, Action onTogglePauseAtStart, Action onToggleTally)
        {
            _ui = ui;
            _onStep = onStep;
            _onTogglePauseAtStart = onTogglePauseAtStart;
            _onToggleTally = onToggleTally;
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            if (visible && _root == null) Build();
            if (_root != null && _root.activeSelf != visible) _root.SetActive(visible);
            ApplyWindow();
        }

        void ApplyWindow()
        {
            if (_window == null) return;
            bool show = _visible && _tallyOpen;
            if (_window.activeSelf != show) _window.SetActive(show);
        }

        void Build()
        {
            var top = new Vector2(0.5f, 1f);
            var background = new Color(0f, 0f, 0f, 0.6f);
            float width = Pad * 2f + ButtonWidth * 3f + Gap * 2f;
            _root = _ui.Panel("YxArenaBattleBar", top, top, new Vector2(0f, -4f), new Vector2(width, RowHeight + Pad * 2f), background);
            if (_root == null) return;
            float x = Pad;
            _step = _ui.TextButton(_root.transform, "step", Loc.T("步进 ▶|", "Step ▶|"), new Vector2(x, -Pad), new Vector2(ButtonWidth, RowHeight), _onStep);
            x += ButtonWidth + Gap;
            _pauseAtStart = _ui.TextButton(_root.transform, "pauseAtStart", "", new Vector2(x, -Pad), new Vector2(ButtonWidth, RowHeight), _onTogglePauseAtStart);
            x += ButtonWidth + Gap;
            _tallyToggle = _ui.TextButton(_root.transform, "tally", "", new Vector2(x, -Pad), new Vector2(ButtonWidth, RowHeight), _onToggleTally);

            var right = new Vector2(1f, 0.5f);
            _window = _ui.Panel("YxArenaTallyWindow", right, right, new Vector2(-12f, 0f), new Vector2(WindowWidth, WindowHeight), new Color(0f, 0f, 0f, 0.72f));
            if (_window == null) return;
            var image = _window.GetComponent(typeof(Image)) as Image;
            if (image != null) image.raycastTarget = false;
            _tally = _ui.Label(_window.transform, "text", "", new Vector2(WindowPad, -WindowPad),
                new Vector2(WindowWidth - WindowPad * 2f, WindowHeight - WindowPad * 2f), 21f);
            if (_tally != null) _tally.raycastTarget = false;
            _window.SetActive(false);
        }

        public void Refresh(bool stepAvailable, bool pauseAtStart, bool paused, bool tallyOpen, string tallyText)
        {
            if (_root == null) return;
            _step.SetText(paused ? Loc.T("步进 ▶|  ‖", "Step ▶|  ‖") : Loc.T("步进 ▶|", "Step ▶|"));
            _step.SetInteractable(stepAvailable);
            _pauseAtStart.SetText(pauseAtStart ? Loc.T("开场暂停：开", "Pause at start: on") : Loc.T("开场暂停：关", "Pause at start: off"));
            _tallyOpen = tallyOpen && !string.IsNullOrEmpty(tallyText);
            if (_tallyToggle != null) _tallyToggle.SetText(tallyOpen ? Loc.T("伤害统计 ▶", "Damage ▶") : Loc.T("◀ 伤害统计", "◀ Damage"));
            ApplyWindow();
            if (_tally != null && _tallyOpen) _tally.text = tallyText;
        }

        public void Destroy()
        {
            if (_root != null) _ui.Destroy(_root);
            if (_window != null) _ui.Destroy(_window);
            _root = null;
            _window = null;
            _tally = null;
        }
    }
}
