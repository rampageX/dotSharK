using System;
using System.Drawing;
using System.Windows.Forms;

namespace dotSharK.UiV2
{
    /// <summary>
    /// Dark owner-painted DataGridView, retaining the existing dotSharK Rows,
    /// Columns, Cells, Tag, CellClick/CellValueChanged and row selection API.
    /// No native scrollbar or native checkbox painting is used.
    /// </summary>
    public sealed class DarkDataGridView : DataGridView
    {
        private readonly DarkScrollBar bar;
        private bool updatingScroll;
        private int hoverRow = -1;
        public string EmptyText { get; set; } = "";

        public DarkDataGridView()
        {
            DoubleBuffered = true;
            SetStyle(DarkPaint.Buffered, true);
            BackgroundColor = Theme.Panel;
            BorderStyle = BorderStyle.None;
            GridColor = Theme.Divider;
            EnableHeadersVisualStyles = false;
            RowHeadersVisible = false;
            AllowUserToAddRows = false;
            AllowUserToDeleteRows = false;
            AllowUserToResizeRows = false;
            AllowUserToResizeColumns = true;
            ScrollBars = System.Windows.Forms.ScrollBars.None;
            SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            CellBorderStyle = DataGridViewCellBorderStyle.None;
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            ColumnHeadersHeight = Theme.TableHeaderHeight;
            RowTemplate.Height = Theme.TableRowHeight;
            RowTemplate.MinimumHeight = Theme.TableRowHeight;
            Font = Theme.BodyFont;
            ForeColor = Theme.PrimaryText;
            DefaultCellStyle.BackColor = Theme.Panel;
            DefaultCellStyle.ForeColor = Theme.PrimaryText;
            DefaultCellStyle.SelectionBackColor = Theme.AccentSelection;
            DefaultCellStyle.SelectionForeColor = Theme.PrimaryText;
            DefaultCellStyle.Padding = Padding.Empty;
            ColumnHeadersDefaultCellStyle.BackColor = Theme.Panel;
            ColumnHeadersDefaultCellStyle.ForeColor = Theme.SecondaryText;
            ColumnHeadersDefaultCellStyle.SelectionBackColor = Theme.Panel;
            ColumnHeadersDefaultCellStyle.SelectionForeColor = Theme.PrimaryText;
            TabStop = true;
            Margin = Padding.Empty;
            bar = new DarkScrollBar { Dock = DockStyle.Right };
            Controls.Add(bar);
            bar.BringToFront();
            bar.ValueChanged += (s, e) => ScrollTo(bar.Value);
            CellMouseEnter += (s, e) => SetHover(e.RowIndex);
            CellMouseLeave += (s, e) => { if (e.RowIndex == hoverRow) SetHover(-1); };
            CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (IsCurrentCellDirty && CurrentCell is DataGridViewCheckBoxCell)
                    CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
        }
        private void SetHover(int index)
        {
            if (hoverRow == index) return;
            int previous = hoverRow;
            hoverRow = index;
            if (previous >= 0 && previous < Rows.Count) InvalidateRow(previous);
            if (index >= 0 && index < Rows.Count) InvalidateRow(index);
        }
        protected override void OnCellPainting(DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 && e.ColumnIndex >= 0)
            {
                using (var fill = new SolidBrush(Theme.Panel))
                    e.Graphics.FillRectangle(fill, e.CellBounds);
                var textRect = new Rectangle(e.CellBounds.X + Theme.Space12, e.CellBounds.Y,
                    Math.Max(0, e.CellBounds.Width - Theme.Space24), e.CellBounds.Height - 1);
                bool right = Columns[e.ColumnIndex].HeaderCell.Style.Alignment ==
                    DataGridViewContentAlignment.MiddleRight ||
                    Columns[e.ColumnIndex].DefaultCellStyle.Alignment ==
                    DataGridViewContentAlignment.MiddleRight;
                DarkPaint.DrawText(e.Graphics, Convert.ToString(e.FormattedValue), Font,
                    textRect, Theme.SecondaryText,
                    right ? TextFormatFlags.Right : TextFormatFlags.Left);
                using (var p = new Pen(Theme.Divider))
                    e.Graphics.DrawLine(p, e.CellBounds.Left, e.CellBounds.Bottom - 1,
                        e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
                e.Handled = true;
                return;
            }
            if (e.RowIndex < 0 || e.ColumnIndex < 0) { base.OnCellPainting(e); return; }
            bool selected = Rows[e.RowIndex].Selected ||
                ((e.State & DataGridViewElementStates.Selected) != 0);
            Color back = selected ? Theme.AccentSelection :
                hoverRow == e.RowIndex ? Theme.Hover : Theme.Panel;
            using (var fill = new SolidBrush(back))
                e.Graphics.FillRectangle(fill, e.CellBounds);

            if (e.CellBounds.Height > 0)
                using (var p = new Pen(Theme.Divider))
                    e.Graphics.DrawLine(p, e.CellBounds.Left, e.CellBounds.Bottom - 1,
                        e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);

            if (e.CellBounds.Width > 0 && e.CellBounds.Height > 0)
            {
                if (e.Cell is DataGridViewCheckBoxCell)
                {
                    bool check = e.Value is bool && (bool)e.Value;
                    int x = e.CellBounds.X + (e.CellBounds.Width - Theme.CheckboxSize) / 2;
                    int y = e.CellBounds.Y + (e.CellBounds.Height - Theme.CheckboxSize) / 2;
                    var box = new Rectangle(x, y, Theme.CheckboxSize, Theme.CheckboxSize);
                    using (var brush = new SolidBrush(check ? Theme.Accent : Theme.Input))
                        e.Graphics.FillRectangle(brush, box);
                    DarkPaint.Border(e.Graphics, box, Theme.Border);
                    if (check)
                        using (var pen = new Pen(Color.White, 2f))
                            e.Graphics.DrawLines(pen, new Point[] {
                                new Point(x + 3, y + 8), new Point(x + 7, y + 11),
                                new Point(x + 13, y + 4) });
                }
                else
                {
                    var rect = new Rectangle(e.CellBounds.X + Theme.Space12,
                        e.CellBounds.Y, Math.Max(0, e.CellBounds.Width - Theme.Space24),
                        e.CellBounds.Height - 1);
                    bool right = e.CellStyle.Alignment == DataGridViewContentAlignment.MiddleRight;
                    Color fore = e.CellStyle.ForeColor.IsEmpty ? Theme.PrimaryText : e.CellStyle.ForeColor;
                    DarkPaint.DrawText(e.Graphics, Convert.ToString(e.FormattedValue), Font,
                        rect, fore, right ? TextFormatFlags.Right : TextFormatFlags.Left);
                }
            }
            e.Handled = true;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (Rows.Count == 0 && !String.IsNullOrEmpty(EmptyText))
                DarkPaint.DrawText(e.Graphics, EmptyText, Theme.BodyFont,
                    new Rectangle(0, ColumnHeadersHeight, Math.Max(0, Width - Theme.ScrollbarWidth),
                        Math.Max(0, Height - ColumnHeadersHeight)), Theme.SecondaryText,
                    TextFormatFlags.HorizontalCenter);
        }
        private void ScrollTo(int index)
        {
            if (updatingScroll || Rows.Count == 0 || !IsHandleCreated) return;
            updatingScroll = true;
            try
            {
                FirstDisplayedScrollingRowIndex = Math.Max(0, Math.Min(Rows.Count - 1, index));
            }
            catch (InvalidOperationException) { }
            finally { updatingScroll = false; }
        }
        private void SyncScroll()
        {
            if (bar == null || updatingScroll || !IsHandleCreated) return;
            updatingScroll = true;
            try
            {
                int visible = Math.Max(1, DisplayedRowCount(false));
                bar.LargeChange = visible;
                bar.Maximum = Math.Max(0, Rows.Count - visible);
                bar.Value = Math.Max(0, FirstDisplayedScrollingRowIndex);
                bar.Visible = Rows.Count > visible;
                if (bar.Visible) bar.BringToFront();
            }
            catch (InvalidOperationException) { }
            finally { updatingScroll = false; }
        }
        protected override void OnRowsAdded(DataGridViewRowsAddedEventArgs e) { base.OnRowsAdded(e); SyncScroll(); }
        protected override void OnRowsRemoved(DataGridViewRowsRemovedEventArgs e) { base.OnRowsRemoved(e); SyncScroll(); }
        protected override void OnScroll(ScrollEventArgs e) { base.OnScroll(e); SyncScroll(); }
        protected override void OnResize(EventArgs e) { base.OnResize(e); SyncScroll(); }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (bar != null && bar.Visible) bar.Value -= Math.Sign(e.Delta) * 3;
            SyncScroll();
        }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); SyncScroll(); }
    }
}