using System;
using System.Collections.Generic;
using System.Text;
using Proto;
using UnityEngine;
using Yx.ModSdk;
using Yx.ModSdk.Hooks;
using Yx.Shared;

namespace YxArena.Game
{
    /// <summary>
    /// 给游戏的全仙命选择框（SelectInfoPanel）补上悬停说明：那个选择框的格子（SelectInfoCell）只有放大动画，没有说明。
    ///   SelectInfoCell.UpdateContent/1  前置：记下「这个格子现在显示的是哪个仙命」（格子是循环复用的，数据不对外暴露）
    ///   SelectInfoCell.OnPointerEnter/1 后置：弹游戏自己的仙命说明框（TalentDescriptionPanel，和别处悬停仙命图标弹的是同一个）
    ///   SelectInfoCell.OnPointerExit/1  后置：收起
    /// 说明框贴在【整个选择框】的右侧，并且不拦截鼠标。0.10.1 贴在格子左侧：最左一列的格子会被说明框盖住、点不到
    /// （用户实机）；贴在格子右侧又会盖住右边几列，所以拿整个框当锚点。不拦截鼠标是保险：屏幕边上挤得重叠了也照样能点到格子。
    /// 这个选择框游戏自己已经不用了，只有练习场会打开它，所以这几个钩子不需要「在不在练习场」的判断。
    /// </summary>
    public sealed class TalentTips
    {
        const int MaxCells = 128;

        readonly ModContext _ctx;
        readonly List<SelectInfoCell> _cells = new List<SelectInfoCell>();
        readonly List<int> _talents = new List<int>();
        bool _broken;

        public TalentTips(ModContext ctx)
        {
            _ctx = ctx;
        }

        public void Install()
        {
            HookGroup group = _ctx.Hooks.Group("仙命悬停说明");
            group.Prefix("SelectInfoCell", "UpdateContent", 1, OnUpdateContent);
            group.Postfix("SelectInfoCell", "OnPointerEnter", 1, OnEnter);
            group.Postfix("SelectInfoCell", "OnPointerExit", 1, OnExit);
            if (group.Complete) return;
            group.CancelAll();
            _broken = true;
            _ctx.Log.Warn("仙命选择框的悬停说明不可用");
        }

        void Fail(string where, Exception e)
        {
            if (!_broken) _ctx.Log.Error("仙命悬停说明 " + where + " 出错（之后不再显示说明）", e);
            _broken = true;
        }

        int IndexOf(SelectInfoCell cell)
        {
            for (int i = 0; i < _cells.Count; i++) if (object.ReferenceEquals(_cells[i], cell)) return i;
            return -1;
        }

        bool OnUpdateContent(HookContext h)
        {
            if (_broken) return true;
            try
            {
                SelectInfoCell cell = h.Instance as SelectInfoCell;
                SelectInfoCell.CellData data = h.Args != null && h.Args.Length > 0 ? h.Args[0] as SelectInfoCell.CellData : null;
                if (cell == null || data == null) return true;
                int talent = data.infoType == SelectInfoType.仙命 && data.param > 0 ? data.param : 0;
                int index = IndexOf(cell);
                if (index >= 0) { _talents[index] = talent; return true; }
                if (_cells.Count >= MaxCells) { _cells.Clear(); _talents.Clear(); }     // 场景换过，旧格子都没了
                _cells.Add(cell);
                _talents.Add(talent);
            }
            catch (Exception e) { Fail("UpdateContent", e); }
            return true;
        }

        static TalentDescriptionPanel FindPanel()
        {
            TooltipsPanel tips = ILRPanelBase.FindILRPanel<TooltipsPanel>();
            return tips != null ? tips.FindILRSubPanel<TalentDescriptionPanel>() : null;
        }

        void OnEnter(HookContext h)
        {
            if (_broken) return;
            try
            {
                SelectInfoCell cell = h.Instance as SelectInfoCell;
                int index = cell != null ? IndexOf(cell) : -1;
                int talent = index >= 0 ? _talents[index] : 0;
                if (talent <= 0) return;
                TalentConfig config = ConfigManager.GetTalentConfig(talent);
                TalentDescriptionPanel panel = FindPanel();
                if (config == null || panel == null) return;
                var title = new StringBuilder();
                title.Append(config.GetName()).Append((char)10).Append(TranslateUtil.GetLevelTranslate(config.level));
                RectTransform anchor = FrameOf(cell.transform) ?? cell.transform as RectTransform;
                panel.ShowBox(anchor, title.ToString(), config.ParseDescription(GameMode.InvalidGameMode), talent, TooltipBoxAlignment.Right, false);
                SetBlocking(panel, false);
            }
            catch (Exception e) { Fail("OnPointerEnter", e); }
        }

        void OnExit(HookContext h)
        {
            Hide();
        }

        /// <summary>格子所在的那个选择框的外框（名字叫 Box 的祖先）。</summary>
        static RectTransform FrameOf(Transform cell)
        {
            Transform t = cell;
            for (int i = 0; i < 12 && t != null; i++)
            {
                if (t.name == "Box") return t as RectTransform;
                t = t.parent;
            }
            return null;
        }

        /// <summary>说明框拦不拦鼠标。我们显示时不拦；收起时放回去，别影响游戏别处用它。</summary>
        static void SetBlocking(TalentDescriptionPanel panel, bool blocking)
        {
            if (panel == null || panel.transform == null) return;
            GameObject go = panel.transform.gameObject;
            CanvasGroup group = go.GetComponent(typeof(CanvasGroup)) as CanvasGroup;
            if (group == null)
            {
                if (blocking) return;
                group = go.AddComponent(typeof(CanvasGroup)) as CanvasGroup;
            }
            group.blocksRaycasts = blocking;
        }

        /// <summary>收起说明框（选完仙命、选择框关掉时也调一下）。</summary>
        public void Hide()
        {
            if (_broken) return;
            try
            {
                TalentDescriptionPanel panel = FindPanel();
                if (panel == null) return;
                SetBlocking(panel, true);
                if (panel.panel != null && panel.panel.isShow) panel.Hide();
            }
            catch (Exception e) { Fail("Hide", e); }
        }
    }
}
