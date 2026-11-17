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
        private readonly Timer sync;
        private bool updating;

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
                sync = new Timer { Interval = 100 };
                sync.Tick += (s, e) => SyncScrollBar();
                sync.Start();
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
            if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)(() => SyncScrollBar()));
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
        protected override void Dispose(bool disposing)
        {
            if (disposing && sync != null) { sync.Stop(); sync.Dispose(); }
            base.Dispose(disposing);
        }
    }
}