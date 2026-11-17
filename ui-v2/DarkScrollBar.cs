using System;
using System.Drawing;
using System.Windows.Forms;

namespace dotSharK.UiV2
{
    /// <summary>
    /// Pure managed 10-logical-pixel scrollbar, without system scroll arrows.
    /// Maximum is the largest top-line/row index (not a Win32 nMax).
    /// LargeChange specifies the viewport size for the thumb ratio.
    /// </summary>
    public sealed class DarkScrollBar : Control
    {
        private int minimum, maximum, value, largeChange = 10, smallChange = 1;
        private bool dragging, hover;
        private int dragOffset;
        public event EventHandler ValueChanged;
        public event ScrollEventHandler Scroll;

        public DarkScrollBar()
        {
            SetStyle(DarkPaint.Buffered | ControlStyles.Selectable, true);
            DarkPaint.Setup(this);
            BackColor = Theme.ScrollTrack;
            Width = Theme.ScrollbarWidth;
            MinimumSize = new Size(Theme.ScrollbarWidth, Theme.ControlHeight);
            TabStop = true;
            AccessibleRole = AccessibleRole.ScrollBar;
        }

        public int Minimum { get { return minimum; } set { minimum = value; if (maximum < value) maximum = value; Value = this.value; Invalidate(); } }
        public int Maximum { get { return maximum; } set { maximum = Math.Max(minimum, value); Value = this.value; Invalidate(); } }
        public int Value
        {
            get { return value; }
            set
            {
                int next = Math.Min(maximum, Math.Max(minimum, value));
                if (this.value == next) return;
                this.value = next;
                Invalidate();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
                if (Scroll != null) Scroll(this, new ScrollEventArgs(ScrollEventType.ThumbPosition, next));
            }
        }
        public int LargeChange { get { return largeChange; } set { largeChange = Math.Max(1, value); Invalidate(); } }
        public int SmallChange { get { return smallChange; } set { smallChange = Math.Max(1, value); } }
        public bool IsDragging { get { return dragging; } }

        private Rectangle Thumb
        {
            get
            {
                int height = Math.Max(0, Height);
                if (height <= 0) return Rectangle.Empty;
                int range = Math.Max(0, maximum - minimum);
                int thumb = range == 0 ? height : Math.Max(Theme.Space24,
                    (int)Math.Round(height * (largeChange / (double)(largeChange + range))));
                thumb = Math.Min(height, thumb);
                int travel = height - thumb;
                int y = range == 0 ? 0 : (int)Math.Round(travel * (value - minimum) / (double)range);
                return new Rectangle(0, y, Width, thumb);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.ScrollTrack);
            Rectangle thumb = Thumb;
            using (var brush = new SolidBrush(!Enabled ? Theme.ScrollTrack :
                dragging ? Theme.ScrollThumbDrag : hover ? Theme.ScrollThumbHover : Theme.ScrollThumb))
                e.Graphics.FillRectangle(brush, thumb);
            if (Focused) DarkPaint.Border(e.Graphics, ClientRectangle, Theme.AccentFocus);
        }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); if (!dragging) { hover = false; Invalidate(); } }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled || e.Button != MouseButtons.Left) return;
            Focus();
            Rectangle thumb = Thumb;
            if (thumb.Contains(e.Location))
            {
                dragging = true;
                dragOffset = e.Y - thumb.Top;
                Capture = true;
            }
            else Value += e.Y < thumb.Top ? -LargeChange : LargeChange;
            Invalidate();
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!dragging) return;
            Rectangle thumb = Thumb;
            int travel = Height - thumb.Height;
            if (travel <= 0 || maximum <= minimum) return;
            int y = Math.Min(travel, Math.Max(0, e.Y - dragOffset));
            Value = minimum + (int)Math.Round(y * (maximum - minimum) / (double)travel);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (!dragging) return;
            dragging = false;
            Capture = false;
            hover = ClientRectangle.Contains(PointToClient(Cursor.Position));
            Invalidate();
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            Value -= Math.Sign(e.Delta) * SmallChange * 3;
        }
        protected override bool IsInputKey(Keys keyData)
        {
            switch (keyData & Keys.KeyCode)
            {
                case Keys.Up: case Keys.Down: case Keys.PageUp: case Keys.PageDown:
                case Keys.Home: case Keys.End: return true;
            }
            return base.IsInputKey(keyData);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            switch (e.KeyCode)
            {
                case Keys.Up: Value -= SmallChange; break;
                case Keys.Down: Value += SmallChange; break;
                case Keys.PageUp: Value -= LargeChange; break;
                case Keys.PageDown: Value += LargeChange; break;
                case Keys.Home: Value = Minimum; break;
                case Keys.End: Value = Maximum; break;
                default: return;
            }
            e.Handled = true;
        }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
    }
}