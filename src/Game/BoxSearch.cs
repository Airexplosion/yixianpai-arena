using System;
using System.Collections.Generic;
using System.Reflection;
using Proto;
using UnityEngine;
using Yx.ModSdk;
using Yx.ModSdk.Hooks;
using Yx.ModSdk.Unity;
using Yx.Shared;

namespace YxArena.Game
{
	public sealed class BoxSearch
	{
		private const int NativeEmpty = -999999;

		private const string InputName = "YxArenaBoxSearch";

		private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

		private readonly ModContext _ctx;

		private bool _hooked;

		private bool _broken;

		private bool _active;

		private bool _talents;

		private string _filter = "";

		private SelectInfoPanel _box;

		private GameObject _row;

		private UiInput _input;

		private RectTransform _rowRect;

		private readonly UiKit _ui;

		public BoxSearch(UiKit ui, ModContext ctx)
		{
			_ui = ui;
			_ctx = ctx;
		}

		public void Install()
		{
			HookGroup hookGroup = _ctx.Hooks.Group("选择框搜索");
			hookGroup.Postfix("SelectInfoPanel", "RefreshParamList", 0, OnListBuilt);
			hookGroup.Prefix("SelectInfoPanel", "OnEveryUpdate", 1, OnOutsideClickCheck);
			_hooked = hookGroup.Complete;
			if (!_hooked)
			{
				hookGroup.CancelAll();
				_ctx.Log.Warn(_ctx.T("选择框的搜索不可用（选择框照常可用）", "Picker search unavailable (the picker still works)"));
			}
		}

		private void Fail(string where, Exception e)
		{
			if (!_broken)
			{
				_ctx.Log.Error(_ctx.T("选择框搜索 " + where + " 出错（之后不再提供搜索）", "Picker search " + where + " failed (search disabled from now on)"), e);
			}
			_broken = true;
			_active = false;
			if (_row != null)
			{
				_row.SetActive(value: false);
			}
		}

		public void Attach(SelectInfoPanel box, bool talents)
		{
			if (!_hooked || _broken || box == null)
			{
				return;
			}
			try
			{
				_box = box;
				_talents = talents;
				_filter = "";
				Transform transform = box.transform.Find("Box");
				if (transform == null)
				{
					return;
				}
				Transform transform2 = transform.Find("YxArenaBoxSearch");
				if (transform2 == null || _row == null || transform2.gameObject != _row)
				{
					if (transform2 != null)
					{
						UnityEngine.Object.Destroy(transform2.gameObject);
					}
					_row = _ui.Row(transform, "YxArenaBoxSearch", new Vector2(0f, 48f), new Vector2(330f, 44f), new Color(0f, 0f, 0f, 0.75f));
					_ui.Label(_row.transform, "caption", _ctx.T("搜名字", "Search"), new Vector2(8f, -8f), new Vector2(70f, 40f), 20f);
					_input = _ui.TextInput(_row.transform, "input", new Vector2(80f, -2f), new Vector2(244f, 40f), 16, OnSearch);
					_rowRect = _row.transform as RectTransform;
				}
				_row.SetActive(value: true);
				_input.SetText("");
				_active = true;
			}
			catch (Exception e)
			{
				Fail("Attach", e);
			}
		}

		public void Detach()
		{
			_active = false;
			_filter = "";
		}

		private void OnSearch(string text)
		{
			if (!_active || _box == null)
			{
				return;
			}
			string text2 = ((text == null) ? "" : text.Trim());
			if (text2 == _filter)
			{
				return;
			}
			_filter = text2;
			try
			{
				MethodInfo method = _box.GetType().GetMethod("Refresh", BindingFlags.Instance | BindingFlags.NonPublic);
				if (method == null)
				{
					throw new InvalidOperationException("找不到 SelectInfoPanel.Refresh");
				}
				method.Invoke(_box, null);
			}
			catch (Exception e)
			{
				Fail("Refresh", e);
			}
		}

		private static string NameOf(bool talents, int id)
		{
			if (talents)
			{
				TalentConfig talentConfig = ConfigManager.GetTalentConfig(id);
				if (talentConfig == null)
				{
					return "";
				}
				return TranslateUtil.GetTalentTranslate(talentConfig.talentId);
			}
			return TranslateUtil.GetCharacterNameTranslate(id);
		}

		private void OnListBuilt(HookContext h)
		{
			if (!_active || _filter.Length == 0 || h.Instance != _box)
			{
				return;
			}
			try
			{
				FieldInfo field = _box.GetType().GetField("m_ParamList", BindingFlags.Instance | BindingFlags.NonPublic);
				List<int> list = ((field != null) ? (field.GetValue(_box) as List<int>) : null);
				if (list == null)
				{
					throw new InvalidOperationException("拿不到 SelectInfoPanel.m_ParamList");
				}
				for (int num = list.Count - 1; num >= 0; num--)
				{
					int num2 = list[num];
					if (num2 != -999999)
					{
						string text = NameOf(_talents, num2);
						if (text == null || !text.Contains(_filter))
						{
							list.RemoveAt(num);
						}
					}
				}
			}
			catch (Exception e)
			{
				Fail("RefreshParamList", e);
			}
		}

		private bool OnOutsideClickCheck(HookContext h)
		{
			if (!_active || _rowRect == null || h.Instance != _box)
			{
				return true;
			}
			try
			{
				if (!RectTransformUtility.RectangleContainsScreenPoint(_rowRect, Input.mousePosition, UIManager.worldCamera))
				{
					return true;
				}
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
