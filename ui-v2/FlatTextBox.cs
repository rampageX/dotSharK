using System;
using System.Drawing;
using System.Windows.Forms;

namespace dotSharK.UiV2
{
    /// <summary>
    /// Borderless native editor inside a square, self-painted dark border.
    /// Keeps Text, TextChanged, ReadOnly, SelectionStart, SelectionLength,
    /// SelectedText, MaxLength, PasswordChar and keyboard edit shortcuts.
    /// </summary>
    public class FlatTextBox : UserControl
    {
        protected readonly TextBox editor;
        private bool focused;
        private bool initializing;

        public FlatTextBox()
        {
            initializing = true;
            SetStyle(DarkPaint.Buffered | ControlStyles.ContainerControl, true);
            DarkPaint.Setup(this);
            TabStop = false;
            Height = Theme.ControlHeight;
            MinimumSize = new Size(Theme.Space24, Theme.ControlHeight);
            Padding = new Padding(Theme.Space8, Theme.Space4, Theme.Space8, Theme.Space4);
            editor = new TextBox
            {
                BorderStyle = BorderStyle.None, Dock = DockStyle.Fill,
                BackColor = Theme.Input, ForeColor = Theme.PrimaryText,
                Font = Theme.BodyFont, Margin = Padding.Empty,
                ShortcutsEnabled = true
            };
            Controls.Add(editor);
            DarkTextContextMenu.Create(editor);
            editor.GotFocus += (s, e) => { focused = true; Invalidate(); };
            editor.LostFocus += (s, e) => { focused = false; CommitPendingInput(); Invalidate(); };
            editor.TextChanged += (s, e) => base.OnTextChanged(e);
            initializing = false;
        }
        protected virtual void CommitPendingInput() { }
        protected TextBox Editor { get { return editor; } }
        public override string Text
        {
            get { return editor == null ? base.Text : editor.Text; }
            set
            {
                if (editor != null) editor.Text = value ?? "";
                else base.Text = value;
            }
        }
        public bool ReadOnly { get { return editor.ReadOnly; } set { editor.ReadOnly = value; Invalidate(); } }
        public int MaxLength { get { return editor.MaxLength; } set { editor.MaxLength = value; } }
        public int TextLength { get { return editor.TextLength; } }
        public int SelectionStart { get { return editor.SelectionStart; } set { editor.SelectionStart = value; } }
        public int SelectionLength { get { return editor.SelectionLength; } set { editor.SelectionLength = value; } }
        public string SelectedText { get { return editor.SelectedText; } set { editor.SelectedText = value; } }
        public char PasswordChar { get { return editor.PasswordChar; } set { editor.PasswordChar = value; } }
        public bool UseSystemPasswordChar { get { return editor.UseSystemPasswordChar; } set { editor.UseSystemPasswordChar = value; } }
        public CharacterCasing CharacterCasing { get { return editor.CharacterCasing; } set { editor.CharacterCasing = value; } }
        public HorizontalAlignment TextAlign { get { return editor.TextAlign; } set { editor.TextAlign = value; } }
        public void SelectAll() { editor.SelectAll(); }
        public void Clear() { editor.Clear(); }
        public void AppendText(string text) { editor.AppendText(text); }
        public void Copy() { editor.Copy(); }
        public void Cut() { editor.Cut(); }
        public void Paste() { editor.Paste(); }
        public void ScrollToCaret() { editor.ScrollToCaret(); }
        protected override void OnEnter(EventArgs e) { base.OnEnter(e); if (!editor.Focused) editor.Focus(); }
        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            if (editor != null) editor.Enabled = Enabled;
            Invalidate();
        }
        protected override void OnFontChanged(EventArgs e)
        {
            base.OnFontChanged(e);
            if (editor != null && !initializing) editor.Font = Font;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Input);
            DarkPaint.Border(e.Graphics, ClientRectangle, !Enabled ? Theme.DisabledText :
                focused ? Theme.AccentFocus : ReadOnly ? Theme.ScrollThumb : Theme.Border);
        }
    }
}