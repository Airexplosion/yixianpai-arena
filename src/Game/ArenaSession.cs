using System;
using System.Collections.Generic;
using System.Globalization;
using Proto;
using UnityEngine;
using Yx.ModSdk;
using Yx.ModSdk.Unity;

namespace YxArena.Game
{
	public sealed class ArenaSession
	{
		public const int StateIdle = 0;

		public const int StateEntering = 1;

		public const int StateReady = 2;

		public const int StateLeaving = 3;

		private const string OtherUid = "YxArenaDummy";

		private const int ParamCount = 4096;

		private const int SettleFrames = 30;

		private const float EnterTimeoutSeconds = 20f;

		private const float LeaveTimeoutSeconds = 10f;

		private static ArenaSession s_current;

		private readonly ModContext _ctx;

		private readonly ArenaConfig _cfg;

		private readonly OfflineHooks _hooks;

		private readonly ArenaSide[] _sides = new ArenaSide[2];

		private readonly BattlePlayerData[] _live = new BattlePlayerData[2];

		private readonly BattlePlayerPrivateData[] _private = new BattlePlayerPrivateData[2];

		private BattleResult _review;

		private ArenaSide[] _savedSides;

		private bool _savedFirst;

		private int _generation;

		private int _editing;

		private int _state;

		private int _settle;

		private float _deadline;

		private int _battleIndex;

		private bool _warnedReplace;

		private bool _lobbyRequested;

		public uint LastSeed;

		private const string SaveKeyPrefix = "side";

		public int State => _state;

		public bool FromReview => _review != null;

		public string ReviewStage { get; private set; }

		public ArenaSide Editing => _sides[_editing];

		public bool EditingOpponent => _editing == 1;

		public static bool Active
		{
			get
			{
				if (s_current != null && s_current._state != 0)
				{
					return RoomGuard.Reason() == null;
				}
				return false;
			}
		}

		public bool InPlacement
		{
			get
			{
				if (_state != 2)
				{
					return false;
				}
				BattleManager instance = BattleManager.Instance;
				if (instance == null || instance.currentGameStatus == null || instance.currentScene != SceneType.修炼阶段)
				{
					return false;
				}
				BattleExecuter defaultBattleExecuter = instance.defaultBattleExecuter;
				if (defaultBattleExecuter != null)
				{
					return !defaultBattleExecuter.isExecuting;
				}
				return true;
			}
		}

		public bool InBattlePhase
		{
			get
			{
				if (_state != 2)
				{
					return false;
				}
				BattleManager instance = BattleManager.Instance;
				if (instance != null && instance.currentGameStatus != null)
				{
					return instance.currentScene == SceneType.斗法阶段;
				}
				return false;
			}
		}

		public int EditingIndex => _editing;

		public ArenaSession(ModContext ctx, ArenaConfig cfg, OfflineHooks hooks)
		{
			_ctx = ctx;
			_cfg = cfg;
			_hooks = hooks;
			_sides[0] = new ArenaSide(ctx.T("我", "Me"), _cfg.CharacterId, _cfg.Level, 0);
			_sides[1] = new ArenaSide(ctx.T("木人", "Dummy"), 1000001, 1, _cfg.DummyHp);
			LoadSides();
			s_current = this;
		}

		public bool SolverReady()
		{
			if (Active)
			{
				return InPlacement;
			}
			return false;
		}

		public string SolverPosition()
		{
			return N(_generation) + ":" + N(_editing);
		}

		public string SideName(int index)
		{
			return _sides[(index == 1) ? 1u : 0u].Name;
		}

		public long TrueTotalHp(int index)
		{
			ArenaSide obj = _sides[(index == 1) ? 1u : 0u];
			return obj.TrueTotalHp(BaseHp(obj.Level));
		}

		public int GameTotalHp(int index)
		{
			ArenaSide obj = _sides[(index == 1) ? 1u : 0u];
			return obj.TotalHp(BaseHp(obj.Level));
		}

		private static string N(int value)
		{
			return value.ToString(CultureInfo.InvariantCulture);
		}

		private void Say(string message)
		{
			_ctx.Log.Info(message);
			Ui.Toast(message);
		}

		private void LoadSides()
		{
			for (int i = 0; i < 2; i++)
			{
				string text = _ctx.Data.Get("side" + N(i), "");
				if (text.Length > 0 && !_sides[i].Load(text))
				{
					_ctx.Log.Warn(_ctx.T("练习场存档读不出来，", "Arena save unreadable; ") + _sides[i].Name + _ctx.T("用默认设置", " uses default settings"));
				}
			}
		}

		public void Persist()
		{
			if (_review != null)
			{
				return;
			}
			try
			{
				for (int i = 0; i < 2; i++)
				{
					_ctx.Data.Set("side" + N(i), _sides[i].Save());
				}
			}
			catch (Exception ex)
			{
				_ctx.Log.Warn(_ctx.T("练习场存档失败：", "Arena save failed: ") + ex.Message);
			}
		}

		public void Autosave()
		{
			if (!InPlacement)
			{
				return;
			}
			try
			{
				if (Capture())
				{
					Persist();
				}
			}
			catch (Exception ex)
			{
				_ctx.Log.Warn(_ctx.T("练习场自动存档失败：", "Arena autosave failed: ") + ex.Message);
			}
		}

		public ArenaSide SideAt(int index)
		{
			return _sides[(index == 1) ? 1u : 0u];
		}

		public void ClearCards()
		{
			for (int i = 0; i < 2; i++)
			{
				_sides[i].Hand.Clear();
				_sides[i].SetBoard(null);
			}
			Persist();
		}

		public void Enter()
		{
			if (CanEnter())
			{
				ResetReview();
				BeginEnter();
			}
		}

		private bool CanEnter()
		{
			if (_state != 0)
			{
				Say(_ctx.T("已经在练习场里了", "Already in the arena"));
				return false;
			}
			if (!_hooks.Complete)
			{
				Say(_ctx.T("离线保护不完整，禁止进场（没挂上：", "Offline protection incomplete; entering is blocked (missing: ") + _hooks.Missing + _ctx.T("）", ")"));
				return false;
			}
			string text = RoomGuard.Reason();
			if (text != null)
			{
				Say(text);
				return false;
			}
			if (SceneLoader.isLoading || SceneLoader.currentSceneName != "Lobby")
			{
				Say(_ctx.T("只能从大厅进练习场", "The arena can only be entered from the lobby"));
				return false;
			}
			return true;
		}

		public void EnterReview(BattleResult source, SeasonMechanismType season)
		{
			ReviewStage = "检查进场条件";
			if (CanEnter())
			{
				ReviewStage = "复制战绩";
				BattleResult battleResult = source.DeepClone();
				ReviewStage = "检查战绩数据";
				ReviewImport.Validate(battleResult);
				PlayerData playerData = ((battleResult.mainViewId == battleResult.p2.publicData.uid) ? battleResult.p2 : battleResult.p1);
				PlayerData playerData2 = ((playerData == battleResult.p1) ? battleResult.p2 : battleResult.p1);
				ReviewStage = "导入我方数据";
				ArenaSide arenaSide = ReviewImport.Side(playerData, step => LogReviewStage("我方", step));
				ReviewStage = "导入对手数据";
				ArenaSide arenaSide2 = ReviewImport.Side(playerData2, step => LogReviewStage("对手", step));
				ReviewStage = "设置练习会话";
				_savedSides = new ArenaSide[2]
				{
					_sides[0],
					_sides[1]
				};
				_savedFirst = _cfg.PlayerFirst;
				_review = battleResult;
				_review.seasonMec = season;
				_sides[0] = arenaSide;
				_sides[1] = arenaSide2;
				_live[0] = playerData.publicData;
				_live[1] = playerData2.publicData;
				_private[0] = playerData.privateData;
				_private[1] = playerData2.privateData;
				_cfg.PlayerFirst = battleResult.firstPlayerId == playerData.publicData.uid;
				BattleManager.backType = BattleBackType.战绩详情面板;
				ReviewStage = "加载战斗场景";
				BeginEnter();
				Say("已导入复盘第 " + N(battleResult.round) + " 轮，可在练习场编辑并求解");
			}
		}

		private void LogReviewStage(string side, string step)
		{
			ReviewStage = "导入" + side + "数据 / " + step;
			_ctx.Log.Info("复盘导入：" + ReviewStage);
		}

		private void ResetReview()
		{
			if (_savedSides != null)
			{
				_sides[0] = _savedSides[0];
				_sides[1] = _savedSides[1];
				_cfg.PlayerFirst = _savedFirst;
			}
			_savedSides = null;
			_review = null;
			for (int i = 0; i < 2; i++)
			{
				_live[i] = null;
				_private[i] = null;
			}
		}

		private void BeginEnter()
		{
			_lobbyRequested = false;
			_generation++;
			_editing = 0;
			BattleManager.currentBattleResult = null;
			_state = 1;
			_settle = 0;
			_deadline = Time.realtimeSinceStartup + 20f;
			SceneLoader.LoadScene("Battle");
			_ctx.Log.Info(_ctx.T("进场：已请求加载 Battle 场景", "Enter: requested loading the Battle scene"));
		}

		public void Tick()
		{
			if (_state == 1)
			{
				TickEntering();
			}
			else if (_state == 3)
			{
				TickLeaving();
			}
		}

		private void TickEntering()
		{
			if (RoomGuard.Reason() != null)
			{
				Abort(_ctx.T("进场途中进了房间，练习场退出", "Entered a room mid-load; leaving the arena"));
				return;
			}
			if (!BattleReady())
			{
				_settle = 0;
				if (Time.realtimeSinceStartup > _deadline)
				{
					Abort(_ctx.T("等战斗场景超时，练习场退出", "Timed out waiting for the battle scene; leaving the arena"));
				}
				return;
			}
			_settle++;
			if (_settle < 30)
			{
				return;
			}
			try
			{
				Present();
				_state = 2;
				_ctx.Log.Info(_ctx.T("进场：本地对局状态已喂给 Refresh", "Enter: local match state handed to Refresh"));
			}
			catch (Exception exception)
			{
				_ctx.Log.Error(_ctx.T("立起备战界面失败", "Failed to stand up the setup screen"), exception);
				Abort(_ctx.T("立起备战界面失败，详见日志", "Failed to stand up the setup screen; see log"));
			}
		}

		private static bool BattleReady()
		{
			if (SceneLoader.isLoading || SceneLoader.currentSceneName != "Battle")
			{
				return false;
			}
			BattleManager instance = BattleManager.Instance;
			if (instance == null || !instance.replaying || instance.defaultBattleExecuter == null)
			{
				return false;
			}
			BattlePanel battlePanel = ILRPanelBase.FindILRPanel<BattlePanel>();
			if (battlePanel != null)
			{
				return battlePanel.readyLayer != null;
			}
			return false;
		}

		private void Abort(string message)
		{
			_state = 0;
			ResetReview();
			Say(message);
		}

		public void OnLobby()
		{
			if (_state != 0 && _state != 1)
			{
				_state = 0;
				_lobbyRequested = false;
				BattleManager.currentBattleResult = null;
				ResetReview();
				_ctx.Log.Info(_ctx.T("已回到大厅，练习场会话结束", "Back in the lobby; arena session ended"));
			}
		}

		private static int BaseHp(int level)
		{
			return CardFactory.FindLevelConfig((Level)level)?.baseMaxHp ?? 0;
		}

		private static void CopyInts(Dictionary<int, int> from, Dictionary<int, int> to)
		{
			if (from == null)
			{
				return;
			}
			foreach (KeyValuePair<int, int> item in from)
			{
				to[item.Key] = item.Value;
			}
		}

		private static void PutBuff(Dictionary<int, int> buffs, BuffType type, int value)
		{
			if (value > 0)
			{
				buffs[(int)type] = value;
			}
			else if (buffs.ContainsKey((int)type))
			{
				buffs.Remove((int)type);
			}
		}

		private static int ReadBuff(Dictionary<int, int> buffs, BuffType type)
		{
			if (buffs == null || !buffs.TryGetValue((int)type, out var value))
			{
				return 0;
			}
			return value;
		}

		private BattlePlayerData MakePublic(int index, string uid)
		{
			ArenaSide arenaSide = _sides[index];
			BattlePlayerData battlePlayerData = _live[index];
			int extraMaxHp = arenaSide.ExtraMaxHp(BaseHp(arenaSide.Level));
			BattlePlayerData battlePlayerData2 = ((battlePlayerData != null) ? battlePlayerData.DeepClone() : new BattlePlayerData());
			battlePlayerData2.uid = uid;
			battlePlayerData2.username = arenaSide.Name;
			battlePlayerData2.characterId = arenaSide.CharacterId;
			CharacterConfig characterConfig = ConfigManager.GetCharacterConfig(arenaSide.CharacterId);
			if (characterConfig != null && (battlePlayerData == null || battlePlayerData.characterId != arenaSide.CharacterId))
			{
				battlePlayerData2.sect = characterConfig.sect;
			}
			battlePlayerData2.skinNumber = arenaSide.SkinNumber;
			battlePlayerData2.skinColor = arenaSide.SkinColor;
			if (battlePlayerData2.cardBack == 0)
			{
				battlePlayerData2.cardBack = 1;
			}
			battlePlayerData2.level = (Level)arenaSide.Level;
			battlePlayerData2.settled = false;
			battlePlayerData2.isPreReady = false;
			battlePlayerData2.runtimeData = new BattlePlayerRuntimeData();
			if (battlePlayerData == null)
			{
				battlePlayerData2.life = 10;
			}
			battlePlayerData2.extraMaxHp = extraMaxHp;
			int[] array = arenaSide.ChosenTalents();
			battlePlayerData2.talents.Clear();
			for (int i = 0; i < array.Length; i++)
			{
				battlePlayerData2.talents.Add(array[i]);
				int value = arenaSide.ValueOfTalent(array[i]);
				battlePlayerData2.talentTempDatas[array[i]] = value;
			}
			PutBuff(battlePlayerData2.permanentBuffTempDatas, BuffType.TiPo, arenaSide.TiPo);
			PutBuff(battlePlayerData2.permanentBuffTempDatas, BuffType.TiPoShangXian, arenaSide.TiPoMax);
			TalentDetails.WritePublic(arenaSide, battlePlayerData2);
			BattlePlayerLastRoundData battlePlayerLastRoundData = (battlePlayerData2.lastRoundData = new BattlePlayerLastRoundData());
			battlePlayerLastRoundData.level = battlePlayerData2.level;
			battlePlayerLastRoundData.life = battlePlayerData2.life;
			battlePlayerLastRoundData.exp = battlePlayerData2.exp;
			battlePlayerLastRoundData.extraMaxHp = extraMaxHp;
			battlePlayerLastRoundData.unlockGrids = arenaSide.UnlockedGrids;
			for (int j = 0; j < battlePlayerData2.talents.Count; j++)
			{
				battlePlayerLastRoundData.talents.Add(battlePlayerData2.talents[j]);
			}
			CopyInts(battlePlayerData2.talentTempDatas, battlePlayerLastRoundData.talentTempDatas);
			CopyInts(battlePlayerData2.permanentBuffTempDatas, battlePlayerLastRoundData.permanentBuffTempDatas);
			for (int k = 0; k < arenaSide.UnlockedGrids; k++)
			{
				battlePlayerLastRoundData.usedCards.Add(arenaSide.Board[k]);
			}
			for (int l = 0; l < arenaSide.Hand.Count; l++)
			{
				battlePlayerLastRoundData.handCards.Add(arenaSide.Hand[l]);
			}
			battlePlayerLastRoundData.talentDatas = battlePlayerData2.talentDatas;
			BattlePlayerPrivateData battlePlayerPrivateData = MakePrivate(index, uid);
			battlePlayerLastRoundData.privateTalentDatas = battlePlayerPrivateData.talentDatas;
			battlePlayerLastRoundData.talentResonanceData = battlePlayerPrivateData.talentResonanceData;
			battlePlayerLastRoundData.usedKeYinCards = battlePlayerPrivateData.keYinData.usedCards;
			battlePlayerLastRoundData.FZJXCareers = battlePlayerPrivateData.FZJXCareers;
			for (int m = 0; m < arenaSide.Fates.Count; m++)
			{
				battlePlayerLastRoundData.fateStrategies.Add(arenaSide.Fates[m]);
			}
			return battlePlayerData2;
		}

		private static void FillFates(BattlePlayerPrivateData priv, ArenaSide side)
		{
			priv.fateStrategyData.strategies.Clear();
			for (int i = 0; i < side.Fates.Count; i++)
			{
				SelectionData selectionData = new SelectionData();
				selectionData.id = i;
				selectionData.selected = side.Fates[i];
				priv.fateStrategyData.strategies.Add(selectionData);
			}
		}

		private bool AnyFates()
		{
			if (_sides[0].Fates.Count <= 0)
			{
				return _sides[1].Fates.Count > 0;
			}
			return true;
		}

		private int RoundForFates()
		{
			if (_review != null)
			{
				return _review.round;
			}
			int num = 1;
			for (int i = 0; i < 2; i++)
			{
				List<int> fates = _sides[i].Fates;
				for (int j = 0; j < fates.Count; j++)
				{
					FateStrategyConfig fateStrategyConfig = ConfigManager.GetFateStrategyConfig(fates[j]);
					if (fateStrategyConfig != null && fateStrategyConfig.effectRound > num)
					{
						num = fateStrategyConfig.effectRound;
					}
				}
			}
			return num;
		}

		private void Present()
		{
			BottlePanel.Close();
			string uid = GameClientUtil.uid;
			GameStatus gameStatus = new GameStatus();
			gameStatus.round = RoundForFates();
			gameStatus.timer = 999;
			gameStatus.gameMode = GameMode.PracticeMode;
			if (_review != null)
			{
				gameStatus.seasonMec = _review.seasonMec;
			}
			else if (AnyFates())
			{
				gameStatus.seasonMec = SeasonMechanismType.FateStrategy;
			}
			BattlePlayerData battlePlayerData = MakePublic(_editing, uid);
			BattlePlayerData battlePlayerData2 = MakePublic(1 - _editing, "YxArenaDummy");
			battlePlayerData.isAI = false;
			battlePlayerData2.isAI = true;
			battlePlayerData.nextOpponent = "YxArenaDummy";
			battlePlayerData2.nextOpponent = uid;
			gameStatus.battlePlayerDatas.Add(battlePlayerData);
			gameStatus.battlePlayerDatas.Add(battlePlayerData2);
			gameStatus.playerPrivateData = MakePrivate(_editing, uid);
			BattleManager.Instance.Refresh(gameStatus);
			HideReplaceArea();
		}

		private BattlePlayerPrivateData MakePrivate(int index, string uid)
		{
			ArenaSide arenaSide = _sides[index];
			BattlePlayerPrivateData battlePlayerPrivateData = ((_private[index] != null) ? _private[index].DeepClone() : new BattlePlayerPrivateData());
			battlePlayerPrivateData.uid = uid;
			battlePlayerPrivateData.unlockGrids = arenaSide.UnlockedGrids;
			battlePlayerPrivateData.handCards.Clear();
			battlePlayerPrivateData.usedCards.Clear();
			for (int i = 0; i < arenaSide.Hand.Count; i++)
			{
				battlePlayerPrivateData.handCards.Add(arenaSide.Hand[i]);
			}
			for (int j = 0; j < arenaSide.UnlockedGrids; j++)
			{
				battlePlayerPrivateData.usedCards.Add(arenaSide.Board[j]);
			}
			FillFates(battlePlayerPrivateData, arenaSide);
			TalentDetails.WritePrivate(arenaSide, battlePlayerPrivateData);
			return battlePlayerPrivateData;
		}

		public void HideReplaceArea()
		{
			try
			{
				Transform transform = FindCardPanel()?.GetReplaceTrans();
				if (transform != null)
				{
					if (transform.gameObject.activeSelf)
					{
						transform.gameObject.SetActive(value: false);
					}
					return;
				}
				if (!_warnedReplace)
				{
					_ctx.Log.Warn(_ctx.T("没找到换牌区，没能隐藏它——别往换牌区拖牌", "Couldn't find the replace area to hide it — don't drag cards into it"));
				}
				_warnedReplace = true;
			}
			catch (Exception ex)
			{
				if (!_warnedReplace)
				{
					_ctx.Log.Warn(_ctx.T("隐藏换牌区失败：", "Failed to hide the replace area: ") + ex.Message);
				}
				_warnedReplace = true;
			}
		}

		private static CardPanel FindCardPanel()
		{
			return ILRPanelBase.FindILRPanel<BattlePanel>()?.FindILRSubPanel<CardPanel>();
		}

		private static BattlePlayerData SelfPublicData(GameStatus gs)
		{
			BattlePanel battlePanel = ILRPanelBase.FindILRPanel<BattlePanel>();
			if (battlePanel != null && battlePanel.readyLayer != null && battlePanel.readyLayer.playerSelfInfoItem != null && battlePanel.readyLayer.playerSelfInfoItem.battlePlayerData != null)
			{
				return battlePanel.readyLayer.playerSelfInfoItem.battlePlayerData;
			}
			return gs.GetSelfBattlePlayerData() ?? gs.GetMainPlayerData();
		}

		private bool Capture()
		{
			return Capture(talentValues: true);
		}

		private bool Capture(bool talentValues)
		{
			GameStatus gameStatus = BattleManager.Instance?.currentGameStatus;
			CardPanel cardPanel = FindCardPanel();
			if (gameStatus == null || cardPanel == null)
			{
				return false;
			}
			BottlePanel.Capture();
			ArenaSide arenaSide = _sides[_editing];
			arenaSide.Hand.Clear();
			List<CardItem> handCards = cardPanel.GetHandCards();
			for (int i = 0; i < handCards.Count; i++)
			{
				if (handCards[i] != null && handCards[i].cardInfo != null)
				{
					arenaSide.Hand.Add(handCards[i].cardInfo.id);
				}
			}
			int[] array = new int[8];
			int num = 0;
			List<CardGrid> cardGrids = cardPanel.GetCardGrids();
			for (int j = 0; j < cardGrids.Count; j++)
			{
				if (num >= 8)
				{
					break;
				}
				if (cardGrids[j] != null && cardGrids[j].unlocked)
				{
					CardItem card = cardGrids[j].GetCard();
					array[num] = ((card != null && card.cardInfo != null) ? card.cardInfo.id : 0);
					num++;
				}
			}
			arenaSide.SetBoard(array);
			BattlePlayerData battlePlayerData = SelfPublicData(gameStatus);
			if (battlePlayerData != null)
			{
				_live[_editing] = battlePlayerData;
				_private[_editing] = gameStatus.playerPrivateData;
				if (talentValues)
				{
					TalentDetails.Read(arenaSide, battlePlayerData, gameStatus.playerPrivateData);
				}
				arenaSide.CaptureExtra(BaseHp(arenaSide.Level), battlePlayerData.extraMaxHp);
				arenaSide.TiPo = ReadBuff(battlePlayerData.permanentBuffTempDatas, BuffType.TiPo);
				arenaSide.TiPoMax = ReadBuff(battlePlayerData.permanentBuffTempDatas, BuffType.TiPoShangXian);
				int[] array2 = (talentValues ? arenaSide.ChosenTalents() : new int[0]);
				for (int k = 0; k < array2.Length; k++)
				{
					if (battlePlayerData.talentTempDatas != null && battlePlayerData.talentTempDatas.TryGetValue(array2[k], out var value))
					{
						arenaSide.CaptureTalentValue(array2[k], value);
					}
				}
			}
			return true;
		}

		private void PushCards()
		{
			BattleManager instance = BattleManager.Instance;
			GameStatus currentGameStatus = instance.currentGameStatus;
			ArenaSide arenaSide = _sides[_editing];
			BattlePlayerPrivateData playerPrivateData = currentGameStatus.playerPrivateData;
			playerPrivateData.handCards.Clear();
			for (int i = 0; i < arenaSide.Hand.Count; i++)
			{
				playerPrivateData.handCards.Add(arenaSide.Hand[i]);
			}
			playerPrivateData.usedCards.Clear();
			for (int j = 0; j < arenaSide.UnlockedGrids; j++)
			{
				playerPrivateData.usedCards.Add(arenaSide.Board[j]);
			}
			PlayerData playerData = new PlayerData();
			playerData.publicData = SelfPublicData(currentGameStatus);
			playerData.privateData = playerPrivateData;
			instance.RefreshMainPlayerInfo(playerData);
		}

		public bool Deal(int cardId)
		{
			if (!InPlacement)
			{
				Say(_ctx.T("只能在备战界面发牌", "Cards can only be dealt on the setup screen"));
				return false;
			}
			try
			{
				if (!Capture())
				{
					return false;
				}
				ArenaSide arenaSide = _sides[_editing];
				if (arenaSide.Hand.Count >= 50)
				{
					string text = N(50);
					Say(_ctx.T("手牌满了（" + text + " 张）", "Hand is full (" + text + " cards)"));
					return false;
				}
				arenaSide.Hand.Add(cardId);
				PushCards();
				return true;
			}
			catch (Exception exception)
			{
				_ctx.Log.Error(_ctx.T("发牌失败 id=", "Deal failed id=") + N(cardId), exception);
				return false;
			}
		}

		public void ClearHand()
		{
			if (!InPlacement)
			{
				Say(_ctx.T("只能在备战界面清手牌", "The hand can only be cleared on the setup screen"));
				return;
			}
			try
			{
				if (Capture())
				{
					_sides[_editing].Hand.Clear();
					PushCards();
				}
			}
			catch (Exception exception)
			{
				_ctx.Log.Error(_ctx.T("清空手牌失败", "Clear hand failed"), exception);
			}
		}

		private bool BeginEdit(string what)
		{
			if (!InPlacement)
			{
				Say(_ctx.T("只能在备战界面", "Only on the setup screen: ") + what);
				return false;
			}
			BottlePanel.Close();
			return Capture();
		}

		public void ReapplySettings()
		{
			if (!InPlacement)
			{
				return;
			}
			try
			{
				if (Capture(talentValues: false))
				{
					Present();
					Persist();
				}
			}
			catch (Exception exception)
			{
				_ctx.Log.Error(_ctx.T("重新应用仙命设置失败", "Reapplying talent settings failed"), exception);
			}
		}

		public void SwitchSide()
		{
			try
			{
				if (BeginEdit(_ctx.T("切换编辑方", "switch editing side")))
				{
					_editing = 1 - _editing;
					_generation++;
					Present();
					Persist();
					_ctx.Log.Info(_ctx.T("现在编辑：", "Now editing: ") + _sides[_editing].Name);
				}
			}
			catch (Exception exception)
			{
				_ctx.Log.Error(_ctx.T("切换编辑方失败", "Switch editing side failed"), exception);
			}
		}

		public void NextLevel()
		{
			try
			{
				if (BeginEdit(_ctx.T("改境界", "change realm")))
				{
					ArenaSide arenaSide = _sides[_editing];
					arenaSide.Level = ((arenaSide.Level >= 6 || arenaSide.Level < 1) ? 1 : (arenaSide.Level + 1));
					if (_editing == 0 && !FromReview)
					{
						_cfg.Level = arenaSide.Level;
					}
					Present();
				}
			}
			catch (Exception exception)
			{
				_ctx.Log.Error(_ctx.T("改境界失败", "Change realm failed"), exception);
			}
		}

		public void SetHp(string text)
		{
			try
			{
				if (!BeginEdit(_ctx.T("改血量", "change HP")))
				{
					return;
				}
				ArenaSide arenaSide = _sides[_editing];
				if (!arenaSide.SetHp(text))
				{
					Say(_ctx.T("血量要填最多 18 位的整数（0 = 跟随境界；超过 20 亿的部分进 64 位血量池）", "HP must be an integer of at most 18 digits (0 = follow realm; the part above ~2 billion goes into the 64-bit HP pool)"));
					return;
				}
				if (_editing == 1 && !FromReview)
				{
					_cfg.DummyHp = ((arenaSide.Hp > 999999) ? 999999 : arenaSide.Hp);
				}
				Present();
			}
			catch (Exception exception)
			{
				_ctx.Log.Error(_ctx.T("改血量失败", "Change HP failed"), exception);
			}
		}

		public void SetTiPo(string text)
		{
			try
			{
				if (BeginEdit(_ctx.T("改体魄", "change body")))
				{
					if (!_sides[_editing].SetTiPo(text))
					{
						string text2 = N(999999);
						Say(_ctx.T("体魄要填 0–" + text2 + " 的整数", "Body must be an integer 0–" + text2));
					}
					else
					{
						Present();
					}
				}
			}
			catch (Exception exception)
			{
				_ctx.Log.Error(_ctx.T("改体魄失败", "Change body failed"), exception);
			}
		}

		public void SetTiPoMax(string text)
		{
			try
			{
				if (BeginEdit(_ctx.T("改体魄上限", "change body max")))
				{
					if (!_sides[_editing].SetTiPoMax(text))
					{
						string text2 = N(999999);
						Say(_ctx.T("体魄上限要填 0–" + text2 + " 的整数", "Body max must be an integer 0–" + text2));
					}
					else
					{
						Present();
					}
				}
			}
			catch (Exception exception)
			{
				_ctx.Log.Error(_ctx.T("改体魄上限失败", "Change body max failed"), exception);
			}
		}

		public int[] HandOf(int side)
		{
			if (side < 0 || side > 1)
			{
				return new int[0];
			}
			List<int> hand = _sides[side].Hand;
			int[] array = new int[hand.Count];
			for (int i = 0; i < hand.Count; i++)
			{
				array[i] = hand[i];
			}
			return array;
		}

		public long EditingTotalHp()
		{
			ArenaSide obj = _sides[_editing];
			return obj.TrueTotalHp(BaseHp(obj.Level));
		}

		public void Fight()
		{
			if (!InPlacement)
			{
				Say(_ctx.T("只能在备战界面开打", "You can only start the fight on the setup screen"));
				return;
			}
			try
			{
				BottlePanel.Close();
				if (!Capture())
				{
					return;
				}
				int num = _sides[0].PlacedCount();
				int num2 = _sides[1].PlacedCount();
				if (num + num2 == 0)
				{
					Say(_ctx.T("场上一张牌都没有，先摆牌", "No cards on the board; place some first"));
					return;
				}
				string uid = GameClientUtil.uid;
				BattleResult battleResult = new BattleResult();
				battleResult.battleScene = 1;
				battleResult.battleSubScene = 1;
				battleResult.battleTime = 1800;
				battleResult.round = RoundForFates();
				battleResult.gameMode = GameMode.PracticeMode;
				battleResult.seasonMec = ((_review != null) ? _review.seasonMec : (AnyFates() ? SeasonMechanismType.FateStrategy : SeasonMechanismType.None));
				battleResult.p1.publicData = MakePublic(0, uid);
				battleResult.p2.publicData = MakePublic(1, "YxArenaDummy");
				battleResult.p1.publicData.isAI = false;
				battleResult.p2.publicData.isAI = true;
				battleResult.p1.privateData = MakePrivate(0, uid);
				battleResult.p2.privateData = MakePrivate(1, "YxArenaDummy");
				Persist();
				battleResult.firstPlayerId = (_cfg.PlayerFirst ? uid : "YxArenaDummy");
				battleResult.homePlayerId = uid;
				battleResult.mainViewId = uid;
				_battleIndex++;
				LastSeed = ParamStream.MixSeed(Time.frameCount, _battleIndex);
				int[] array = ParamStream.Generate(LastSeed, 4096);
				for (int i = 0; i < array.Length; i++)
				{
					battleResult.battleParams.Add(array[i]);
				}
				BattleManager.currentBattleResult = battleResult;
				BattleManager.Instance.PlayBattle();
				string text = _ctx.T(N(num) + " 张 / " + DamageTally.Group(TrueTotalHp(0)) + " 血", N(num) + " cards / " + DamageTally.Group(TrueTotalHp(0)) + " HP");
				string text2 = _ctx.T(N(num2) + " 张 / " + DamageTally.Group(TrueTotalHp(1)) + " 血", N(num2) + " cards / " + DamageTally.Group(TrueTotalHp(1)) + " HP");
				string text3 = LastSeed.ToString(CultureInfo.InvariantCulture);
				_ctx.Log.Info(_ctx.T("开打：我方 " + text + "，对手 " + text2 + "，种子 " + text3, "Fight: mine " + text + ", foe " + text2 + ", seed " + text3));
			}
			catch (Exception exception)
			{
				_ctx.Log.Error(_ctx.T("开打失败", "Fight failed"), exception);
			}
		}

        public void ReturnToPlacement()
        {
            if (!Active || !InBattlePhase || SceneLoader.isLoading) return;
            BattleReplayPanel replay = ILRPanelBase.FindILRPanel<BattlePanel>()?.FindILRSubPanel<BattleReplayPanel>();
            if (replay == null)
            {
                Say(_ctx.T("暂时无法回到摆牌，请稍后重试", "Couldn't return to setup yet; please retry shortly"));
                return;
            }
            // Native PracticeMode exit stops executing battles (including paused
            // battles), waits for them to finish, then restores the ready layer.
            // Keep the session active so leaving does not continue to the lobby.
            replay.OnExitBtnClick();
        }

		public void Leave()
		{
			if (_state == 0)
			{
				Say(_ctx.T("不在练习场里", "Not in the arena"));
			}
			else
			{
				if (_state == 3)
				{
					return;
				}
				Autosave();
				string text = RoomGuard.Reason();
				if (text == null)
				{
					_state = 3;
					_lobbyRequested = false;
					_deadline = Time.realtimeSinceStartup + 10f;
					try
					{
						BattleManager instance = BattleManager.Instance;
						if (instance != null && (IsExecuting(instance) || instance.currentScene == SceneType.斗法阶段))
						{
							BattleReplayPanel battleReplayPanel = ILRPanelBase.FindILRPanel<BattlePanel>()?.FindILRSubPanel<BattleReplayPanel>();
							if (battleReplayPanel == null)
							{
								LeaveFailed();
							}
							else
							{
								battleReplayPanel.OnExitBtnClick();
							}
						}
						else
						{
							TickLeaving();
						}
						return;
					}
					catch (Exception exception)
					{
						_ctx.Log.Error(_ctx.T("离场失败", "Leave failed"), exception);
						LeaveFailed();
						return;
					}
				}
				_state = 0;
				ResetReview();
				Say(text);
			}
		}

		private static bool IsExecuting(BattleManager bm)
		{
			if (bm.defaultBattleExecuter != null && bm.defaultBattleExecuter.isExecuting)
			{
				return true;
			}
			for (int i = 0; i < bm.allBattleExecuters.Count; i++)
			{
				if (bm.allBattleExecuters[i] != null && bm.allBattleExecuters[i].isExecuting)
				{
					return true;
				}
			}
			return false;
		}

		private void TickLeaving()
		{
			string text = RoomGuard.Reason();
			if (text != null)
			{
				_state = 0;
				ResetReview();
				Say(text);
			}
			else if (!SceneLoader.isLoading && SceneLoader.currentSceneName == "Lobby")
			{
				OnLobby();
			}
			else if (Time.realtimeSinceStartup > _deadline)
			{
				LeaveFailed();
			}
			else if (!_lobbyRequested && !SceneLoader.isLoading)
			{
				BattleManager instance = BattleManager.Instance;
				if (instance != null && BattleReady() && !IsExecuting(instance) && instance.currentScene != SceneType.斗法阶段)
				{
					ToLobby();
				}
			}
		}

		private void LeaveFailed()
		{
			_lobbyRequested = false;
			_state = 2;
			Say(_ctx.T("暂时无法返回主界面，请等战斗退出或场景加载完成后重试", "Couldn't return to the lobby yet; wait for the battle or scene transition to finish, then retry"));
		}

		private void ToLobby()
		{
			try
			{
				BattleManager instance = BattleManager.Instance;
				BattleReplayPanel battleReplayPanel = ILRPanelBase.FindILRPanel<BattlePanel>()?.FindILRSubPanel<BattleReplayPanel>();
				if (instance == null || battleReplayPanel == null || DebugGamePanel.inDebug || DebugBattleCommands.showReplay || instance.isJudge)
				{
					LeaveFailed();
					return;
				}
				GameStatus currentGameStatus = instance.currentGameStatus;
				GameMode gameMode = currentGameStatus?.gameMode ?? GameMode.InvalidGameMode;
				BattleBackType backType = BattleManager.backType;
				_lobbyRequested = true;
				_deadline = Time.realtimeSinceStartup + 20f;
				BattleManager.backType = BattleBackType.标准;
				try
				{
					if (currentGameStatus != null)
					{
						currentGameStatus.gameMode = GameMode.InvalidGameMode;
					}
					battleReplayPanel.OnExitBtnClick();
				}
				finally
				{
					if (currentGameStatus != null)
					{
						currentGameStatus.gameMode = gameMode;
					}
					if (!SceneLoader.isLoading)
					{
						BattleManager.backType = backType;
					}
				}
				_ctx.Log.Info(_ctx.T("离场：已请求回大厅", "Leave: requested return to lobby"));
			}
			catch (Exception exception)
			{
				_ctx.Log.Error(_ctx.T("回大厅失败", "Return to lobby failed"), exception);
				LeaveFailed();
			}
		}
	}
}
