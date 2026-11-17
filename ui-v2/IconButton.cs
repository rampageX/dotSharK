using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace dotSharK.UiV2
{
    /// <summary>32x32 square GDI+ update icon, stable=green, nightly=yellow.</summary>
    public sealed class IconButton : DarkButtonBase
    {
        public bool Nightly { get; set; }
        public IconButton()
        {
            Size = new Size(Theme.IconButtonSize, Theme.IconButtonSize);
            MinimumSize = Size;
            MaximumSize = Size;
            AccessibleRole = AccessibleRole.PushButton;
        }
        protected override Color NormalColor { get { return Theme.Panel; } }
        protected override void OnPaint(PaintEventArgs e)
        {
            PaintFace(e.Graphics);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Color color = !Enabled ? Theme.DisabledText : Nightly ? Theme.Warning : Theme.SuccessText;
            float r = Math.Min(Width, Height) * .25f;
            float cx = Width / 2f, cy = Height / 2f;
            using (var pen = new Pen(color, 2f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                e.Graphics.DrawArc(pen, cx - r, cy - r, r * 2, r * 2, 35, 280);
            }
            double theta = 315 * Math.PI / 180.0;
            var tip = new PointF(cx + r * (float)Math.Cos(theta),
                cy + r * (float)Math.Sin(theta));
            using (var pen = new Pen(color, 2f))
            {
                e.Graphics.DrawLines(pen, new PointF[] {
                    new PointF(tip.X - 2f, tip.Y - 5f), tip,
                    new PointF(tip.X - 5f, tip.Y + 1f) });
            }
        }
    }
}