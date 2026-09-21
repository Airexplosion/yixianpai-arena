using Xunit;
using YxArena;

namespace YxArena.Tests
{
    public class ListFilterTests
    {
        static readonly string[] Names = { "清血苦炼", "剑意凝神", "五行调和", "星弈", "剑心通明", "苦修" };
        static readonly int[] Groups = { 0, 1, 3, 2, 1, 0 };

        [Fact]
        public void No_group_and_no_text_lists_everything_in_order()
        {
            Assert.Equal(new[] { 0, 1, 2, 3, 4, 5 }, ListFilter.Match(Names, Groups, ListFilter.AnyGroup, ""));
        }

        [Fact]
        public void A_group_narrows_the_list()
        {
            Assert.Equal(new[] { 1, 4 }, ListFilter.Match(Names, Groups, 1, null));
            Assert.Empty(ListFilter.Match(Names, Groups, 9, ""));
        }

        [Fact]
        public void Text_matches_any_part_of_the_name_and_combines_with_the_group()
        {
            Assert.Equal(new[] { 0, 5 }, ListFilter.Match(Names, Groups, ListFilter.AnyGroup, "苦"));
            Assert.Equal(new[] { 1, 4 }, ListFilter.Match(Names, Groups, ListFilter.AnyGroup, " 剑 "));
            Assert.Equal(new[] { 4 }, ListFilter.Match(Names, Groups, 1, "心"));
        }

        [Theory]
        [InlineData(0, 4, 0, 0)]
        [InlineData(10, 4, 0, 0)]
        [InlineData(10, 4, 7, 2)]      // 页码越界：钳到最后一页
        [InlineData(10, 4, -3, 0)]
        [InlineData(8, 4, 1, 1)]
        public void Pages_are_clamped(int count, int pageSize, int wanted, int expectedPage)
        {
            Assert.Equal(expectedPage, ListFilter.ClampPage(count, pageSize, wanted));
        }

        [Fact]
        public void Page_count_is_at_least_one()
        {
            Assert.Equal(1, ListFilter.PageCount(0, 32));
            Assert.Equal(1, ListFilter.PageCount(32, 32));
            Assert.Equal(2, ListFilter.PageCount(33, 32));
        }
    }
}
