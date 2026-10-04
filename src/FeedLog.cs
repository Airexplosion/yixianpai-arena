using System.Text;

namespace YxArena
{
	public sealed class FeedLog
	{
		private readonly string[] _texts;

		private readonly double[] _expires;

		private readonly double _lifetime;

		private int _count;

		private string _rendered = "";

		private bool _changed;

		public FeedLog(int capacity, double lifetimeSeconds)
		{
			int num = ((capacity < 1) ? 1 : capacity);
			_texts = new string[num];
			_expires = new double[num];
			_lifetime = lifetimeSeconds;
		}

		public void Add(string text, double now)
		{
			if (_count == _texts.Length)
			{
				for (int i = 1; i < _count; i++)
				{
					_texts[i - 1] = _texts[i];
					_expires[i - 1] = _expires[i];
				}
				_count--;
			}
			_texts[_count] = text ?? "";
			_expires[_count] = now + _lifetime;
			_count++;
			_changed = true;
		}

		public bool Dirty(double now)
		{
			if (_changed)
			{
				return true;
			}
			if (_count > 0)
			{
				return _expires[0] <= now;
			}
			return false;
		}

		public string Render(double now)
		{
			int i;
			for (i = 0; i < _count && _expires[i] <= now; i++)
			{
			}
			if (i > 0)
			{
				for (int j = i; j < _count; j++)
				{
					_texts[j - i] = _texts[j];
					_expires[j - i] = _expires[j];
				}
				_count -= i;
			}
			StringBuilder stringBuilder = new StringBuilder();
			for (int k = 0; k < _count; k++)
			{
				if (k > 0)
				{
					stringBuilder.Append('\n');
				}
				stringBuilder.Append(_texts[k]);
			}
			_rendered = stringBuilder.ToString();
			_changed = false;
			return _rendered;
		}
	}
}
