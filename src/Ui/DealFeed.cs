using TMPro;
using UnityEngine;
using Yx.ModSdk.Unity;

namespace YxArena.Views
{
	public sealed class DealFeed
	{
		private const int Lines = 10;

		private const double LifetimeSeconds = 6.0;

		private readonly FeedLog _log = new FeedLog(10, 6.0);

		private OutlinedLabel _label;

		public void Add(string text)
		{
			_log.Add(text, Time.realtimeSinceStartup);
		}

		public void Tick()
		{
			double now = Time.realtimeSinceStartup;
			if (!_log.Dirty(now))
			{
				return;
			}
			string text = _log.Render(now);
			if (_label == null || !_label.IsAlive)
			{
				if (text.Length == 0)
				{
					return;
				}
				_label = Ui.CreateLabel("YxArenaDealFeed", new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(24f, -60f), new Vector2(520f, 360f), 24f, new Color(1f, 0.88f, 0.42f, 1f), TextAlignmentOptions.BottomLeft);
				if (_label == null)
				{
					return;
				}
			}
			_label.SetText(text);
			_label.SetVisible(text.Length > 0);
		}

		public void Destroy()
		{
			if (_label != null)
			{
				_label.Destroy();
			}
			_label = null;
		}
	}
}
