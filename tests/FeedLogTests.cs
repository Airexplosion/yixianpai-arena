using Xunit;
using YxArena;

namespace YxArena.Tests
{
    public class FeedLogTests
    {
        [Fact]
        public void Newest_entries_are_at_the_bottom_so_the_feed_scrolls_upwards()
        {
            var feed = new FeedLog(3, 5.0);
            feed.Add("一", 0.0);
            feed.Add("二", 1.0);
            Assert.Equal("一" + (char)10 + "二", feed.Render(1.5));
        }

        [Fact]
        public void Only_the_last_few_entries_are_kept()
        {
            var feed = new FeedLog(3, 5.0);
            feed.Add("一", 0.0);
            feed.Add("二", 0.1);
            feed.Add("三", 0.2);
            feed.Add("四", 0.3);
            Assert.Equal("二" + (char)10 + "三" + (char)10 + "四", feed.Render(0.4));
        }

        [Fact]
        public void Entries_expire_after_their_lifetime()
        {
            var feed = new FeedLog(3, 5.0);
            feed.Add("一", 0.0);
            feed.Add("二", 4.0);
            Assert.Equal("二", feed.Render(6.0));
            Assert.Equal("", feed.Render(10.0));
        }

        [Fact]
        public void Render_reports_whether_anything_changed_since_the_last_call()
        {
            var feed = new FeedLog(3, 5.0);
            Assert.False(feed.Dirty(0.0));
            feed.Add("一", 0.0);
            Assert.True(feed.Dirty(0.1));
            feed.Render(0.1);
            Assert.False(feed.Dirty(0.2));
            Assert.True(feed.Dirty(5.5));      // 过期也算变化
        }
    }
}
