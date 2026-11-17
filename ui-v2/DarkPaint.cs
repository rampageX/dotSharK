using System;
using System.Drawing;
using System.Windows.Forms;

namespace dotSharK.UiV2
{
    internal static class DarkPaint
    {
        internal const ControlStyles Buffered = ControlStyles.UserPaint |
            ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.ResizeRedraw;

        internal static void Border(Graphics g, Rectangle bounds, Color color)
        {
            if (bounds.Width < 2 || bounds.Height < 2) return;
            using (var pen = new Pen(color, 1f))
                g.DrawRectangle(pen, bounds.X, bounds.Y, bounds.Width - 1, bounds.Height - 1);
        }

        internal static void DrawText(Graphics g, string text, Font font, Rectangle rect,
            Color color, TextFormatFlags extra = TextFormatFlags.Left)
        {
            TextRenderer.DrawText(g, text ?? "", font, rect, color,
                TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis |
                TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine | extra);
        }

        internal static Color Disabled(Color color)
        {
            return Color.FromArgb((color.R + Theme.Window.R) / 2,
                (color.G + Theme.Window.G) / 2, (color.B + Theme.Window.B) / 2);
        }

        internal static void Setup(Control control)
        {
            control.Margin = Padding.Empty;
            control.BackColor = Theme.Input;
            control.ForeColor = Theme.PrimaryText;
            control.Font = Theme.BodyFont;
        }
    }
}