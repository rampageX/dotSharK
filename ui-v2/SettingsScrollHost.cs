using System;
using System.Drawing;
using System.Windows.Forms;

namespace dotSharK.UiV2
{
    /// <summary>
    /// Content itself uses only table/flow layout. Its Top coordinate is varied
    /// solely to implement viewport scrolling, never for form layout.
    /// Native light scrollbars are not created.
    /// </summary>
    public sealed class SettingsScrollHost : UserControl
    {
        private readonly Panel viewport;
        private readonly DarkScrollBar scrollbar;
        private Control content;
        private bool synchronizing;

        public SettingsScrollHost()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.AllPaintingInWmPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Window;
            Margin = Padding.Empty;
            var root = new TableLayoutPanel {
                Dock = DockStyle.Fill, Margin = Padding.Empty,
                Padding = Padding.Empty, ColumnCount = 2, RowCount = 1,
                BackColor = Theme.Window
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.ScrollbarWidth));
            viewport = new Panel {
                Dock = DockStyle.Fill, Margin = Padding.Empty,
                BackColor = Theme.Window, AutoScroll = false
            };
            scrollbar = new DarkScrollBar { Dock = DockStyle.Fill, Margin = Padding.Empty };
            scrollbar.ValueChanged += (s, e) => OffsetContent();
            root.Controls.Add(viewport, 0, 0);
            root.Controls.Add(scrollbar, 1, 0);
            Controls.Add(root);
            viewport.Resize += (s, e) => RefreshRange();
            viewport.MouseWheel += WheelScroll;
            scrollbar.SmallChange = Theme.ControlHeight;
            scrollbar.LargeChange = Theme.ControlHeight * 4;
        }
        public Control Content
        {
            get { return content; }
            set
            {
                if (content == value) return;
                if (content != null)
                {
                    content.Layout -= ContentLayout;
                    viewport.Controls.Remove(content);
                }
                content = value;
                if (content != null)
                {
                    content.Dock = DockStyle.None;
                    content.Margin = Padding.Empty;
                    content.AutoSize = true;
                    content.Layout += ContentLayout;
                    viewport.Controls.Add(content);
                }
                RefreshRange();
            }
        }
        public int ScrollPosition { get { return scrollbar.Value; } set { scrollbar.Value = value; } }
        public int ScrollMaximum { get { return scrollbar.Maximum; } }
        private void WheelScroll(object sender, MouseEventArgs e)
        {
            scrollbar.Value -= Math.Sign(e.Delta) * Theme.ControlHeight * 3;
        }
        private void ContentLayout(object sender, LayoutEventArgs e) { RefreshRange(); }
        private void RefreshRange()
        {
            if (synchronizing || content == null || viewport.IsDisposed) return;
            synchronizing = true;
            try
            {
                if (content.Width != viewport.ClientSize.Width)
                    content.Width = viewport.ClientSize.Width;
                int height = Math.Max(content.Height, content.GetPreferredSize(
                    new Size(viewport.ClientSize.Width, 0)).Height);
                scrollbar.LargeChange = Math.Max(Theme.ControlHeight, viewport.ClientSize.Height);
                scrollbar.Maximum = Math.Max(0, height - viewport.ClientSize.Height);
                scrollbar.Enabled = scrollbar.Maximum > 0;
                OffsetContent();
            }
            finally { synchronizing = false; }
        }
        private void OffsetContent()
        {
            if (content == null) return;
            // Scroll-offset positioning is separate from layout positioning.
            if (content.Left != 0 || content.Top != -scrollbar.Value)
                content.Location = new Point(0, -scrollbar.Value);
        }
    }
}
