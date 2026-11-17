using System;
using System.Drawing;
using System.Windows.Forms;

namespace dotSharK.UiV2
{
    /// <summary>
    /// Owner-drawn details ListView with green checkboxes, dark headers and
    /// attached thin scrollbar. dotSharK's current three lists use DataGridView;
    /// DarkDataGridView is preferred for those existing business bindings.
    /// </summary>
    public sealed class DarkListView : ListView
    {
        private readonly DarkScrollBar bar;
        private int hoverRow = -1;
        private bool syncing;
        public string EmptyText { get; set; } = "";
        public bool GreenCheckBoxes { get; set; }

        public DarkListView()
        {
            DoubleBuffered = true;
            SetStyle(DarkPaint.Buffered, true);
            DarkPaint.Setup(this);
            View = View.Details;
            OwnerDraw = true;
            FullRowSelect = true;
            GridLines = false;
            HideSelection = false;
            BorderStyle = BorderStyle.None;
            HeaderStyle = ColumnHeaderStyle.Nonclickable;
            CheckBoxes = false;
            BackColor = Theme.Panel;
            bar = new DarkScrollBar { Dock = DockStyle.Right };
            Controls.Add(bar);
            bar.BringToFront();
            bar.ValueChanged += (s, e) => ScrollTo(bar.Value);
        }
        protected override void OnDrawColumnHeader(DrawListViewColumnHeaderEventArgs e)
        {
            using (var brush = new SolidBrush(Theme.Panel))
                e.Graphics.FillRectangle(brush, e.Bounds);
            var rect = new Rectangle(e.Bounds.X + Theme.Space12, e.Bounds.Y,
                Math.Max(0, e.Bounds.Width - Theme.Space24), e.Bounds.Height - 1);
            DarkPaint.DrawText(e.Graphics, e.Header.Text, Font, rect, Theme.SecondaryText,
                e.Header.TextAlign == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left);
            using (var pen = new Pen(Theme.Divider))
                e.Graphics.DrawLine(pen, e.Bounds.Left, e.Bounds.Bottom - 1,
                    e.Bounds.Right - 1, e.Bounds.Bottom - 1);
        }
        protected override void OnDrawItem(DrawListViewItemEventArgs e)
        {
            if (View != View.Details) DrawFallbackItem(e);
            // Details: OnDrawSubItem paints each cell without native focus cues.
        }
        private void DrawFallbackItem(DrawListViewItemEventArgs e)
        {
            using (var brush = new SolidBrush(e.Item.Selected ? Theme.AccentSelection : Theme.Panel))
                e.Graphics.FillRectangle(brush, e.Bounds);
            DarkPaint.DrawText(e.Graphics, e.Item.Text, Font, e.Bounds, Theme.PrimaryText);
        }
        protected override void OnDrawSubItem(DrawListViewSubItemEventArgs e)
        {
            bool selected = e.Item.Selected;
            using (var brush = new SolidBrush(selected ? Theme.AccentSelection :
                e.ItemIndex == hoverRow ? Theme.Hover : Theme.Panel))
                e.Graphics.FillRectangle(brush, e.Bounds);
            using (var p = new Pen(Theme.Divider))
                e.Graphics.DrawLine(p, e.Bounds.Left, e.Bounds.Bottom - 1,
                    e.Bounds.Right - 1, e.Bounds.Bottom - 1);
            if (GreenCheckBoxes && e.ColumnIndex == 0)
            {
                int x = e.Bounds.Left + (e.Bounds.Width - Theme.CheckboxSize) / 2;
                int y = e.Bounds.Top + (e.Bounds.Height - Theme.CheckboxSize) / 2;
                var box = new Rectangle(x, y, Theme.CheckboxSize, Theme.CheckboxSize);
                using (var brush = new SolidBrush(e.Item.Checked ? Theme.Accent : Theme.Input))
                    e.Graphics.FillRectangle(brush, box);
                DarkPaint.Border(e.Graphics, box, Theme.Border);
                if (e.Item.Checked)
                    using (var p = new Pen(Color.White, 2f))
                        e.Graphics.DrawLines(p, new Point[] {
                            new Point(x + 3, y + 8), new Point(x + 7, y + 11),
                            new Point(x + 13, y + 4) });
            }
            else
            {
                var rect = new Rectangle(e.Bounds.X + Theme.Space12, e.Bounds.Y,
                    Math.Max(0, e.Bounds.Width - Theme.Space24), e.Bounds.Height - 1);
                bool right = e.Header != null && e.Header.TextAlign == HorizontalAlignment.Right;
                DarkPaint.DrawText(e.Graphics, e.SubItem.Text, Font, rect,
                    Theme.PrimaryText, right ? TextFormatFlags.Right : TextFormatFlags.Left);
            }
        }
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            var hit = GetItemAt(e.X, e.Y);
            int row = hit == null ? -1 : hit.Index;
            if (row != hoverRow) { hoverRow = row; Invalidate(); }
        }
        protected override void OnMouseLeave(EventArgs e)
        { base.OnMouseLeave(e); hoverRow = -1; Invalidate(); }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (GreenCheckBoxes && e.Button == MouseButtons.Left && Columns.Count > 0 &&
                e.X < Columns[0].Width)
            {
                var hit = GetItemAt(e.X, e.Y);
                if (hit != null) { hit.Checked = !hit.Checked; Invalidate(hit.Bounds); }
            }
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (GreenCheckBoxes && e.KeyCode == Keys.Space)
            {
                foreach (ListViewItem item in SelectedItems) item.Checked = !item.Checked;
                e.Handled = true;
                Invalidate();
            }
        }
        private void ScrollTo(int index)
        {
            if (syncing || Items.Count == 0 || !IsHandleCreated) return;
            syncing = true;
            try { TopItem = Items[Math.Min(Items.Count - 1, Math.Max(0, index))]; }
            catch (InvalidOperationException) { }
            finally { syncing = false; HideNativeScrollBars(); }
        }
        private void HideNativeScrollBars()
        {
            if (IsHandleCreated) DarkNative.ShowScrollBar(Handle, DarkNative.SbBoth, false);
        }
        private void SyncScroll()
        {
            if (syncing || bar == null || !IsHandleCreated) return;
            syncing = true;
            try
            {
                int visible = Math.Max(1, ClientSize.Height / Theme.TableRowHeight - 1);
                bar.LargeChange = visible;
                bar.Maximum = Math.Max(0, Items.Count - visible);
                bar.Value = TopItem == null ? 0 : Math.Min(bar.Maximum, TopItem.Index);
                bar.Visible = Items.Count > visible;
                if (bar.Visible) bar.BringToFront();
            }
            catch (InvalidOperationException) { }
            finally { syncing = false; HideNativeScrollBars(); }
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); SyncScroll(); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); SyncScroll(); }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (bar != null && bar.Visible) bar.Value -= Math.Sign(e.Delta) * 3;
            SyncScroll();
        }
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x000F && Items.Count == 0 && !String.IsNullOrEmpty(EmptyText))
            {
                using (Graphics g = CreateGraphics())
                {
                    var rect = new Rectangle(0, Theme.TableHeaderHeight,
                        Math.Max(0, Width - Theme.ScrollbarWidth),
                        Math.Max(0, Height - Theme.TableHeaderHeight));
                    DarkPaint.DrawText(g, EmptyText, Font, rect, Theme.SecondaryText,
                        TextFormatFlags.HorizontalCenter);
                }
            }
        }
    }
}