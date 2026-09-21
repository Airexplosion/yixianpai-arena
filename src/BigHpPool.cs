namespace YxArena
{
    /// <summary>
    /// 64 位血量池：游戏里的 hp 是 32 位整数，血量想填到 21 亿以上，就让游戏里的 hp 最多到 <see cref="Cap"/>，
    /// 超出的部分记在池子里（<see cref="Reserve"/>）。每次游戏改完血量，用 <see cref="Normalize"/> 把「游戏里的 hp + 余量」
    /// 重新分配：余量先补回游戏里的 hp，补满为止。一刀打出超过游戏 hp 的真实伤害（hp 已经是负数）时，只要余量盖得住就不死。
    /// 纯逻辑；谁来读写游戏里的 hp 由调用方负责。
    /// </summary>
    public sealed class BigHpPool
    {
        /// <summary>游戏里的 hp / 血量上限最多到这里（比 int.MaxValue 留了余地）。</summary>
        public const int Cap = 2000000000;

        long _reserve;
        long _hp;
        long _lastLoss;
        bool _active;

        /// <summary>总血量不超过 <see cref="Cap"/> 就不需要池子（Active = false，Normalize 原样放行）。</summary>
        public void Reset(long total, int startHp)
        {
            _active = total > Cap;
            _hp = startHp;
            _reserve = _active ? total - startHp : 0L;
            if (_reserve < 0L) _reserve = 0L;
            _lastLoss = 0L;
        }

        public bool Active { get { return _active; } }

        public long Reserve { get { return _reserve; } }

        /// <summary>上一次 Normalize 之后游戏里的 hp。</summary>
        public long Hp { get { return _hp; } }

        /// <summary>真实的剩余血量。</summary>
        public long Effective { get { return _hp + _reserve; } }

        /// <summary>最近一次 Normalize 看到的真实掉血量（回血 / 没变 = 0）。</summary>
        public long LastLoss { get { return _lastLoss; } }

        /// <param name="hpNow">游戏刚改完的 hp（被钉在 int.MinValue 时传 SatMath 记下的真值）。</param>
        /// <returns>游戏里的 hp 现在应该是多少。</returns>
        public int Normalize(long hpNow)
        {
            if (!_active) return Clamp(hpNow);
            long before = _hp + _reserve;
            long after = hpNow + _reserve;
            if (after <= 0L)
            {
                _reserve = 0L;
                _hp = hpNow;
            }
            else
            {
                _hp = after > Cap ? Cap : after;
                _reserve = after - _hp;
            }
            _lastLoss = before > _hp + _reserve ? before - (_hp + _reserve) : 0L;
            return Clamp(_hp);
        }

        static int Clamp(long value)
        {
            if (value > int.MaxValue) return int.MaxValue;
            if (value < int.MinValue) return int.MinValue;
            return (int)value;
        }
    }
}
