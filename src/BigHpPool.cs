namespace YxArena
{
	public sealed class BigHpPool
	{
		public const int Cap = 2000000000;

		private long _reserve;

		private long _hp;

		private long _lastLoss;

		private bool _active;

		public bool Active => _active;

		public long Reserve => _reserve;

		public long Hp => _hp;

		public long Effective => _hp + _reserve;

		public long LastLoss => _lastLoss;

		public void Reset(long total, int startHp)
		{
			_active = total > 2000000000;
			_hp = startHp;
			_reserve = (_active ? (total - startHp) : 0);
			if (_reserve < 0)
			{
				_reserve = 0L;
			}
			_lastLoss = 0L;
		}

		public int Normalize(long hpNow)
		{
			if (!_active)
			{
				return Clamp(hpNow);
			}
			long num = _hp + _reserve;
			long num2 = hpNow + _reserve;
			if (num2 <= 0)
			{
				_reserve = 0L;
				_hp = hpNow;
			}
			else
			{
				_hp = ((num2 > 2000000000) ? 2000000000 : num2);
				_reserve = num2 - _hp;
			}
			_lastLoss = ((num > _hp + _reserve) ? (num - (_hp + _reserve)) : 0);
			return Clamp(_hp);
		}

		private static int Clamp(long value)
		{
			if (value > int.MaxValue)
			{
				return int.MaxValue;
			}
			if (value < int.MinValue)
			{
				return int.MinValue;
			}
			return (int)value;
		}
	}
}
