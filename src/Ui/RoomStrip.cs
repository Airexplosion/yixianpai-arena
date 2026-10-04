using System;
using System.Globalization;
using UnityEngine;
using Yx.ModSdk.Unity;

namespace YxArena.Views
{
	public sealed class RoomStrip
	{
		private const float RowHeight = 40f;

		private const float Gap = 6f;

		private const float Pad = 6f;

		private readonly Action _onBack;

		private readonly Action _onSwitchSide;

		private readonly Action _onFates;

		private readonly Action _onTalents;

		private GameObject _root;

		private UiButton _side;

		private UiButton _fates;

		private readonly UiKit _ui;

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
			if (visible && _root == null)
			{
				Build();
			}
			if (_root != null && _root.activeSelf != visible)
			{
				_root.SetActive(visible);
			}
		}

		private void Build()
		{
			Vector2 vector = new Vector2(0f, 1f);
			float[] array = new float[4] { 90f, 230f, 150f, 190f };
			float num = 30f;
			for (int i = 0; i < array.Length; i++)
			{
				num += array[i];
			}
			_root = _ui.Panel("YxArenaRoomStrip", vector, vector, new Vector2(16f, -16f), new Vector2(num, 98f), new Color(0f, 0f, 0f, 0.6f));
			if (!(_root == null))
			{
				Transform transform = _root.transform;
				_ui.Label(transform, "title", Loc.T("练习场\u3000点角色旁的仙命图标换仙命（全仙命表，不限角色）；右下「开始」进场", "Arena  Click a talent icon by the character to change it (all talents, any character); click Start at lower-right to enter"), new Vector2(6f, -12f), new Vector2(num - 12f, 40f), 18f);
				float num2 = 6f;
				float y = -52f;
				_ui.TextButton(transform, "back", Loc.T("返回", "Back"), new Vector2(num2, y), new Vector2(array[0], 40f), _onBack);
				num2 += array[0] + 6f;
				_side = _ui.TextButton(transform, "side", "", new Vector2(num2, y), new Vector2(array[1], 40f), _onSwitchSide);
				num2 += array[1] + 6f;
				_ui.TextButton(transform, "talents", Loc.T("仙命 / 计数", "Talents / count"), new Vector2(num2, y), new Vector2(array[2], 40f), _onTalents);
				num2 += array[2] + 6f;
				_fates = _ui.TextButton(transform, "fates", "", new Vector2(num2, y), new Vector2(array[3], 40f), _onFates);
			}
		}

		public void Refresh(bool opponent, int fateCount)
		{
			if (!(_root == null))
			{
				_side.SetText(opponent ? Loc.T("正在设置：对手 ⇄", "Editing: foe ⇄") : Loc.T("正在设置：我方 ⇄", "Editing: mine ⇄"));
				string text = fateCount.ToString(CultureInfo.InvariantCulture);
				_fates.SetText(Loc.T("天衍仙命（" + text + "）", "Fates (" + text + ")"));
			}
		}

		public void Destroy()
		{
			if (_root != null)
			{
				_ui.Destroy(_root);
			}
			_root = null;
		}
	}
}
