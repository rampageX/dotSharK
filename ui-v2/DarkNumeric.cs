using System;
using System.Drawing;
using System.Windows.Forms;

namespace dotSharK.UiV2
{
    /// <summary>32px square native-edit numeric input, two hand-painted 16px spin buttons.</summary>
    public sealed class DarkNumeric : UserControl
    {
        private readonly TextBox input;
        private readonly Timer repeat;
        private decimal minimum, maximum = 100, value, increment = 1;
        private int decimalPlaces, spinDirection;
        private bool hovered, focused, syncing;
        public event EventHandler ValueChanged;

        public DarkNumeric()
        {
            SetStyle(DarkPaint.Buffered | ControlStyles.ContainerControl, true);
            DarkPaint.Setup(this);
            Height = Theme.ControlHeight;
            Width = Theme.NumericWidth;
            MinimumSize = new Size(Theme.Space24 * 2, Theme.ControlHeight);
            TabStop = false;
            Padding = new Padding(Theme.Space8, Theme.Space4,
                Theme.ArrowRegionWidth + Theme.Space4, Theme.Space4);
            input = new TextBox
            {
                Dock = DockStyle.Fill, BorderStyle = BorderStyle.None,
                BackColor = Theme.Input, ForeColor = Theme.PrimaryText,
                Font = Theme.BodyFont, Margin = Padding.Empty,
                TextAlign = HorizontalAlignment.Left
            };
            Controls.Add(input);
            DarkTextContextMenu.Create(input);
            input.TextChanged += (s, e) => { if (!syncing) base.OnTextChanged(e); };
            input.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
                { Step(e.KeyCode == Keys.Up ? 1 : -1); e.Handled = true; e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Enter) Commit(); // let the Form invoke AcceptButton
            };
            input.Enter += (s, e) => { focused = true; Invalidate(); };
            input.Leave += (s, e) => { focused = false; Commit(); Invalidate(); };
            repeat = new Timer { Interval = 450 };
            repeat.Tick += (s, e) => { repeat.Interval = 70; Step(spinDirection); };
            UpdateEditor();
        }
        public decimal Minimum { get { return minimum; } set { minimum = value; if (maximum < minimum) maximum = minimum; Value = this.value; Invalidate(); } }
        public decimal Maximum { get { return maximum; } set { maximum = Math.Max(minimum, value); Value = this.value; Invalidate(); } }
        public decimal Increment { get { return increment; } set { if (value <= 0) throw new ArgumentOutOfRangeException("value"); increment = value; } }
        public int DecimalPlaces { get { return decimalPlaces; } set { decimalPlaces = Math.Max(0, Math.Min(28, value)); UpdateEditor(); } }
        public decimal Value
        {
            get { return value; }
            set
            {
                decimal next = Math.Min(maximum, Math.Max(minimum, value));
                if (this.value == next) { UpdateEditor(); return; }
                this.value = next;
                UpdateEditor();
                if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
            }
        }
        public override string Text { get { return input == null ? base.Text : input.Text; } set { if (input != null) { input.Text = value; Commit(); } else base.Text = value; } }
        public bool ReadOnly { get { return input.ReadOnly; } set { input.ReadOnly = value; Invalidate(); } }
        private void UpdateEditor()
        {
            if (input == null) return;
            syncing = true;
            try { input.Text = value.ToString("F" + decimalPlaces, System.Globalization.CultureInfo.CurrentCulture); }
            finally { syncing = false; }
        }
        private void Commit()
        {
            decimal parsed;
            if (Decimal.TryParse(input.Text, out parsed)) Value = parsed;
            else UpdateEditor();
        }
        private void Step(int direction)
        {
            if (!Enabled || ReadOnly || direction == 0) return;
            Commit();
            Value += increment * direction;
        }
        private void StopRepeat() { repeat.Stop(); spinDirection = 0; Capture = false; }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left || !Enabled || ReadOnly ||
                e.X < Width - Theme.ArrowRegionWidth) return;
            spinDirection = e.Y < Height / 2 ? 1 : -1;
            Step(spinDirection);
            repeat.Interval = 450;
            repeat.Start();
            Capture = true;
            Invalidate();
        }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); StopRepeat(); Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; if (spinDirection != 0) StopRepeat(); Invalidate(); }
        protected override void OnMouseMove(MouseEventArgs e)
        { base.OnMouseMove(e); hovered = e.X >= Width - Theme.ArrowRegionWidth; Invalidate(); }
        protected override void OnMouseWheel(MouseEventArgs e) { base.OnMouseWheel(e); Step(Math.Sign(e.Delta)); }
        protected override void OnEnabledChanged(EventArgs e)
        { base.OnEnabledChanged(e); input.Enabled = Enabled; Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Input);
            Rectangle spin = new Rectangle(Width - Theme.ArrowRegionWidth, 0,
                Theme.ArrowRegionWidth, Height);
            using (var brush = new SolidBrush(hovered ? Theme.Hover : Theme.Panel))
                e.Graphics.FillRectangle(brush, spin);
            using (var p = new Pen(Theme.Divider))
            {
                e.Graphics.DrawLine(p, spin.Left, 1, spin.Left, Height - 2);
                e.Graphics.DrawLine(p, spin.Left, Height / 2, Width - 2, Height / 2);
            }
            DrawArrow(e.Graphics, spin.Left + spin.Width / 2, Height / 4, true);
            DrawArrow(e.Graphics, spin.Left + spin.Width / 2, 3 * Height / 4, false);
            DarkPaint.Border(e.Graphics, ClientRectangle,
                !Enabled ? Theme.DisabledText : focused ? Theme.AccentFocus : Theme.Border);
        }
        private void DrawArrow(Graphics graphics, int x, int y, bool up)
        {
            using (var pen = new Pen(Enabled ? Theme.SecondaryText : Theme.DisabledText))
            {
                int d = Theme.Space4;
                graphics.DrawLines(pen, up ? new Point[] { new Point(x - d, y + 2),
                    new Point(x, y - 2), new Point(x + d, y + 2) } :
                    new Point[] { new Point(x - d, y - 2), new Point(x, y + 2),
                        new Point(x + d, y - 2) });
            }
        }
        protected override void Dispose(bool disposing)
        { if (disposing) repeat.Dispose(); base.Dispose(disposing); }
    }
}