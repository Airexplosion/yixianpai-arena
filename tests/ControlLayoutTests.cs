using Xunit;

namespace YxArena.Tests
{
    public class ControlLayoutTests
    {
        [Theory]
        [InlineData(360f)]
        [InlineData(800f)]
        [InlineData(1450f)]
        public void Full_toolbar_and_input_groups_fit_without_overlapping(float width)
        {
            var layout = new ControlLayout(width);
            float right = 0f, row = 0f;
            float[] widths = { 118f, 118f, 118f, 118f, 118f, 118f, 118f, 118f,
                118f, 118f, 118f, 118f, 118f, 118f, 270f, 150f, 192f, 118f, 118f, 118f, 272f };
            for (int i = 0; i < widths.Length; i++)
            {
                var slot = layout.Add(widths[i]);
                Assert.True(slot.X + widths[i] + ControlLayout.Pad <= width);
                if (i == 0) Assert.Equal(-ControlLayout.Pad, slot.Y);
                else if (slot.Y == row) Assert.True(slot.X >= right + ControlLayout.Gap);
                else Assert.True(slot.Y <= row - ControlLayout.RowHeight);
                right = slot.X + widths[i]; row = slot.Y;
            }
            Assert.True(layout.Width <= width);
            Assert.True(-layout.Bottom >= -row + ControlLayout.RowHeight);
        }

        [Fact]
        public void Hiding_controls_compacts_the_bar_and_show_all_restores_them()
        {
            var visible = new ControlVisibility();
            for (int i = 0; i < ControlVisibility.Count; i++) visible.Set(i, false);
            visible.Set(ControlVisibility.Quick, true);
            var layout = new ControlLayout(1450f);
            layout.Add(118f); layout.Add(118f); // permanent collapse and settings
            for (int i = 0; i < ControlVisibility.Count; i++) if (visible.Shows(i)) layout.Add(118f);
            Assert.Equal(376f, layout.Width);
            Assert.Equal(-52f, layout.Bottom);
            visible.Reset();
            for (int i = 0; i < ControlVisibility.Count; i++) Assert.True(visible.Shows(i));
        }
    }
}
