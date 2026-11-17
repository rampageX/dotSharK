using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;

namespace dotSharK.UiV2
{
    /// <summary>
    /// Square non-native dropdown. Public API mirrors common ComboBox use:
    /// Items.Add/AddRange/Clear, SelectedIndex, SelectedItem, SelectedIndexChanged,
    /// SelectionChangeCommitted, Text/TextChanged, DropDownStyle, DroppedDown,
    /// BeginUpdate/EndUpdate, MouseWheel and keyboard navigation.
    /// </summary>
    public sealed class DarkComboBox : Control
    {
        public sealed class ItemCollection : IEnumerable<object>
        {
            private readonly DarkComboBox owner;
            private readonly List<object> items = new List<object>();
            internal ItemCollection(DarkComboBox owner) { this.owner = owner; }
            public int Count { get { return items.Count; } }
            public object this[int i] { get { return items[i]; } set { items[i] = value; owner.Invalidate(); } }
            public int Add(object item) { items.Add(item); owner.NotifyItemsChanged(); return items.Count - 1; }
            public void AddRange(object[] values)
            {
                if (values == null) return;
                items.AddRange(values);
                owner.NotifyItemsChanged();
            }
            public void Clear() { items.Clear(); owner.SelectedIndex = -1; owner.NotifyItemsChanged(); }
            public void RemoveAt(int index)
            {
                items.RemoveAt(index);
                if (owner.SelectedIndex == index)
                    owner.SelectedIndex = Math.Min(index, items.Count - 1);
                else if (owner.SelectedIndex > index)
                    owner.SelectedIndex = owner.SelectedIndex - 1;
                owner.NotifyItemsChanged();
            }
            public bool Remove(object item)
            {
                int index = items.IndexOf(item);
                if (index < 0) return false;
                RemoveAt(index);
                return true;
            }
            public bool Contains(object item) { return items.Contains(item); }
            public int IndexOf(object item) { return items.IndexOf(item); }
            public IEnumerator<object> GetEnumerator() { return items.GetEnumerator(); }
            IEnumerator IEnumerable.GetEnumerator() { return GetEnumerator(); }
        }

        private readonly ItemCollection items;
        private int selectedIndex = -1;
        private bool hovered, suppressUpdate, syncingEditor;
        private ComboBoxStyle dropDownStyle = ComboBoxStyle.DropDownList;
        private TextBox editableText;
        private ToolStripDropDown popup;
        private PopupView view;
        private DarkScrollBar scrollbar;
        private Panel popupPanel;

        public event EventHandler SelectedIndexChanged;
        public event EventHandler SelectionChangeCommitted;
        public event EventHandler DropDown;
        public event EventHandler DropDownClosed;

        public DarkComboBox()
        {
            SetStyle(DarkPaint.Buffered | ControlStyles.Selectable, true);
            DarkPaint.Setup(this);
            items = new ItemCollection(this);
            Height = Theme.ControlHeight;
            MinimumSize = new Size(Theme.Space24 * 2, Theme.ControlHeight);
            TabStop = true;
            Cursor = Cursors.Hand;
            AccessibleRole = AccessibleRole.ComboBox;
        }
        public ItemCollection Items { get { return items; } }
        public ComboBoxStyle DropDownStyle
        {
            get { return dropDownStyle; }
            set
            {
                if (dropDownStyle == value) return;
                dropDownStyle = value;
                if (value == ComboBoxStyle.DropDown)
                {
                    editableText = new TextBox
                    {
                        BorderStyle = BorderStyle.None,
                        BackColor = Theme.Input, ForeColor = Theme.PrimaryText,
                        Font = Font, Margin = Padding.Empty
                    };
                    editableText.Text = SelectedItem == null ? "" : SelectedItem.ToString();
                    editableText.TextChanged += (s, e) =>
                    {
                        if (syncingEditor) return;
                        selectedIndex = -1;
                        base.OnTextChanged(e);
                    };
                    editableText.KeyDown += (s, e) =>
                    {
                        if (e.KeyCode == Keys.Down && e.Alt) { OpenPopup(); e.Handled = true; }
                        else if (e.KeyCode == Keys.Escape && DroppedDown)
                            { ClosePopup(); e.Handled = true; e.SuppressKeyPress = true; }
                    };
                    DarkTextContextMenu.Create(editableText);
                    Controls.Add(editableText);
                    LayoutEditor();
                }
                else if (editableText != null)
                {
                    var old = editableText;
                    editableText = null;
                    Controls.Remove(old);
                    old.Dispose();
                }
                Invalidate();
            }
        }
        private void LayoutEditor()
        {
            if (editableText == null) return;
            editableText.Bounds = new Rectangle(Theme.Space8,
                Math.Max(0, (Height - editableText.PreferredHeight) / 2),
                Math.Max(0, Width - Theme.ArrowRegionWidth - Theme.Space16),
                editableText.PreferredHeight);
        }
        protected override void OnResize(EventArgs e) { base.OnResize(e); LayoutEditor(); }
        protected override void OnFontChanged(EventArgs e)
        { base.OnFontChanged(e); if (editableText != null) { editableText.Font = Font; LayoutEditor(); } }
        public bool Sorted { get; set; }
        public int MaxDropDownItems { get; set; } = 8;
        public int DropDownWidth { get; set; }
        public int ItemHeight { get { return Theme.ControlHeight; } }
        public int SelectedIndex
        {
            get { return selectedIndex; }
            set
            {
                if (value < -1 || value >= items.Count) throw new ArgumentOutOfRangeException("value");
                if (selectedIndex == value) return;
                selectedIndex = value;
                if (editableText != null)
                {
                    syncingEditor = true;
                    editableText.Text = SelectedItem == null ? "" : SelectedItem.ToString();
                    syncingEditor = false;
                }
                Invalidate();
                if (SelectedIndexChanged != null) SelectedIndexChanged(this, EventArgs.Empty);
                base.OnTextChanged(EventArgs.Empty);
            }
        }
        public object SelectedItem
        {
            get { return selectedIndex >= 0 && selectedIndex < items.Count ? items[selectedIndex] : null; }
            set { SelectedIndex = value == null ? -1 : items.IndexOf(value); }
        }
        public override string Text
        {
            get
            {
                return editableText != null ? editableText.Text :
                    SelectedItem == null ? "" : SelectedItem.ToString();
            }
            set
            {
                string text = value ?? "";
                int match = -1;
                for (int i = 0; i < items.Count; i++)
                    if (String.Equals(Convert.ToString(items[i]), text, StringComparison.Ordinal))
                    { match = i; break; }
                SelectedIndex = match;
                if (editableText != null && match < 0) editableText.Text = text;
            }
        }
        public bool DroppedDown { get { return popup != null && popup.Visible; } set { if (value) OpenPopup(); else ClosePopup(); } }
        public void BeginUpdate() { suppressUpdate = true; }
        public void EndUpdate() { suppressUpdate = false; NotifyItemsChanged(); }
        internal void NotifyItemsChanged()
        {
            if (selectedIndex >= items.Count) SelectedIndex = items.Count - 1;
            if (!suppressUpdate) Invalidate();
        }
        private void CommitIndex(int index)
        {
            if (index < 0 || index >= items.Count) return;
            SelectedIndex = index;
            if (SelectionChangeCommitted != null) SelectionChangeCommitted(this, EventArgs.Empty);
            ClosePopup();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(!Enabled ? Theme.Panel : Theme.Input);
            Rectangle arrow = new Rectangle(Math.Max(0, Width - Theme.ArrowRegionWidth),
                0, Theme.ArrowRegionWidth, Height);
            using (var brush = new SolidBrush(hovered ? Theme.Hover : Theme.Panel))
                e.Graphics.FillRectangle(brush, arrow);
            using (var pen = new Pen(Theme.Divider))
                e.Graphics.DrawLine(pen, arrow.Left, 1, arrow.Left, Height - 2);
            var rect = new Rectangle(Theme.Space8, 0,
                Math.Max(0, Width - Theme.ArrowRegionWidth - 2 * Theme.Space8), Height);
            if (editableText == null)
                DarkPaint.DrawText(e.Graphics, Text, Font, rect,
                    Enabled ? Theme.PrimaryText : Theme.DisabledText);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int centerX = arrow.Left + arrow.Width / 2, centerY = Height / 2;
            using (var brush = new SolidBrush(Enabled ? Theme.SecondaryText : Theme.DisabledText))
                e.Graphics.FillPolygon(brush, new Point[] {
                    new Point(centerX - Theme.Space4, centerY - 2),
                    new Point(centerX + Theme.Space4, centerY - 2),
                    new Point(centerX, centerY + 2) });
            DarkPaint.Border(e.Graphics, ClientRectangle, !Enabled ? Theme.Divider :
                Focused || DroppedDown ? Theme.AccentFocus : Theme.Border);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (!Enabled || e.Button != MouseButtons.Left) return;
            if (editableText != null && e.X < Width - Theme.ArrowRegionWidth)
                { editableText.Focus(); return; }
            Focus();
            if (DroppedDown) ClosePopup(); else OpenPopup();
        }
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (items.Count == 0) return;
            int next = selectedIndex + (e.Delta > 0 ? -1 : 1);
            SelectedIndex = Math.Max(0, Math.Min(items.Count - 1, next));
        }
        protected override void OnMouseEnter(EventArgs e) { base.OnMouseEnter(e); hovered = true; Invalidate(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); hovered = false; Invalidate(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); Invalidate(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); Invalidate(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); Invalidate(); }
        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            return key == Keys.Down || key == Keys.Up || key == Keys.Home || key == Keys.End ||
                key == Keys.Enter || key == Keys.Escape || base.IsInputKey(keyData);
        }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (!Enabled) return;
            switch (e.KeyCode)
            {
                case Keys.Down: if (e.Alt) OpenPopup(); else SelectOffset(1); break;
                case Keys.Up: if (e.Alt) OpenPopup(); else SelectOffset(-1); break;
                case Keys.Home: if (items.Count > 0) SelectedIndex = 0; break;
                case Keys.End: if (items.Count > 0) SelectedIndex = items.Count - 1; break;
                case Keys.Enter: if (DroppedDown) ClosePopup(); else OpenPopup(); break;
                case Keys.Escape: if (DroppedDown) ClosePopup(); else return; break;
                default: return;
            }
            e.Handled = true;
        }
        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            base.OnKeyPress(e);
            if (Char.IsControl(e.KeyChar)) return;
            int index = FindByPrefix(e.KeyChar.ToString());
            if (index >= 0) SelectedIndex = index;
        }
        private void SelectOffset(int offset)
        {
            if (items.Count == 0) return;
            SelectedIndex = Math.Max(0, Math.Min(items.Count - 1, selectedIndex + offset));
        }
        internal int FindByPrefix(string text)
        {
            if (items.Count == 0 || String.IsNullOrEmpty(text)) return -1;
            for (int step = 1; step <= items.Count; step++)
            {
                int i = ((selectedIndex < 0 ? -1 : selectedIndex) + step) % items.Count;
                if (Convert.ToString(items[i]).StartsWith(text, StringComparison.CurrentCultureIgnoreCase))
                    return i;
            }
            return -1;
        }
        private void OpenPopup()
        {
            if (!Enabled || items.Count == 0 || DroppedDown) return;
            ClosePopup();
            int longest = items.Max(x => TextRenderer.MeasureText(Convert.ToString(x), Font).Width);
            int width = Math.Max(Math.Max(Width, DropDownWidth),
                longest + Theme.Space24 + Theme.ScrollbarWidth);
            Rectangle work = Screen.FromControl(this).WorkingArea;
            width = Math.Min(width, work.Width);
            int visible = Math.Min(items.Count, Math.Max(1, MaxDropDownItems));
            int height = visible * Theme.ControlHeight + 2;

            view = new PopupView(this) { Dock = DockStyle.Fill, BackColor = Theme.Input };
            scrollbar = new DarkScrollBar { Dock = DockStyle.Right };
            scrollbar.LargeChange = visible;
            scrollbar.Maximum = Math.Max(0, items.Count - visible);
            scrollbar.Value = Math.Max(0, Math.Min(scrollbar.Maximum, selectedIndex));
            scrollbar.ValueChanged += (s, e) => view.Invalidate();
            popupPanel = new Panel { BackColor = Theme.Input, Size = new Size(width, height), Margin = Padding.Empty };
            popupPanel.Controls.Add(view);
            popupPanel.Controls.Add(scrollbar);
            scrollbar.BringToFront();
            var host = new ToolStripControlHost(popupPanel)
            {
                AutoSize = false, Size = popupPanel.Size,
                Margin = Padding.Empty, Padding = Padding.Empty
            };
            popup = new ToolStripDropDown
            {
                AutoSize = false, Size = popupPanel.Size,
                BackColor = Theme.Input, Padding = Padding.Empty,
                Margin = Padding.Empty, DropShadowEnabled = false,
                Renderer = new DarkContextMenuRenderer()
            };
            popup.Items.Add(host);
            popup.Closed += (s, e) =>
            {
                if (DropDownClosed != null) DropDownClosed(this, EventArgs.Empty);
                Invalidate();
            };
            popup.Show(this, new Point(0, Height));
            view.Focus();
            if (DropDown != null) DropDown(this, EventArgs.Empty);
            Invalidate();
        }
        private void ClosePopup()
        {
            if (popup == null) return;
            var current = popup;
            popup = null;
            if (!current.IsDisposed) { current.Close(); current.Dispose(); }
            view = null;
            scrollbar = null;
            popupPanel = null;
            Invalidate();
        }
        protected override void Dispose(bool disposing)
        { if (disposing) ClosePopup(); base.Dispose(disposing); }

        private sealed class PopupView : Control
        {
            private readonly DarkComboBox owner;
            private readonly DarkToolTip tooltip = new DarkToolTip();
            private int hoverIndex = -1;
            internal PopupView(DarkComboBox owner)
            {
                this.owner = owner;
                SetStyle(DarkPaint.Buffered | ControlStyles.Selectable, true);
                TabStop = true;
                Font = owner.Font;
                AccessibleRole = AccessibleRole.List;
            }
            private int RowAt(int y)
            {
                return owner.scrollbar.Value + Math.Max(0, y / Theme.ControlHeight);
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(Theme.Input);
                for (int row = 0; row < Math.Min(owner.MaxDropDownItems, owner.Items.Count); row++)
                {
                    int index = row + owner.scrollbar.Value;
                    if (index >= owner.Items.Count) break;
                    Rectangle bounds = new Rectangle(0, row * Theme.ControlHeight, Width, Theme.ControlHeight);
                    using (var brush = new SolidBrush(index == owner.SelectedIndex ? Theme.AccentSelection :
                        index == hoverIndex ? Theme.Hover : Theme.Input))
                        e.Graphics.FillRectangle(brush, bounds);
                    var text = new Rectangle(Theme.Space8, bounds.Y,
                        Math.Max(0, Width - Theme.Space16), bounds.Height);
                    DarkPaint.DrawText(e.Graphics, Convert.ToString(owner.Items[index]), owner.Font,
                        text, Theme.PrimaryText);
                }
                DarkPaint.Border(e.Graphics, ClientRectangle, Theme.Border);
            }
            protected override void OnMouseMove(MouseEventArgs e)
            {
                base.OnMouseMove(e);
                int index = RowAt(e.Y);
                if (index == hoverIndex) return;
                hoverIndex = index;
                string tip = index >= 0 && index < owner.Items.Count ?
                    Convert.ToString(owner.Items[index]) : "";
                if (TextRenderer.MeasureText(tip, owner.Font).Width <= Width - Theme.Space16)
                    tip = "";
                tooltip.SetToolTip(this, tip);
                Invalidate();
            }
            protected override void OnMouseLeave(EventArgs e)
            { base.OnMouseLeave(e); hoverIndex = -1; tooltip.SetToolTip(this, ""); Invalidate(); }
            protected override void OnMouseUp(MouseEventArgs e)
            {
                base.OnMouseUp(e);
                if (e.Button == MouseButtons.Left && e.Y >= 0 && e.Y < Height)
                    owner.CommitIndex(RowAt(e.Y));
            }
            protected override void OnMouseWheel(MouseEventArgs e)
            { base.OnMouseWheel(e); owner.scrollbar.Value -= Math.Sign(e.Delta) * 3; }
            protected override bool IsInputKey(Keys keyData)
            {
                Keys key = keyData & Keys.KeyCode;
                return key == Keys.Up || key == Keys.Down || key == Keys.Home ||
                    key == Keys.End || key == Keys.PageUp || key == Keys.PageDown ||
                    key == Keys.Enter || key == Keys.Escape || base.IsInputKey(keyData);
            }
            protected override void OnKeyDown(KeyEventArgs e)
            {
                base.OnKeyDown(e);
                switch (e.KeyCode)
                {
                    case Keys.Up: owner.SelectOffset(-1); break;
                    case Keys.Down: owner.SelectOffset(1); break;
                    case Keys.Home: owner.SelectedIndex = 0; break;
                    case Keys.End: owner.SelectedIndex = owner.Items.Count - 1; break;
                    case Keys.PageUp: owner.SelectOffset(-owner.MaxDropDownItems); break;
                    case Keys.PageDown: owner.SelectOffset(owner.MaxDropDownItems); break;
                    case Keys.Enter: owner.CommitIndex(owner.SelectedIndex); break;
                    case Keys.Escape: owner.ClosePopup(); owner.Focus(); break;
                    default: return;
                }
                if (owner.scrollbar != null)
                    owner.scrollbar.Value = Math.Max(0, Math.Min(owner.scrollbar.Maximum,
                        owner.SelectedIndex - owner.MaxDropDownItems + 1));
                Invalidate();
                e.Handled = true;
            }
            protected override void OnKeyPress(KeyPressEventArgs e)
            {
                base.OnKeyPress(e);
                if (Char.IsControl(e.KeyChar)) return;
                int found = owner.FindByPrefix(e.KeyChar.ToString());
                if (found >= 0) owner.SelectedIndex = found;
                if (owner.scrollbar != null && found >= 0)
                    owner.scrollbar.Value = Math.Max(0, Math.Min(owner.scrollbar.Maximum, found));
                Invalidate();
            }
            protected override void Dispose(bool disposing)
            { if (disposing) tooltip.Dispose(); base.Dispose(disposing); }
        }
    }
}