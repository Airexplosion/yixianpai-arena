using System.Collections.Generic;
using System.Globalization;

namespace YxArena
{
	public sealed class OverflowPlan
	{
		public const int MinBudget = 300;

		public const int MaxBudget = 40000;

		public const int StartBudget = 1500;

		public const float TargetMs = 10f;

		private const string Core = "*core";

		private readonly Dictionary<string, string> _done = new Dictionary<string, string>();

		public bool CoreDone => _done.ContainsKey("*core");

		public static string[] CoreTypes()
		{
			return new string[11]
			{
				"BattleCharacter", "CardActionBase", "BattleExecuter", "FallbackCardAction", "RefineCardActionBase", "DanYaoCardActionBase", "CardReadyLayerActionBase", "FateStrategyFunctions", "KeYinCardFunctions", "PlayerSelfInfoItem",
				"BattleCharacterUI"
			};
		}

		public static string CardType(int cardId)
		{
			return "Card_" + CardIds.BaseOf(cardId).ToString(CultureInfo.InvariantCulture);
		}

		public bool IsDone(string type)
		{
			return _done.ContainsKey(type);
		}

		public string[] Pending(int[] cardIds)
		{
			List<string> list = new List<string>();
			Dictionary<string, string> dictionary = new Dictionary<string, string>();
			if (!CoreDone)
			{
				string[] array = CoreTypes();
				for (int i = 0; i < array.Length; i++)
				{
					list.Add(array[i]);
				}
			}
			if (cardIds != null)
			{
				for (int j = 0; j < cardIds.Length; j++)
				{
					if (cardIds[j] > 0)
					{
						string text = CardType(cardIds[j]);
						if (!_done.ContainsKey(text) && !dictionary.ContainsKey(text))
						{
							dictionary[text] = "";
							list.Add(text);
						}
					}
				}
			}
			return list.ToArray();
		}

		public void MarkDone(string[] types)
		{
			if (types == null)
			{
				return;
			}
			string[] array = CoreTypes();
			for (int i = 0; i < types.Length; i++)
			{
				_done[types[i]] = "";
				if (types[i] == array[0])
				{
					_done["*core"] = "";
				}
			}
		}

		public void MarkCardDone(string type)
		{
			_done[type] = "";
		}

		public static int NextBudget(int budget, float elapsedMs)
		{
			int num = budget;
			if (elapsedMs < 5f)
			{
				num = budget * 2;
			}
			else if (elapsedMs > 20f)
			{
				num = budget / 2;
			}
			if (num < 300)
			{
				num = 300;
			}
			if (num > 40000)
			{
				num = 40000;
			}
			return num;
		}

		public static string Progress(int typeIndex, int typeCount)
		{
			return ((typeIndex + 1 > typeCount) ? typeCount : (typeIndex + 1)).ToString(CultureInfo.InvariantCulture) + "/" + typeCount.ToString(CultureInfo.InvariantCulture);
		}
	}
}
