using System;
using TMPro;
using UnityEngine;
using Yx.ModSdk;
using Yx.Shared;

namespace YxArena.Game
{
    /// <summary>
    /// 在大厅「模式选择 → 单人模式」页里加一个「练习场」入口。
    ///
    /// 那一页（GameEntrancePanel 里的 SinglePlayerModePart）的入口是预制体里固定的几个物体，每个挂着一个
    /// ILRComponentBridge（scriptName = "SinglePlayerModeItem"），点击走 SinglePlayerModeItem.OnClick(PointerEventData)，
    /// 按序列化变量 PanelName 分发。没有「面板显示了」的事件，所以轮询：页面显示着且还没有我们的入口，就克隆第一个
    /// 入口排到最后、改标题。克隆体带着原入口的 PanelName，所以在 OnClick 上挂前置钩子：点的是克隆体 → 跳过原逻辑、进练习场。
    /// </summary>
    public sealed class LobbyEntry
    {
        const string PanelName = "GameEntrancePanel";
        const string PartName = "SinglePlayerModePart";
        const string ItemScript = "SinglePlayerModeItem";
        const string CloneName = "YxArenaEntry";
        static string Title { get { return Loc.T("练习场", "Arena"); } }
        static string Hint { get { return Loc.T("本地练习：自选卡牌，打木人", "Local practice: pick cards, fight a dummy"); } }

        readonly ModContext _ctx;
        readonly Action _enter;
        GameObject _clone;
        bool _hooked;
        bool _gaveUp;
        bool _announced;

        public LobbyEntry(ModContext ctx, Action enter)
        {
            _ctx = ctx;
            _enter = enter;
        }

        /// <summary>单人模式页里的入口已经放上去了。</summary>
        public bool Injected { get { return _hooked && _clone != null; } }

        public void Install()
        {
            _hooked = _ctx.Hooks.TryPrefix(ItemScript, "OnClick", 1, OnItemClick) != null;
            if (!_hooked) _ctx.Log.Warn(_ctx.T("单人模式页里的入口不可用：用 CTRL+ALT+1 进练习场", "The Single Player entry is unavailable: use CTRL+ALT+1 to enter the arena"));
        }

        bool OnItemClick(HookContext h)
        {
            if (_clone == null) return true;
            SinglePlayerModeItem item = h.Instance as SinglePlayerModeItem;
            if (item == null || item.transform != _clone.transform) return true;
            h.Skip(null);
            _enter();
            return false;
        }

        /// <summary>大厅里每隔一会儿调一次。</summary>
        public void Tick()
        {
            if (!_hooked || _gaveUp) return;
            try
            {
                if (_clone != null) { KeepTexts(); return; }
                Transform part = FindShownPart();
                if (part == null) return;
                Inject(part);
            }
            catch (Exception e)
            {
                _gaveUp = true;
                _ctx.Log.Warn(_ctx.T("往单人模式页加入口失败，不再重试（用 CTRL+ALT+1 进练习场）：", "Failed to add the entry to the Single Player page; won't retry (use CTRL+ALT+1 to enter the arena): ") + e.Message);
            }
        }

        /// <summary>模式选择窗口显示着、且单人模式页是激活的，才返回它。不去碰没初始化过的面板。</summary>
        static Transform FindShownPart()
        {
            LobbyPanel lobby = ILRPanelBase.FindILRPanel<LobbyPanel>();
            if (lobby == null || lobby.panel == null) return null;
            UISubPanelBase sub = lobby.panel.FindSubPanel(PanelName);
            if (sub == null || !sub.isShow) return null;
            Component[] all = sub.GetComponentsInChildren(typeof(Transform), false);
            for (int i = 0; i < all.Length; i++)
            {
                Transform t = all[i] as Transform;
                if (t != null && t.name == PartName) return t;
            }
            return null;
        }

        void Inject(Transform part)
        {
            Component[] bridges = part.GetComponentsInChildren(typeof(ILRComponentBridge), false);
            GameObject source = null;
            for (int i = 0; i < bridges.Length; i++)
            {
                ILRComponentBridge bridge = bridges[i] as ILRComponentBridge;
                if (bridge == null || bridge.scriptName != ItemScript) continue;
                if (bridge.gameObject.name == CloneName) { _clone = bridge.gameObject; return; }   // 上次注入的还在
                if (source == null) source = bridge.gameObject;
            }
            if (source == null) return;
            // 强转成 Object 选非泛型的 Instantiate 重载。
            _clone = UnityEngine.Object.Instantiate((UnityEngine.Object)source, source.transform.parent) as GameObject;
            if (_clone == null) throw new InvalidOperationException("Instantiate 返回了空");
            _clone.name = CloneName;
            _clone.SetActive(true);
            _clone.transform.SetAsLastSibling();
            KeepTexts();
            if (!_announced) _ctx.Log.Info(_ctx.T("单人模式页里已加入「练习场」入口（克隆自 ", "Added the \"Arena\" entry to the Single Player page (cloned from ") + source.name + _ctx.T("）", ")"));
            _announced = true;
        }

        /// <summary>入口自己的初始化 / 语言刷新会把标题写回原来的，所以隔一会儿看一眼。</summary>
        void KeepTexts()
        {
            Component[] labels = _clone.GetComponentsInChildren(typeof(TextMeshProUGUI), true);
            for (int i = 0; i < labels.Length; i++)
            {
                TextMeshProUGUI label = labels[i] as TextMeshProUGUI;
                if (label == null) continue;
                if (label.gameObject.name == "TitleLabel" && label.text != Title) label.text = Title;
                else if (label.gameObject.name == "HintLabel" && label.text != Hint) label.text = Hint;
            }
        }

        public void OnLeftLobby()
        {
            _clone = null;      // 场景没了，物体也没了
        }
    }
}
