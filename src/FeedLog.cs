using System.Text;

namespace YxArena
{
    /// <summary>
    /// 向上滚动的消息流（「发牌：XX」）：新消息在最下面，旧的往上顶；只留最近几条，过一会儿自己消失。
    /// 纯逻辑：时间由调用方给，渲染成一段多行文本。
    /// </summary>
    public sealed class FeedLog
    {
        readonly string[] _texts;
        readonly double[] _expires;
        readonly double _lifetime;
        int _count;
        string _rendered = "";
        bool _changed;

        public FeedLog(int capacity, double lifetimeSeconds)
        {
            int size = capacity < 1 ? 1 : capacity;
            _texts = new string[size];
            _expires = new double[size];
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

        /// <summary>自上次 Render 以来有没有变化（新消息，或有消息到期）。</summary>
        public bool Dirty(double now)
        {
            if (_changed) return true;
            return _count > 0 && _expires[0] <= now;
        }

        public string Render(double now)
        {
            int drop = 0;
            while (drop < _count && _expires[drop] <= now) drop++;
            if (drop > 0)
            {
                for (int i = drop; i < _count; i++)
                {
                    _texts[i - drop] = _texts[i];
                    _expires[i - drop] = _expires[i];
                }
                _count -= drop;
            }
            var sb = new StringBuilder();
            for (int i = 0; i < _count; i++)
            {
                if (i > 0) sb.Append((char)10);
                sb.Append(_texts[i]);
            }
            _rendered = sb.ToString();
            _changed = false;
            return _rendered;
        }
    }
}
