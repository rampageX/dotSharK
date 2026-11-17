using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace dotSharK.UiV2
{
    /// <summary>
    /// .NET Framework ToolTip is sealed: DarkToolTip wraps an OwnerDraw ToolTip
    /// rather than inheriting from it. Provides SetToolTip/GetToolTip/Hide/Show
    /// and common delay properties without exposing a light native tooltip.
    /// </summary>
    public sealed class DarkToolTip : Component
    {
        private readonly ToolTip native;
        public DarkToolTip()
        {
            native = new ToolTip
            {
                OwnerDraw = true,
                IsBalloon = false,
                BackColor = Theme.Panel,
                ForeColor = Theme.PrimaryText
            };
            native.Popup += OnPopup;
            native.Draw += OnDraw;
        }
        public int AutomaticDelay { get { return native.AutomaticDelay; } set { native.AutomaticDelay = value; } }
        public int InitialDelay { get { return native.InitialDelay; } set { native.InitialDelay = value; } }
        public int ReshowDelay { get { return native.ReshowDelay; } set { native.ReshowDelay = value; } }
        public int AutoPopDelay { get { return native.AutoPopDelay; } set { native.AutoPopDelay = value; } }
        public bool Active { get { return native.Active; } set { native.Active = value; } }
        public void SetToolTip(Control control, string caption) { native.SetToolTip(control, caption); }
        public string GetToolTip(Control control) { return native.GetToolTip(control); }
        public void Hide(IWin32Window window) { native.Hide(window); }
        public void Show(string text, IWin32Window window) { native.Show(text, window); }
        public void RemoveAll() { native.RemoveAll(); }
        private void OnPopup(object sender, PopupEventArgs e)
        {
            string text = GetToolTip(e.AssociatedControl) ?? "";
            Size size = TextRenderer.MeasureText(text, Theme.SmallFont,
                new Size(640, 0), TextFormatFlags.WordBreak);
            e.ToolTipSize = new Size(size.Width + Theme.Space16, size.Height + Theme.Space12);
        }
        private void OnDraw(object sender, DrawToolTipEventArgs e)
        {
            e.Graphics.Clear(Theme.Panel);
            DarkPaint.Border(e.Graphics, e.Bounds, Theme.Border);
            var rect = new Rectangle(Theme.Space8, Theme.Space4,
                Math.Max(0, e.Bounds.Width - Theme.Space16),
                Math.Max(0, e.Bounds.Height - Theme.Space8));
            TextRenderer.DrawText(e.Graphics, e.ToolTipText, Theme.SmallFont, rect,
                Theme.PrimaryText, TextFormatFlags.WordBreak | TextFormatFlags.NoPrefix);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) native.Dispose();
            base.Dispose(disposing);
        }
    }
}