namespace YxArena
{
	public static class RowLayout
	{
		public static float RightEdge(float pivotX, float rectXMax, float scale)
		{
			return pivotX + rectXMax * scale;
		}

		public static float CenterY(float pivotY, float rectCenterY, float scale)
		{
			return pivotY + rectCenterY * scale;
		}

		public static float PivotXForLeftEdge(float leftEdge, float rectXMin, float scale)
		{
			return leftEdge - rectXMin * scale;
		}

		public static float PivotYForCenter(float centerY, float rectCenterY, float scale)
		{
			return centerY - rectCenterY * scale;
		}
	}
}
