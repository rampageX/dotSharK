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
        private DarkCheckBox keepCompletedBox;
        private V2Combo silentResolutionBox;
        private static readonly int[] SilentHeightOptions = { 0, 4320, 2160, 1080, 720 };
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
                NoProxySites = options.NoProxySites, CustomArguments = options.CustomArguments,
                KeepCompletedTasks = options.KeepCompletedTasks, SilentMaxHeight = options.SilentMaxHeight
            };
            SuspendLayout();
            try
            {
                Text = "dotSharK 配置";
                StartPosition = FormStartPosition.CenterParent;
                // Keep every page visible without a dialog-level scroll bar.
                // Resizing is allowed; the minimum protects the longest YT-DLP page.
                FormBorderStyle = FormBorderStyle.Sizable;
                MaximizeBox = true; MinimizeBox = false; ShowInTaskbar = false;
                Icon = AppBranding.CreateIcon();
                BackColor = Theme.Window; ForeColor = Theme.PrimaryText;
                Font = Theme.BodyFont;
                ClientSize = Theme.SettingsClientSize;
                MinimumSize = Theme.SettingsMinimumSize;
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
                var pagePlugins = NewPage();
                tabs.AddPage("插件注册", pagePlugins);
                BuildPluginsPage(pagePlugins);
                root.Controls.Add(tabs, 0, 0);
                BuildToolsPage(pageTools, new[] { ytDlp, ffmpeg, ffprobe, deno, aria2c });
                BuildYtPage(pageYt);
                BuildAdvancedPage(pageAdvanced);
                BuildSilentPage(pageSilent);
                tabs.SelectedIndex = 1;

                var footer = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill, Margin = Padding.Empty,
                    Padding = new Padding(0, 0, 0, Theme.Space16),
                    ColumnCount = 4, RowCount = 3, BackColor = Theme.Window
                };
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.FooterButtonWidth));
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.Space8));
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.FooterButtonWidth));
                footer.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.BorderWidth));
                footer.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space16));
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
                footer.Controls.Add(cancel, 1, 2);
                footer.Controls.Add(save, 3, 2);
                root.Controls.Add(footer, 0, 1);
                AssignDialogButtons(save, cancel);
                ActiveControl = tabs;
            }
            finally { ResumeLayout(true); }
        }

        private void BuildPluginsPage(Panel page)
        {
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ControlHeight + Theme.Space8));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ControlHeight + Theme.Space8));
            page.Controls.Add(layout);
            layout.Controls.Add(Label("在浏览器加载 BrowserExtension 后，点击插件图标 → 复制配对码。\n选择浏览器，粘贴配对码并点击注册，再回插件点击检测连接。\n多个浏览器可依次注册。同一 ID 共用授权，删除后这些浏览器都会断开。"), 0, 0);
            var input = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Margin = Padding.Empty };
            var browser = StyledCombo(new object[] { "Catsxp", "Chrome", "Edge", "Chromium", "其他" });
            browser.Width = 112;
            browser.Margin = new Padding(0, 0, Theme.Space8, 0);
            var code = StyledTextBox("");
            code.Dock = DockStyle.None;
            code.Size = new Size(290, Theme.ControlHeight);
            code.Margin = new Padding(0, 0, Theme.Space8, 0);
            var register = StyledButton("注册", true);
            register.Width = 88;
            input.Controls.AddRange(new Control[] { browser, code, register });
            layout.Controls.Add(input, 0, 1);
            var list = new DarkDataGridView
            {
                Dock = DockStyle.Fill, ReadOnly = true, MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            list.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "已注册的扩展 ID", FillWeight = 60 });
            list.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "浏览器", FillWeight = 40 });
            list.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            layout.Controls.Add(list, 0, 2);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, Margin = Padding.Empty };
            actions.Padding = new Padding(0, Theme.Space8, 0, 0);
            var remove = StyledButton("删除选中", false);
            remove.Width = 96;
            remove.Margin = new Padding(0, 0, Theme.Space8, 0);
            var clear = StyledButton("清除全部注册", false);
            clear.Width = 120;
            actions.Controls.AddRange(new Control[] { remove, clear });
            layout.Controls.Add(actions, 0, 3);
            Action refresh = () =>
            {
                list.Rows.Clear();
                foreach (var entry in BrowserPairing.Read())
                    list.Rows.Add(entry.Key, entry.Value);
                list.ClearSelection();
                clear.Enabled = list.Rows.Count > 0;
                remove.Enabled = false;
            };
            Action<Action> apply = operation =>
            {
                try { operation(); refresh(); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "插件注册", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            list.SelectionChanged += (sender, args) => remove.Enabled = list.SelectedRows.Count == 1;
            register.Click += (sender, args) => apply(() =>
            {
                string id = code.Text.Trim();
                if (!BrowserPairing.ValidId(id)) throw new ArgumentException("请粘贴插件复制的 32 位配对码。");
                var entries = BrowserPairing.Read();
                string known;
                string label = Convert.ToString(browser.SelectedItem);
                if (entries.TryGetValue(id, out known) && known != "旧注册（未标注）")
                    label = String.Join(" / ", known.Split(new[] { " / " }, StringSplitOptions.None).Concat(new[] { label }).Distinct());
                entries[id] = label;
                BrowserPairing.Save(entries);
                code.Text = "";
            });
            remove.Click += (sender, args) =>
            {
                if (list.SelectedRows.Count != 1) return;
                string id = Convert.ToString(list.SelectedRows[0].Cells[0].Value);
                apply(() => { var entries = BrowserPairing.Read(); entries.Remove(id); BrowserPairing.Save(entries); });
            };
            clear.Click += (sender, args) =>
            {
                if (MessageBox.Show(this, "清除全部 dotSharK 插件注册？", "插件注册", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    apply(() => BrowserPairing.Save(new SortedDictionary<string, string>()));
            };
            page.VisibleChanged += (sender, args) => { if (page.Visible) apply(() => { }); };
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
        // 32px fields in a 44px row: the lower 12px belongs to spacing,
        // never to the control itself. Same rule across all settings pages.
        private static Control SpacedRow(Control content)
        {
            var frame = new TableLayoutPanel {
                Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
                BackColor = Theme.Window, Margin = Padding.Empty, Padding = Padding.Empty
            };
            frame.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ControlHeight));
            frame.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space12));
            content.Dock = DockStyle.Fill;
            content.Margin = Padding.Empty;
            frame.Controls.Add(content, 0, 0);
            return frame;
        }
        private static void SettingRow(TableLayoutPanel table, int index, string text, Control field)
        {
            table.RowStyles.Add(new RowStyle(SizeType.Absolute,
                Theme.ControlHeight + Theme.Space12));
            // Dock.Top fixes the control at 32px. The unoccupied last 12px
            // is the row gap. Avoid fourth-level nested tables: WinForms'
            // DrawToBitmap otherwise skips some owner-painted descendants.
            var label = Label(text);
            label.Dock = DockStyle.Top;
            label.Height = Theme.ControlHeight;
            table.Controls.Add(label, 0, index);
            field.Dock = DockStyle.Top;
            field.Height = Theme.ControlHeight;
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
                    t.Controls.Add(p.Control, column, 0);
                }
                column++;
            }
            return t;
        }
        // No page-level scrolling. All rows remain visible at the window's
        // minimum size; resizing expands the flexible control column only.
        private static void MountPage(Panel page, Control body)
        {
            var table = body as TableLayoutPanel;
            if (table != null)
            {
                table.AutoSize = false;
                // All visible rows use fixed design-token heights. Consume
                // surplus room in one EMPTY percent row at the bottom:
                // never let WinForms stretch the last explanation label.
                table.RowCount += 1;
                table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            }
            body.Dock = DockStyle.Fill;
            body.Margin = Padding.Empty;
            page.Controls.Add(body);
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
                    Name = "btnUpdateStable" + i,
                    AccessibleName = toolNames[i] + " 更新稳定版" };
                var nightly = new IconButton { Nightly = true,
                    Name = "btnUpdateNightly" + i,
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
                content.Controls.Add(SpacedRow(row), 0, rowIndex);
            }
            content.RowStyles.Add(new RowStyle(SizeType.Absolute,
                Theme.Space16 + Theme.Space12 * 3));
            toolUpdateStatus = Note("绿色按钮更新稳定版；黄色按钮更新 Nightly。更新使用 YT-DLP 页中的主代理。未指定路径时写入程序目录的 tools 子目录。");
            toolUpdateStatus.Name = "lblUpdateStatus";
            content.Controls.Add(toolUpdateStatus, 0, 6);
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space12 * 5));
            content.Controls.Add(Note("路径保存在 dotSharK.ini 中。浏览按钮只选择路径；在线更新会先下载到临时位置并验证版本，再替换目标文件。FFmpeg 稳定版来自 Gyan Builds，Nightly 来自 BtbN FFmpeg Builds。"), 0, 7);
            MountPage(page, content);
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
            var grid = FormTable(12);
            proxyBox = StyledTextBox(Options.Proxy);
            proxyBox.Name = "txtProxy";
            proxyEnabledBox = StyledToggle("启用", Options.ProxyEnabled);
            proxyEnabledBox.Name = "chkProxyEnabled";
            SettingRow(grid, 0, "代理", Row(Fill(proxyBox),
                Fixed(Theme.Space8), Fixed(Theme.Space24 * 2 + Theme.Space16, proxyEnabledBox)));

            geoProxyBox = StyledTextBox(Options.GeoVerificationProxy);
            geoProxyBox.Name = "txtGeoProxy";
            geoProxyEnabledBox = StyledToggle("启用", Options.GeoProxyEnabled);
            geoProxyEnabledBox.Name = "chkGeoProxyEnabled";
            v2ToolTips.SetToolTip(geoProxyEnabledBox,
                "仅用于 yt-dlp 地区验证；关闭时保留地址，不传给 yt-dlp。");
            v2ToolTips.SetToolTip(geoProxyBox,
                "仅用于 yt-dlp 地区验证；关闭时保留地址，不传给 yt-dlp。");
            SettingRow(grid, 1, "地域验证代理", Row(Fill(geoProxyBox),
                Fixed(Theme.Space8), Fixed(Theme.Space24 * 2 + Theme.Space16, geoProxyEnabledBox)));

            fragmentsBox = new DarkNumeric
            {
                Name = "numFragments", Minimum = 1, Maximum = 32,
                Value = Math.Max(1, Math.Min(32, Options.ConcurrentFragments)),
                Width = Theme.NumericWidth, Height = Theme.ControlHeight
            };
            SettingRow(grid, 2, "分片并发", Row(
                Fixed(Theme.NumericWidth, fragmentsBox), Fixed(Theme.Space12),
                Fill(Note("下载分片并发数"))));

            downloadsBox = new DarkNumeric
            {
                Name = "numConcurrentDownloads", Minimum = 1, Maximum = 6,
                Value = Math.Max(1, Math.Min(6, Options.ConcurrentDownloads)),
                Width = Theme.NumericWidth, Height = Theme.ControlHeight
            };
            SettingRow(grid, 3, "同时下载数", Row(
                Fixed(Theme.NumericWidth, downloadsBox), Fixed(Theme.Space12),
                Fill(Note("队列同时运行的视频数（1–6）"))));

            sourceBox = StyledCombo(new object[] { "浏览器", "Cookie 文件" });
            sourceBox.Name = "cboCookieSource";
            sourceBox.SelectedIndex = Options.CookieMode == "file" ? 1 : 0;
            SettingRow(grid, 4, "Cookie 来源", Row(
                Fixed(Theme.Space24 * 8 + Theme.Space8, sourceBox), Fill(null)));

            var browsers = new[] {
                new BrowserOption { Name="firefox", Label="Firefox" }, new BrowserOption { Name="chrome", Label="Chrome" },
                new BrowserOption { Name="edge", Label="Edge" }, new BrowserOption { Name="brave", Label="Brave" },
                new BrowserOption { Name="chromium", Label="Chromium" }, new BrowserOption { Name="opera", Label="Opera" },
                new BrowserOption { Name="vivaldi", Label="Vivaldi" }, new BrowserOption { Name="safari", Label="Safari" },
                new BrowserOption { Name="whale", Label="Whale" }
            };
            browserBox = StyledCombo(browsers.Cast<object>().ToArray());
            browserBox.Name = "cboBrowser";
            string browserName = String.IsNullOrWhiteSpace(Options.CookieBrowser) ? "firefox" : Options.CookieBrowser;
            for (int i = 0; i < browserBox.Items.Count; i++)
                if (((BrowserOption)browserBox.Items[i]).Name == browserName)
                    browserBox.SelectedIndex = i;
            SettingRow(grid, 5, "浏览器", Row(
                Fixed(Theme.Space24 * 8 + Theme.Space8, browserBox), Fill(null)));

            profileRootBox = StyledTextBox(Options.CookieProfileRoot);
            profileRootBox.Name = "txtProfileRoot";
            var rootBrowse = StyledButton("选择", false);
            rootBrowse.Name = "btnBrowseProfileRoot";
            rootBrowse.Click += BrowseProfileRoot;
            profileSearchButton = StyledButton("搜索", true);
            profileSearchButton.Name = "btnSearchProfiles";
            profileSearchButton.Click += async (s, e) => await SearchProfiles();
            // Equal 80px browse/search buttons, with a shared 8px gap.
            SettingRow(grid, 6, "搜索目录", Row(
                Fill(profileRootBox), Fixed(Theme.Space8),
                Fixed(Theme.BrowseButtonWidth, rootBrowse), Fixed(Theme.Space8),
                Fixed(Theme.BrowseButtonWidth, profileSearchButton)));

            profileBox = StyledCombo(new object[0]);
            profileBox.Name = "cboBrowserProfile";
            profileBox.DropDownStyle = ComboBoxStyle.DropDown;
            profileFoundCheck = Label("");
            profileFoundCheck.Name = "lblProfileFound";
            profileFoundCheck.Font = Theme.HeadingFont;
            profileFoundCheck.ForeColor = green;
            profileFoundCheck.TextAlign = ContentAlignment.MiddleCenter;
            profileFoundCheck.Visible = false;
            SettingRow(grid, 7, "浏览器配置", Row(
                Fill(profileBox), Fixed(Theme.Space8),
                Fixed(Theme.Space24, profileFoundCheck)));
            if (!String.IsNullOrWhiteSpace(Options.CookieProfile))
            {
                profileBox.Items.Add(Options.CookieProfile);
                profileBox.Text = Options.CookieProfile;
            }
            profileStatus = Note("默认浏览器配置自动读取；指定目录后可递归搜寻 Cookie 数据库。");
            profileStatus.Name = "lblProfileStatus";
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute,
                Theme.ControlHeight + Theme.Space12));
            grid.Controls.Add(SpacedRow(profileStatus), 1, 8);

            cookieFileBox = StyledTextBox(Options.CookieFile);
            cookieFileBox.Name = "txtCookieFile";
            var cookieBrowse = StyledButton("浏览…", false);
            cookieBrowse.Name = "btnBrowseCookieFile";
            cookieBrowse.Click += BrowseCookieFile;
            SettingRow(grid, 9, "Cookie 文件", Row(
                Fill(cookieFileBox), Fixed(Theme.Space8),
                Fixed(Theme.BrowseButtonWidth, cookieBrowse)));

            qualityLimitBox = new DarkNumeric
            {
                Name = "numVideoQualityLimit", Minimum = 0, Maximum = 9999,
                Value = Math.Max(0, Math.Min(9999, Options.VideoQualityLimit)),
                Width = Theme.FullNumericWidth, Height = Theme.ControlHeight
            };
            // One continuous caption before the numeric field. The normal
            // 96px settings-label column would otherwise clip "前：" away.
            var qualityCaption = Label("视频质量限制 前：");
            qualityCaption.Name = "lblVideoQualityLimitCaption";
            int qualityCaptionWidth = TextRenderer.MeasureText(qualityCaption.Text,
                Theme.BodyFont).Width + Theme.Space12;
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute,
                Theme.ControlHeight + Theme.Space12));
            var qualityRow = SpacedRow(Row(
                Fixed(qualityCaptionWidth, qualityCaption),
                Fixed(Theme.FullNumericWidth, qualityLimitBox), Fill(null)));
            grid.Controls.Add(qualityRow, 0, 10);
            grid.SetColumnSpan(qualityRow, 2);
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute,
                Theme.ControlHeight + Theme.Space12));
            grid.Controls.Add(SpacedRow(Note("默认前三个不同分辨率档位；0 为不限制。同分辨率的不同编码、HDR 选项均保留。")), 1, 11);
            MountPage(page, grid);

            // Keep the original async search and save event handlers unchanged.
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
            profileBox.SelectedIndexChanged += (s, e) =>
                SetProfileFound(profileBox.SelectedIndex >= 0);
            UpdateCookieMode();
            if (sourceBox.SelectedIndex == 0)
                Shown += async (s, e) => await SearchDefaultBrowserProfiles();
        }

        private void BuildSilentPage(Panel page)
        {
            var content = new TableLayoutPanel
            {
                ColumnCount = 1, RowCount = 7, Margin = Padding.Empty,
                Padding = Padding.Empty, BackColor = bg,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            content.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ControlHeight));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space12));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ControlHeight));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space12));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ControlHeight));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space12));
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space12 * 4));

            content.Controls.Add(Label("浏览器右键 · 静默下载", true), 0, 0);
            silentFolderBox = StyledTextBox(Options.SilentDownloadFolder);
            silentFolderBox.Name = "txtSilentFolder";
            var browse = StyledButton("浏览…", false);
            browse.Name = "btnBrowseSilentFolder";
            browse.Click += (sender, args) =>
            {
                string selected = silentFolderBox.Text.Trim();
                using (var picker = new FolderBrowserDialog
                {
                    Description = "选择浏览器静默下载的保存目录",
                    SelectedPath = Directory.Exists(selected) ? selected :
                        Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                    ShowNewFolderButton = true
                })
                if (picker.ShowDialog(this) == DialogResult.OK)
                    silentFolderBox.Text = picker.SelectedPath;
            };
            content.Controls.Add(Row(
                Fixed(Theme.LabelColumnWidth, Label("独立保存到")),
                Fill(silentFolderBox), Fixed(Theme.Space8),
                Fixed(Theme.BrowseButtonWidth, browse)), 0, 2);
            silentResolutionBox = StyledCombo(new object[] { "最高", "8K", "4K", "1080p", "720p" });
            silentResolutionBox.Name = "cboSilentMaxResolution";
            int resolutionIndex = Array.IndexOf(SilentHeightOptions, Options.SilentMaxHeight);
            silentResolutionBox.SelectedIndex = resolutionIndex < 0 ? 0 : resolutionIndex;
            content.Controls.Add(Row(
                Fixed(Theme.LabelColumnWidth, Label("最高分辨率")),
                Fixed(Theme.Space24 * 8 + Theme.Space8, silentResolutionBox),
                Fixed(Theme.Space16), Fill(Note("仅限制视频分辨率，其余质量排序不变"))), 0, 4);
            content.Controls.Add(Row(
                Fixed(Theme.LabelColumnWidth), Fill(Note(
                    "只影响浏览器右键“发送到 dotSharK”下载。主窗口的“保存到”保持独立。" +
                    Environment.NewLine +
                    "帧率 / HDR / AV1 / 音频优先级不变；继续使用已保存的代理和 Cookie。"))), 0, 6);
            MountPage(page, content);
        }

        private DarkCheckBox StyledToggle(string title, bool isChecked)
        {
            return new DarkCheckBox
            {
                Text = title, Checked = isChecked, ForeColor = Theme.PrimaryText,
                BackColor = Theme.Window, Margin = Padding.Empty
            };
        }

        private void BuildAdvancedPage(Panel page)
        {
            var root = new TableLayoutPanel
            {
                ColumnCount = 3, RowCount = 9, Margin = Padding.Empty,
                Padding = Padding.Empty, BackColor = bg,
                AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.Space16));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ControlHeight));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space12));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ControlHeight));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space24 * 5));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space24 * 2));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space24));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ControlHeight));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space24 * 4));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space24 * 2));

            keepCompletedBox = StyledToggle("重启后保留已完成任务", Options.KeepCompletedTasks);
            keepCompletedBox.Name = "chkKeepCompleted";
            keepCompletedBox.ForeColor = Theme.SecondaryText;
            // Standalone checkboxes do not autosize; fill the entire row so the
            // Chinese caption renders, not only the 16px checkmark square.
            keepCompletedBox.Dock = DockStyle.Fill;
            keepCompletedBox.Margin = Padding.Empty;
            root.Controls.Add(keepCompletedBox, 0, 0);
            root.SetColumnSpan(keepCompletedBox, 3);
            root.Controls.Add(Label("使用 aria2c 下载的站点", true), 0, 2);
            root.Controls.Add(Label("以下站点不使用代理", true), 2, 2);
            ariaSitesBox = new FlatTextArea
            {
                Name = "txtAria2cSites", Text = Options.Aria2cSites ?? "",
                Font = Theme.MonoFont, Dock = DockStyle.Fill, Margin = Padding.Empty
            };
            noProxySitesBox = new FlatTextArea
            {
                Name = "txtNoProxySites", Text = Options.NoProxySites ?? "",
                Font = Theme.MonoFont, Dock = DockStyle.Fill, Margin = Padding.Empty
            };
            root.Controls.Add(ariaSitesBox, 0, 3);
            root.Controls.Add(noProxySitesBox, 2, 3);
            root.Controls.Add(Note("空格/换行分隔域名，如 youtube.com，自动覆盖所有子域名；按输入网页网址匹配，不看视频 CDN。"), 0, 4);
            root.Controls.Add(Note("规则相同；命中时主代理和地域验证代理均不使用，已保存的代理地址不受影响。"), 2, 4);
            var commandTitle = Label("自定义 yt-dlp 原生命令行参数（下载与分析均生效）", true);
            root.Controls.Add(commandTitle, 0, 6);
            root.SetColumnSpan(commandTitle, 3);
            customArgumentsBox = new FlatTextArea
            {
                Name = "txtCustomYtDlpArgs",
                Text = Options.CustomArguments ?? "",
                Font = Theme.MonoFont, Dock = DockStyle.Fill, Margin = Padding.Empty
            };
            root.Controls.Add(customArgumentsBox, 0, 7);
            root.SetColumnSpan(customArgumentsBox, 3);
            var hint = Note("例如 --retries 20 --no-mtime；支持带引号的参数值，自定义参数覆盖一般内置设置。" +
                Environment.NewLine +
                "无代理站点规则优先于自定义代理参数。aria2c.exe 在「外部工具」页指定。");
            root.Controls.Add(hint, 0, 8);
            root.SetColumnSpan(hint, 3);
            MountPage(page, root);
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
            { DarkMessageBox.Show(this, "找不到指定的 Cookie 文件。", "dotSharK", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
            if (sourceBox.SelectedIndex == 1 && String.IsNullOrWhiteSpace(cookieFileBox.Text))
            { DarkMessageBox.Show(this, "请指定 Netscape 格式的 Cookie 文件。", "dotSharK", MessageBoxButtons.OK, MessageBoxIcon.Warning); return; }
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
                DarkMessageBox.Show(this, ex.Message, "高级下载参数无效", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Options.ConcurrentFragments = (int)fragmentsBox.Value; Options.ConcurrentDownloads = (int)downloadsBox.Value; Options.VideoQualityLimit = (int)qualityLimitBox.Value;
            Options.KeepCompletedTasks = keepCompletedBox.Checked;
            Options.SilentMaxHeight = SilentHeightOptions[Math.Max(0, silentResolutionBox.SelectedIndex)];
            Options.CookieMode = sourceBox.SelectedIndex == 1 ? "file" : "browser";
            string silentFolder = silentFolderBox.Text.Trim();
            if (String.IsNullOrWhiteSpace(silentFolder))
                silentFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads", "Silent");
            if (!Path.IsPathRooted(silentFolder))
            {
                DarkMessageBox.Show(this, "静默下载目录需要填写完整的绝对路径。", "dotSharK", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try { silentFolder = Path.GetFullPath(silentFolder); }
            catch (Exception)
            {
                DarkMessageBox.Show(this, "静默下载目录路径无效。", "dotSharK", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
        private V2Combo StyledCombo(object[] items)
        {
            var combo = new V2Combo
            {
                Height = Theme.ControlHeight, Font = Theme.BodyFont,
                Margin = Padding.Empty
            };
            combo.Items.AddRange(items);
            if (combo.Items.Count > 0) combo.SelectedIndex = 0;
            return combo;
        }
        private FlatTextBox StyledTextBox(string text)
        {
            return new FlatTextBox
            {
                Text = text ?? "", Font = Theme.BodyFont,
                Height = Theme.ControlHeight, Margin = Padding.Empty,
                Dock = DockStyle.Fill
            };
        }
        private Label StyledLabel(string text) { return Label(text); }
        private Button StyledButton(string text, bool accent)
        {
            Button button = accent ? (Button)new PrimaryButton() : new SecondaryButton();
            button.Text = text; button.Font = Theme.BodyFont;
            button.Height = Theme.ControlHeight;
            button.Margin = Padding.Empty;
            button.MinimumSize = new Size(Theme.Space24, Theme.ControlHeight);
            return button;
        }
        protected override void Dispose(bool disposing)
        {
            if (disposing) v2ToolTips.Dispose();
            base.Dispose(disposing);
        }
    }
}
