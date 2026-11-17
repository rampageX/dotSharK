using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using dotSharK.UiV2;
using V2Combo = dotSharK.UiV2.DarkComboBox;
using Microsoft.Win32.SafeHandles;

namespace dotSharK
{
    internal sealed class ConfigurationDialogV2 : DarkForm
    {
        private sealed class UpdatePromptForm : DarkForm { internal UpdatePromptForm() { } }
        private readonly Color bg = Theme.Window, panel = Theme.Panel, field = Theme.Input, green = Theme.SuccessText, fg = Theme.PrimaryText, muted = Theme.SecondaryText;
        private readonly DarkToolTip v2ToolTips = new DarkToolTip();
        private readonly FlatTextBox[] toolPaths = new FlatTextBox[5];
        private readonly string[] toolNames = { "yt-dlp.exe", "ffmpeg.exe", "ffprobe.exe", "deno.exe", "aria2c.exe" };
        private FlatTextBox proxyBox, geoProxyBox, profileRootBox, cookieFileBox;
        private FlatTextBox silentFolderBox;
        private FlatTextArea ariaSitesBox, noProxySitesBox, customArgumentsBox;
        private DarkCheckBox proxyEnabledBox, geoProxyEnabledBox;
        private DarkNumeric fragmentsBox;
        private DarkNumeric downloadsBox;
        private DarkNumeric qualityLimitBox;
        private V2Combo sourceBox, browserBox, profileBox;
        private Button profileSearchButton;
        private Label profileStatus, profileFoundCheck;
        private Label toolUpdateStatus;
        private readonly List<IconButton> toolUpdateButtons = new List<IconButton>();
        private bool toolUpdateRunning;
        private int profileSearchGeneration;

        public string YtDlpPath { get { return toolPaths[0].Text.Trim(); } }
        public string FfmpegPath { get { return toolPaths[1].Text.Trim(); } }
        public string FfprobePath { get { return toolPaths[2].Text.Trim(); } }
        public string DenoPath { get { return toolPaths[3].Text.Trim(); } }
        public string Aria2cPath { get { return toolPaths[4].Text.Trim(); } }
        public YtDlpOptions Options { get; private set; }

        public ConfigurationDialogV2(string ytDlp, string ffmpeg, string ffprobe,
            string deno, string aria2c, YtDlpOptions options)
        {
            options = options ?? new YtDlpOptions();
            Options = new YtDlpOptions
            {
                Proxy = options.Proxy, GeoVerificationProxy = options.GeoVerificationProxy,
                ConcurrentFragments = options.ConcurrentFragments,
                ConcurrentDownloads = options.ConcurrentDownloads,
                VideoQualityLimit = options.VideoQualityLimit,
                CookieMode = options.CookieMode, CookieBrowser = options.CookieBrowser,
                CookieProfileRoot = options.CookieProfileRoot, CookieProfile = options.CookieProfile,
                CookieFile = options.CookieFile, SilentDownloadFolder = options.SilentDownloadFolder,
                ProxyEnabled = options.ProxyEnabled, GeoProxyEnabled = options.GeoProxyEnabled,
                Aria2cPath = options.Aria2cPath, Aria2cSites = options.Aria2cSites,
                NoProxySites = options.NoProxySites, CustomArguments = options.CustomArguments
            };
            SuspendLayout();
            try
            {
                Text = "dotSharK 配置";
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false;
                Icon = AppBranding.CreateIcon();
                BackColor = Theme.Window; ForeColor = Theme.PrimaryText;
                Font = Theme.BodyFont;
                ClientSize = Theme.SettingsClientSize;
                MinimumSize = Size; MaximumSize = Size;
                var root = new TableLayoutPanel
                {
                    Name = "layoutSettingsRoot", Dock = DockStyle.Fill,
                    BackColor = Theme.Window, Margin = Padding.Empty,
                    Padding = new Padding(Theme.Space24), ColumnCount = 1, RowCount = 2
                };
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute,
                    Theme.ActionHeight + Theme.Space16 * 2 + Theme.BorderWidth));
                Controls.Add(root);
                var tabs = new DarkTabStrip { Name = "tabSettings", Dock = DockStyle.Fill,
                    Margin = Padding.Empty };
                var pageTools = NewPage();
                var pageYt = NewPage();
                var pageAdvanced = NewPage();
                var pageSilent = NewPage();
                tabs.AddPage("外部工具", pageTools);
                tabs.AddPage("YT-DLP", pageYt);
                tabs.AddPage("高级下载", pageAdvanced);
                tabs.AddPage("静默下载", pageSilent);
                root.Controls.Add(tabs, 0, 0);
                BuildToolsPage(pageTools, new[] { ytDlp, ffmpeg, ffprobe, deno, aria2c });
                BuildYtPage(pageYt);
                BuildAdvancedPage(pageAdvanced);
                BuildSilentPage(pageSilent);
                tabs.SelectedIndex = 1;

                var footer = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill, Margin = Padding.Empty,
                    Padding = new Padding(0, Theme.Space16, 0, Theme.Space16),
                    ColumnCount = 4, RowCount = 2, BackColor = Theme.Window
                };
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.FooterButtonWidth));
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.Space8));
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.FooterButtonWidth));
                footer.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.BorderWidth));
                footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                var divider = new Panel { Dock = DockStyle.Fill, Margin = Padding.Empty,
                    BackColor = Theme.Divider };
                footer.Controls.Add(divider, 0, 0);
                footer.SetColumnSpan(divider, 4);
                var cancel = StyledButton("取消", false);
                var save = StyledButton("保存", true);
                cancel.Name = "btnCancel"; save.Name = "btnSave";
                cancel.DialogResult = DialogResult.Cancel;
                save.DialogResult = DialogResult.None;
                cancel.Height = save.Height = Theme.ActionHeight;
                cancel.Dock = save.Dock = DockStyle.Fill;
                cancel.Margin = save.Margin = Padding.Empty;
                save.Click += SaveClicked;
                footer.Controls.Add(cancel, 1, 1);
                footer.Controls.Add(save, 3, 1);
                root.Controls.Add(footer, 0, 1);
                AssignDialogButtons(save, cancel);
                ActiveControl = tabs;
            }
            finally { ResumeLayout(true); }
        }

        private static Panel NewPage()
        {
            return new Panel { BackColor = Theme.Window, Dock = DockStyle.Fill,
                Margin = Padding.Empty, Padding = new Padding(0, Theme.Space16, 0, 0) };
        }
        private static Label Label(string text, bool heading = false)
        {
            return new Label { Text = text,
                ForeColor = heading ? Theme.PrimaryText : Theme.SecondaryText,
                Font = heading ? Theme.HeadingFont : Theme.BodyFont,
                TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true,
                Dock = DockStyle.Fill, Margin = Padding.Empty };
        }
        private static Label Note(string text)
        {
            return new Label { Text = text, ForeColor = Theme.SecondaryText,
                Font = Theme.SmallFont, Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true, Margin = Padding.Empty };
        }
        private static TableLayoutPanel FormTable(int rows)
        {
            var t = new TableLayoutPanel {
                ColumnCount = 2, RowCount = rows,
                Margin = Padding.Empty, Padding = Padding.Empty,
                BackColor = Theme.Window, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.LabelColumnWidth));
            t.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            return t;
        }
        private static void SettingRow(TableLayoutPanel table, int index, string text, Control field)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute,
                Theme.ControlHeight + Theme.Space12));
            table.Controls.Add(Label(text), 0, index);
            field.Margin = Padding.Empty;
            table.Controls.Add(field, 1, index);
        }
        private sealed class RowPart
        {
            internal readonly int Width; internal readonly bool Percent;
            internal readonly Control Control;
            internal RowPart(int width, bool percent, Control control)
            { Width = width; Percent = percent; Control = control; }
        }
        private static RowPart Fixed(int width, Control control = null)
        { return new RowPart(width, false, control); }
        private static RowPart Fill(Control control)
        { return new RowPart(100, true, control); }
        private static TableLayoutPanel Row(params RowPart[] parts)
        {
            var t = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = parts.Length,
                RowCount = 1, Margin = Padding.Empty,
                Padding = Padding.Empty, BackColor = Theme.Window
            };
            int column = 0;
            foreach (var p in parts)
            {
                t.ColumnStyles.Add(p.Percent
                    ? new ColumnStyle(SizeType.Percent, p.Width)
                    : new ColumnStyle(SizeType.Absolute, p.Width));
                if (p.Control != null)
                {
                    p.Control.Dock = DockStyle.Fill;
                    p.Control.Margin = Padding.Empty;
                    t.Controls.Add(p.Control, column);
                }
                column++;
            }
            return t;
        }
        private static SettingsScrollHost Scrolled(Panel page, Control body)
        {
            var host = new SettingsScrollHost
            { Dock = DockStyle.Fill, Margin = Padding.Empty, Content = body };
            page.Controls.Add(host);
            return host;
        }

        private void BuildToolsPage(Panel page, string[] values)
        {
            var content = new TableLayoutPanel
            {
                ColumnCount = 1, RowCount = 8, Margin = Padding.Empty,
                Padding = Padding.Empty, BackColor = bg, AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ControlHeight));
            var header = Row(
                Fixed(Theme.LabelColumnWidth, Note("程序")),
                Fill(Note("可执行文件路径（留空时自动检查 dotSharK 目录和 tools 子目录）")),
                Fixed(Theme.Space8 + Theme.BrowseButtonWidth + Theme.Space8 +
                    Theme.IconButtonSize * 2 + Theme.Space8));
            content.Controls.Add(header, 0, 0);
            for (int i = 0; i < 5; i++)
            {
                int index = i;
                int rowIndex = i + 1;
                content.RowStyles.Add(new RowStyle(SizeType.Absolute,
                    Theme.ControlHeight + Theme.Space12));
                toolPaths[i] = StyledTextBox(values[i]);
                toolPaths[i].Name = "txtTool" + i;
                var browse = StyledButton("浏览…", false);
                browse.Name = "btnBrowseTool" + i;
                browse.Click += (s, e) => BrowseTool(index);
                var stable = new IconButton { Nightly = false,
                    AccessibleName = toolNames[i] + " 更新稳定版" };
                var nightly = new IconButton { Nightly = true,
                    AccessibleName = toolNames[i] + " 更新 Nightly" };
                if (i < 4)
                {
                    v2ToolTips.SetToolTip(stable, "更新 " + toolNames[i] + " 稳定版");
                    v2ToolTips.SetToolTip(nightly, i == 3 ?
                        "更新 Deno Canary（开发版）" :
                        "更新 " + toolNames[i] + " Nightly");
                    stable.Click += async (s, e) => await UpdateToolAsync(index, false);
                    nightly.Click += async (s, e) => await UpdateToolAsync(index, true);
                    toolUpdateButtons.Add(stable);
                    toolUpdateButtons.Add(nightly);
                }
                else { stable.Visible = false; nightly.Visible = false; }
                var row = Row(
                    Fixed(Theme.LabelColumnWidth, Label(toolNames[i])),
                    Fill(toolPaths[i]), Fixed(Theme.Space8),
                    Fixed(Theme.BrowseButtonWidth, browse), Fixed(Theme.Space8),
                    Fixed(Theme.IconButtonSize, stable), Fixed(Theme.Space8),
                    Fixed(Theme.IconButtonSize, nightly));
                content.Controls.Add(row, 0, rowIndex);
            }
            content.RowStyles.Add(new RowStyle(SizeType.Absolute,
                Theme.Space16 + Theme.Space12 * 3));
            toolUpdateStatus = Note("绿色按钮更新稳定版；黄色按钮更新 Nightly。更新使用 YT-DLP 页中的主代理。未指定路径时写入程序目录的 tools 子目录。");
            toolUpdateStatus.Name = "lblUpdateStatus";
            content.Controls.Add(toolUpdateStatus, 0, 6);
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space12 * 5));
            content.Controls.Add(Note("路径保存在 dotSharK.ini 中。浏览按钮只选择路径；在线更新会先下载到临时位置并验证版本，再替换目标文件。FFmpeg 稳定版来自 Gyan Builds，Nightly 来自 BtbN FFmpeg Builds。"), 0, 7);
            Scrolled(page, content);
        }

        private async Task UpdateToolAsync(int toolIndex, bool nightly)
        {
            if (toolUpdateRunning) return;
            toolUpdateRunning = true;
            foreach (var button in toolUpdateButtons) button.Enabled = false;
            string channel = nightly ? (toolIndex == 3 ? "Canary" : "Nightly") : "稳定版";
            string target = ExternalToolUpdater.GetTargetPath(toolIndex, toolPaths[toolIndex].Text);
            toolUpdateStatus.ForeColor = muted;
            toolUpdateStatus.Text = "正在检查 " + toolNames[toolIndex] + " " + channel + " 版本…";
            try
            {
                ExternalToolUpdater.UpdateCheckResult check = await ExternalToolUpdater.CheckAsync(toolIndex, nightly, target, (proxyEnabledBox.Checked ? proxyBox.Text.Trim() : ""));
                DialogResult choice = ShowUpdateVersionDialog(toolNames[toolIndex], channel, check);
                if (choice == DialogResult.Cancel)
                {
                    toolUpdateStatus.Text = "已取消更新检查。";
                    return;
                }
                if (choice == DialogResult.Yes && check.IsCurrentOrNewer)
                {
                    toolUpdateStatus.ForeColor = green;
                    toolUpdateStatus.Text = "当前版本已是最新或更新；如需重新安装，请选择“强制更新”。";
                    return;
                }
                toolUpdateStatus.Text = "正在下载 " + toolNames[toolIndex] + " " + channel + (proxyEnabledBox.Checked ? "（通过主代理）…" : "（直连）…");
                string version = await ExternalToolUpdater.UpdateAsync(toolIndex, nightly, target, (proxyEnabledBox.Checked ? proxyBox.Text.Trim() : ""));
                toolUpdateStatus.ForeColor = green;
                toolUpdateStatus.Text = "更新完成 · " + toolNames[toolIndex] + " · " + version + " · " + target;
            }
            catch (Exception ex)
            {
                toolUpdateStatus.ForeColor = Color.FromArgb(225, 135, 120);
                toolUpdateStatus.Text = "更新失败 · " + ex.Message;
            }
            finally
            {
                toolUpdateRunning = false;
                foreach (var button in toolUpdateButtons) if (!button.IsDisposed) button.Enabled = true;
            }
        }

        private DialogResult ShowUpdateVersionDialog(string toolName, string channel, ExternalToolUpdater.UpdateCheckResult check)
        {
            using (var dialog = new UpdatePromptForm())
            {
                dialog.Text = "检查更新 · " + toolName;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.MaximizeBox = false; dialog.MinimizeBox = false; dialog.ShowInTaskbar = false;
                dialog.Icon = AppBranding.CreateIcon(); dialog.BackColor = bg; dialog.ForeColor = fg;
                dialog.Font = new Font("Microsoft YaHei UI", 9F); dialog.ClientSize = new Size(500, 222);
                var root = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = bg, Padding = new Padding(18, 14, 18, 12), ColumnCount = 2, RowCount = 4 };
                root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 94)); root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
                root.Controls.Add(new Label { Text = "当前版本", ForeColor = muted, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
                root.Controls.Add(new Label { Text = check.CurrentVersion, ForeColor = fg, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true }, 1, 0);
                root.Controls.Add(new Label { Text = "远端版本", ForeColor = muted, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
                root.Controls.Add(new Label { Text = check.RemoteVersion + "  ·  " + channel, ForeColor = green, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true }, 1, 1);
                string note = check.IsCurrentOrNewer
                    ? "当前版本已是最新或更新。普通更新不会重复下载；强制更新会重新下载并覆盖。"
                    : "普通更新仅在远端版本较新时执行；强制更新会重新下载并覆盖当前文件。";
                root.Controls.Add(new Label { Text = note, ForeColor = muted, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true }, 0, 2);
                root.SetColumnSpan(root.GetControlFromPosition(0, 2), 2);
                var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = bg, Margin = Padding.Empty };
                var cancel = StyledButton("取消", false); cancel.Width = 86; cancel.DialogResult = DialogResult.Cancel; cancel.Margin = new Padding(8, 0, 0, 0);
                var force = StyledButton("强制更新", false); force.Width = 98; force.DialogResult = DialogResult.Retry; force.Margin = new Padding(8, 0, 0, 0);
                var update = StyledButton("更新", true); update.Width = 86; update.DialogResult = DialogResult.Yes; update.Enabled = !check.IsCurrentOrNewer; update.Margin = new Padding(0, 0, 0, 0);
                buttons.Controls.Add(cancel); buttons.Controls.Add(force); buttons.Controls.Add(update); root.Controls.Add(buttons, 0, 3); root.SetColumnSpan(buttons, 2);
                dialog.Controls.Add(root); dialog.CancelButton = cancel; dialog.AcceptButton = update;
                return dialog.ShowDialog(this);
            }
        }

        private void BuildYtPage(Panel page)
        {
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 12, BackColor = bg, Padding = new Padding(2) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 106));
            for (int i = 0; i < 8; i++) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            grid.Controls.Add(StyledLabel("代理"), 0, 0);
            proxyBox = StyledTextBox(Options.Proxy); SetBottomField(proxyBox, true, 0); grid.Controls.Add(proxyBox, 1, 0);
            proxyEnabledBox = StyledToggle("启用", Options.ProxyEnabled);
            grid.Controls.Add(proxyEnabledBox, 2, 0);
            grid.Controls.Add(StyledLabel("地域验证代理"), 0, 1);
            geoProxyBox = StyledTextBox(Options.GeoVerificationProxy); SetBottomField(geoProxyBox, true, 0); grid.Controls.Add(geoProxyBox, 1, 1);
            geoProxyEnabledBox = StyledToggle("启用", Options.GeoProxyEnabled);
            var geoTip = new ToolTip();
            geoTip.SetToolTip(geoProxyEnabledBox, "仅用于 yt-dlp 地区验证；关闭时保留地址，不传给 yt-dlp。");
            geoTip.SetToolTip(geoProxyBox, "仅用于 yt-dlp 地区验证；关闭时保留地址，不传给 yt-dlp。");
            grid.Controls.Add(geoProxyEnabledBox, 2, 1);
            grid.Controls.Add(StyledLabel("分片并发"), 0, 2);
            fragmentsBox = new DarkNumericUpDown { Minimum = 1, Maximum = 32, Value = Math.Max(1, Math.Min(32, Options.ConcurrentFragments)), Width = 116, Height = DarkPalette.ControlHeight, Margin = Padding.Empty, Anchor = AnchorStyles.Left | AnchorStyles.Bottom, BackColor = field, ForeColor = fg, BorderStyle = BorderStyle.FixedSingle, TextAlign = HorizontalAlignment.Center };
            grid.Controls.Add(fragmentsBox, 1, 2);
            grid.Controls.Add(new Label { Text = "下载分片并发数", ForeColor = muted, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 2, 2);

            grid.Controls.Add(StyledLabel("同时下载数"), 0, 3);
            downloadsBox = new DarkNumericUpDown { Minimum = 1, Maximum = 6, Value = Math.Max(1, Math.Min(6, Options.ConcurrentDownloads)), Width = 116, Height = DarkPalette.ControlHeight, Margin = Padding.Empty, Anchor = AnchorStyles.Left | AnchorStyles.Bottom, BackColor = field, ForeColor = fg, BorderStyle = BorderStyle.FixedSingle, TextAlign = HorizontalAlignment.Center };
            grid.Controls.Add(downloadsBox, 1, 3);
            grid.Controls.Add(new Label { Text = "队列同时运行的视频数（1–6）", ForeColor = muted, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 2, 3);

            grid.Controls.Add(StyledLabel("Cookie 来源"), 0, 4);
            sourceBox = StyledCombo(new[] { "浏览器", "Cookie 文件" }); sourceBox.SelectedIndex = Options.CookieMode == "file" ? 1 : 0; sourceBox.Width = 150; sourceBox.Anchor = AnchorStyles.Left | AnchorStyles.Bottom; grid.Controls.Add(sourceBox, 1, 4);
            grid.Controls.Add(StyledLabel("浏览器"), 0, 5);
            var browsers = new[] {
                new BrowserOption { Name="firefox", Label="Firefox" }, new BrowserOption { Name="chrome", Label="Chrome" },
                new BrowserOption { Name="edge", Label="Edge" }, new BrowserOption { Name="brave", Label="Brave" },
                new BrowserOption { Name="chromium", Label="Chromium" }, new BrowserOption { Name="opera", Label="Opera" },
                new BrowserOption { Name="vivaldi", Label="Vivaldi" }, new BrowserOption { Name="safari", Label="Safari" },
                new BrowserOption { Name="whale", Label="Whale" }
            };
            browserBox = StyledCombo(browsers.Cast<object>().ToArray()); browserBox.Width = 190; browserBox.Anchor = AnchorStyles.Left | AnchorStyles.Bottom; grid.Controls.Add(browserBox, 1, 5);
            string browserName = String.IsNullOrWhiteSpace(Options.CookieBrowser) ? "firefox" : Options.CookieBrowser;
            for (int i = 0; i < browserBox.Items.Count; i++) if (((BrowserOption)browserBox.Items[i]).Name == browserName) browserBox.SelectedIndex = i;

            grid.Controls.Add(StyledLabel("搜索目录"), 0, 6);
            profileRootBox = StyledTextBox(Options.CookieProfileRoot); SetBottomField(profileRootBox, true, 8); grid.Controls.Add(profileRootBox, 1, 6);
            var rootButtons = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, BackColor = bg, Height = DarkPalette.ControlHeight, Margin = Padding.Empty, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom };
            rootButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); rootButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            var rootBrowse = StyledButton("选择", false); rootBrowse.Dock = DockStyle.Fill; rootBrowse.Margin = new Padding(0, 0, 3, 0); rootBrowse.Click += BrowseProfileRoot;
            profileSearchButton = StyledButton("搜索", true); profileSearchButton.Dock = DockStyle.Fill; profileSearchButton.Margin = new Padding(3, 0, 0, 0); profileSearchButton.Click += async (s, e) => await SearchProfiles();
            rootButtons.Controls.Add(rootBrowse, 0, 0); rootButtons.Controls.Add(profileSearchButton, 1, 0); grid.Controls.Add(rootButtons, 2, 6);

            grid.Controls.Add(StyledLabel("浏览器配置"), 0, 7);
            var profileRow = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, BackColor = bg, Height = DarkPalette.ControlHeight, Margin = Padding.Empty, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom };
            profileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); profileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
            profileBox = StyledCombo(new object[0]); profileBox.DropDownStyle = ComboBoxStyle.DropDown; profileBox.Dock = DockStyle.Fill; profileBox.Margin = new Padding(0, 0, 5, 0); profileRow.Controls.Add(profileBox, 0, 0);
            profileFoundCheck = new Label { Text = "", ForeColor = green, Font = new Font("Segoe UI Symbol", 14F, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Visible = false, BackColor = bg };
            profileRow.Controls.Add(profileFoundCheck, 1, 0); grid.Controls.Add(profileRow, 1, 7); grid.SetColumnSpan(profileRow, 2);
            if (!String.IsNullOrWhiteSpace(Options.CookieProfile)) { profileBox.Text = Options.CookieProfile; profileBox.Items.Add(Options.CookieProfile); }
            profileStatus = new Label { Text = "默认浏览器配置自动读取；指定目录后可递归搜寻 Cookie 数据库。", ForeColor = muted, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            grid.Controls.Add(profileStatus, 1, 8); grid.SetColumnSpan(profileStatus, 2);
            grid.Controls.Add(StyledLabel("Cookie 文件"), 0, 9);
            cookieFileBox = StyledTextBox(Options.CookieFile); SetBottomField(cookieFileBox, true, 8); grid.Controls.Add(cookieFileBox, 1, 9);
            var cookieBrowse = StyledButton("浏览…", false); SetBottomField(cookieBrowse, true, 0); cookieBrowse.Click += BrowseCookieFile; grid.Controls.Add(cookieBrowse, 2, 9);
            grid.Controls.Add(StyledLabel("视频质量限制"), 0, 10);
            // Use the same centered label style as all other settings; avoid FlowLayoutPanel's top padding.
            var qualityLimitRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty, Padding = Padding.Empty, BackColor = bg };
            qualityLimitRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 32));
            qualityLimitRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
            qualityLimitRow.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var qualityPrefix = StyledLabel("前："); qualityPrefix.Margin = Padding.Empty;
            qualityLimitRow.Controls.Add(qualityPrefix, 0, 0);
            qualityLimitBox = new DarkNumericUpDown { Minimum = 0, Maximum = 9999, Value = Math.Max(0, Math.Min(9999, Options.VideoQualityLimit)), Width = 96, Height = DarkPalette.ControlHeight, Margin = Padding.Empty, Anchor = AnchorStyles.Left, BackColor = field, ForeColor = fg, BorderStyle = BorderStyle.FixedSingle, TextAlign = HorizontalAlignment.Center };
            qualityLimitRow.Controls.Add(qualityLimitBox, 1, 0); grid.Controls.Add(qualityLimitRow, 1, 10);
            var qualityHint = new Label { Text = "默认前三个不同分辨率档位；0 为不限制。同分辨率的不同编码、HDR 选项均保留。", ForeColor = muted, Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopLeft, Padding = new Padding(0, 5, 0, 0), AutoEllipsis = true };
            grid.Controls.Add(qualityHint, 1, 11); grid.SetColumnSpan(qualityHint, 2);
            page.Controls.Add(grid);
            sourceBox.SelectedIndexChanged += async (s, e) =>
            {
                UpdateCookieMode();
                if (sourceBox.SelectedIndex == 0) await SearchDefaultBrowserProfiles();
                else { profileSearchGeneration++; profileSearchButton.Enabled = true; SetProfileFound(false); }
            };
            browserBox.SelectedIndexChanged += async (s, e) =>
            {
                profileSearchGeneration++; profileBox.Items.Clear(); profileBox.Text = ""; SetProfileFound(false);
                if (sourceBox.SelectedIndex == 0) await SearchDefaultBrowserProfiles();
            };
            profileBox.SelectedIndexChanged += (s, e) => SetProfileFound(profileBox.SelectedIndex >= 0);
            UpdateCookieMode();
            if (sourceBox.SelectedIndex == 0) Shown += async (s, e) => await SearchDefaultBrowserProfiles();
        }

        private void BuildSilentPage(Panel page)
        {
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 4, BackColor = bg };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 94));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var title = new Label { Text = "浏览器右键 · 静默下载", ForeColor = fg,
                Font = new Font(Font, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            layout.Controls.Add(title, 0, 0); layout.SetColumnSpan(title, 3);
            layout.Controls.Add(StyledLabel("独立保存到"), 0, 1);
            silentFolderBox = StyledTextBox(Options.SilentDownloadFolder);
            silentFolderBox.Height = DarkPalette.ControlHeight;
            silentFolderBox.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            silentFolderBox.Margin = new Padding(0, 0, 8, 0);
            layout.Controls.Add(silentFolderBox, 1, 1);
            var browse = StyledButton("浏览…", false);
            browse.Height = DarkPalette.ControlHeight;
            browse.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            browse.Margin = Padding.Empty;
            browse.Click += (sender, args) =>
            {
                string selected = silentFolderBox.Text.Trim();
                using (var picker = new FolderBrowserDialog { Description = "选择浏览器静默下载的保存目录",
                    SelectedPath = Directory.Exists(selected) ? selected :
                        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                    ShowNewFolderButton = true })
                    if (picker.ShowDialog(this) == DialogResult.OK) silentFolderBox.Text = picker.SelectedPath;
            };
            layout.Controls.Add(browse, 2, 1);
            var hint = new Label
            {
                Text = "只影响浏览器右键“发送到 dotSharK”下载。主窗口的“保存到”保持独立。" +
                       Environment.NewLine +
                       "最高分辨率 / 最高帧率 / HDR / AV1 优先；按已保存的 yt-dlp 代理和 Cookie 下载，不弹窗口。",
                ForeColor = muted, Dock = DockStyle.Fill, AutoEllipsis = true,
                TextAlign = ContentAlignment.TopLeft, Padding = new Padding(0, 8, 0, 0)
            };
            layout.Controls.Add(hint, 0, 2); layout.SetColumnSpan(hint, 3);
            page.Controls.Add(layout);
        }

        private CheckBox StyledToggle(string title, bool isChecked)
        {
            return new CheckBox { Text = title, Checked = isChecked, BackColor = bg, ForeColor = fg,
                AutoSize = false, Dock = DockStyle.Fill, FlatStyle = FlatStyle.Flat, TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(3, 0, 0, 0) };
        }

        private void BuildAdvancedPage(Panel page)
        {
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 6,
                BackColor = bg, Padding = new Padding(2) };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 96));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 53));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 31));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 49));

            layout.Controls.Add(new Label { Text = "使用 aria2c 下载的站点",
                ForeColor = fg, Font = new Font(Font, FontStyle.Bold),
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            layout.Controls.Add(new Label { Text = "以下站点不使用代理",
                ForeColor = fg, Font = new Font(Font, FontStyle.Bold),
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 1, 0);
            ariaSitesBox = new DarkTextBox { Text = Options.Aria2cSites ?? "", Dock = DockStyle.Fill,
                Multiline = true, ScrollBars = ScrollBars.Vertical, BackColor = field, ForeColor = fg,
                Margin = new Padding(0, 0, 7, 0) };
            noProxySitesBox = new DarkTextBox { Text = Options.NoProxySites ?? "", Dock = DockStyle.Fill,
                Multiline = true, ScrollBars = ScrollBars.Vertical, BackColor = field, ForeColor = fg,
                Margin = new Padding(7, 0, 0, 0) };
            layout.Controls.Add(ariaSitesBox, 0, 1);
            layout.Controls.Add(noProxySitesBox, 1, 1);

            layout.Controls.Add(new Label
            {
                Text = "空格/换行分隔域名，如 youtube.com，自动覆盖所有子域名；按输入网页网址匹配，不看视频 CDN。",
                ForeColor = muted, Dock = DockStyle.Fill, AutoEllipsis = true,
                Padding = new Padding(0, 5, 9, 0)
            }, 0, 2);
            layout.Controls.Add(new Label
            {
                Text = "规则相同；命中时主代理和地域验证代理均不使用，已保存的代理地址不受影响。",
                ForeColor = muted, Dock = DockStyle.Fill, AutoEllipsis = true,
                Padding = new Padding(7, 5, 0, 0)
            }, 1, 2);
            var commandTitle = new Label
            {
                Text = "自定义 yt-dlp 原生命令行参数（下载与分析均生效）",
                ForeColor = fg, Font = new Font(Font, FontStyle.Bold),
                Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft
            };
            layout.Controls.Add(commandTitle, 0, 3);
            layout.SetColumnSpan(commandTitle, 2);
            customArgumentsBox = new DarkTextBox { Text = Options.CustomArguments ?? "",
                Dock = DockStyle.Fill, Multiline = true, ScrollBars = ScrollBars.Vertical,
                BackColor = field, ForeColor = fg };
            layout.Controls.Add(customArgumentsBox, 0, 4);
            layout.SetColumnSpan(customArgumentsBox, 2);
            var hint = new Label
            {
                Text = "例如 --retries 20 --no-mtime；支持带引号的参数值，自定义参数覆盖一般内置设置。" +
                       Environment.NewLine +
                       "无代理站点规则优先于自定义代理参数。aria2c.exe 在「外部工具」页指定。",
                ForeColor = muted, Dock = DockStyle.Fill, AutoEllipsis = true,
                Padding = new Padding(0, 7, 0, 0)
            };
            layout.Controls.Add(hint, 0, 5);
            layout.SetColumnSpan(hint, 2);
            page.Controls.Add(layout);
        }

        private void UpdateCookieMode()
        {
            bool file = sourceBox != null && sourceBox.SelectedIndex == 1;
            browserBox.Enabled = !file; profileRootBox.Enabled = !file; profileBox.Enabled = !file; cookieFileBox.Enabled = file;
        }
        private void SaveClicked(object sender, EventArgs e)
        {
            string proxy = proxyBox.Text.Trim();
            if (proxyEnabledBox.Checked && proxy.Length > 0)
            {
                Uri uri;
                if (!Uri.TryCreate(proxy, UriKind.Absolute, out uri) || !(uri.Scheme == "http" || uri.Scheme == "https" || uri.Scheme == "socks4" || uri.Scheme == "socks4a" || uri.Scheme == "socks5" || uri.Scheme == "socks5h"))
                { profileStatus.Text = "代理地址格式不正确，请检查协议和端口。"; return; }
            }
            if (sourceBox.SelectedIndex == 1 && !String.IsNullOrWhiteSpace(cookieFileBox.Text) && !File.Exists(cookieFileBox.Text.Trim()))
            { MessageBox.Show(this, "找不到指定的 Cookie 文件。", "dotSharK", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            if (sourceBox.SelectedIndex == 1 && String.IsNullOrWhiteSpace(cookieFileBox.Text))
            { MessageBox.Show(this, "请指定 Netscape 格式的 Cookie 文件。", "dotSharK", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            var browser = browserBox.SelectedItem as BrowserOption;
            string geoProxy = geoProxyBox.Text.Trim();
            if (geoProxyEnabledBox.Checked && geoProxy.Length > 0 && !IsValidProxy(geoProxy))
            { profileStatus.Text = "地域验证代理格式不正确，请检查协议和端口。"; return; }
            Options.Proxy = proxy; Options.GeoVerificationProxy = geoProxy;
            Options.ProxyEnabled = proxyEnabledBox.Checked; Options.GeoProxyEnabled = geoProxyEnabledBox.Checked;
            Options.Aria2cPath = Aria2cPath;
            Options.Aria2cSites = ariaSitesBox.Text.Trim();
            Options.NoProxySites = noProxySitesBox.Text.Trim();
            Options.CustomArguments = customArgumentsBox.Text.Trim();
            try
            {
                YtDlpClient.ParseCustomArguments(Options.CustomArguments);
                YtDlpClient.ValidateAriaSites(Options.Aria2cSites);
                YtDlpClient.ValidateAriaSites(Options.NoProxySites);
            }
            catch (ArgumentException ex)
            {
                MessageBox.Show(this, ex.Message, "高级下载参数无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Options.ConcurrentFragments = (int)fragmentsBox.Value; Options.ConcurrentDownloads = (int)downloadsBox.Value; Options.VideoQualityLimit = (int)qualityLimitBox.Value;
            Options.CookieMode = sourceBox.SelectedIndex == 1 ? "file" : "browser";
            string silentFolder = silentFolderBox.Text.Trim();
            if (String.IsNullOrWhiteSpace(silentFolder))
                silentFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads", "Silent");
            if (!Path.IsPathRooted(silentFolder))
            {
                MessageBox.Show(this, "静默下载目录需要填写完整的绝对路径。", "dotSharK", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try { silentFolder = Path.GetFullPath(silentFolder); }
            catch (Exception)
            {
                MessageBox.Show(this, "静默下载目录路径无效。", "dotSharK", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Options.SilentDownloadFolder = silentFolder;
            Options.CookieBrowser = browser == null ? "firefox" : browser.Name;
            Options.CookieProfileRoot = profileRootBox.Text.Trim(); Options.CookieProfile = profileBox.Text.Trim(); Options.CookieFile = cookieFileBox.Text.Trim();
            DialogResult = DialogResult.OK; Close();
        }

        private static bool IsValidProxy(string value)
        {
            Uri uri;
            return Uri.TryCreate(value, UriKind.Absolute, out uri) &&
                (uri.Scheme == "http" || uri.Scheme == "https" || uri.Scheme == "socks4" || uri.Scheme == "socks4a" || uri.Scheme == "socks5" || uri.Scheme == "socks5h");
        }

        private async Task SearchProfiles()
        {
            string root = profileRootBox.Text.Trim(); var browser = browserBox.SelectedItem as BrowserOption;
            if (!Directory.Exists(root)) { profileSearchGeneration++; profileSearchButton.Enabled = true; SetProfileFound(false); profileStatus.ForeColor = muted; profileStatus.Text = "先选择一个存在的搜索目录。"; return; }
            await SearchProfileRoots(new[] { root }, browser == null ? "firefox" : browser.Name);
        }

        private async Task SearchDefaultBrowserProfiles()
        {
            var browser = browserBox == null ? null : browserBox.SelectedItem as BrowserOption;
            string name = browser == null ? "firefox" : browser.Name;
            List<string> roots = GetDefaultProfileRoots(name).Where(Directory.Exists).ToList();
            profileRootBox.Text = roots.Count == 0 ? "" : roots[0];
            if (roots.Count == 0)
            {
                profileSearchGeneration++; profileSearchButton.Enabled = true;
                profileBox.Items.Clear(); profileBox.Text = ""; SetProfileFound(false);
                profileStatus.ForeColor = muted; profileStatus.Text = "未找到该浏览器的默认配置目录；可手动选择搜索目录。"; return;
            }
            await SearchProfileRoots(roots, name);
        }

        private async Task SearchProfileRoots(IEnumerable<string> roots, string browser)
        {
            int generation = ++profileSearchGeneration;
            profileSearchButton.Enabled = false; SetProfileFound(false); profileStatus.ForeColor = muted; profileStatus.Text = "正在递归搜索 Cookie 数据库…";
            try
            {
                var found = await Task.Run(() => roots.SelectMany(root => FindProfiles(root, browser))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(profile => GetCookieDatabaseLastWriteTime(profile, browser))
                    .ThenBy(profile => profile, StringComparer.OrdinalIgnoreCase).ToList());
                if (generation != profileSearchGeneration) return;
                profileBox.Items.Clear();
                foreach (string profile in found) profileBox.Items.Add(profile);
                if (found.Count > 0)
                {
                    profileBox.SelectedIndex = 0; SetProfileFound(true); profileStatus.ForeColor = green;
                    DateTime newest = GetCookieDatabaseLastWriteTime(found[0], browser);
                    profileStatus.Text = "找到 " + found.Count + " 个配置；已选择 Cookie 数据库最新的一个（" + newest.ToString("yyyy-MM-dd HH:mm") + "）。";
                }
                else { SetProfileFound(false); profileStatus.ForeColor = muted; profileStatus.Text = "默认目录中没有找到该浏览器的 Cookie 数据库。"; }
            }
            catch (Exception ex) { if (generation == profileSearchGeneration) { SetProfileFound(false); profileStatus.ForeColor = Color.FromArgb(226, 166, 90); profileStatus.Text = "搜索失败：" + ex.Message; } }
            finally { if (generation == profileSearchGeneration) profileSearchButton.Enabled = true; }
        }

        private void SetProfileFound(bool found)
        {
            if (profileFoundCheck == null) return;
            profileFoundCheck.Text = found ? "✓" : ""; profileFoundCheck.Visible = found;
            if (!found && profileStatus != null && profileStatus.ForeColor == green) profileStatus.ForeColor = muted;
        }

        private List<string> GetDefaultProfileRoots(string browser)
        {
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var roots = new List<string>();
            switch ((browser ?? "").ToLowerInvariant())
            {
                case "firefox":
                    string firefoxRoot = Path.Combine(roaming, "Mozilla", "Firefox");
                    roots.Add(Path.Combine(firefoxRoot, "Profiles"));
                    roots.Add(Path.Combine(local, "Packages", "Mozilla.Firefox_n80bbvh6b1yt2", "LocalCache", "Roaming", "Mozilla", "Firefox", "Profiles"));
                    roots.AddRange(ReadFirefoxProfilePaths(firefoxRoot));
                    roots.AddRange(FindRunningFirefoxPortableProfiles());
                    roots.AddRange(FindKnownPortableFirefoxProfiles());
                    break;
                case "chrome": roots.Add(Path.Combine(local, "Google", "Chrome", "User Data")); break;
                case "edge": roots.Add(Path.Combine(local, "Microsoft", "Edge", "User Data")); break;
                case "brave": roots.Add(Path.Combine(local, "BraveSoftware", "Brave-Browser", "User Data")); break;
                case "chromium": roots.Add(Path.Combine(local, "Chromium", "User Data")); break;
                case "opera": roots.Add(Path.Combine(roaming, "Opera Software")); break;
                case "vivaldi": roots.Add(Path.Combine(local, "Vivaldi", "User Data")); break;
                case "whale": roots.Add(Path.Combine(local, "Naver", "Naver Whale", "User Data")); break;
                case "safari": roots.Add(Path.Combine(roaming, "Apple Computer", "Safari")); break;
            }
            // Keep user-selected portable/custom roots across app restarts. They are
            // searched alongside the browser's standard roots, never instead of them.
            if (!String.IsNullOrWhiteSpace(Options.CookieProfileRoot)) roots.Add(Options.CookieProfileRoot);
            if (!String.IsNullOrWhiteSpace(Options.CookieProfile)) roots.Add(Options.CookieProfile);
            return roots.Where(Directory.Exists).Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static List<string> ReadFirefoxProfilePaths(string firefoxRoot)
        {
            var paths = new List<string>();
            string ini = Path.Combine(firefoxRoot, "profiles.ini");
            if (!File.Exists(ini)) return paths;
            string section = ""; string profilePath = null; bool relative = true;
            Action flush = () =>
            {
                if (profilePath == null || !(section.StartsWith("Profile", StringComparison.OrdinalIgnoreCase) || section.StartsWith("Install", StringComparison.OrdinalIgnoreCase))) return;
                try
                {
                    string value = profilePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                    string path = relative ? Path.GetFullPath(Path.Combine(firefoxRoot, value)) : Path.GetFullPath(value);
                    if (Directory.Exists(path)) paths.Add(path);
                }
                catch { }
            };
            try
            {
                foreach (string raw in File.ReadAllLines(ini))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith(";", StringComparison.Ordinal) || line.StartsWith("#", StringComparison.Ordinal)) continue;
                    if (line.StartsWith("[", StringComparison.Ordinal) && line.EndsWith("]", StringComparison.Ordinal))
                    {
                        flush(); section = line.Substring(1, line.Length - 2); profilePath = null; relative = true; continue;
                    }
                    int equals = line.IndexOf('='); if (equals <= 0) continue;
                    string key = line.Substring(0, equals).Trim(); string value = line.Substring(equals + 1).Trim();
                    if (String.Equals(key, "Path", StringComparison.OrdinalIgnoreCase) && section.StartsWith("Profile", StringComparison.OrdinalIgnoreCase)) profilePath = value;
                    else if (String.Equals(key, "Default", StringComparison.OrdinalIgnoreCase) && section.StartsWith("Install", StringComparison.OrdinalIgnoreCase)) profilePath = value;
                    else if (String.Equals(key, "IsRelative", StringComparison.OrdinalIgnoreCase)) relative = value != "0";
                }
                flush();
            }
            catch { }
            return paths;
        }

        private static List<string> FindRunningFirefoxPortableProfiles()
        {
            var paths = new List<string>();
            try
            {
                foreach (Process process in Process.GetProcessesByName("firefox"))
                {
                    try
                    {
                        string exe = process.MainModule.FileName;
                        for (DirectoryInfo dir = Directory.GetParent(exe); dir != null; dir = dir.Parent)
                        {
                            if (dir.Name.IndexOf("FirefoxPortable", StringComparison.OrdinalIgnoreCase) < 0) continue;
                            string profile = Path.Combine(dir.FullName, "Data", "profile");
                            if (Directory.Exists(profile)) paths.Add(profile);
                            break;
                        }
                    }
                    catch { }
                    finally { process.Dispose(); }
                }
            }
            catch { }
            return paths;
        }

        private static List<string> FindKnownPortableFirefoxProfiles()
        {
            var paths = new List<string>();
            try
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady || drive.DriveType != DriveType.Fixed) continue;
                    string root = drive.RootDirectory.FullName;
                    string[] candidates = {
                        Path.Combine(root, "CoolTools", "Browser", "FirefoxPortable", "Data", "profile"),
                        Path.Combine(root, "PortableApps", "FirefoxPortable", "Data", "profile"),
                        Path.Combine(root, "FirefoxPortable", "Data", "profile")
                    };
                    foreach (string candidate in candidates) if (Directory.Exists(candidate)) paths.Add(candidate);
                }
            }
            catch { }
            return paths;
        }

        private static List<string> FindProfiles(string root, string browser)
        {
            bool firefox = browser == "firefox" || browser == "floorp";
            string databaseName = firefox ? "cookies.sqlite" : browser == "safari" ? "Cookies.binarycookies" : "Cookies";
            var results = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var pending = new Stack<string>(); pending.Push(root);
            while (pending.Count > 0)
            {
                string current = ResolveDirectoryPath(pending.Pop());
                if (current == null) continue;
                try { current = Path.GetFullPath(current).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar); } catch { continue; }
                string driveRoot = Path.GetPathRoot(current);
                if (!String.IsNullOrEmpty(driveRoot) && String.Equals(current, driveRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar), StringComparison.OrdinalIgnoreCase)) current = driveRoot;
                if (!visited.Add(current)) continue;
                try
                {
                    foreach (string file in Directory.EnumerateFiles(current, "*", SearchOption.TopDirectoryOnly))
                    {
                        if (!String.Equals(Path.GetFileName(file), databaseName, StringComparison.OrdinalIgnoreCase)) continue;
                        string profile = Path.GetDirectoryName(file);
                        if (!firefox && String.Equals(new DirectoryInfo(profile).Name, "Network", StringComparison.OrdinalIgnoreCase)) profile = Directory.GetParent(profile).FullName;
                        results.Add(profile);
                    }
                }
                catch { }
                try
                {
                    foreach (string dir in Directory.EnumerateDirectories(current, "*", SearchOption.TopDirectoryOnly))
                    {
                        try
                        {
                            FileAttributes attributes = File.GetAttributes(dir);
                            if ((attributes & FileAttributes.ReparsePoint) != 0)
                            {
                                // Firefox/Floorp Portable profiles are often exposed under
                                // %APPDATA% through /J junctions. Resolve each target before
                                // queueing it so aliases are deduplicated and junction loops stop.
                                string target = ResolveDirectoryPath(dir);
                                if (target != null && !visited.Contains(target)) pending.Push(target);
                            }
                            else pending.Push(dir);
                        }
                        catch { }
                    }
                }
                catch { }
            }
            return results.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static DateTime GetCookieDatabaseLastWriteTime(string profile, string browser)
        {
            try
            {
                if (browser == "firefox" || browser == "floorp")
                {
                    // Firefox and Floorp can keep recent cookie changes in the SQLite
                    // write-ahead log before they are checkpointed into cookies.sqlite.
                    string sqliteDatabase = Path.Combine(profile, "cookies.sqlite");
                    string wal = sqliteDatabase + "-wal";
                    DateTime lastWrite = File.Exists(sqliteDatabase) ? File.GetLastWriteTimeUtc(sqliteDatabase) : DateTime.MinValue;
                    if (File.Exists(wal)) lastWrite = Max(lastWrite, File.GetLastWriteTimeUtc(wal));
                    return lastWrite;
                }
                if (browser == "safari") return File.GetLastWriteTimeUtc(Path.Combine(profile, "Cookies.binarycookies"));
                string networkDb = Path.Combine(profile, "Network", "Cookies");
                string database = File.Exists(networkDb) ? networkDb : Path.Combine(profile, "Cookies");
                return File.GetLastWriteTimeUtc(database);
            }
            catch { return DateTime.MinValue; }
        }

        private static DateTime Max(DateTime first, DateTime second) { return first >= second ? first : second; }

        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "CreateFileW")]
        private static extern SafeFileHandle OpenDirectoryHandle(string path, uint desiredAccess, uint shareMode, IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);
        [System.Runtime.InteropServices.DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "GetFinalPathNameByHandleW")]
        private static extern uint GetFinalPathNameByHandle(SafeFileHandle handle, StringBuilder path, uint pathLength, uint flags);

        private static string ResolveDirectoryPath(string path)
        {
            try
            {
                using (SafeFileHandle handle = OpenDirectoryHandle(path, 0x80, 0x7, IntPtr.Zero, 3, 0x02000000, IntPtr.Zero))
                {
                    if (handle == null || handle.IsInvalid) return Directory.Exists(path) ? Path.GetFullPath(path) : null;
                    uint capacity = GetFinalPathNameByHandle(handle, null, 0, 0);
                    if (capacity == 0 || capacity > 32768) return Directory.Exists(path) ? Path.GetFullPath(path) : null;
                    var buffer = new StringBuilder((int)capacity + 1);
                    uint written = GetFinalPathNameByHandle(handle, buffer, (uint)buffer.Capacity, 0);
                    if (written == 0 || written >= buffer.Capacity) return Directory.Exists(path) ? Path.GetFullPath(path) : null;
                    string finalPath = buffer.ToString();
                    if (finalPath.StartsWith("\\\\?\\UNC\\", StringComparison.OrdinalIgnoreCase)) finalPath = "\\\\" + finalPath.Substring(8);
                    else if (finalPath.StartsWith("\\\\?\\", StringComparison.Ordinal)) finalPath = finalPath.Substring(4);
                    return finalPath;
                }
            }
            catch { return Directory.Exists(path) ? Path.GetFullPath(path) : null; }
        }

        private void BrowseTool(int index)
        {
            using (var dialog = new OpenFileDialog { Title = "选择 " + toolNames[index], Filter = toolNames[index] + "|" + toolNames[index] + "|程序文件 (*.exe)|*.exe|所有文件 (*.*)|*.*", CheckFileExists = true })
                if (dialog.ShowDialog(this) == DialogResult.OK) toolPaths[index].Text = dialog.FileName;
        }
        private void BrowseProfileRoot(object sender, EventArgs e)
        {
            using (var dialog = new FolderBrowserDialog { Description = "选择要递归搜索浏览器 Cookie 数据库的目录", SelectedPath = Directory.Exists(profileRootBox.Text) ? profileRootBox.Text : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ShowNewFolderButton = false })
                if (dialog.ShowDialog(this) == DialogResult.OK) profileRootBox.Text = dialog.SelectedPath;
        }
        private void BrowseCookieFile(object sender, EventArgs e)
        {
            using (var dialog = new OpenFileDialog { Title = "选择 Netscape Cookie 文件", Filter = "Cookie 文件 (*.txt;*.cookies)|*.txt;*.cookies|所有文件 (*.*)|*.*", CheckFileExists = true })
                if (dialog.ShowDialog(this) == DialogResult.OK) cookieFileBox.Text = dialog.FileName;
        }
        private DarkComboBox StyledCombo(object[] items)
        {
            var combo = new DarkComboBox { Height = DarkPalette.ControlHeight, Margin = Padding.Empty };
            combo.Items.AddRange(items); if (combo.Items.Count > 0) combo.SelectedIndex = 0; return combo;
        }
        private DarkTextBox StyledTextBox(string text) { return new DarkTextBox { Text = text ?? "", Font = Font, Multiline = false, AutoSize = false, Height = DarkPalette.ControlHeight, Margin = Padding.Empty }; }
        private static void SetBottomField(Control control, bool stretch, int rightMargin)
        {
            control.Dock = DockStyle.None;
            control.Height = DarkPalette.ControlHeight;
            control.Margin = new Padding(0, 0, rightMargin, 0);
            control.Anchor = AnchorStyles.Left | AnchorStyles.Bottom | (stretch ? AnchorStyles.Right : AnchorStyles.None);
        }
        private Label StyledLabel(string text) { return new Label { Text = text, ForeColor = fg, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true }; }
        private Button StyledButton(string text, bool accent) { return new DarkButton { Text = text, Primary = accent, Font = Font, Height = DarkPalette.ControlHeight, MinimumSize = new Size(0, DarkPalette.ControlHeight) }; }
    }
}
