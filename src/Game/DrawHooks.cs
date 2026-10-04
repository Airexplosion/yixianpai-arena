using System;
using System.Collections.Generic;
using System.Globalization;
using Proto;
using Yx.ModSdk;
using Yx.ModSdk.Hooks;
using Yx.Shared;
using YxArena.Draws;

namespace YxArena.Game
{
	public sealed class DrawHooks
	{
		private readonly ModContext _ctx;

		private readonly ArenaSession _session;

		private readonly Dictionary<int, CardFacts> _facts = new Dictionary<int, CardFacts>();

		private DrawPlanner _planner;

		private bool _valuePending;

		private bool _guaXiang;

		private int _installed;

		public const int HookCount = 5;

		private HookGroup _group;

		public bool Enabled => _installed == 5;

		public DrawHooks(ModContext ctx, ArenaSession session)
		{
			_ctx = ctx;
			_session = session;
		}

		public void Install()
		{
			Try("BattleManager", "PlayBattle", 1, OnPlayBattle);
			Try("CardActionBase", "Execute", 2, OnExecute);
			Try("CardActionBase", "ExecuteEffect", 3, OnExecuteEffect);
			Try("BattleCharacter", "GetNextRandomValue", 1, OnNextRandomValue);
			Try("BattleCharacter", "GetNextParam", 0, OnNextParam);
			if (!Enabled)
			{
				_ctx.Log.Warn(_ctx.T("本地随机数没有启用：随机出牌 / 随机负面状态 / X～Y 数值这几类牌在练习场里会不正常", "Local RNG not enabled: random-play / random-debuff / X–Y value cards will misbehave in the arena"));
			}
		}

		private void Try(string type, string method, int paramCount, Func<HookContext, bool> handler)
		{
			if (_group == null)
			{
				_group = _ctx.Hooks.Group("本地随机数");
			}
			if (_group.Prefix(type, method, paramCount, handler) != null)
			{
				_installed++;
			}
		}

		public string Summary()
		{
			if (!Enabled || _planner == null)
			{
				if (!Enabled)
				{
					return _ctx.T("本地随机数未启用", "Local RNG off");
				}
				return "";
			}
			string text = _planner.RawDraws.ToString(CultureInfo.InvariantCulture);
			string text2 = _planner.ValueDraws.ToString(CultureInfo.InvariantCulture);
			return _ctx.T("取值 " + text + " / " + text2, "Draws " + text + " / " + text2);
		}

		private void EnsurePlanner()
		{
			if (_planner == null)
			{
				// Kept hands can contain cards from a replay's older season.
				List<CardConfig> cardConfigs = ConfigManager.GetCardConfigs((SeasonMechanismType)(-1), true);
				CardFacts[] array = new CardFacts[cardConfigs.Count];
				for (int i = 0; i < cardConfigs.Count; i++)
				{
					array[i] = ToFacts(cardConfigs[i]);
					_facts[array[i].Id] = array[i];
				}
				_planner = new DrawPlanner(array);
				_ctx.Log.Info(_ctx.T("本地随机数：已载入 " + array.Length.ToString(CultureInfo.InvariantCulture) + " 张牌的配置", "Local RNG: loaded configs for " + array.Length.ToString(CultureInfo.InvariantCulture) + " cards"));
			}
		}

		public static CardFacts ToFacts(CardConfig c)
		{
			CardFacts cardFacts = new CardFacts();
			cardFacts.Id = c.id;
			cardFacts.Name = c.name ?? "";
			cardFacts.Desc = c.desc ?? "";
			cardFacts.Sect = (int)c.sect;
			cardFacts.Career = (int)c.career;
			cardFacts.Level = (int)c.level;
			cardFacts.Rarity = c.rarity;
			cardFacts.Subcategory = (int)c.subcategory;
			cardFacts.Owner = c.owner;
            int seasons = c.seasonMechanics != null ? c.seasonMechanics.Count : 0;
            cardFacts.SeasonMechanics = new int[seasons];
            for (int i = 0; i < seasons; i++) cardFacts.SeasonMechanics[i] = c.seasonMechanics[i];
			cardFacts.Hidden = c.hidden;
			cardFacts.Obsolete = c.obsolete;
			cardFacts.ActionAgain = c.actionAgain;
			cardFacts.Attack = c.attack;
			cardFacts.RandomAttack = c.randomAttack;
			cardFacts.AttackCount = c.attackCount;
			cardFacts.Def = c.def;
			cardFacts.RandomDef = c.randomDef;
			int num = ((c.otherParams != null) ? c.otherParams.Count : 0);
			cardFacts.OtherParams = new int[num];
			for (int i = 0; i < num; i++)
			{
				cardFacts.OtherParams[i] = c.otherParams[i];
			}
			return cardFacts;
		}

		private CardFacts FactsOf(CardConfig config)
		{
			if (config == null)
			{
				return null;
			}
			if (_facts.TryGetValue(config.id, out var value))
			{
				return value;
			}
			value = ToFacts(config);
			_facts[config.id] = value;
			return value;
		}

		private bool Ready()
		{
			if (!Enabled || !ArenaSession.Active)
			{
				return false;
			}
			EnsurePlanner();
			return true;
		}

		private bool OnPlayBattle(HookContext h)
		{
			if (!Ready())
			{
				return true;
			}
			_planner.Reset(_session.LastSeed);
			_valuePending = false;
			return true;
		}

		private static int SectOf(object character)
		{
			if (!(character is BattleCharacter battleCharacter) || battleCharacter.battleTempData == null || battleCharacter.battleTempData.playerData == null)
			{
				return 0;
			}
			return battleCharacter.battleTempData.playerData.publicData.characterId / 1000000;
		}

		private bool OnExecute(HookContext h)
		{
			if (!Ready())
			{
				return true;
			}
			if (!(h.Instance is CardActionBase cardActionBase))
			{
				return true;
			}
			_planner.BeginCard(FactsOf(cardActionBase.cardConfig), nested: false, SectOf((h.Args != null && h.Args.Length != 0) ? h.Args[0] : null));
			return true;
		}

		private bool OnExecuteEffect(HookContext h)
		{
			if (!Ready())
			{
				return true;
			}
			if (!(h.Instance is CardActionBase cardActionBase))
			{
				return true;
			}
			_planner.BeginCard(FactsOf(cardActionBase.cardConfig), nested: true, SectOf((h.Args != null && h.Args.Length != 0) ? h.Args[0] : null));
			return true;
		}

		private bool OnNextRandomValue(HookContext h)
		{
			if (!Ready())
			{
				return true;
			}
			BattleCharacter battleCharacter = h.Instance as BattleCharacter;
			bool flag = true;
			if (h.Args != null && h.Args.Length != 0 && h.Args[0] is bool)
			{
				flag = (bool)h.Args[0];
			}
			_guaXiang = flag && battleCharacter != null && GuaXiangActive(battleCharacter);
			_valuePending = true;
			return true;
		}

		private static bool GuaXiangActive(BattleCharacter c)
		{
			if (c.HasBuff(BuffType.ZiMangXingBao) && c.GetBuffValue(BuffType.XingLi) > 0)
			{
				return true;
			}
			if (c.HasBuff(BuffType.GuaXiang))
			{
				return true;
			}
			if (c.IsTalentResonanceEffective(71) && c.CheckTalentResonanceTempFlag(71))
			{
				return c.battleTempData.anima > 0;
			}
			return false;
		}

		private bool OnNextParam(HookContext h)
		{
			if (!Ready())
			{
				return true;
			}
			int num;
			if (_valuePending)
			{
				_valuePending = false;
				num = _planner.DrawValue(_guaXiang);
			}
			else
			{
				BattleCharacter c = h.Instance as BattleCharacter;
				num = _planner.DrawRaw(DebuffsOf(c), HandOf(c));
			}
			h.Skip(num);
			return false;
		}

		private static int[] DebuffsOf(BattleCharacter c)
		{
			if (c == null)
			{
				return new int[0];
			}
			List<BuffType> debuffList = c.GetDebuffList();
			int[] array = new int[debuffList.Count];
			for (int i = 0; i < debuffList.Count; i++)
			{
				array[i] = (int)debuffList[i];
			}
			return array;
		}

		private CardFacts[] HandOf(BattleCharacter c)
		{
			if (c == null || c.battleTempData == null || c.battleTempData.playerData == null)
			{
				return new CardFacts[0];
			}
			// Read the executing character's actual battle snapshot, so changing
			// the edited side or the view cannot swap the two retained hands.
			BattlePlayerLastRoundData snapshot = c.battleTempData.playerData.publicData.lastRoundData;
			if (snapshot == null) return new CardFacts[0];
			CardFacts[] array2 = new CardFacts[snapshot.handCards.Count];
			for (int i = 0; i < snapshot.handCards.Count; i++)
			{
				array2[i] = FactsOf(CardFactory.FindCardConfig(snapshot.handCards[i]));
			}
			return array2;
		}
	}
}
