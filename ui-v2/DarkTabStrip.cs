using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace dotSharK.UiV2
{
    /// <summary>
    /// Managed tab strip + content host, never creates a native TabControl.
    /// Supports AddPage, SelectedIndex, SelectedIndexChanged, Ctrl+Tab and arrows.
    /// </summary>
    public sealed class DarkTabStrip : UserControl
    {
        public sealed class Page
        {
            public string Title { get; private set; }
            public Control Content { get; private set; }
            internal Page(string title, Control content) { Title = title; Content = content; }
        }
        private readonly List<Page> pages = new List<Page>();
        private readonly Header header;
        private readonly Panel host;
        private int selectedIndex = -1;
        public event EventHandler SelectedIndexChanged;

        public DarkTabStrip()
        {
            SetStyle(DarkPaint.Buffered | ControlStyles.ContainerControl, true);
            DarkPaint.Setup(this);
            BackColor = Theme.Window;
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, Margin = Padding.Empty, Padding = Padding.Empty,
                ColumnCount = 1, RowCount = 2, BackColor = Theme.Window
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.TabHeight));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            header = new Header(this) { Dock = DockStyle.Fill, Margin = Padding.Empty };
            host = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Window, Margin = Padding.Empty };
            root.Controls.Add(header, 0, 0);
            root.Controls.Add(host, 0, 1);
            Controls.Add(root);
        }
        public IReadOnlyList<Page> Pages { get { return pages.AsReadOnly(); } }
        public int SelectedIndex
        {
            get { return selectedIndex; }
            set
            {
                if (value < -1 || value >= pages.Count) throw new ArgumentOutOfRangeException("value");
                if (value == selectedIndex) return;
                host.SuspendLayout();
                try
                {
                    if (selectedIndex >= 0) pages[selectedIndex].Content.Visible = false;
                    selectedIndex = value;
                    if (selectedIndex >= 0)
                    {
                        Control visible = pages[selectedIndex].Content;
                        visible.Dock = DockStyle.Fill;
                        visible.Visible = true;
                        visible.BringToFront();
                    }
                }
                finally { host.ResumeLayout(true); }
                header.Invalidate();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
            }
        }
        public Page SelectedPage { get { return selectedIndex < 0 ? null : pages[selectedIndex]; } }
        public void AddPage(string title, Control content)
        {
            if (content == null) throw new ArgumentNullException("content");
            if (title == null) throw new ArgumentNullException("title");
            content.Margin = Padding.Empty;
            content.Visible = false;
            content.Dock = DockStyle.Fill;
            pages.Add(new Page(title, content));
            host.Controls.Add(content);
            if (selectedIndex == -1) SelectedIndex = 0;
            header.Invalidate();
        }
        public void SelectNext(bool forward)
        {
            if (pages.Count == 0) return;
            SelectedIndex = (selectedIndex + (forward ? 1 : pages.Count - 1)) % pages.Count;
        }
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if ((keyData & Keys.Control) == Keys.Control &&
                (keyData & Keys.KeyCode) == Keys.Tab)
            {
                SelectNext((keyData & Keys.Shift) != Keys.Shift);
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private sealed class Header : Control
        {
            private readonly DarkTabStrip owner;
            private int hoverIndex = -1;
            internal Header(DarkTabStrip owner)
            {
                this.owner = owner;
                SetStyle(DarkPaint.Buffered | ControlStyles.Selectable, true);
                BackColor = Theme.Window;
                TabStop = true;
            }
            private int TabWidth(int index)
            {
                return TextRenderer.MeasureText(owner.pages[index].Title, Theme.BodyFont).Width +
                    Theme.Space16 * 2;
            }
            private int HitTest(int x)
            {
                int origin = 0;
                for (int i = 0; i < owner.pages.Count; i++)
                {
                    int width = TabWidth(i);
                    if (x >= origin && x < origin + width) return i;
                    origin += width;
                }
                return -1;
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(Theme.Window);
                int x = 0;
                using (var separator = new Pen(Theme.Divider))
                    e.Graphics.DrawLine(separator, 0, Height - 1, Width - 1, Height - 1);
                for (int i = 0; i < owner.pages.Count; i++)
                {
                    int width = TabWidth(i);
                    var rect = new Rectangle(x, 0, width, Height - Theme.BorderWidth);
                    bool active = i == owner.SelectedIndex;
                    using (var brush = new SolidBrush(active ? Theme.Panel :
                        i == hoverIndex ? Theme.Hover : Theme.Window))
                        e.Graphics.FillRectangle(brush, rect);
                    DarkPaint.DrawText(e.Graphics, owner.pages[i].Title, Theme.BodyFont,
                        rect, active || i == hoverIndex ? Theme.PrimaryText : Theme.SecondaryText,
                        TextFormatFlags.HorizontalCenter);
                    if (active)
                        using (var brush = new SolidBrush(Theme.AccentFocus))
                            e.Graphics.FillRectangle(brush, x, Height - 2, width, 2);
                    x += width;
                }
                if (Focused) DarkPaint.Border(e.Graphics, ClientRectangle, Theme.FocusBorder);
            }
            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int hit = HitTest(e.X);
                if (hoverIndex != hit) { hoverIndex = hit; Invalidate(); }
            }
            protected override void OnMouseLeave(EventArgs e)
            { base.OnMouseLeave(e); hoverIndex = -1; Invalidate(); }
            protected override void OnMouseDown(MouseEventArgs e)
            {
                base.OnMouseDown(e);
                if (e.Button != MouseButtons.Left) return;
                int index = HitTest(e.X);
                if (index >= 0) { Focus(); owner.SelectedIndex = index; }
            }
            protected override bool IsInputKey(Keys keys)
            {
                return (keys & Keys.KeyCode) == Keys.Left || (keys & Keys.KeyCode) == Keys.Right ||
                    (keys & Keys.KeyCode) == Keys.Home || (keys & Keys.KeyCode) == Keys.End ||
                    base.IsInputKey(keys);
            }
            protected override void OnKeyDown(KeyEventArgs e)
            {
                base.OnKeyDown(e);
                if (owner.pages.Count == 0) return;
                switch (e.KeyCode)
                {
                    case Keys.Left: owner.SelectNext(false); break;
                    case Keys.Right: owner.SelectNext(true); break;
                    case Keys.Home: owner.SelectedIndex = 0; break;
                    case Keys.End: owner.SelectedIndex = owner.pages.Count - 1; break;
                    default: return;
                }
                e.Handled = true;
            }
            protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
            protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        }
    }
}