using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace dotSharK.UiV2
{
    /// <summary>
    /// Dark modal alternative to MessageBox.Show with matching five-argument
    /// signature, native DialogResult values and default Enter/Esc semantics.
    /// </summary>
    public static class DarkMessageBox
    {
        public static DialogResult Show(IWin32Window owner, string text, string caption,
            MessageBoxButtons buttons, MessageBoxIcon icon)
        {
            using (var dialog = new Dialog(text ?? "", caption ?? "", buttons, icon))
                return owner == null ? dialog.ShowDialog() : dialog.ShowDialog(owner);
        }
        public static DialogResult Show(string text, string caption)
        { return Show(null, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None); }
        public static DialogResult Show(IWin32Window owner, string text, string caption)
        { return Show(owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.None); }

        private sealed class Dialog : DarkForm
        {
            internal Dialog(string text, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
            {
                SuspendLayout();
                try
                {
                    Text = caption;
                    ShowInTaskbar = false;
                    FormBorderStyle = FormBorderStyle.FixedDialog;
                    MaximizeBox = false;
                    MinimizeBox = false;
                    MinimumSize = new Size(Theme.Space24 * 15, Theme.Space24 * 7);
                    ClientSize = new Size(Theme.Space24 * 22, Theme.Space24 * 9);
                    var root = new TableLayoutPanel
                    {
                        Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
                        Padding = new Padding(Theme.Space24), Margin = Padding.Empty,
                        BackColor = Theme.Window
                    };
                    root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                    root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ActionHeight + Theme.Space16));
                    var body = new TableLayoutPanel
                    {
                        Dock = DockStyle.Fill, ColumnCount = icon == MessageBoxIcon.None ? 1 : 2,
                        RowCount = 1, Margin = Padding.Empty, Padding = Padding.Empty,
                        BackColor = Theme.Window
                    };
                    if (icon != MessageBoxIcon.None)
                    {
                        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.Space24 * 2));
                        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                        body.Controls.Add(new IconGlyph(icon) { Dock = DockStyle.Top,
                            Height = Theme.Space24, Margin = Padding.Empty }, 0, 0);
                    }
                    var message = new Label
                    {
                        Text = text, Font = Theme.BodyFont, ForeColor = Theme.PrimaryText,
                        BackColor = Theme.Window, Dock = DockStyle.Fill, Margin = Padding.Empty,
                        TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = false
                    };
                    body.Controls.Add(message, icon == MessageBoxIcon.None ? 0 : 1, 0);
                    var footer = new FlowLayoutPanel
                    {
                        Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
                        WrapContents = false, Margin = Padding.Empty,
                        Padding = new Padding(0, Theme.Space16, 0, 0),
                        BackColor = Theme.Window
                    };
                    var choices = GetButtons(buttons);
                    IButtonControl accept = null, cancel = null;
                    for (int i = choices.Count - 1; i >= 0; i--)
                    {
                        var choice = choices[i];
                        DarkButtonBase button = i == 0 ? (DarkButtonBase)new PrimaryButton() : new SecondaryButton();
                        button.Text = choice.Item1;
                        button.Width = Theme.FooterButtonWidth;
                        button.Height = Theme.ActionHeight;
                        button.Margin = new Padding(Theme.Space8, 0, 0, 0);
                        button.DialogResult = choice.Item2;
                        footer.Controls.Add(button);
                        if (i == 0) accept = button;
                        if (choice.Item2 == DialogResult.Cancel || choice.Item2 == DialogResult.No)
                            cancel = button;
                    }
                    if (cancel == null && choices.Count == 1) cancel = accept;
                    AssignDialogButtons(accept, cancel);
                    root.Controls.Add(body, 0, 0);
                    root.Controls.Add(footer, 0, 1);
                    Controls.Add(root);
                }
                finally { ResumeLayout(true); }
            }
            private static List<Tuple<string, DialogResult>> GetButtons(MessageBoxButtons buttons)
            {
                switch (buttons)
                {
                    case MessageBoxButtons.OKCancel: return new List<Tuple<string, DialogResult>> {
                        Tuple.Create("确定", DialogResult.OK), Tuple.Create("取消", DialogResult.Cancel) };
                    case MessageBoxButtons.YesNo: return new List<Tuple<string, DialogResult>> {
                        Tuple.Create("是", DialogResult.Yes), Tuple.Create("否", DialogResult.No) };
                    case MessageBoxButtons.YesNoCancel: return new List<Tuple<string, DialogResult>> {
                        Tuple.Create("是", DialogResult.Yes), Tuple.Create("否", DialogResult.No),
                        Tuple.Create("取消", DialogResult.Cancel) };
                    case MessageBoxButtons.RetryCancel: return new List<Tuple<string, DialogResult>> {
                        Tuple.Create("重试", DialogResult.Retry), Tuple.Create("取消", DialogResult.Cancel) };
                    case MessageBoxButtons.AbortRetryIgnore: return new List<Tuple<string, DialogResult>> {
                        Tuple.Create("中止", DialogResult.Abort), Tuple.Create("重试", DialogResult.Retry),
                        Tuple.Create("忽略", DialogResult.Ignore) };
                    case MessageBoxButtons.CancelTryContinue: return new List<Tuple<string, DialogResult>> {
                        Tuple.Create("取消", DialogResult.Cancel), Tuple.Create("重试", DialogResult.TryAgain),
                        Tuple.Create("继续", DialogResult.Continue) };
                    default: return new List<Tuple<string, DialogResult>> {
                        Tuple.Create("确定", DialogResult.OK) };
                }
            }
        }
        private sealed class IconGlyph : Control
        {
            private readonly MessageBoxIcon icon;
            internal IconGlyph(MessageBoxIcon icon)
            {
                this.icon = icon;
                SetStyle(DarkPaint.Buffered, true);
                BackColor = Theme.Window;
            }
            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.Clear(Theme.Window);
                string glyph;
                Color color;
                switch (icon)
                {
                    case MessageBoxIcon.Error: glyph = "×"; color = Theme.Error; break;
                    case MessageBoxIcon.Warning: glyph = "!"; color = Theme.Warning; break;
                    case MessageBoxIcon.Question: glyph = "?"; color = Theme.SuccessText; break;
                    default: glyph = "i"; color = Theme.SuccessText; break;
                }
                DarkPaint.DrawText(e.Graphics, glyph, Theme.HeadingFont, ClientRectangle,
                    color, TextFormatFlags.HorizontalCenter);
            }
        }
    }
}