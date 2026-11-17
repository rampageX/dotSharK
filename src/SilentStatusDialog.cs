using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace dotSharK
{
    // Read-only dashboard for the independent browser download queue.
    internal sealed class SilentStatusDialog : DarkSettingsForm
    {
        private readonly Label heading;
        private readonly Label current;
        private readonly Label queued;
        private readonly Label percent;
        private readonly DarkProgressBar progress;
        private readonly DarkTextBox log;
        private readonly System.Windows.Forms.Timer refresh;
        private string lastLog = "";

        internal SilentStatusDialog()
        {
            Text = "dotSharK · 静默下载状态";
            Icon = AppBranding.CreateIcon();
            Size = new Size(820, 530);
            MinimumSize = new Size(680, 400);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = DarkPalette.Background;
            ForeColor = DarkPalette.Foreground;
            Font = new Font("Microsoft YaHei UI", 9F);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6,
                BackColor = DarkPalette.Background, Padding = new Padding(16, 14, 16, 12)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40));

            heading = new Label { Text = "浏览器静默下载", Font = new Font(Font, FontStyle.Bold),
                ForeColor = DarkPalette.Accent, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            root.Controls.Add(heading, 0, 0);

            current = new Label { Text = "当前没有下载任务", ForeColor = DarkPalette.Foreground,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            root.Controls.Add(current, 0, 1);

            queued = new Label { Text = "排队 0", ForeColor = DarkPalette.Muted,
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            root.Controls.Add(queued, 0, 2);

            var progressRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = DarkPalette.Background };
            progressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            progressRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 84));
            progress = new DarkProgressBar { Dock = DockStyle.Fill, Margin = new Padding(0, 8, 12, 10) };
            progress.SetIndeterminate(false);
            progress.SetProgress(0);
            percent = new Label { Text = "—", ForeColor = DarkPalette.Muted, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleRight };
            progressRow.Controls.Add(progress, 0, 0);
            progressRow.Controls.Add(percent, 1, 0);
            root.Controls.Add(progressRow, 0, 3);

            log = new DarkTextBox { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true,
                BorderStyle = BorderStyle.None, ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 8.5F), ForeColor = DarkPalette.Foreground };
            var logFrame = new Panel { Dock = DockStyle.Fill, BackColor = DarkPalette.Border, Padding = new Padding(1) };
            logFrame.Controls.Add(log);
            root.Controls.Add(logFrame, 0, 4);
            NativeDarkTheme.ApplyOnHandleCreated(log, "DarkMode_Explorer");
            NativeDarkTheme.ApplyScrollBarsOnHandleCreated(log, "DarkMode_Explorer");

            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false, BackColor = DarkPalette.Background, Padding = Padding.Empty };
            var close = new DarkButton { Text = "关闭", Width = 100, Height = DarkPalette.ControlHeight };
            close.Click += (s, e) => Close();
            var open = new DarkButton { Text = "打开下载目录", Width = 128, Height = DarkPalette.ControlHeight, Margin = new Padding(0, 0, 10, 0) };
            open.Click += (s, e) =>
            {
                try
                {
                    string folder = SilentDownloadBridge.SilentFolder;
                    Directory.CreateDirectory(folder);
                    Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "打开目录失败", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            footer.Controls.Add(close);
            footer.Controls.Add(open);
            root.Controls.Add(footer, 0, 5);
            Controls.Add(root);

            refresh = new System.Windows.Forms.Timer { Interval = 850 };
            refresh.Tick += (s, e) => RefreshStatus();
            Shown += (s, e) => { RefreshStatus(); refresh.Start(); };
            FormClosed += (s, e) => refresh.Stop();
        }

        private void RefreshStatus()
        {
            string active = SilentDownloadBridge.ActiveUrl;
            int pending = SilentDownloadBridge.PendingCount;
            string text = SilentDownloadBridge.ReadRecentLog();
            bool working = !String.IsNullOrEmpty(active);
            current.Text = working ? "正在下载 · " + active : pending > 0 ? "等待后台队列处理…" : "当前没有下载任务";
            queued.Text = "等待 " + pending + " 项" + (working ? "   ·   当前下载 1 项" : "   ·   后台队列就绪");

            if (!String.Equals(text, lastLog, StringComparison.Ordinal))
            {
                lastLog = text;
                log.Text = text;
                log.SelectionStart = log.TextLength;
                log.ScrollToCaret();
            }

            if (!working)
            {
                progress.SetIndeterminate(false);
                progress.SetProgress(0);
                percent.Text = "—";
                return;
            }

            // Only use progress output after the last [开始] line; never show stale
            // percentages from the previously completed download.
            int start = text.LastIndexOf("[开始]", StringComparison.Ordinal);
            string recent = start >= 0 ? text.Substring(start) : "";
            MatchCollection matches = Regex.Matches(recent, @"\[download\]\s+(\d+(?:\.\d+)?)%");
            if (matches.Count > 0)
            {
                double value;
                if (Double.TryParse(matches[matches.Count - 1].Groups[1].Value,
                        System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out value))
                {
                    progress.SetIndeterminate(false);
                    progress.SetProgress(value / 100.0);
                    percent.Text = value.ToString("0.#") + "%";
                    return;
                }
            }
            progress.SetIndeterminate(true);
            percent.Text = "解析中";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) refresh.Dispose();
            base.Dispose(disposing);
        }
    }
}
