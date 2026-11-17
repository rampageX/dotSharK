using System;
using System.Drawing;
using System.Windows.Forms;

namespace dotSharK.UiV2
{
    /// <summary>Self-painted flat 1px-border WinForms Button with native Click/DialogResult.</summary>
    public abstract class DarkButtonBase : Button
    {
        protected bool hovered, pressed;
        protected DarkButtonBase()
        {
            SetStyle(DarkPaint.Buffered | ControlStyles.Selectable, true);
            DarkPaint.Setup(this);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            UseVisualStyleBackColor = false;
            Height = Theme.ControlHeight;
            MinimumSize = new Size(Theme.Space24, Theme.ControlHeight);
            Cursor = Cursors.Hand;
        }
        protected abstract Color NormalColor { get; }
        protected virtual Color HoverColor { get { return Theme.Hover; } }
        protected virtual Color PressedColor { get { return Theme.Pressed; } }
        protected virtual Color OutlineColor { get { return Theme.Border; } }
        protected virtual Color TextColor { get { return Theme.PrimaryText; } }

        protected override void OnPaint(PaintEventArgs e)
        {
            PaintFace(e.Graphics);
            if (Text.Length > 0)
                DarkPaint.DrawText(e.Graphics, Text, Font, ClientRectangle,
                    Enabled ? TextColor : DarkPaint.Disabled(TextColor), TextFormatFlags.HorizontalCenter);
        }
        protected void PaintFace(Graphics graphics)
        {
            Color back = !Enabled ? Theme.Panel : pressed ? PressedColor : hovered ? HoverColor : NormalColor;
            graphics.Clear(back);
            DarkPaint.Border(graphics, ClientRectangle,
                !Enabled ? Theme.Divider : Focused ? Theme.AccentFocus : OutlineColor);
        }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; pressed = false; Invalidate(); }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) { pressed = true; Invalidate(); } }
        protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); pressed = false; Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); pressed = false; Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
    }
}