using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace dotSharK.UiV2
{
    /// <summary>16px green square checkbox, accessible by Space and mouse.</summary>
    public sealed class DarkCheckBox : Control
    {
        private bool isChecked, hover, pressed;
        private System.Windows.Forms.CheckState checkState;
        public event EventHandler CheckedChanged;
        public event EventHandler CheckStateChanged;

        public DarkCheckBox()
        {
            SetStyle(DarkPaint.Buffered | ControlStyles.Selectable, true);
            DarkPaint.Setup(this);
            Height = Theme.ControlHeight;
            ForeColor = Theme.PrimaryText;
            MinimumSize = new Size(Theme.CheckboxSize, Theme.ControlHeight);
            TabStop = true;
            AccessibleRole = AccessibleRole.CheckButton;
            Cursor = Cursors.Hand;
        }
        public bool AutoCheck { get; set; } = true;
        public bool ThreeState { get; set; }
        public bool Checked
        {
            get { return isChecked; }
            set { CheckState = value ? System.Windows.Forms.CheckState.Checked : System.Windows.Forms.CheckState.Unchecked; }
        }
        public System.Windows.Forms.CheckState CheckState
        {
            get { return checkState; }
            set
            {
                if (checkState == value) return;
                bool old = isChecked;
                checkState = value;
                isChecked = value != System.Windows.Forms.CheckState.Unchecked;
                Invalidate();
                if (old != isChecked && CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
                if (CheckStateChanged != null) CheckStateChanged(this, EventArgs.Empty);
            }
        }
        private void Toggle()
        {
            if (!AutoCheck || !Enabled) return;
            CheckState = ThreeState ? (System.Windows.Forms.CheckState)(((int)CheckState + 1) % 3) :
                (isChecked ? System.Windows.Forms.CheckState.Unchecked : System.Windows.Forms.CheckState.Checked);
        }
        protected override void OnClick(EventArgs e) { Toggle(); base.OnClick(e); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            // WmMouseUp already raises OnClick for StandardClick controls.
            // Calling OnClick here a second time would undo the checkbox toggle.
            if (e.Button == MouseButtons.Left && ClientRectangle.Contains(e.Location)) Focus();
            pressed = false;
            Invalidate();
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); }
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.KeyCode == Keys.Space) { OnClick(EventArgs.Empty); e.Handled = true; }
        }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hover = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hover = false; pressed = false; Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent == null ? Theme.Window : Parent.BackColor);
            int y = (Height - Theme.CheckboxSize) / 2;
            var check = new Rectangle(0, y, Theme.CheckboxSize, Theme.CheckboxSize);
            using (var fill = new SolidBrush(!Enabled ? Theme.Panel : isChecked ? Theme.Accent : Theme.Input))
                e.Graphics.FillRectangle(fill, check);
            DarkPaint.Border(e.Graphics, check, !Enabled ? Theme.DisabledText :
                Focused ? Theme.AccentFocus : pressed ? Theme.AccentPressed : hover ? Theme.SuccessText : Theme.Border);
            if (isChecked)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var pen = new Pen(Color.White, 2f))
                    e.Graphics.DrawLines(pen, new PointF[] {
                        new PointF(check.Left + 3, y + 8),
                        new PointF(check.Left + 7, y + 11),
                        new PointF(check.Left + 13, y + 4) });
            }
            var text = new Rectangle(Theme.CheckboxSize + Theme.Space8, 0,
                Math.Max(0, Width - Theme.CheckboxSize - Theme.Space8), Height);
            DarkPaint.DrawText(e.Graphics, Text, Font, text,
                Enabled ? ForeColor : Theme.DisabledText);
        }
    }
}