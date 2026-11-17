using System;
using System.Drawing;
using System.Windows.Forms;

namespace dotSharK.UiV2
{
    /// <summary>
    /// Queue/analysis progress, compatible with the original SetProgress and
    /// SetIndeterminate methods; green fill, dark track, no system rendering.
    /// </summary>
    public sealed class DarkProgressIndicator : Control
    {
        private readonly Timer animation;
        private int segmentX = -1;
        private int fraction;
        private bool indeterminate = true;
        private int SegmentWidth
        {
            get { return Math.Min(Theme.ActionHeight * 2, Math.Max(Theme.Space24, Width / 4)); }
        }

        public DarkProgressIndicator()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.ScrollTrack;
            Height = Theme.Space8;
            Margin = Padding.Empty;
            animation = new Timer { Interval = 40 };
            animation.Tick += (s, e) =>
            {
                if (!indeterminate || Width <= 0) return;
                segmentX += Theme.Space4;
                if (segmentX > Width) segmentX = -SegmentWidth;
                Invalidate();
            };
            VisibleChanged += (s, e) => SyncAnimation();
        }

        public void SetProgress(double value)
        {
            fraction = (int)Math.Round(Math.Max(0, Math.Min(1, value)) * 10000);
            if (!indeterminate) Invalidate();
        }
        public void SetIndeterminate(bool value)
        {
            indeterminate = value;
            if (value) segmentX = -SegmentWidth;
            SyncAnimation();
        }
        private void SyncAnimation()
        {
            if (IsHandleCreated && Visible && indeterminate) animation.Start();
            else animation.Stop();
            Invalidate();
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            SyncAnimation();
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.ScrollTrack);
            if (Width <= 0 || Height <= 0) return;
            int width = indeterminate ? SegmentWidth : (int)Math.Round(Width * fraction / 10000.0);
            var rect = new Rectangle(indeterminate ? segmentX : 0, 0, width, Height);
            rect.Intersect(ClientRectangle);
            if (rect.Width > 0)
                using (var brush = new SolidBrush(Theme.Accent))
                    e.Graphics.FillRectangle(brush, rect);
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) animation.Dispose();
            base.Dispose(disposing);
        }
    }
}
