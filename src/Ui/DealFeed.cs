using TMPro;
using UnityEngine;
using Yx.ModSdk.Unity;

namespace YxArena.Views
{
    /// <summary>
    /// 屏幕左侧向上滚动的发牌记录：每发一张加一行，新的在最下面，旧的往上顶，过几秒自己消失。
    /// 逻辑在 <see cref="FeedLog"/>，这里只管把它画出来（SDK 的描边文本，不挡点击）。
    /// </summary>
    public sealed class DealFeed
    {
        const int Lines = 10;
        const double LifetimeSeconds = 6.0;

        readonly FeedLog _log = new FeedLog(Lines, LifetimeSeconds);
        OutlinedLabel _label;

        public void Add(string text)
        {
            _log.Add(text, Time.realtimeSinceStartup);
        }

        public void Tick()
        {
            double now = Time.realtimeSinceStartup;
            if (!_log.Dirty(now)) return;
            string text = _log.Render(now);
            if (_label == null || !_label.IsAlive)
            {
                if (text.Length == 0) return;
                // 左下角锚点 + 底部对齐：文本块从下往上长，看起来就是向上滚动。
                _label = Ui.CreateLabel("YxArenaDealFeed", new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(24f, -60f),
                    new Vector2(520f, 360f), 24f, new Color(1f, 0.88f, 0.42f, 1f), TextAlignmentOptions.BottomLeft);
                if (_label == null) return;
            }
            _label.SetText(text);
            _label.SetVisible(text.Length > 0);
        }

        public void Destroy()
        {
            if (_label != null) _label.Destroy();
            _label = null;
        }
    }
}
