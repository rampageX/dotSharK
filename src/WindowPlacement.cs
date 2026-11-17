using System;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace dotSharK
{
    // Persist each window's normal rectangle so maximized/minimized windows restore correctly.
    // Screen bounds are checked on startup in case a monitor has been disconnected.
    internal sealed class WindowPlacement
    {
        internal readonly Rectangle NormalBounds;
        internal readonly bool Maximized;

        private WindowPlacement(Rectangle normalBounds, bool maximized)
        {
            NormalBounds = normalBounds;
            Maximized = maximized;
        }

        internal static WindowPlacement Capture(Form window, bool lastMaximized)
        {
            Rectangle normal = window.WindowState == FormWindowState.Normal ? window.Bounds : window.RestoreBounds;
            bool maximized = window.WindowState == FormWindowState.Maximized ||
                (window.WindowState == FormWindowState.Minimized && lastMaximized);
            return new WindowPlacement(normal, maximized);
        }

        internal string Serialize()
        {
            return String.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3},{4}",
                NormalBounds.X, NormalBounds.Y, NormalBounds.Width, NormalBounds.Height, Maximized ? 1 : 0);
        }

        internal static WindowPlacement Parse(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return null;
            string[] parts = value.Split(',');
            if (parts.Length != 5) return null;
            var numbers = new int[5];
            for (int i = 0; i < parts.Length; i++)
                if (!Int32.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out numbers[i]))
                    return null;
            if (numbers[2] <= 0 || numbers[3] <= 0 || (numbers[4] != 0 && numbers[4] != 1)) return null;
            return new WindowPlacement(new Rectangle(numbers[0], numbers[1], numbers[2], numbers[3]), numbers[4] == 1);
        }

        internal void Restore(Form window)
        {
            Screen screen = Screen.AllScreens
                .OrderByDescending(s => Area(Rectangle.Intersect(s.WorkingArea, NormalBounds)))
                .FirstOrDefault(s => Area(Rectangle.Intersect(s.WorkingArea, NormalBounds)) > 0) ?? Screen.PrimaryScreen;
            if (screen == null) return;

            Rectangle work = screen.WorkingArea;
            int width = Math.Min(Math.Max(window.MinimumSize.Width, NormalBounds.Width), work.Width);
            int height = Math.Min(Math.Max(window.MinimumSize.Height, NormalBounds.Height), work.Height);
            int x = Math.Max(work.Left, Math.Min(NormalBounds.X, work.Right - width));
            int y = Math.Max(work.Top, Math.Min(NormalBounds.Y, work.Bottom - height));
            window.StartPosition = FormStartPosition.Manual;
            window.Bounds = new Rectangle(x, y, width, height);
            if (Maximized) window.WindowState = FormWindowState.Maximized;
        }

        private static long Area(Rectangle rectangle)
        {
            return Math.Max(0, (long)rectangle.Width) * Math.Max(0, (long)rectangle.Height);
        }
    }
}
