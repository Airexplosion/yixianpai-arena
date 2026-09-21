using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Yx.ModSdk;
using Yx.ModSdk.Hooks;
using Yx.Shared;

namespace YxArena.Game
{
    /// <summary>
    /// 用游戏自己的「切换」按钮换边（用户 2026-09-20 要求：换边不要用我们自绘的按钮）。
    ///
    /// 复盘模式的备战界面上有一组控件 ReadyLayerRecoveryingCI：「复盘中」标签 + 恢复 / 发牌 / 切换三个按钮，
    /// 由 ReadyLayer 的 containerCI 按需从 Addressables 加载。练习场里借它的「切换」按钮：
    ///   · 这组控件只在 ReviewMode 下由游戏刷新出来，练习场（PracticeMode）里我们自己 FindComponentItem + Refresh 把它调出来；
    ///   · 它的 OnStart 会按复盘的条件把「切换」「发牌」藏起来，所以每次慢 tick 重新摆一遍：只留「切换」，标签改成「练习场」；
    ///   · 三个按钮原本都要发包（ReviewSwitchReq / BattleRecoveryReq）或打开复盘专用的发牌面板——练习场全程不发包，
    ///     所以三个点击处理都挂前置并跳过；「切换」转给练习场自己的换边。**三个钩子不全就不借这组控件**（退回控制栏上的按钮）：
    ///     宁可少一个原生按钮，也不能让一次点击把包发出去。
    ///   · 位置：这组控件原本在备战界面偏上的地方。用户 2026-09-20 要求挪到「修为 / 血量」那一行——按钮放在那一行（CardScroll.attrLayout：
    ///     修为 / 血量 / 体魄……）正在显示的各项里最靠右那一项的后面，标签跟在按钮后面。偏移量可以在配置里调。
    /// </summary>
    public sealed class NativeSwitch
    {
        readonly ModContext _ctx;
        readonly Action _onSwitch;
        bool _hooked;
        bool _failed;
        bool _shown;
        bool _detached;

        /// <summary>按钮相对「修为 / 血量」右边缘的水平间距、相对那一行中线的竖直偏移（画布单位；来自配置，可以在管理器里调）。</summary>
        public int OffsetX = 24;
        public int OffsetY;

        public NativeSwitch(ModContext ctx, Action onSwitch)
        {
            _ctx = ctx;
            _onSwitch = onSwitch;
        }

        /// <summary>原生按钮能用（钩子齐了，也没有在运行时失败过）。为 false 时控制栏保留自己的换边按钮。</summary>
        public bool Available { get { return _hooked && !_failed; } }

        public void Install()
        {
            HookGroup group = _ctx.Hooks.Group("原生切换按钮");
            group.Prefix("ReadyLayerRecoveryingCI", "OnSwitchButtonClick", 0, OnSwitchClick);
            group.Prefix("ReadyLayerRecoveryingCI", "OnRecoveryButtonClick", 0, OnBlockedClick);
            group.Prefix("ReadyLayerRecoveryingCI", "OnDealCardButtonClick", 0, OnBlockedClick);
            _hooked = group.Complete;
            if (_hooked) return;
            group.CancelAll();
            _ctx.Log.Warn("游戏自己的「切换」按钮借不了（换边用控制栏上的按钮）");
        }

        bool OnSwitchClick(HookContext h)
        {
            if (!ArenaSession.Active) return true;
            h.Skip(null);
            try { _onSwitch(); }
            catch (Exception e) { _ctx.Log.Error("换边出错", e); }
            return false;
        }

        bool OnBlockedClick(HookContext h)
        {
            if (!ArenaSession.Active) return true;
            h.Skip(null);
            return false;
        }

        /// <summary>备战界面里每次慢 tick 调：把那组控件调出来并摆成练习场要的样子。</summary>
        /// <param name="editingOpponent">现在正在编辑对手（标签上写出来，免得换完边不知道自己在哪边）。</param>
        public void Ensure(bool editingOpponent)
        {
            if (!Available) return;
            try
            {
                ReadyLayerRecoveryingCI ci = Find();
                if (ci == null) { Fail("这组控件加载不出来", null); return; }
                if (!ci.showing) ci.Refresh();
                _shown = true;
                Button switchButton = ci.FindComponent<Button>("SwitchButton");
                Show(switchButton, true);
                Show(ci.FindComponent<Button>("RecoveryButton"), false);
                Show(ci.FindComponent<Button>("DealCardButton"), false);
                TextMeshProUGUI label = ci.FindComponent<TextMeshProUGUI>("ReviewLabel");
                if (label != null)
                {
                    string text = editingOpponent ? "练习场 · 对手" : "练习场 · 我方";
                    if (label.text != text) label.text = text;
                }
                PlaceOnStatsRow(switchButton, label);
            }
            catch (Exception e) { Fail("摆放出错", e); }
        }

        /// <summary>离开练习场时收起来（Battle 场景下次会重新加载，这里只是不留尾巴）。</summary>
        public void Hide()
        {
            if (!_shown) return;
            _shown = false;
            _detached = false;
            try
            {
                ReadyLayerRecoveryingCI ci = Find();
                if (ci != null && ci.showing) ci.Hide();
            }
            catch (Exception) { }
        }

        static ReadyLayerRecoveryingCI Find()
        {
            BattlePanel bp = ILRPanelBase.FindILRPanel<BattlePanel>();
            if (bp == null || bp.readyLayer == null || bp.readyLayer.containerCI == null) return null;
            return bp.readyLayer.containerCI.FindComponentItem<ReadyLayerRecoveryingCI>();
        }

        // ── 摆到「修为 / 血量」那一行 ────────────────────────────────────────

        void PlaceOnStatsRow(Button button, TextMeshProUGUI label)
        {
            if (button == null) return;
            BattlePanel bp = ILRPanelBase.FindILRPanel<BattlePanel>();
            CardPanel cards = bp != null ? bp.FindILRSubPanel<CardPanel>() : null;
            if (cards == null || cards.cardScroll == null) return;
            // 那一行是 CardScroll 的 AttrLayout：修为、血量、体魄……（体魄只在有体魄时显示）。取正在显示的各项里最靠右的那个，
            // 按钮放它后面——0.14.1 只看了修为和血量，结果压在了体魄上。
            RectTransform anchor = RightmostShown(cards.cardScroll.attrLayout);
            if (anchor == null) return;          // 那一行现在没显示：留在原位

            var rt = button.transform as RectTransform;
            if (rt == null) return;
            if (!_detached)
            {
                // 万一这组控件的父物体上有自动布局：让这两个元素不受它管，否则每次重排都会被拉回去。
                IgnoreLayout(rt.gameObject);
                if (label != null) IgnoreLayout(label.gameObject);
                _detached = true;
            }
            float scale = anchor.lossyScale.x;
            float left = RowLayout.RightEdge(anchor.position.x, anchor.rect.xMax, scale) + OffsetX * scale;
            float row = RowLayout.CenterY(anchor.position.y, anchor.rect.center.y, anchor.lossyScale.y) + OffsetY * anchor.lossyScale.y;
            MoveTo(rt, left, row);
            if (label == null) return;
            float afterButton = RowLayout.RightEdge(rt.position.x, rt.rect.xMax, rt.lossyScale.x) + 10f * scale;
            MoveTo(label.rectTransform, afterButton, row);
        }

        static RectTransform RightmostShown(Transform row)
        {
            if (row == null || !row.gameObject.activeInHierarchy) return null;
            RectTransform best = null;
            float bestEdge = 0f;
            for (int i = 0; i < row.childCount; i++)
            {
                RectTransform item = row.GetChild(i) as RectTransform;
                if (item == null || !item.gameObject.activeInHierarchy) continue;
                float edge = RowLayout.RightEdge(item.position.x, item.rect.xMax, item.lossyScale.x);
                if (best != null && edge <= bestEdge) continue;
                best = item;
                bestEdge = edge;
            }
            return best;
        }

        static void MoveTo(RectTransform rt, float leftEdge, float centerY)
        {
            float x = RowLayout.PivotXForLeftEdge(leftEdge, rt.rect.xMin, rt.lossyScale.x);
            float y = RowLayout.PivotYForCenter(centerY, rt.rect.center.y, rt.lossyScale.y);
            Vector3 now = rt.position;
            // 已经在位就不动它（免得每次慢 tick 都把画布标脏）。容差按缩放算：画布不是屏幕空间时，世界单位可能很小。
            float tolerance = 0.5f * Mathf.Abs(rt.lossyScale.x);
            if (Mathf.Abs(now.x - x) < tolerance && Mathf.Abs(now.y - y) < tolerance) return;
            rt.position = new Vector3(x, y, now.z);
        }

        static void IgnoreLayout(GameObject go)
        {
            var element = go.GetComponent(typeof(LayoutElement)) as LayoutElement;
            if (element == null) element = go.AddComponent(typeof(LayoutElement)) as LayoutElement;
            if (element != null) element.ignoreLayout = true;
        }

        static void Show(Button button, bool visible)
        {
            if (button == null) return;
            if (button.gameObject.activeSelf != visible) button.gameObject.SetActive(visible);
        }

        void Fail(string what, Exception e)
        {
            _failed = true;
            if (e != null) _ctx.Log.Error("游戏自己的「切换」按钮" + what + "（之后用控制栏上的按钮换边）", e);
            else _ctx.Log.Warn("游戏自己的「切换」按钮" + what + "（之后用控制栏上的按钮换边）");
        }
    }
}
