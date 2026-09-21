using Xunit;
using YxArena;

namespace YxArena.Tests
{
    public class RowLayoutTests
    {
        [Fact]
        public void An_element_lands_with_its_left_edge_where_asked_whatever_its_pivot()
        {
            // 宽 100、pivot 在正中（rect.xMin = -50），缩放 2：左边缘要落在 300 → pivot 在 400。
            float pivot = RowLayout.PivotXForLeftEdge(300f, -50f, 2f);
            Assert.Equal(400f, pivot);
            Assert.Equal(500f, RowLayout.RightEdge(pivot, 50f, 2f));
            // pivot 在左边缘（rect.xMin = 0）：pivot 就是左边缘。
            Assert.Equal(300f, RowLayout.PivotXForLeftEdge(300f, 0f, 2f));
        }

        [Fact]
        public void Two_elements_with_different_pivots_share_a_row()
        {
            // 参照物：pivot 在底边（rect 中心 y = 20），缩放 1.5 → 中心在 130。
            float row = RowLayout.CenterY(100f, 20f, 1.5f);
            Assert.Equal(130f, row);
            // 按钮：pivot 在顶边（rect 中心 y = -18），缩放 1.5 → pivot 要放到 157。
            float pivot = RowLayout.PivotYForCenter(row, -18f, 1.5f);
            Assert.Equal(157f, pivot);
            Assert.Equal(row, RowLayout.CenterY(pivot, -18f, 1.5f));
        }
    }
}
