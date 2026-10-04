using System;
using System.Globalization;
using System.Text;
using Proto;
using UnityEngine;
using UnityEngine.UI;
using Yx.ModSdk;
using Yx.ModSdk.Hooks;
using Yx.Shared;

namespace YxArena.Game
{
	public sealed class BattleHooks
	{
		private readonly ModContext _ctx;

		private readonly DamageTally _tally;

		private readonly StepController _step;

		private readonly Overflow _overflow;

		private readonly ArenaSession _session;

		private readonly BigHpPool[] _pools = new BigHpPool[2]
		{
			new BigHpPool(),
			new BigHpPool()
		};

		private readonly long[] _totals = new long[2];

		private readonly OncePerOwner _talentIcons = new OncePerOwner();

		private HookGroup _group;

		private long _hitTrue;

		private long _hitPoolLoss;

		private bool _poolHooked;

		private bool _stepHooked;

		private bool _damageHooked;

		private bool _structReadBroken;

		private bool _warned;

		private bool _pending;

		private bool _reported = true;

		private int _pendingSide;

		private bool _reviewTrace;

		private int _traceCards;

		private int _traceTurns;

		private BattleExecuter _traceExecuter;

		private long _pendingDamage;

		public bool StepAvailable => _stepHooked;

		public bool TallyAvailable => _damageHooked;

		public static bool IsPaused => Time.timeScale == 0f;

		public BattleHooks(ModContext ctx, DamageTally tally, StepController step, Overflow overflow, ArenaSession session)
		{
			_session = session;
			_ctx = ctx;
			_tally = tally;
			_step = step;
			_overflow = overflow;
		}

		public void Install()
		{
			_group = _ctx.Hooks.Group("战斗");
			bool flag = Try("BattleManager", "PlayBattle", 1, OnPlayBattle);
			bool flag2 = Try("CardActionBase", "CheckCardCost", 2, OnCardAboutToPlay);
			_stepHooked = flag && flag2;
			Try("BattleCharacter", "OnTurnStarted", 0, OnTurnStarted);
			bool flag3 = Try("BattleCharacter", "ApplyDamage", 2, OnApplyDamage);
			bool flag4 = _group.Postfix("BattleCharacter", "OnHit", 1, OnHitDone) != null;
			_damageHooked = flag && flag2 && flag3 && flag4;
			Try("BattleCharacterUI", "AddTalentBuff", 1, OnAddTalentIcon);
			Try("BattleCharacterUI", "ResetBuffItem", 0, OnResetIcons);
			bool flag5 = Try("BattleCharacter", "OnHit", 1, OnHitStart);
			_poolHooked = _group.Postfix("BattleCharacter", "ModifyHp", 5, OnHpModified) != null && flag && flag5;
			if (!_poolHooked)
			{
				_ctx.Log.Warn(_ctx.T("64 位血量池不可用：血量超过 20 亿的部分不生效", "64-bit HP pool unavailable: HP above ~2 billion won't take effect"));
			}
		}

		private bool Try(string type, string method, int paramCount, Func<HookContext, bool> handler)
		{
			return _group.Prefix(type, method, paramCount, handler) != null;
		}

		private void Broken(string where, Exception e)
		{
			if (!_warned)
			{
				_ctx.Log.Error(_ctx.T("战斗钩子 " + where + " 出错（之后不再报）", "Battle hook " + where + " failed (won't report again)"), e);
			}
			_warned = true;
		}

		private static int SideOf(object character)
		{
			if (!(character is BattleCharacter battleCharacter) || battleCharacter.battleExecuter == null)
			{
				return -1;
			}
			return (battleCharacter != battleCharacter.battleExecuter.leftCharacter) ? 1 : 0;
		}

		private bool OnPlayBattle(HookContext h)
		{
			if (!ArenaSession.Active)
			{
				return true;
			}
			LogReport();
			_reviewTrace = _session.FromReview;
			_traceCards = 0;
			_traceTurns = 0;
			_traceExecuter = null;
			if (_reviewTrace)
			{
				try
				{
					Proto.BattleResult result = BattleManager.currentBattleResult;
					if (result != null)
					{
						string first = result.firstPlayerId == result.mainViewId ? "我方" : "对手";
						_ctx.Log.Info("对拍开局：轮次 " + TraceNumber(result.round) + "，先手 " + first);
					}
				}
				catch (Exception) { _ctx.Log.Warn("对拍开局：无法读取先手"); }
			}
			_reported = false;
			_tally.Reset();
			_step.OnBattleStart();
			_pending = false;
			SatMath.ClearTrue();
			_talentIcons.Reset();
			_hitTrue = 0L;
			_hitPoolLoss = 0L;
			for (int i = 0; i < 2; i++)
			{
				_totals[i] = (_poolHooked ? _session.TrueTotalHp(i) : 0);
				_pools[i].Reset(_totals[i], _session.GameTotalHp(i));
			}
			return true;
		}

		private bool OnCardAboutToPlay(HookContext h)
		{
			if (!ArenaSession.Active)
			{
				return true;
			}
			try
			{
				FlushPending();
				CardActionBase cardActionBase = h.Instance as CardActionBase;
				if (_reviewTrace && cardActionBase != null && cardActionBase.cardConfig != null && h.Args != null && h.Args.Length > 0)
				{
					_traceCards++;
					TraceState("出牌前 #" + TraceNumber(_traceCards) + " card=" + TraceNumber(cardActionBase.cardConfig.id), h.Args[0] as BattleCharacter);
				}
				int side = SideOf((h.Args != null && h.Args.Length != 0) ? h.Args[0] : null);
				string cardName = ((cardActionBase != null && cardActionBase.cardConfig != null) ? cardActionBase.cardConfig.name : "");
				_tally.BeginCard(side, cardName);
				SatMath.ClearTrue();
				if (cardActionBase != null && cardActionBase.cardConfig != null)
				{
					_overflow.OnCardAboutToPlay(cardActionBase.cardConfig.id);
				}
				if (_step.ShouldPauseBeforeCard())
				{
					SetPaused(paused: true);
				}
			}
			catch (Exception e)
			{
				Broken("CheckCardCost", e);
			}
			return true;
		}

		private bool OnTurnStarted(HookContext h)
		{
			if (!ArenaSession.Active)
			{
				return true;
			}
			try
			{
				FlushPending();
				_tally.BeginTurn(SideOf(h.Instance));
				if (_reviewTrace)
				{
					_traceTurns++;
					TraceState("回合前 #" + TraceNumber(_traceTurns), h.Instance as BattleCharacter);
				}
			}
			catch (Exception e)
			{
				Broken("OnTurnStarted", e);
			}
			return true;
		}

		private bool OnApplyDamage(HookContext h)
		{
			if (!ArenaSession.Active)
			{
				return true;
			}
			try
			{
				FlushPending();
				if (h.Args == null || h.Args.Length < 2)
				{
					return true;
				}
				if (h.Args[0] == h.Instance)
				{
					return true;
				}
				_pendingSide = SideOf(h.Instance);
				_pendingDamage = -1L;
				if (!_structReadBroken)
				{
					try
					{
						_pendingDamage = TrueDamage(((DamageInfo)h.Args[1]).damage);
					}
					catch (Exception)
					{
						_structReadBroken = true;
						_ctx.Log.Warn(_ctx.T("伤害统计：读不到减防前的伤害值，之后「总伤害」按实际掉血算", "Damage tally: can't read pre-defense damage; \"Total dmg\" will be counted as actual HP lost"));
					}
				}
				_pending = true;
			}
			catch (Exception e)
			{
				Broken("ApplyDamage", e);
			}
			return true;
		}

		private static long TrueDamage(int damage)
		{
			long num = damage;
			if (damage == int.MaxValue && SatMath.HasTrue && SatMath.TrueValue > num)
			{
				return SatMath.TrueValue;
			}
			return num;
		}

		private bool OnResetIcons(HookContext h)
		{
			_talentIcons.Forget(h.Instance);
			return true;
		}

		private bool OnAddTalentIcon(HookContext h)
		{
			if (!ArenaSession.Active)
			{
				return true;
			}
			if (h.Args == null || h.Args.Length < 1 || !(h.Args[0] is int))
			{
				return true;
			}
			if (_talentIcons.FirstTime(h.Instance, (int)h.Args[0]))
			{
				return true;
			}
			h.Skip(null);
			return false;
		}

		private bool OnHitStart(HookContext h)
		{
			if (!ArenaSession.Active)
			{
				return true;
			}
			_hitTrue = 0L;
			_hitPoolLoss = 0L;
			if (_structReadBroken || h.Args == null || h.Args.Length < 1)
			{
				return true;
			}
			try
			{
				_hitTrue = TrueDamage(((DamageInfo)h.Args[0]).damage);
			}
			catch (Exception)
			{
				_structReadBroken = true;
			}
			return true;
		}

		private void OnHpModified(HookContext h)
		{
			if (!ArenaSession.Active)
			{
				return;
			}
			try
			{
				int num = SideOf(h.Instance);
				if (num < 0 || !_pools[num].Active || !(h.Instance is BattleCharacter battleCharacter) || battleCharacter.battleTempData == null)
				{
					return;
				}
				BigHpPool bigHpPool = _pools[num];
				int hp = battleCharacter.battleTempData.hp;
				long num2 = hp;
				if (hp == int.MinValue)
				{
					if (_hitTrue > 0)
					{
						num2 = bigHpPool.Hp - _hitTrue;
					}
					else if (SatMath.HasTrue && SatMath.TrueValue < num2)
					{
						num2 = SatMath.TrueValue;
					}
				}
				int num3 = bigHpPool.Normalize(num2);
				_hitPoolLoss += bigHpPool.LastLoss;
				if (num3 != hp)
				{
					battleCharacter.battleTempData.hp = num3;
					if (battleCharacter.characterUI != null)
					{
						battleCharacter.characterUI.hp = num3;
					}
				}
			}
			catch (Exception e)
			{
				Broken("ModifyHp", e);
			}
		}

		public string Report(int topCards)
		{
			StringBuilder stringBuilder = new StringBuilder();
			for (int i = 0; i < 2; i++)
			{
				if (i > 0)
				{
					stringBuilder.Append('\n').Append('\n');
				}
				stringBuilder.Append(_tally.Render(i, _session.SideName(i), topCards));
				string text = HpLine(i, _session.SideName(i));
				if (text.Length > 0)
				{
					stringBuilder.Append('\n').Append(text);
				}
			}
			return stringBuilder.ToString();
		}

		public void LogReport()
		{
			if (!_reported)
			{
				_reported = true;
				if (_reviewTrace && _traceExecuter != null)
				{
					TraceState("结束或退出", _traceExecuter.leftCharacter);
				}
				_reviewTrace = false;
				_traceExecuter = null;
				if (_tally.TotalDamage(0) + _tally.TotalDamage(1) > 0)
				{
					string text = Report(12).Replace('\n'.ToString(), " ｜ ");
					string text2 = SatMath.Overflows.ToString(CultureInfo.InvariantCulture);
					_ctx.Log.Info(_ctx.T("伤害统计：" + text + " ｜ 溢出 " + text2 + " 次（累计）", "Damage tally: " + text + " ｜ overflows " + text2 + " (cumulative)"));
				}
			}
		}

		private static string TraceNumber(int value)
		{
			return value.ToString(CultureInfo.InvariantCulture);
		}

		private static string TraceSide(BattleCharacter character)
		{
			if (character == null || character.battleTempData == null) return "未就绪";
			BattleTempData data = character.battleTempData;
			return "hp=" + TraceNumber(data.hp) + "/" + TraceNumber(data.maxHp)
				+ ",def=" + TraceNumber(data.def) + ",anima=" + TraceNumber(data.anima)
				+ ",body=" + TraceNumber(character.GetBuffValue(BuffType.TiPo))
				+ ",bodyCap=" + TraceNumber(character.GetBuffValue(BuffType.TiPoShangXian))
				+ ",agility=" + TraceNumber(character.GetBuffValue(BuffType.ShenFa))
				+ ",force=" + TraceNumber(character.GetBuffValue(BuffType.QiShi))
				+ ",injury=" + TraceNumber(character.GetBuffValue(BuffType.NeiShang))
				+ ",earth=" + TraceNumber(character.GetBuffValue(BuffType.JiHuoTuLing))
				+ ",chase=" + TraceNumber(character.GetBuffValue(BuffType.ZhuChenBu));
		}

		private void TraceState(string stage, BattleCharacter actor)
		{
			if (_traceCards > 256 || _traceTurns > 128 || actor == null || actor.battleExecuter == null) return;
			try
			{
				_traceExecuter = actor.battleExecuter;
				string side = actor == _traceExecuter.leftCharacter ? "我方" : "对手";
				_ctx.Log.Info("对拍 " + stage + " actor=" + side + " | L " + TraceSide(_traceExecuter.leftCharacter)
					+ " | R " + TraceSide(_traceExecuter.rightCharacter));
			}
			catch (Exception)
			{
				_reviewTrace = false;
				_ctx.Log.Warn("逐牌对拍记录不可用，本场不再记录");
			}
		}

		public string HpLine(int side, string name)
		{
			if (side < 0 || side > 1 || !_pools[side].Active)
			{
				return "";
			}
			long num = _pools[side].Effective;
			if (num < 0)
			{
				num = 0L;
			}
			return name + _ctx.T("\u3000真实血量 ", "  True HP ") + DamageTally.Group(num) + " / " + DamageTally.Group(_totals[side]);
		}

		private void OnHitDone(HookContext h)
		{
			if (!ArenaSession.Active || !_pending)
			{
				return;
			}
			try
			{
				long num = ((h.Result is int) ? TrueDamage((int)h.Result) : 0);
				int num2 = SideOf(h.Instance);
				if (num2 >= 0 && _pools[num2].Active)
				{
					num = _hitPoolLoss;
				}
				_hitTrue = 0L;
				_tally.AddDamage(_pendingSide, (_pendingDamage >= 0) ? _pendingDamage : num, num);
				_pending = false;
			}
			catch (Exception e)
			{
				Broken("OnHit", e);
			}
		}

		private void FlushPending()
		{
			if (_pending)
			{
				_pending = false;
				if (_pendingDamage > 0)
				{
					_tally.AddDamage(_pendingSide, _pendingDamage, 0L);
				}
			}
		}

		private static Button FindPlayButton()
		{
			BattleReplayPanel battleReplayPanel = ILRPanelBase.FindILRPanel<BattlePanel>()?.FindILRSubPanel<BattleReplayPanel>();
			if (battleReplayPanel == null || battleReplayPanel.panel == null || !battleReplayPanel.panel.isShow)
			{
				return null;
			}
			return battleReplayPanel.FindComponent<Button>("PlayButton");
		}

		private void SetPaused(bool paused)
		{
			if (IsPaused != paused)
			{
				Button button = FindPlayButton();
				if (!(button == null) && button.IsActive() && button.IsInteractable())
				{
					button.onClick.Invoke();
				}
			}
		}

		public void Step()
		{
			if (_stepHooked && _step.RequestStep(IsPaused))
			{
				SetPaused(paused: false);
			}
		}
	}
}
