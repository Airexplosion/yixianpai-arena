using Xunit;
using YxArena;

namespace YxArena.Tests
{
    public class OncePerOwnerTests
    {
        [Fact]
        public void The_same_id_on_the_same_owner_counts_once()
        {
            var once = new OncePerOwner();
            var left = new object();
            var right = new object();
            Assert.True(once.FirstTime(left, 30016));
            Assert.False(once.FirstTime(left, 30016));
            Assert.False(once.FirstTime(left, 30016));
            Assert.True(once.FirstTime(left, 18));
            Assert.True(once.FirstTime(right, 30016));      // 另一方各算各的
        }

        [Fact]
        public void A_new_battle_starts_over()
        {
            var once = new OncePerOwner();
            var ui = new object();
            once.FirstTime(ui, 7);
            once.Reset();
            Assert.True(once.FirstTime(ui, 7));
        }

        [Fact]
        public void Forgetting_one_owner_leaves_the_other_alone()
        {
            var once = new OncePerOwner();
            var left = new object();
            var right = new object();
            once.FirstTime(left, 7);
            once.FirstTime(right, 7);
            once.FirstTime(left, 8);
            once.Forget(left);
            Assert.True(once.FirstTime(left, 7));
            Assert.True(once.FirstTime(left, 8));
            Assert.False(once.FirstTime(right, 7));
        }

        [Fact]
        public void It_grows_past_its_first_block()
        {
            var once = new OncePerOwner();
            var ui = new object();
            for (int i = 0; i < 100; i++) Assert.True(once.FirstTime(ui, i));
            for (int i = 0; i < 100; i++) Assert.False(once.FirstTime(ui, i));
        }
    }
}
