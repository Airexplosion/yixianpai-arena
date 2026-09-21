namespace YxArena
{
    /// <summary>
    /// 把一个界面元素摆到另一个元素的同一行、紧挨着它右边——只有算术，不碰 Unity。
    /// 约定：元素没有旋转；pivot 是它的世界坐标位置，rect 是以 pivot 为原点的本地矩形（xMin / xMax / 中心 y），scale 是 lossyScale。
    /// </summary>
    public static class RowLayout
    {
        /// <summary>元素右边缘的世界 x。</summary>
        public static float RightEdge(float pivotX, float rectXMax, float scale)
        {
            return pivotX + rectXMax * scale;
        }

        /// <summary>元素竖直方向中心的世界 y。</summary>
        public static float CenterY(float pivotY, float rectCenterY, float scale)
        {
            return pivotY + rectCenterY * scale;
        }

        /// <summary>要让元素的左边缘落在 leftEdge，它的 pivot 该放在哪个世界 x。</summary>
        public static float PivotXForLeftEdge(float leftEdge, float rectXMin, float scale)
        {
            return leftEdge - rectXMin * scale;
        }

        /// <summary>要让元素的竖直中心落在 centerY，它的 pivot 该放在哪个世界 y。</summary>
        public static float PivotYForCenter(float centerY, float rectCenterY, float scale)
        {
            return centerY - rectCenterY * scale;
        }
    }
}
