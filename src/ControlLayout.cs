namespace YxArena
{
    public sealed class ControlSlot
    {
        public float X, Y;
        public ControlSlot(float x, float y) { X = x; Y = y; }
    }

    /// <summary>Pack visible controls into rows; hidden controls leave no gaps.</summary>
    public sealed class ControlLayout
    {
        public const float Pad = 6f, Gap = 5f, RowHeight = 40f;
        readonly float _maxWidth;
        float _x = Pad, _y = -Pad, _usedWidth;
        public ControlLayout(float maxWidth) { _maxWidth = maxWidth; }
        public ControlSlot Add(float width)
        {
            if (_x > Pad && _x + width + Pad > _maxWidth) NextRow();
            var slot = new ControlSlot(_x, _y);
            _x += width + Gap;
            if (_x - Gap + Pad > _usedWidth) _usedWidth = _x - Gap + Pad;
            return slot;
        }
        public void NextRow() { if (_x <= Pad) return; _x = Pad; _y -= RowHeight + Gap; }
        public float Width { get { return _usedWidth; } }
        public float Bottom { get { return _y - RowHeight - Pad; } }
    }
}
