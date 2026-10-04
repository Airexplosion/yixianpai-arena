using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Proto;
using UnityEngine;
using Yx.ModSdk;
using Yx.ModSdk.Unity;
using YxArena.Game;

namespace YxArena.Views
{
	public sealed class SetupWindow
	{
		private sealed class Bound
		{
			public SetupWindow Owner;

			public int Kind;

			public int Value;

			public void Run()
			{
				Owner.OnBound(Kind, Value, null);
			}

			public void RunText(string text)
			{
				Owner.OnBound(Kind, Value, text);
			}
		}

		private const float Width = 900f;

		private const float Height = 610f;

		private const float Row = 44f;

		private const float Pad = 16f;

		private const int Columns = 4;

		private const int Rows = 8;

		private const float CellWidth = 212f;

		private const int PageSlots = 0;

		private const int PageTalents = 1;

		private const int PageFates = 2;

		private const int PageLearned = 3;

		private const int KindPickSlot = 1;

		private const int KindClearSlot = 2;

		private const int KindSlotValue = 3;

		private const int KindChooseTalent = 4;

		private const int KindToggleFate = 5;

		private const int KindGroup = 6;

		private const int KindPickCharacter = 7;

		private const int KindToggleLearned = 8;

		private const int GroupGeneral = 0;

		private const int GroupExclusive = 9;

		private static readonly string[] SlotNames = new string[5] { "炼气", "筑基", "金丹", "元婴", "化神" };

		private static readonly string[] SlotNamesEn = new string[5] { "Qi Refining", "Foundation", "Golden Core", "Nascent Soul", "Spirit Severing" };

		private static readonly string[] LevelTags = new string[7] { "", "炼", "筑", "金", "元", "化", "返" };

		private static readonly string[] LevelTagsEn = new string[7] { "", "Q", "F", "G", "N", "S", "V" };

		private readonly ModContext _ctx;

		private readonly ArenaSession _session;

		private readonly Action _changed;

		private readonly List<Bound> _bounds = new List<Bound>();

		private GameObject _root;

		private int _side;

		private int _page;

		private int _slot;

		private int _pageIndex;

		private int _group = -1;

		private string _filter = "";

		private int[] _talentIds;

		private string[] _talentNames;

		private int[] _talentGroups;

		private int[] _fateIds;

		private string[] _fateNames;

		private int[] _cardIds;

		private string[] _cardNames;

		private bool _learnedOnly;

		private bool _allowCharacter;

		private bool _direct;

		private readonly Action _hideTips;

		private readonly BoxSearch _search;

		private bool _nativeBroken;

		private SelectInfoPanel _waiting;

		private bool _returnToWindow;

		private int _waitingFrames;

		private readonly UiKit _ui;

		private const int NativeEmpty = -999999;

		public bool IsOpen
		{
			get
			{
				if (!(_root != null))
				{
					return _waiting != null;
				}
				return true;
			}
		}

		private ArenaSide Side => _session.SideAt(_side);

		public SetupWindow(UiKit ui, ModContext ctx, ArenaSession session, Action changed, Action hideTips, BoxSearch search)
		{
			_ui = ui;
			_search = search;
			_hideTips = hideTips;
			_ctx = ctx;
			_session = session;
			_changed = changed;
		}

		private static string N(int value)
		{
			return value.ToString(CultureInfo.InvariantCulture);
		}

		public void OpenSlots(int side, bool allowCharacter)
		{
			_direct = false;
			_allowCharacter = allowCharacter;
			_side = ((side == 1) ? 1 : 0);
			_page = 0;
			Rebuild();
		}

		public void PickTalentDirect(int side, int slot, RectTransform anchor)
		{
			Close();
			_allowCharacter = false;
			_side = ((side == 1) ? 1 : 0);
			_slot = ((slot >= 0 && slot < 5) ? slot : 0);
			_direct = true;
			if (!(anchor != null) || !OpenNativeBox(SelectInfoType.仙命, OnNativeTalent, anchor, returnToWindow: false))
			{
				GoToList(1);
			}
		}

		public void OpenFates(int side)
		{
			_direct = false;
			_side = ((side == 1) ? 1 : 0);
			GoToList(2);
		}

        public void OpenLearned(int side)
        {
            _direct = false;
            _allowCharacter = _session.InPlacement;
            _side = side == 1 ? 1 : 0;
            _learnedOnly = false;
            GoToList(PageLearned);
        }

		private void GoToList(int page)
		{
			_page = page;
			_pageIndex = 0;
			_filter = "";
			_group = -1;
			Rebuild();
		}

		public void Close()
		{
			_waiting = null;
			if (_root != null)
			{
				_ui.Destroy(_root);
			}
			_root = null;
			_bounds.Clear();
		}

		private static string TalentName(int id)
		{
			if (id <= 0)
			{
				return Loc.T("（空）", "(empty)");
			}
			try
			{
				return TranslateUtil.GetTalentTranslate(id);
			}
			catch (Exception)
			{
				return N(id);
			}
		}

		private void EnsureTalents()
		{
			if (_talentIds == null)
			{
				List<TalentConfig> talentConfigs = ConfigManager.talentConfigs;
				_talentIds = new int[talentConfigs.Count];
				_talentNames = new string[talentConfigs.Count];
				_talentGroups = new int[talentConfigs.Count];
				for (int i = 0; i < talentConfigs.Count; i++)
				{
					TalentConfig talentConfig = talentConfigs[i];
					_talentIds[i] = talentConfig.id;
					int level = (int)talentConfig.level;
					string text = ((level > 0 && level < LevelTags.Length) ? Loc.T("［" + LevelTags[level] + "］", " [" + LevelTagsEn[level] + "]") : "");
					_talentNames[i] = TalentName(talentConfig.id) + text;
					_talentGroups[i] = (int)((talentConfig.charId != 0) ? ((Sect)9) : talentConfig.sect);
				}
			}
		}

		private void EnsureFates()
		{
			if (_fateIds != null)
			{
				return;
			}
			List<FateStrategyConfig> fateStrategyConfigs = ConfigManager.fateStrategyConfigs;
			_fateIds = new int[fateStrategyConfigs.Count];
			_fateNames = new string[fateStrategyConfigs.Count];
			for (int i = 0; i < fateStrategyConfigs.Count; i++)
			{
				_fateIds[i] = fateStrategyConfigs[i].id;
				string text;
				try
				{
					text = TranslateUtil.GetFateStrategyNameTranslate(fateStrategyConfigs[i].id);
				}
				catch (Exception)
				{
					text = "";
				}
				_fateNames[i] = (string.IsNullOrEmpty(text) ? N(fateStrategyConfigs[i].id) : text);
			}
		}

		private void EnsureCards()
		{
			if (_cardIds != null)
			{
				return;
			}
			List<int> list = new List<int>();
			List<string> list2 = new List<string>();
			List<CardConfig> cardConfigs = ConfigManager.GetCardConfigs((SeasonMechanismType)(-1), getOriginal: true);
			for (int i = 0; i < cardConfigs.Count; i++)
			{
				CardConfig cardConfig = cardConfigs[i];
				if (cardConfig != null && cardConfig.id > 0 && CardIds.RarityOf(cardConfig.id) == 0 && !cardConfig.obsolete)
				{
					list.Add(cardConfig.id);
					list2.Add(cardConfig.name);
				}
			}
			_cardIds = list.ToArray();
			_cardNames = list2.ToArray();
		}

		private static string GroupName(int group)
		{
			switch (group)
			{
			case -1:
				return Loc.T("全部", "All");
			case 0:
				return Loc.T("通用", "General");
			case 9:
				return Loc.T("角色专属", "Exclusive");
			default:
				try
				{
					return TranslateUtil.GetSectTranslate(group);
				}
				catch (Exception)
				{
					return N(group);
				}
			}
		}

		private Bound Bind(int kind, int value)
		{
			Bound bound = new Bound();
			bound.Owner = this;
			bound.Kind = kind;
			bound.Value = value;
			_bounds.Add(bound);
			return bound;
		}

		private void Rebuild()
		{
			Close();
			Vector2 vector = new Vector2(0.5f, 0.5f);
			_root = _ui.Panel("YxArenaSetup", vector, vector, Vector2.zero, new Vector2(900f, 610f), new Color(0.05f, 0.04f, 0.03f, 0.95f));
			if (!(_root == null))
			{
				if (_page == 0)
				{
					BuildSlotsPage();
				}
				else
				{
					BuildListPage();
				}
			}
		}

		private void BuildSlotsPage()
		{
			Transform transform = _root.transform;
			ArenaSide side = Side;
			float num = -16f;
			_ui.Label(transform, "title", Loc.T("仙命（" + side.Name + "）", "Talents (" + side.Name + ")"), new Vector2(16f, num), new Vector2(420f, 44f), 26f);
			_ui.TextButton(transform, "done", Loc.T("完成", "Done"), new Vector2(774f, num), new Vector2(110f, 40f), Close);
			num -= 48f;
			_ui.Label(transform, "hint", Loc.T("点名字选仙命（所有仙命，不限角色）。「计数」是这个仙命攒的层数 / 次数在开打时已经有多少，不需要就留 0。", "Click a name to pick a talent (all talents, any character). \"Count\" is how many stacks / triggers it already has when the fight starts; leave 0 if not needed."), new Vector2(16f, num), new Vector2(868f, 44f), 17f);
			num -= 44f;
			if (_allowCharacter)
			{
				_ui.Label(transform, "charCaption", Loc.T("角色", "Character"), new Vector2(16f, num - 6f), new Vector2(90f, 44f), 22f);
				_ui.TextButton(transform, "char", CharacterName(side.CharacterId), new Vector2(112f, num), new Vector2(380f, 40f), Bind(7, 0).Run);
				_ui.Label(transform, "charHint", Loc.T("换角色会换成它自带的仙命，皮肤回到默认", "Switching character loads its innate talents; skin resets to default"), new Vector2(506f, num - 8f), new Vector2(380f, 44f), 16f);
				num -= 48f;
			}
			for (int i = 0; i < 5; i++)
			{
				_ui.Label(transform, "slot", Loc.T(SlotNames[i], SlotNamesEn[i]), new Vector2(16f, num - 6f), new Vector2(90f, 44f), 22f);
				_ui.TextButton(transform, "talent", TalentName(side.Talents[i]), new Vector2(112f, num), new Vector2(380f, 40f), Bind(1, i).Run);
				_ui.TextButton(transform, "clear", "×", new Vector2(500f, num), new Vector2(44f, 40f), Bind(2, i).Run);
				if (side.Talents[i] != 0)
				{
					_ui.Label(transform, "valueCaption", Loc.T("计数", "Count"), new Vector2(568f, num - 6f), new Vector2(54f, 44f), 20f);
					_ui.NumberInput(transform, "value", new Vector2(626f, num), new Vector2(130f, 40f), 6, Bind(3, i).RunText).Show(N(side.TalentValues[i]));
				}
				num -= 44f;
			}
			num -= 10f;
			_ui.TextButton(transform, "learned", Loc.T("编辑已悟卡牌（" + N(side.LearnedCards.Count) + "）", "Edit learned cards (" + N(side.LearnedCards.Count) + ")"), new Vector2(16f, num), new Vector2(250f, 40f), OnOpenLearned);
			if (_session.InPlacement)
			{
				_ui.TextButton(transform, "bottle", Loc.T("打开五行玉瓶", "Open jade bottle"), new Vector2(281f, num), new Vector2(250f, 40f), OnOpenBottle);
			}
			else
			{
				_ui.Label(transform, "bottleHint", Loc.T("玉瓶内容进入备战后点瓶子存取", "Manage the jade bottle on the setup screen"), new Vector2(281f, num - 6f), new Vector2(570f, 44f), 18f);
			}
			num -= 48f;
			_ui.Label(transform, "fateCaption", Loc.T("天衍仙命", "Fates"), new Vector2(16f, num - 6f), new Vector2(96f, 44f), 22f);
			string text = N(side.Fates.Count);
			_ui.TextButton(transform, "fates", Loc.T("选择…（已选 " + text + "）", "Choose… (" + text + " picked)"), new Vector2(112f, num), new Vector2(380f, 40f), OnOpenFates);
			EnsureFates();
			StringBuilder stringBuilder = new StringBuilder();
			for (int j = 0; j < side.Fates.Count; j++)
			{
				if (j > 0)
				{
					stringBuilder.Append(Loc.T("、", ", "));
				}
				stringBuilder.Append(FateName(side.Fates[j]));
			}
			_ui.Label(transform, "fateList", stringBuilder.ToString(), new Vector2(16f, num - 44f), new Vector2(868f, 80f), 18f);
		}

		private string FateName(int id)
		{
			for (int i = 0; i < _fateIds.Length; i++)
			{
				if (_fateIds[i] == id)
				{
					return _fateNames[i];
				}
			}
			return N(id);
		}

		private void BuildListPage()
		{
			bool flag = _page == 1;
			bool flag2 = _page == 3;
			if (flag)
			{
				EnsureTalents();
			}
			else if (flag2)
			{
				EnsureCards();
			}
			else
			{
				EnsureFates();
			}
			int[] array = (flag ? _talentIds : (flag2 ? _cardIds : _fateIds));
			string[] array2 = (flag ? _talentNames : (flag2 ? _cardNames : _fateNames));
			int[] groups = (flag ? _talentGroups : null);
			Transform transform = _root.transform;
			ArenaSide side = Side;
			float num = -16f;
			string text = (flag ? Loc.T("全仙命表 → " + SlotNames[_slot] + "（" + side.Name + "）", "All talents → " + SlotNamesEn[_slot] + " (" + side.Name + ")") : Loc.T("天衍仙命（" + side.Name + "）\u3000已选 " + N(side.Fates.Count), "Fates (" + side.Name + ")  " + N(side.Fates.Count) + " picked"));
			if (flag2)
			{
				text = Loc.T("悟剑（" + side.Name + "）\u3000已悟 " + N(side.LearnedCards.Count), "Learned (" + side.Name + ")  " + N(side.LearnedCards.Count));
			}
			_ui.Label(transform, "title", text, new Vector2(16f, num), new Vector2(430f, 44f), 24f);
			_ui.Label(transform, "searchCaption", Loc.T("搜名字", "Search"), new Vector2(456f, num - 4f), new Vector2(70f, 44f), 20f);
			_ui.TextInput(transform, "search", new Vector2(530f, num), new Vector2(220f, 40f), 16, OnFilter).Show(_filter);
			_ui.TextButton(transform, "back", Loc.T("返回", "Back"), new Vector2(774f, num), new Vector2(110f, 40f), OnBackToSlots);
			num -= 48f;
			if (flag)
			{
				int[] array3 = new int[7] { -1, 0, 1, 2, 3, 4, 9 };
				float num2 = 16f;
				for (int i = 0; i < array3.Length; i++)
				{
					string text2 = ((array3[i] == _group) ? ("● " + GroupName(array3[i])) : GroupName(array3[i]));
					_ui.TextButton(transform, "tab", text2, new Vector2(num2, num), new Vector2(118f, 38f), Bind(6, array3[i]).Run);
					num2 += 122f;
				}
				num -= 44f;
			}
			if (flag2)
			{
				_ui.TextButton(transform, "learnedFilter", _learnedOnly ? Loc.T("显示全部卡牌", "Show all cards") : Loc.T("只看已悟", "Show learned only"), new Vector2(16f, num), new Vector2(200f, 38f), OnToggleLearnedFilter);
				_ui.Label(transform, "learnedHint", Loc.T("点击勾选 / 取消，立即保存；牌级共用，需选悟剑相关仙命才生效", "Click to toggle and save; shared across levels, requires learning talents"), new Vector2(232f, num - 5f), new Vector2(480f, 44f), 16f);
                _ui.TextButton(transform, "clearLearned", Loc.T("清空已悟", "Clear learned"), new Vector2(744f, num), new Vector2(140f, 38f), OnClearLearned);
				num -= 44f;
			}
			int[] array4 = ListFilter.Match(array2, groups, flag ? _group : (-1), _filter);
			if (flag2 && _learnedOnly)
			{
				List<int> list = new List<int>();
				for (int j = 0; j < array4.Length; j++)
				{
					if (side.LearnedCards.Contains(array[array4[j]]))
					{
						list.Add(array4[j]);
					}
				}
				array4 = list.ToArray();
			}
			int num3 = 4 * ((flag || flag2) ? 7 : 8);
			_pageIndex = ListFilter.ClampPage(array4.Length, num3, _pageIndex);
			int num4 = _pageIndex * num3;
			for (int k = 0; k < num3 && num4 + k < array4.Length; k++)
			{
				int num5 = array4[num4 + k];
				int num6 = k % 4;
				int num7 = k / 4;
				string text3 = ((flag ? (side.Talents[_slot] == array[num5]) : (flag2 ? side.LearnedCards.Contains(array[num5]) : side.HasFate(array[num5]))) ? ("✔ " + array2[num5]) : array2[num5]);
				_ui.TextButton(transform, "item", text3, new Vector2(16f + (float)num6 * 218f, num - (float)num7 * 44f), new Vector2(212f, 40f), Bind(flag ? 4 : (flag2 ? 8 : 5), array[num5]).Run);
			}
			float num8 = -550f;
			_ui.TextButton(transform, "prev", Loc.T("◀ 上一页", "◀ Prev"), new Vector2(16f, num8), new Vector2(130f, 44f), OnPrev);
			string text4 = N(_pageIndex + 1) + " / " + N(ListFilter.PageCount(array4.Length, num3));
			string text5 = N(array4.Length);
			string text6 = Loc.T(text4 + "\u3000共 " + text5 + " 个", text4 + "  " + text5 + " total");
			_ui.Label(transform, "page", text6, new Vector2(162f, num8 - 8f), new Vector2(260f, 44f), 20f);
			_ui.TextButton(transform, "next", Loc.T("下一页 ▶", "Next ▶"), new Vector2(426f, num8), new Vector2(130f, 44f), OnNext);
		}

		private static SelectInfoPanel FindNativeBox()
		{
			if (SceneLoader.currentSceneName == "Battle")
			{
				BattlePanel battlePanel = ILRPanelBase.FindILRPanel<BattlePanel>();
				if (battlePanel == null || battlePanel.readyLayer == null)
				{
					return null;
				}
				return battlePanel.FindILRSubPanelRuntime<SelectInfoPanel>(battlePanel.readyLayer.subPanelContainer);
			}
			return ILRPanelBase.FindILRPanel<LobbyPanel>()?.FindILRSubPanelRuntime<SelectInfoPanel>();
		}

		private bool OpenNativeBox(SelectInfoType type, Action<int> onPicked)
		{
			if (_root == null)
			{
				return false;
			}
			return OpenNativeBox(type, onPicked, _root.transform as RectTransform, returnToWindow: true);
		}

		private bool OpenNativeBox(SelectInfoType type, Action<int> onPicked, RectTransform anchor, bool returnToWindow)
		{
			if (_nativeBroken || anchor == null)
			{
				return false;
			}
			try
			{
				SelectInfoPanel selectInfoPanel = FindNativeBox();
				if (selectInfoPanel == null)
				{
					_nativeBroken = true;
					return false;
				}
				selectInfoPanel.ShowBox(anchor, onPicked, type);
				if (_search != null)
				{
					_search.Attach(selectInfoPanel, type == SelectInfoType.仙命);
				}
				_waiting = selectInfoPanel;
				_waitingFrames = 0;
				_returnToWindow = returnToWindow;
				if (_root != null)
				{
					_root.SetActive(value: false);
				}
				return true;
			}
			catch (Exception ex)
			{
				_nativeBroken = true;
				_ctx.Log.Warn(Loc.T("游戏自己的选择框打不开，改用自绘的列表：", "Game's own picker won't open; falling back to the custom list: ") + ex.Message);
				return false;
			}
		}

		public void Tick()
		{
			if (_waiting == null)
			{
				return;
			}
			_waitingFrames++;
			if ((!(_waiting.panel != null) || !_waiting.panel.isShow) && _waitingFrames >= 2)
			{
				_waiting = null;
				if (_search != null)
				{
					_search.Detach();
				}
				if (_hideTips != null)
				{
					_hideTips();
				}
				if (_returnToWindow && _root != null)
				{
					Rebuild();
				}
			}
		}

		private void OnNativeTalent(int id)
		{
			try
			{
				if (id == -999999 || id <= 0)
				{
					Side.SetTalent(_slot, 0);
				}
				else
				{
					if (ConfigManager.GetTalentConfig(id) == null)
					{
						return;
					}
					ChooseTalent(id);
				}
				Notify();
			}
			catch (Exception exception)
			{
				_ctx.Log.Error(Loc.T("仙命小窗：选仙命出错", "Talent window: pick talent failed"), exception);
			}
		}

		private void OnNativeCharacter(int id)
		{
			try
			{
				if (id > 0 && ConfigManager.GetCharacterConfig(id) != null && id != Side.CharacterId)
				{
					Side.SetCharacter(id, 0, 0, ArenaRoom.InnateTalents(id));
					Notify();
				}
			}
			catch (Exception exception)
			{
				_ctx.Log.Error(Loc.T("仙命小窗：换角色出错", "Talent window: switch character failed"), exception);
			}
		}

		private void PickSlot(int slot)
		{
			_slot = slot;
			if (!OpenNativeBox(SelectInfoType.仙命, OnNativeTalent))
			{
				GoToList(1);
			}
		}

		private static string CharacterName(int id)
		{
			try
			{
				return TranslateUtil.GetCharacterNameTranslate(id);
			}
			catch (Exception)
			{
				return N(id);
			}
		}

		private void Notify()
		{
			_session.Persist();
			if (_changed != null)
			{
				_changed();
			}
		}

		private void OnBound(int kind, int value, string text)
		{
			try
			{
                if (kind == KindToggleLearned && _session.InPlacement && _session.EditingIndex != _side)
                {
                    Close();
                    Ui.Toast(Loc.T("编辑方已切换，请重新打开悟剑列表", "The editing side changed; reopen the learned card list"));
                    return;
                }
				switch (kind)
				{
				case 1:
					PickSlot(value);
					return;
				case 7:
					if (!OpenNativeBox(SelectInfoType.角色, OnNativeCharacter))
					{
						Ui.Toast(Loc.T("游戏的角色选择框打不开；换角色请回大厅的选英雄界面", "Game's character picker won't open; switch characters from the lobby's hero-select screen"));
					}
					return;
				case 2:
					Side.SetTalent(value, 0);
					Notify();
					break;
				case 3:
					if (text == null || text.Trim() == N(Side.TalentValues[value]))
					{
						return;
					}
					if (!Side.SetTalentValue(value, text))
					{
						Ui.Toast(Loc.T("计数要填 0–999999 的整数", "Count must be an integer 0–999999"));
					}
					Notify();
					break;
				case 4:
					ChooseTalent(value);
					_page = 0;
					Notify();
					if (_direct)
					{
						Close();
						return;
					}
					break;
				case 5:
					Side.ToggleFate(value);
					Notify();
					break;
				case 8:
					Side.ToggleLearnedCard(value);
					Notify();
					break;
				case 6:
					_group = value;
					_pageIndex = 0;
					break;
				}
				Rebuild();
			}
			catch (Exception exception)
			{
				_ctx.Log.Error(Loc.T("仙命小窗：操作出错", "Talent window: operation failed"), exception);
				Ui.Toast(Loc.T("操作失败，详见日志", "Operation failed; see log"));
			}
		}

		private void ChooseTalent(int id)
		{
			Side.SetTalent(_slot, id);
		}

		private void OnOpenFates()
		{
			GoToList(2);
		}

		private void OnOpenLearned()
		{
			_learnedOnly = false;
			GoToList(3);
		}

        private void OnClearLearned()
        {
            if (_session.InPlacement && _session.EditingIndex != _side) { Close(); return; }
            Side.SetLearnedCards(null);
            Notify();
            Rebuild();
        }

		private void OnToggleLearnedFilter()
		{
			_learnedOnly = !_learnedOnly;
			_pageIndex = 0;
			Rebuild();
		}

		private void OnOpenBottle()
		{
			Close();
			BottlePanel.Open();
		}

		private void OnBackToSlots()
		{
			if (_direct)
			{
				Close();
				return;
			}
			_page = 0;
			Rebuild();
		}

		private void OnPrev()
		{
			_pageIndex--;
			Rebuild();
		}

		private void OnNext()
		{
			_pageIndex++;
			Rebuild();
		}

		private void OnFilter(string text)
		{
			string text2 = ((text == null) ? "" : text.Trim());
			if (!(text2 == _filter))
			{
				_filter = text2;
				_pageIndex = 0;
				Rebuild();
			}
		}
	}
}
