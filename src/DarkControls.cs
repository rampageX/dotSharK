using System;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace dotSharK
{
    internal static class DarkPalette
    {
        internal const int ControlHeight = 24;
        internal static readonly Color Background = Color.FromArgb(34, 34, 34);
        internal static readonly Color Surface = Color.FromArgb(43, 43, 43);
        internal static readonly Color Field = Color.FromArgb(51, 51, 51);
        internal static readonly Color Foreground = Color.FromArgb(235, 238, 240);
        internal static readonly Color Muted = Color.FromArgb(153, 163, 170);
        internal static readonly Color Accent = Color.FromArgb(96, 185, 28);
        internal static readonly Color Border = Color.FromArgb(65, 65, 65);
        internal static readonly Color Focus = Color.FromArgb(96, 185, 28);
    }

    internal static class NativeDarkTheme
    {
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode, EntryPoint = "SetWindowTheme")]
        private static extern int SetWindowTheme(IntPtr hwnd, string appName, string idList);
        internal static void Apply(Control control, string theme)
        {
            if (control == null || !control.IsHandleCreated || control.IsDisposed) return;
            try { SetWindowTheme(control.Handle, theme, null); }
            catch (DllNotFoundException) { }
            catch (EntryPointNotFoundException) { }
        }
        internal static void ApplyOnHandleCreated(Control control, string theme)
        {
            control.HandleCreated += (s, e) => Apply((Control)s, theme);
            if (control.IsHandleCreated) Apply(control, theme);
        }
    }

    internal sealed class DarkButton : Button
    {
        internal bool Primary { get; set; }
        internal DarkButton() { FlatStyle = FlatStyle.Flat; UseVisualStyleBackColor = false; Cursor = Cursors.Hand; Height = DarkPalette.ControlHeight; MinimumSize = new Size(0, DarkPalette.ControlHeight); Margin = new Padding(0); }
        protected override void OnCreateControl() { base.OnCreateControl(); ApplyStyle(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); ApplyStyle(); }
        private void ApplyStyle()
        {
            BackColor = Primary ? DarkPalette.Accent : DarkPalette.Surface;
            ForeColor = DarkPalette.Foreground;
            FlatAppearance.BorderSize = Primary ? 0 : 1;
            FlatAppearance.BorderColor = DarkPalette.Border;
            FlatAppearance.MouseOverBackColor = Primary ? Color.FromArgb(111, 202, 40) : Color.FromArgb(55, 55, 55);
            FlatAppearance.MouseDownBackColor = Primary ? Color.FromArgb(79, 158, 22) : Color.FromArgb(47, 47, 47);
        }
    }

    // Keeps NumericUpDown's native input, mouse capture and repeat behavior. Only
    // its frame and spinner child are painted here; spin painting allocates no bitmap.
    internal sealed class DarkNumericUpDown : NumericUpDown
    {
        [StructLayout(LayoutKind.Sequential)]
        private struct PaintRect { internal int Left, Top, Right, Bottom; }
        [StructLayout(LayoutKind.Sequential)]
        private struct PaintInfo
        {
            internal IntPtr Hdc; internal int Erase; internal PaintRect Paint;
            internal int Restore, Incremental; internal int R0, R1, R2, R3, R4, R5, R6, R7;
        }
        [DllImport("user32.dll")] private static extern IntPtr BeginPaint(IntPtr hwnd, out PaintInfo info);
        [DllImport("user32.dll")] private static extern bool EndPaint(IntPtr hwnd, ref PaintInfo info);
        private static readonly Brush ArrowBack = new SolidBrush(DarkPalette.Field);
        private static readonly Brush ArrowFore = new SolidBrush(DarkPalette.Foreground);
        private static readonly Brush ArrowDisabled = new SolidBrush(DarkPalette.Muted);
        private static readonly Pen ArrowBorder = new Pen(DarkPalette.Border);
        private static readonly Pen InnerBorder = new Pen(DarkPalette.Field);
        private bool pointerOver, focusWithin;
        private Control arrowButtons;
        private ArrowPaintWindow arrowWindow;

        private sealed class ArrowPaintWindow : NativeWindow, IDisposable
        {
            private readonly DarkNumericUpDown owner;
            private readonly Control buttons;
            internal ArrowPaintWindow(DarkNumericUpDown owner, Control buttons)
            {
                this.owner = owner; this.buttons = buttons;
                buttons.HandleCreated += HandleCreated; buttons.HandleDestroyed += HandleDestroyed;
                if (buttons.IsHandleCreated) AssignHandle(buttons.Handle);
            }
            private void HandleCreated(object sender, EventArgs e) { AssignHandle(buttons.Handle); }
            private void HandleDestroyed(object sender, EventArgs e) { ReleaseHandle(); }
            protected override void WndProc(ref Message m)
            {
                if (!owner.IsDisposed && !owner.Disposing)
                {
                    if (m.Msg == 0x0014) { m.Result = new IntPtr(1); return; }
                    if (m.Msg == 0x000F)
                    {
                        PaintInfo info; IntPtr hdc = BeginPaint(Handle, out info);
                        if (hdc != IntPtr.Zero)
                        {
                            try { using (Graphics g = Graphics.FromHdc(hdc)) owner.DrawArrows(g, buttons.ClientRectangle); }
                            finally { EndPaint(Handle, ref info); }
                            m.Result = IntPtr.Zero; return;
                        }
                    }
                    if (m.Msg == 0x0318 && m.WParam != IntPtr.Zero)
                    {
                        using (Graphics g = Graphics.FromHdc(m.WParam)) owner.DrawArrows(g, buttons.ClientRectangle);
                        m.Result = IntPtr.Zero; return;
                    }
                }
                base.WndProc(ref m);
            }
            public void Dispose()
            {
                buttons.HandleCreated -= HandleCreated; buttons.HandleDestroyed -= HandleDestroyed; ReleaseHandle();
            }
        }

        internal DarkNumericUpDown()
        {
            BackColor = DarkPalette.Field; ForeColor = DarkPalette.Foreground; BorderStyle = BorderStyle.FixedSingle;
            TextAlign = HorizontalAlignment.Center;
        }
        protected override void OnControlAdded(ControlEventArgs e)
        {
            base.OnControlAdded(e); e.Control.MouseEnter += ChildMouseEnter; e.Control.MouseLeave += ChildMouseLeave;
            if (!(e.Control is TextBoxBase))
            {
                if (arrowWindow != null) arrowWindow.Dispose();
                arrowButtons = e.Control; arrowWindow = new ArrowPaintWindow(this, e.Control);
            }
        }
        protected override void OnControlRemoved(ControlEventArgs e)
        {
            e.Control.MouseEnter -= ChildMouseEnter; e.Control.MouseLeave -= ChildMouseLeave;
            if (e.Control == arrowButtons) { if (arrowWindow != null) arrowWindow.Dispose(); arrowWindow = null; arrowButtons = null; }
            base.OnControlRemoved(e);
        }
        private void DrawOuterBorder(Graphics g)
        {
            using (Region gap = new Region(ClientRectangle))
            {
                foreach (Control child in Controls) if (child.Visible) gap.Exclude(child.Bounds);
                g.FillRegion(ArrowBack, gap);
            }
            int w = ClientSize.Width, h = ClientSize.Height; if (w < 3 || h < 3) return;
            g.DrawRectangle(InnerBorder, 1, 1, w - 3, h - 3);
            Color edge = !Enabled ? DarkPalette.Border : focusWithin ? DarkPalette.Focus : pointerOver ? Color.FromArgb(88, 94, 101) : DarkPalette.Border;
            using (Pen p = new Pen(edge)) g.DrawRectangle(p, 0, 0, w - 1, h - 1);
        }
        protected override void OnPaint(PaintEventArgs e) { DrawOuterBorder(e.Graphics); }
        private void RefreshOuterBorder() { if (IsHandleCreated && !IsDisposed && !Disposing) using (Graphics g = Graphics.FromHwnd(Handle)) DrawOuterBorder(g); }
        private void UpdatePointer() { pointerOver = IsHandleCreated && RectangleToScreen(ClientRectangle).Contains(Cursor.Position); RefreshOuterBorder(); }
        private void ChildMouseEnter(object sender, EventArgs e) { pointerOver = true; RefreshOuterBorder(); }
        private void ChildMouseLeave(object sender, EventArgs e) { UpdatePointer(); }
        protected override void OnMouseEnter(EventArgs e) { pointerOver = true; base.OnMouseEnter(e); RefreshOuterBorder(); }
        protected override void OnMouseLeave(EventArgs e) { base.OnMouseLeave(e); UpdatePointer(); }
        protected override void OnEnter(EventArgs e) { focusWithin = true; base.OnEnter(e); RefreshOuterBorder(); }
        protected override void OnLeave(EventArgs e) { focusWithin = false; base.OnLeave(e); RefreshOuterBorder(); }
        protected override void OnEnabledChanged(EventArgs e) { base.OnEnabledChanged(e); RefreshOuterBorder(); foreach (Control child in Controls) child.Invalidate(); }
        protected override void OnValueChanged(EventArgs e) { base.OnValueChanged(e); if (arrowButtons != null) arrowButtons.Invalidate(); }
        protected override void Dispose(bool disposing)
        {
            if (disposing && arrowWindow != null) { arrowWindow.Dispose(); arrowWindow = null; arrowButtons = null; }
            base.Dispose(disposing);
        }
        private void DrawArrows(Graphics g, Rectangle bounds)
        {
            int w = bounds.Width, h = bounds.Height; if (w <= 0 || h <= 0) return;
            g.FillRectangle(ArrowBack, 0, 0, w, h);
            g.DrawRectangle(ArrowBorder, 0, 0, Math.Max(0, w - 1), Math.Max(0, h - 1));
            g.DrawLine(ArrowBorder, 0, h / 2, w - 1, h / 2);
            int rows = Math.Min(4, Math.Min(w / 2, h / 2)); if (rows <= 0) return;
            int upper = (h / 2 - rows) / 2, lower = h / 2 + (h - h / 2 - rows) / 2;
            Brush up = !Enabled || Value >= Maximum ? ArrowDisabled : ArrowFore;
            Brush down = !Enabled || Value <= Minimum ? ArrowDisabled : ArrowFore;
            for (int row = 0; row < rows; row++)
            {
                int upWidth = 1 + row * 2, downWidth = 1 + (rows - 1 - row) * 2;
                g.FillRectangle(up, w / 2 - upWidth / 2, upper + row, upWidth, 1);
                g.FillRectangle(down, w / 2 - downWidth / 2, lower + row, downWidth, 1);
            }
        }
    }

    // Native edit behavior is retained; only its non-client frame is painted by the app.
    internal sealed class DarkTextBox : TextBox
    {
        [DllImport("user32.dll")] private static extern IntPtr GetWindowDC(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr dc);
        [DllImport("user32.dll")] private static extern bool RedrawWindow(IntPtr hwnd, IntPtr rect, IntPtr region, uint flags);
        private bool hover;
        internal DarkTextBox() { BackColor = DarkPalette.Field; ForeColor = DarkPalette.Foreground; BorderStyle = BorderStyle.FixedSingle; }
        private void PaintFrame()
        {
            if (Multiline || BorderStyle == BorderStyle.None || !IsHandleCreated || IsDisposed) return;
            IntPtr dc = GetWindowDC(Handle); if (dc == IntPtr.Zero) return;
            try { using (Graphics g = Graphics.FromHdc(dc)) using (Pen p = new Pen(Focused ? DarkPalette.Focus : hover ? Color.FromArgb(90, 90, 90) : DarkPalette.Border)) g.DrawRectangle(p, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1)); }
            finally { ReleaseDC(Handle, dc); }
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x85 && !Multiline && BorderStyle != BorderStyle.None) { PaintFrame(); m.Result = IntPtr.Zero; return; }
            base.WndProc(ref m); if (m.Msg == 0xF) PaintFrame();
        }
        protected override void OnMouseEnter(EventArgs e) { hover = true; base.OnMouseEnter(e); PaintFrame(); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; base.OnMouseLeave(e); PaintFrame(); }
        protected override void OnGotFocus(EventArgs e) { base.OnGotFocus(e); PaintFrame(); }
        protected override void OnLostFocus(EventArgs e) { base.OnLostFocus(e); PaintFrame(); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); if (!Multiline) NativeDarkTheme.Apply(this, ""); }
        protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); if (IsHandleCreated) RedrawWindow(Handle, IntPtr.Zero, IntPtr.Zero, 0x401); }
    }

    internal sealed class DarkComboBox : UserControl
    {
        internal sealed class ItemCollection
        {
            private readonly DarkComboBox owner;
            private readonly System.Collections.Generic.List<object> data = new System.Collections.Generic.List<object>();
            internal ItemCollection(DarkComboBox owner) { this.owner = owner; }
            public int Count { get { return data.Count; } }
            public object this[int index] { get { return data[index]; } }
            public void Add(object value) { data.Add(value); }
            public void AddRange(object[] values) { if (values != null) data.AddRange(values); }
            public void Clear() { data.Clear(); owner.ItemsCleared(); }
            internal int IndexOf(object value) { return data.IndexOf(value); }
        }

        private readonly TextBox editor = new TextBox();
        private readonly Panel arrow = new Panel();
        private readonly ToolStripDropDown popup;
        private readonly ListBox list = new ListBox();
        private bool syncing;
        private bool hover;
        private int selectedIndex = -1;
        private ComboBoxStyle dropDownStyle = ComboBoxStyle.DropDownList;
        public readonly ItemCollection Items;
        public event EventHandler SelectedIndexChanged;

        public ComboBoxStyle DropDownStyle
        {
            get { return dropDownStyle; }
            set { dropDownStyle = value; editor.ReadOnly = value != ComboBoxStyle.DropDown; }
        }
        public int SelectedIndex
        {
            get { return selectedIndex; }
            set
            {
                int next = value < -1 || value >= Items.Count ? -1 : value;
                if (selectedIndex == next) return;
                selectedIndex = next; syncing = true;
                editor.Text = next < 0 ? "" : Display(Items[next]);
                syncing = false;
                // The dropdown's ListBox is populated only when opened. Keeping its
                // SelectedIndex in sync before then throws ArgumentOutOfRangeException
                // during MainForm construction (the user-visible startup crash).
                list.SelectedIndex = next >= 0 && next < list.Items.Count ? next : -1;
                EventHandler handler = SelectedIndexChanged; if (handler != null) handler(this, EventArgs.Empty);
            }
        }
        public object SelectedItem
        {
            get { return selectedIndex >= 0 && selectedIndex < Items.Count ? Items[selectedIndex] : null; }
            set
            {
                int index = Items.IndexOf(value);
                if (index < 0 && value != null)
                    for (int i = 0; i < Items.Count; i++) if (String.Equals(Display(Items[i]), Convert.ToString(value), StringComparison.Ordinal)) { index = i; break; }
                SelectedIndex = index;
            }
        }
        public new string Text
        {
            get { return editor.Text; }
            set { editor.Text = value ?? ""; }
        }

        internal DarkComboBox()
        {
            Items = new ItemCollection(this);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = DarkPalette.Field; ForeColor = DarkPalette.Foreground; Height = DarkPalette.ControlHeight; MinimumSize = new Size(45, DarkPalette.ControlHeight); Margin = new Padding(0);
            editor.BorderStyle = BorderStyle.None; editor.BackColor = DarkPalette.Field; editor.ForeColor = DarkPalette.Foreground; editor.Font = Font; editor.ReadOnly = true;
            editor.TextChanged += (s, e) => { if (!syncing && dropDownStyle == ComboBoxStyle.DropDown) { selectedIndex = -1; EventHandler h = SelectedIndexChanged; if (h != null) h(this, EventArgs.Empty); } };
            editor.MouseDown += (s, e) => { if (dropDownStyle != ComboBoxStyle.DropDown) TogglePopup(); };
            editor.KeyDown += EditorKeyDown;
            arrow.BackColor = DarkPalette.Field; arrow.Cursor = Cursors.Hand; arrow.Paint += PaintArrow; arrow.Click += (s, e) => TogglePopup();
            Controls.Add(editor); Controls.Add(arrow);
            list.BorderStyle = BorderStyle.None; list.BackColor = DarkPalette.Field; list.ForeColor = DarkPalette.Foreground; list.DrawMode = DrawMode.OwnerDrawFixed; list.ItemHeight = 26; list.IntegralHeight = false; list.Font = Font;
            list.DrawItem += DrawListItem; list.Click += (s, e) => { if (list.SelectedIndex >= 0) { SelectedIndex = list.SelectedIndex; popup.Close(); editor.Focus(); } };
            list.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter && list.SelectedIndex >= 0) { SelectedIndex = list.SelectedIndex; popup.Close(); editor.Focus(); e.Handled = true; }
                else if (e.KeyCode == Keys.Escape) { popup.Close(); editor.Focus(); e.Handled = true; }
            };
            popup = new ToolStripDropDown { AutoClose = true, BackColor = DarkPalette.Border, Padding = new Padding(1), Margin = Padding.Empty, Renderer = new DarkDropDownRenderer() };
            var host = new ToolStripControlHost(list) { AutoSize = false, Margin = Padding.Empty, Padding = Padding.Empty, Size = new Size(240, 26 * 6) };
            popup.Items.Add(host);
            popup.Closed += (s, e) => { if (!IsDisposed && IsHandleCreated && Enabled) editor.Focus(); };
            NativeDarkTheme.ApplyOnHandleCreated(list, "DarkMode_Explorer");
            SizeChanged += (s, e) => LayoutParts(); LayoutParts();
            MouseEnter += HoverEnter; MouseLeave += HoverLeave; editor.MouseEnter += HoverEnter; editor.MouseLeave += HoverLeave; arrow.MouseEnter += HoverEnter; arrow.MouseLeave += HoverLeave;
            EnabledChanged += (s, e) => { editor.Enabled = Enabled; editor.BackColor = Enabled ? DarkPalette.Field : DarkPalette.Surface; editor.ForeColor = Enabled ? DarkPalette.Foreground : DarkPalette.Muted; arrow.Invalidate(); Invalidate(); };
        }
        private static string Display(object item) { return item == null ? "" : Convert.ToString(item); }
        private void ItemsCleared()
        {
            bool changed = selectedIndex != -1; selectedIndex = -1; syncing = true; editor.Text = ""; syncing = false; list.Items.Clear();
            if (changed) { EventHandler handler = SelectedIndexChanged; if (handler != null) handler(this, EventArgs.Empty); }
        }
        private void LayoutParts()
        {
            int arrowWidth = 25; editor.Bounds = new Rectangle(8, Math.Max(1, (Height - editor.PreferredHeight) / 2), Math.Max(0, Width - arrowWidth - 12), editor.PreferredHeight);
            arrow.Bounds = new Rectangle(Math.Max(0, Width - arrowWidth), 1, Math.Max(0, arrowWidth - 1), Math.Max(0, Height - 2));
        }
        protected override void OnFontChanged(EventArgs e) { base.OnFontChanged(e); if (editor != null) { editor.Font = Font; if (list != null) list.Font = Font; LayoutParts(); } }
        protected override void OnPaintBackground(PaintEventArgs e) { using (Brush b = new SolidBrush(Enabled ? DarkPalette.Field : DarkPalette.Surface)) e.Graphics.FillRectangle(b, ClientRectangle); }
        protected override void OnPaint(PaintEventArgs e)
        {
            Color border = !Enabled ? DarkPalette.Border : ContainsFocus ? DarkPalette.Focus : hover ? Color.FromArgb(88, 94, 101) : DarkPalette.Border;
            using (Pen p = new Pen(border)) e.Graphics.DrawRectangle(p, 0, 0, Math.Max(0, Width - 1), Math.Max(0, Height - 1));
            using (Pen p = new Pen(DarkPalette.Border)) e.Graphics.DrawLine(p, Width - 26, 1, Width - 26, Height - 2);
        }
        private void PaintArrow(object sender, PaintEventArgs e)
        {
            int x = e.ClipRectangle.Width / 2, y = e.ClipRectangle.Height / 2;
            Point[] points = { new Point(x - 4, y - 2), new Point(x + 4, y - 2), new Point(x, y + 2) };
            using (Brush b = new SolidBrush(!Enabled ? Color.Gray : hover || ContainsFocus ? DarkPalette.Foreground : DarkPalette.Muted)) e.Graphics.FillPolygon(b, points);
        }
        private void DrawListItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= list.Items.Count) return;
            bool active = (e.State & DrawItemState.Selected) != 0;
            using (Brush b = new SolidBrush(active ? Color.FromArgb(54, 78, 42) : DarkPalette.Field)) e.Graphics.FillRectangle(b, e.Bounds);
            TextRenderer.DrawText(e.Graphics, Display(list.Items[e.Index]), Font, Rectangle.Inflate(e.Bounds, -8, 0), DarkPalette.Foreground, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
        private void TogglePopup()
        {
            if (!Enabled || Items.Count == 0) return;
            if (popup.Visible) { popup.Close(); return; }
            list.BeginUpdate(); list.Items.Clear(); foreach (object item in EnumerableItems()) list.Items.Add(item); list.SelectedIndex = selectedIndex; list.EndUpdate();
            int height = Math.Min(26 * Math.Min(Items.Count, 8), Screen.FromControl(this).WorkingArea.Height / 2);
            ToolStripControlHost host = (ToolStripControlHost)popup.Items[0]; host.Size = new Size(Math.Max(Width - 2, 150), height);
            list.Size = host.Size; popup.Show(this, new Point(0, Height)); list.Focus();
        }
        private System.Collections.Generic.IEnumerable<object> EnumerableItems() { for (int i = 0; i < Items.Count; i++) yield return Items[i]; }
        private void EditorKeyDown(object sender, KeyEventArgs e)
        {
            if ((e.Alt && e.KeyCode == Keys.Down) || e.KeyCode == Keys.F4 || (dropDownStyle == ComboBoxStyle.DropDownList && (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space))) { TogglePopup(); e.Handled = true; return; }
            if (popup.Visible && e.KeyCode == Keys.Escape) { popup.Close(); e.Handled = true; return; }
            if (dropDownStyle == ComboBoxStyle.DropDownList && (e.KeyCode == Keys.Down || e.KeyCode == Keys.Up || e.KeyCode == Keys.Home || e.KeyCode == Keys.End))
            {
                if (Items.Count == 0) return;
                int next = e.KeyCode == Keys.Home ? 0 : e.KeyCode == Keys.End ? Items.Count - 1 : Math.Max(0, Math.Min(Items.Count - 1, selectedIndex + (e.KeyCode == Keys.Down ? 1 : -1)));
                SelectedIndex = next; e.Handled = true;
            }
        }
        private void HoverEnter(object sender, EventArgs e) { hover = true; Invalidate(); arrow.Invalidate(); }
        private void HoverLeave(object sender, EventArgs e)
        {
            Point p = PointToClient(Cursor.Position); hover = ClientRectangle.Contains(p); Invalidate(); arrow.Invalidate();
        }
        protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if (e.Button == MouseButtons.Left) TogglePopup(); }
        protected override void Dispose(bool disposing) { if (disposing && popup != null) popup.Dispose(); base.Dispose(disposing); }

        private sealed class DarkDropDownRenderer : ToolStripProfessionalRenderer
        {
            internal DarkDropDownRenderer() : base(new DarkColorTable()) { RoundedEdges = false; }
            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e) { using (Pen p = new Pen(DarkPalette.Border)) e.Graphics.DrawRectangle(p, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1); }
        }
        private sealed class DarkColorTable : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return DarkPalette.Field; } }
            public override Color MenuBorder { get { return DarkPalette.Border; } }
            public override Color MenuItemSelected { get { return DarkPalette.Surface; } }
            public override Color ImageMarginGradientBegin { get { return DarkPalette.Field; } }
            public override Color ImageMarginGradientMiddle { get { return DarkPalette.Field; } }
            public override Color ImageMarginGradientEnd { get { return DarkPalette.Field; } }
        }
    }

    internal sealed class DarkTabHost : UserControl
    {
        private readonly FlowLayoutPanel header = new FlowLayoutPanel();
        private readonly Panel pageSurface = new Panel();
        private readonly System.Collections.Generic.List<Panel> pages = new System.Collections.Generic.List<Panel>();
        private readonly System.Collections.Generic.List<DarkTabButton> buttons = new System.Collections.Generic.List<DarkTabButton>();
        private int selected;
        internal DarkTabHost()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = DarkPalette.Background; Padding = Padding.Empty;
            header.Dock = DockStyle.Top; header.Height = 36; header.WrapContents = false; header.BackColor = DarkPalette.Background; header.Padding = Padding.Empty; header.Margin = Padding.Empty;
            pageSurface.Dock = DockStyle.Fill; pageSurface.BackColor = DarkPalette.Background; pageSurface.Margin = Padding.Empty; pageSurface.Padding = Padding.Empty;
            Controls.Add(pageSurface); Controls.Add(header);
        }
        internal void AddPage(string title, Panel page)
        {
            page.Dock = DockStyle.Fill; page.BackColor = DarkPalette.Background; page.Visible = pages.Count == 0; pages.Add(page); pageSurface.Controls.Add(page);
            var button = new DarkTabButton { Text = title, Width = 112, Height = 36, Margin = Padding.Empty, Selected = pages.Count == 1 };
            int index = buttons.Count; button.Click += (s, e) => SelectPage(index); buttons.Add(button); header.Controls.Add(button);
            if (pages.Count == 1) selected = 0;
        }
        private void SelectPage(int index)
        {
            if (index < 0 || index >= pages.Count || selected == index) return;
            pages[selected].Visible = false; buttons[selected].Selected = false;
            selected = index; pages[selected].Visible = true; pages[selected].BringToFront(); buttons[selected].Selected = true;
        }
        protected override void OnPaintBackground(PaintEventArgs e) { using (Brush b = new SolidBrush(DarkPalette.Background)) e.Graphics.FillRectangle(b, ClientRectangle); }
        protected override void OnPaint(PaintEventArgs e)
        {
            using (Pen p = new Pen(DarkPalette.Border)) e.Graphics.DrawRectangle(p, 0, header.Bottom, Math.Max(0, Width - 1), Math.Max(0, Height - header.Bottom - 1));
        }
    }

    internal sealed class DarkTabButton : Control
    {
        private bool selected;
        internal bool Selected { get { return selected; } set { if (selected != value) { selected = value; Invalidate(); } } }
        internal DarkTabButton() { SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true); Cursor = Cursors.Hand; }
        protected override void OnPaint(PaintEventArgs e)
        {
            using (Brush b = new SolidBrush(Selected ? DarkPalette.Surface : DarkPalette.Background)) e.Graphics.FillRectangle(b, ClientRectangle);
            TextRenderer.DrawText(e.Graphics, Text, Font, ClientRectangle, DarkPalette.Foreground, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            if (Selected) using (Brush b = new SolidBrush(DarkPalette.Accent)) e.Graphics.FillRectangle(b, 10, Height - 3, Width - 20, 2);
        }
        protected override void OnClick(EventArgs e) { base.OnClick(e); Invalidate(); }
    }

    internal class DarkSettingsForm : Form
    {
        protected override CreateParams CreateParams
        {
            get { CreateParams cp = base.CreateParams; if ((cp.ClassStyle & 0xE0) == 0) cp.ExStyle |= 0x02000000; return cp; }
        }
        protected DarkSettingsForm() { SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true); }
    }
}
