using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace dotSharK.UiV2
{
    /// <summary>
    /// Multiline dark editor with self-drawn 10px scrollbar. Native edit
    /// scrollbar is never created; EM_LINESCROLL keeps native text selection,
    /// undo, clipboard and IME behavior. Long wrapped lines are handled by
    /// EM_GETLINECOUNT (visual lines, not merely newline counts).
    /// </summary>
    public sealed class FlatTextArea : FlatTextBox
    {
        private const int EmGetFirstVisibleLine = 0x00CE;
        private const int EmGetLineCount = 0x00BA;
        private const int EmLineScroll = 0x00B6;
        private readonly DarkScrollBar scrollbar;
        private readonly EditHook hook;
        private bool updating, syncPosted;

        public FlatTextArea()
        {
            SuspendLayout();
            try
            {
                Height = Theme.Space24 * 4;
                MinimumSize = new Size(Theme.Space24, Theme.ControlHeight);
                Font = Theme.MonoFont;
                editor.Multiline = true;
                editor.AcceptsReturn = true;
                editor.AcceptsTab = false;
                editor.WordWrap = true;
                editor.ScrollBars = System.Windows.Forms.ScrollBars.None;
                editor.Dock = DockStyle.Fill;
                scrollbar = new DarkScrollBar { Dock = DockStyle.Right };
                Controls.Add(scrollbar);
                scrollbar.BringToFront();
                scrollbar.ValueChanged += (s, e) => ScrollToLine(scrollbar.Value);
                editor.TextChanged += (s, e) => BeginSync();
                editor.Resize += (s, e) => BeginSync();
                editor.MouseWheel += (s, e) => BeginSync();
                editor.KeyUp += (s, e) => BeginSync();
                editor.MouseUp += (s, e) => BeginSync();
                // NativeWindow observes scrolling without a per-editor polling timer.
                hook = new EditHook(BeginSync);
                editor.HandleCreated += (s, e) => { hook.AssignHandle(editor.Handle); BeginSync(); };
                editor.HandleDestroyed += (s, e) => { if (hook.Handle != IntPtr.Zero) hook.ReleaseHandle(); };
                if (editor.IsHandleCreated) { hook.AssignHandle(editor.Handle); BeginSync(); }
            }
            finally { ResumeLayout(true); }
        }

        public bool WordWrap { get { return editor.WordWrap; } set { editor.WordWrap = value; BeginSync(); } }
        public bool AcceptsReturn { get { return editor.AcceptsReturn; } set { editor.AcceptsReturn = value; } }
        public bool AcceptsTab { get { return editor.AcceptsTab; } set { editor.AcceptsTab = value; } }
        public ScrollBars ScrollBars { get { return System.Windows.Forms.ScrollBars.Vertical; } set { /* always custom vertical */ } }
        public bool HideSelection { get { return editor.HideSelection; } set { editor.HideSelection = value; } }
        public string[] Lines { get { return editor.Lines; } set { editor.Lines = value; } }

        private void BeginSync()
        {
            if (IsDisposed || !IsHandleCreated || syncPosted) return;
            syncPosted = true;
            try
            {
                BeginInvoke((Action)(() =>
                {
                    syncPosted = false;
                    if (!IsDisposed) SyncScrollBar();
                }));
            }
            catch (InvalidOperationException) { syncPosted = false; }
        }
        private int NativeLineCount
        {
            get { return editor.IsHandleCreated ? DarkNative.SendMessage(editor.Handle, EmGetLineCount,
                IntPtr.Zero, IntPtr.Zero).ToInt32() : 0; }
        }
        private int NativeTopLine
        {
            get { return editor.IsHandleCreated ? DarkNative.SendMessage(editor.Handle, EmGetFirstVisibleLine,
                IntPtr.Zero, IntPtr.Zero).ToInt32() : 0; }
        }
        private void SyncScrollBar()
        {
            if (updating || IsDisposed || !editor.IsHandleCreated) return;
            updating = true;
            try
            {
                int display = Math.Max(1, editor.ClientSize.Height /
                    Math.Max(1, TextRenderer.MeasureText("Ag", editor.Font).Height));
                int lines = NativeLineCount;
                scrollbar.LargeChange = display;
                scrollbar.Maximum = Math.Max(0, lines - display);
                scrollbar.Value = Math.Max(0, Math.Min(scrollbar.Maximum, NativeTopLine));
            }
            finally { updating = false; }
        }
        private void ScrollToLine(int target)
        {
            if (updating || !editor.IsHandleCreated) return;
            int offset = target - NativeTopLine;
            if (offset != 0)
                DarkNative.SendMessage(editor.Handle, EmLineScroll, IntPtr.Zero,
                    new IntPtr(offset));
        }
        private sealed class EditHook : NativeWindow
        {
            private readonly Action changed;
            internal EditHook(Action changed) { this.changed = changed; }
            protected override void WndProc(ref Message m)
            {
                base.WndProc(ref m);
                // WM_VSCROLL, WM_MOUSEWHEEL, WM_KEYUP, WM_LBUTTONUP.
                if (m.Msg == 0x0115 || m.Msg == 0x020A ||
                    m.Msg == 0x0101 || m.Msg == 0x0202)
                    changed();
            }
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing && hook != null && hook.Handle != IntPtr.Zero) hook.ReleaseHandle();
            base.Dispose(disposing);
        }
    }
}