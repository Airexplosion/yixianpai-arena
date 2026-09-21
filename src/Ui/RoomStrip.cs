using System;
using Yx.ModSdk.Unity;
using UnityEngine;
using UnityEngine.Events;

namespace YxArena.Views
{
    /// <summary>
    /// 选英雄界面（游戏自己的单人房间）左上角的一小条：界面本身没有位置放的几样——
    /// 返回、切换我方 / 对手、天衍仙命、仙命计数。选角色、换皮肤、换仙命、开始都在游戏自己的界面上点。
    /// </summary>
    public sealed class RoomStrip
    {
        const float RowHeight = 40f;
        const float Gap = 6f;
        const float Pad = 6f;

        readonly Action _onBack;
        readonly Action _onSwitchSide;
        readonly Action _onFates;
        readonly Action _onTalents;
        GameObject _root;
        UiButton _side;
        UiButton _fates;

        readonly UiKit _ui;

        public RoomStrip(UiKit ui, Action onBack, Action onSwitchSide, Action onTalents, Action onFates)
        {
            _ui = ui;
            _onBack = onBack;
            _onSwitchSide = onSwitchSide;
            _onFates = onFates;
            _onTalents = onTalents;
        }

        public void SetVisible(bool visible)
        {
            if (visible && _root == null) Build();
            if (_root != null && _root.activeSelf != visible) _root.SetActive(visible);
        }

        void Build()
        {
            var corner = new Vector2(0f, 1f);
            float[] widths = { 90f, 230f, 150f, 190f };
            float width = Pad * 2f + Gap * 3f;
            for (int i = 0; i < widths.Length; i++) width += widths[i];
            _root = _ui.Panel("YxArenaRoomStrip", corner, corner, new Vector2(16f, -16f), new Vector2(width, RowHeight * 2f + Pad * 2f + Gap), new Color(0f, 0f, 0f, 0.6f));
            if (_root == null) return;
            Transform t = _root.transform;
            _ui.Label(t, "title", "练习场　点角色旁的仙命图标换仙命（全仙命表，不限角色）；右下「开始」进场", new Vector2(Pad, -Pad - 6f), new Vector2(width - Pad * 2f, RowHeight), 18f);
            float x = Pad;
            float y = -(Pad + RowHeight + Gap);
            _ui.TextButton(t, "back", "返回", new Vector2(x, y), new Vector2(widths[0], RowHeight), _onBack);
            x += widths[0] + Gap;
            _side = _ui.TextButton(t, "side", "", new Vector2(x, y), new Vector2(widths[1], RowHeight), _onSwitchSide);
            x += widths[1] + Gap;
            _ui.TextButton(t, "talents", "仙命 / 计数", new Vector2(x, y), new Vector2(widths[2], RowHeight), _onTalents);
            x += widths[2] + Gap;
            _fates = _ui.TextButton(t, "fates", "", new Vector2(x, y), new Vector2(widths[3], RowHeight), _onFates);
        }

        public void Refresh(bool opponent, int fateCount)
        {
            if (_root == null) return;
            _side.SetText(opponent ? "正在设置：对手 ⇄" : "正在设置：我方 ⇄");
            _fates.SetText("天衍仙命（" + fateCount.ToString(System.Globalization.CultureInfo.InvariantCulture) + "）");
        }

        public void Destroy()
        {
            if (_root != null) _ui.Destroy(_root);
            _root = null;
        }
    }
}
