using Yx.ModSdk.Unity;
using System;
using System.Collections.Generic;
using System.Reflection;
using Proto;
using UnityEngine;
using Yx.ModSdk;
using Yx.ModSdk.Hooks;
using Yx.Shared;
using YxArena.Views;

namespace YxArena.Game
{
    /// <summary>
    /// 给游戏的选择框（SelectInfoPanel：全仙命 / 全角色）加一个按名字搜索的输入框。
    ///
    /// 选择框自己没有搜索，列表是它的私有字段 m_ParamList（RefreshParamList() 按当前页签重建，Refresh() 再据此排格子）。
    ///   SelectInfoPanel.RefreshParamList/0  后置：把名字里不含搜索词的条目从 m_ParamList 里删掉（保留最前面的「空」）
    ///   SelectInfoPanel.OnEveryUpdate/1     前置：它每帧检查「鼠标点在框外就关掉」；搜索框挂在框的上方（框外），
    ///                                        鼠标在搜索框上时跳过这次检查，不然一点搜索框选择框就关了
    /// 私有成员只能反射着拿（SDK 约定：反射只在非用不可的地方用——这里没有公开入口）。反射拿不到就不显示搜索框，别的不受影响。
    /// </summary>
    public sealed class BoxSearch
    {
        const int NativeEmpty = -999999;
        const string InputName = "YxArenaBoxSearch";
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

        readonly ModContext _ctx;
        bool _hooked;
        bool _broken;
        bool _active;
        bool _talents;
        string _filter = "";
        SelectInfoPanel _box;
        GameObject _row;
        UiInput _input;
        RectTransform _rowRect;

        readonly UiKit _ui;

        public BoxSearch(UiKit ui, ModContext ctx)
        {
            _ui = ui;
            _ctx = ctx;
        }

        public void Install()
        {
            HookGroup group = _ctx.Hooks.Group("选择框搜索");
            group.Postfix("SelectInfoPanel", "RefreshParamList", 0, OnListBuilt);
            group.Prefix("SelectInfoPanel", "OnEveryUpdate", 1, OnOutsideClickCheck);
            _hooked = group.Complete;
            if (_hooked) return;
            group.CancelAll();
            _ctx.Log.Warn("选择框的搜索不可用（选择框照常可用）");
        }

        void Fail(string where, Exception e)
        {
            if (!_broken) _ctx.Log.Error("选择框搜索 " + where + " 出错（之后不再提供搜索）", e);
            _broken = true;
            _active = false;
            if (_row != null) _row.SetActive(false);
        }

        /// <summary>选择框刚打开：把搜索框挂上去（挂过就复用），清空搜索词。</summary>
        public void Attach(SelectInfoPanel box, bool talents)
        {
            if (!_hooked || _broken || box == null) return;
            try
            {
                _box = box;
                _talents = talents;
                _filter = "";
                Transform frame = box.transform.Find("Box");
                if (frame == null) return;
                Transform existing = frame.Find(InputName);
                if (existing == null || _row == null || existing.gameObject != _row)
                {
                    if (existing != null) UnityEngine.Object.Destroy(existing.gameObject);
                    // 挂在框的正上方：y 取正值 = 在父物体上沿之外。
                    _row = _ui.Row(frame, InputName, new Vector2(0f, 48f), new Vector2(330f, 44f), new Color(0f, 0f, 0f, 0.75f));
                    _ui.Label(_row.transform, "caption", "搜名字", new Vector2(8f, -8f), new Vector2(70f, 40f), 20f);
                    _input = _ui.TextInput(_row.transform, "input", new Vector2(80f, -2f), new Vector2(244f, 40f), 16, OnSearch);
                    _rowRect = _row.transform as RectTransform;
                }
                _row.SetActive(true);
                _input.SetText("");
                _active = true;
            }
            catch (Exception e) { Fail("Attach", e); }
        }

        /// <summary>选择框关了。</summary>
        public void Detach()
        {
            _active = false;
            _filter = "";
        }

        void OnSearch(string text)
        {
            if (!_active || _box == null) return;
            string wanted = text == null ? "" : text.Trim();
            if (wanted == _filter) return;
            _filter = wanted;
            try
            {
                // 让选择框按当前页签重排一遍（私有方法 Refresh：先 RefreshParamList——我们的后置在那里筛——再排格子）。
                MethodInfo refresh = _box.GetType().GetMethod("Refresh", Private);
                if (refresh == null) throw new InvalidOperationException("找不到 SelectInfoPanel.Refresh");
                refresh.Invoke(_box, null);
            }
            catch (Exception e) { Fail("Refresh", e); }
        }

        static string NameOf(bool talents, int id)
        {
            if (talents)
            {
                TalentConfig config = ConfigManager.GetTalentConfig(id);
                return config != null ? TranslateUtil.GetTalentTranslate(config.talentId) : "";
            }
            return TranslateUtil.GetCharacterNameTranslate(id);
        }

        void OnListBuilt(HookContext h)
        {
            if (!_active || _filter.Length == 0 || !object.ReferenceEquals(h.Instance, _box)) return;
            try
            {
                FieldInfo field = _box.GetType().GetField("m_ParamList", Private);
                List<int> list = field != null ? field.GetValue(_box) as List<int> : null;
                if (list == null) throw new InvalidOperationException("拿不到 SelectInfoPanel.m_ParamList");
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    int id = list[i];
                    if (id == NativeEmpty) continue;
                    string name = NameOf(_talents, id);
                    if (name == null || !name.Contains(_filter)) list.RemoveAt(i);
                }
            }
            catch (Exception e) { Fail("RefreshParamList", e); }
        }

        bool OnOutsideClickCheck(HookContext h)
        {
            if (!_active || _rowRect == null || !object.ReferenceEquals(h.Instance, _box)) return true;
            try
            {
                if (!RectTransformUtility.RectangleContainsScreenPoint(_rowRect, Input.mousePosition, UIManager.worldCamera)) return true;
                h.Skip(null);
                return false;
            }
            catch (Exception e)
            {
                Fail("OnEveryUpdate", e);
                return true;
            }
        }
    }
}
