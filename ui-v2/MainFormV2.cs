using System;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Runtime.InteropServices;
using dotSharK.UiV2;

namespace dotSharK
{
    internal sealed class MainFormV2 : DarkForm
    {
        // Handle the native minimize command before WinForms moves the window
        // off-screen. The tray owner performs one smooth hide operation.
        internal event EventHandler TrayMinimizeRequested;
        protected override void WndProc(ref Message m)
        {
            const int WmSysCommand = 0x0112;
            const long ScMinimize = 0xF020;
            if (m.Msg == WmSysCommand && (m.WParam.ToInt64() & 0xFFF0L) == ScMinimize &&
                TrayMinimizeRequested != null && Visible && !IsDisposed)
            {
                TrayMinimizeRequested(this, EventArgs.Empty);
                m.Result = IntPtr.Zero;
                return;
            }
            base.WndProc(ref m);
        }

        private readonly Color bg = Theme.Window, panel = Theme.Panel, green = Theme.Accent,
            muted = Theme.SecondaryText, fg = Theme.PrimaryText, line = Theme.Border;
        private FlatTextBox urlBox, folderBox;
        private FlatTextArea logBox;
        private PrimaryButton analyzeButton;
        private Button downloadButton, configButton, browseFolderButton;
        private Button removeQueueButton, clearFinishedButton, retryFailedButton;
        private DataGridView queueGrid;
        private Label stateLabel, queueCountLabel;
        private DarkProgressIndicator progress;
        private readonly List<DownloadQueueItem> queueItems = new List<DownloadQueueItem>();
        private YtDlpClient client;
        private YtDlpOptions ytdlpOptions = new YtDlpOptions();
        private string ytDlpPath = "", ffmpegPath = "", ffprobePath = "", denoPath = "", aria2cPath = "";
        private readonly Dictionary<DownloadQueueItem, CancellationTokenSource> itemCancellation =
            new Dictionary<DownloadQueueItem, CancellationTokenSource>();
        private bool loadingSettings;
        private bool downloadRunning;
        private bool queueStopRequested;
        private CancellationTokenSource queueCancellation;
        private readonly Dictionary<DownloadQueueItem, double> currentRunProgress = new Dictionary<DownloadQueueItem, double>();
        private List<DownloadQueueItem> currentRunItems = new List<DownloadQueueItem>();
        private readonly string settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dotSharK.ini");
        private readonly string completedPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dotSharK-completed.json");
        private WindowPlacement savedMainWindow, savedPlaylistWindow;
        private bool lastMainMaximized;
        private readonly System.Windows.Forms.Timer clipboardWatchTimer = new System.Windows.Forms.Timer { Interval = 550 };
        private string clipboardCandidateUrl;
        private bool clipboardFlashBright;
        private bool browserAutoRunRequested;

        internal int ActiveDownloadCount { get { return queueItems.Count(x => x.Status == "下载中"); } }
        internal int PendingDownloadCount { get { return queueItems.Count(x => x.Status == "等待中"); } }

        // The native host's inbox is drained on the WinForms UI thread, even if
        // the main window is currently hidden in the notification area.
        internal void EnqueueBrowserUrl(string url, string jobPath)
        {
            if (IsDisposed || String.IsNullOrWhiteSpace(url)) return;
            if (queueItems.Any(x => x.BrowserSilent &&
                (x.Status == "等待中" || x.Status == "下载中" || x.Status == "已暂停") &&
                String.Equals(x.Url, url, StringComparison.OrdinalIgnoreCase)))
            {
                SilentDownloadBridge.CompleteJob(jobPath);
                return;
            }

            AddQueueItem(new DownloadQueueItem
            {
                Title = url, Url = url, BrowserSilent = true,
                BrowserJobPath = jobPath,
                OutputFolder = String.IsNullOrWhiteSpace(ytdlpOptions.SilentDownloadFolder)
                    ? SilentDownloadBridge.SilentFolder : ytdlpOptions.SilentDownloadFolder,
                Status = "等待中",
                ProgressWeight = 1
            });
            SetState("浏览器已加入队列 · " + url);
            if (downloadRunning) browserAutoRunRequested = true;
            else { var run = DownloadQueue(); }
        }

        internal async Task CancelQueueForExitAsync()
        {
            if (!downloadRunning) return;
            StopQueue();
            // Cancellation terminates the child yt-dlp process through the
            // existing process-tree cancellation handler.
            for (int attempt = 0; attempt < 100 && downloadRunning; attempt++)
                await Task.Delay(100);
        }

        public MainFormV2()
        {
            Text = "dotSharK v" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString(3) + "  ·  YouTube Downloader"; StartPosition = FormStartPosition.CenterScreen; MinimumSize = Theme.MainMinimumSize; Size = new Size(1120, 820);
            BackColor = bg; ForeColor = fg; Font = Theme.BodyFont; FormBorderStyle = FormBorderStyle.Sizable; KeyPreview = true; Icon = AppBranding.CreateIcon();
            BuildUi(); LoadSettings(); LoadCompletedQueue();
            if (savedMainWindow != null)
            {
                savedMainWindow.Restore(this);
                lastMainMaximized = savedMainWindow.Maximized;
            }
            Resize += (s, e) =>
            {
                if (WindowState != FormWindowState.Minimized) lastMainMaximized = WindowState == FormWindowState.Maximized;
            };
            ytDlpPath = ExistingOrDefault(ytDlpPath, "yt-dlp.exe");
            ffmpegPath = ExistingOrDefault(ffmpegPath, "ffmpeg.exe");
            ffprobePath = ExistingOrDefault(ffprobePath, "ffprobe.exe");
            denoPath = ExistingOrDefault(denoPath, "deno.exe");
            aria2cPath = ExistingOrDefault(aria2cPath, "aria2c.exe");
            ytdlpOptions.Aria2cPath = aria2cPath;
            RefreshClient();
            SetState(client == null ? "请先在“配置…”中指定 yt-dlp.exe。" : "就绪 · " + Path.GetFileName(client.ExecutablePath));
            clipboardWatchTimer.Tick += (s, e) => UpdateClipboardAttention();
            Activated += (s, e) => { UpdateClipboardAttention(); clipboardWatchTimer.Start(); };
            Deactivate += (s, e) => { clipboardWatchTimer.Stop(); ClearClipboardAttention(); };
        }

        private void BuildUi()
        {
            SuspendLayout();
            try
            {
                // All top-level content shares a 24px inset and right edge.
                // Only the queue is elastic; logs keep a 120px initial height.
                var root = new TableLayoutPanel
                {
                    Name = "layoutMainRoot", Dock = DockStyle.Fill, ColumnCount = 1,
                    RowCount = 11, BackColor = Theme.Window, Margin = Padding.Empty,
                    Padding = new Padding(Theme.Space24)
                };
                root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space24 * 2));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space16));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ActionHeight));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space16));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space16));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ControlHeight));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space16));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space24 * 5));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space16));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ActionHeight));
                Controls.Add(root);

                var brand = new TableLayoutPanel
                {
                    Name = "layoutHeader", Dock = DockStyle.Fill,
                    ColumnCount = 5, RowCount = 1, Margin = Padding.Empty,
                    Padding = Padding.Empty, BackColor = Theme.Window
                };
                brand.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space24 * 2));
                brand.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                brand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.Space16));
                brand.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                brand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.Space16));
                brand.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.Space24 * 4 + Theme.Space16));
                var logo = new Label
                {
                    Name = "lblLogo", Text = "dotSharK",
                    Font = new Font("Microsoft YaHei UI", 19f, FontStyle.Bold),
                    ForeColor = green, AutoSize = true,
                    Anchor = AnchorStyles.Left, Margin = Padding.Empty
                };
                var tagline = new Label
                {
                    Name = "lblTagline", Text = "做最快的 yt-dlp 桌面外壳",
                    Dock = DockStyle.Fill, ForeColor = muted, Font = Theme.SmallFont,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Margin = Padding.Empty
                };
                configButton = GhostButton("配置…", Theme.Space24 * 4 + Theme.Space16);
                configButton.Name = "btnSettings";
                // Keep this secondary button 32px tall inside the 48px
                // header row; horizontal right and vertical center alignment.
                configButton.Dock = DockStyle.None;
                configButton.Anchor = AnchorStyles.Right;
                configButton.Height = Theme.ControlHeight;
                configButton.Click += OpenConfiguration;
                brand.Controls.Add(logo, 0, 0);
                brand.Controls.Add(tagline, 2, 0);
                brand.Controls.Add(configButton, 4, 0);
                root.Controls.Add(brand, 0, 0);

                var address = new TableLayoutPanel
                {
                    Name = "layoutAddress", Dock = DockStyle.Fill,
                    ColumnCount = 3, RowCount = 1, Margin = Padding.Empty,
                    Padding = Padding.Empty, BackColor = Theme.Window
                };
                address.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ActionHeight));
                address.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                address.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.Space8));
                address.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.AnalyzeButtonWidth));
                urlBox = InputBox();
                urlBox.Name = "txtUrl";
                urlBox.Dock = DockStyle.Fill;
                urlBox.TextChanged += (s, e) =>
                {
                    if (AddressContainsClipboardLink(clipboardCandidateUrl, urlBox.Text))
                        ClearClipboardAttention();
                };
                analyzeButton = AccentButton("分析链接");
                analyzeButton.Name = "btnAnalyze";
                analyzeButton.Dock = DockStyle.Fill;
                analyzeButton.Click += async (s, e) => await AnalyzeFromButton();
                address.Controls.Add(urlBox, 0, 0);
                address.Controls.Add(analyzeButton, 2, 0);
                root.Controls.Add(address, 0, 2);

                var queueArea = new TableLayoutPanel
                {
                    Name = "layoutQueue", Dock = DockStyle.Fill, ColumnCount = 1,
                    RowCount = 3, Margin = Padding.Empty, Padding = Padding.Empty,
                    BackColor = Theme.Window
                };
                queueArea.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ControlHeight));
                queueArea.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space8));
                queueArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                var toolbar = new TableLayoutPanel
                {
                    Name = "layoutQueueToolbar", Dock = DockStyle.Fill,
                    ColumnCount = 6, RowCount = 1, Margin = Padding.Empty,
                    Padding = Padding.Empty, BackColor = Theme.Window
                };
                toolbar.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ControlHeight));
                toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.QueueRunButtonWidth));
                toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.Space8));
                toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.AnalyzeButtonWidth));
                toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.Space8));
                toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.QueueRunButtonWidth));
                queueCountLabel = new Label
                {
                    Name = "lblQueueCount", Text = "下载队列（0）",
                    Dock = DockStyle.Fill, Font = Theme.HeadingFont,
                    ForeColor = fg, TextAlign = ContentAlignment.MiddleLeft,
                    Margin = Padding.Empty
                };
                retryFailedButton = GhostButton("重试所有失败", Theme.QueueRunButtonWidth);
                retryFailedButton.Name = "btnRetryAll";
                retryFailedButton.Dock = DockStyle.Fill;
                retryFailedButton.Click += async (s, e) => await RetryAllFailed();
                removeQueueButton = GhostButton("移除选中", Theme.AnalyzeButtonWidth);
                removeQueueButton.Name = "btnRemoveSelected";
                removeQueueButton.Dock = DockStyle.Fill;
                removeQueueButton.Click += (s, e) => RemoveSelectedQueueItems();
                clearFinishedButton = GhostButton("清理已完成", Theme.QueueRunButtonWidth);
                clearFinishedButton.Name = "btnClearFinished";
                clearFinishedButton.Dock = DockStyle.Fill;
                clearFinishedButton.Click += (s, e) => ClearFinishedQueueItems();
                toolbar.Controls.Add(queueCountLabel, 0, 0);
                toolbar.Controls.Add(retryFailedButton, 1, 0);
                toolbar.Controls.Add(removeQueueButton, 3, 0);
                toolbar.Controls.Add(clearFinishedButton, 5, 0);
                queueArea.Controls.Add(toolbar, 0, 0);

                queueGrid = new DarkDataGridView
                {
                    Name = "gridQueue", Dock = DockStyle.Fill, Margin = Padding.Empty,
                    ReadOnly = true, MultiSelect = true,
                    SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                    AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
                };
                var titleColumn = new DataGridViewTextBoxColumn
                {
                    Name = "title", HeaderText = "视频",
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    MinimumWidth = Theme.Space24 * 4
                };
                queueGrid.Columns.Add(titleColumn);
                queueGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "source", HeaderText = "来源", Width = Theme.SourceColumnWidth,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.None
                });
                queueGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "quality", HeaderText = "质量方案", MinimumWidth = Theme.QualityColumnWidth, Width = Theme.QualityColumnWidth,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.None
                });
                queueGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "status", HeaderText = "状态", Width = Theme.StatusColumnWidth,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.None
                });
                EnsureQueueColumnWidths();
                queueGrid.FontChanged += (s, e) => EnsureQueueColumnWidths();
                queueGrid.SelectionChanged += (s, e) => UpdateQueueControls();
                // Dark context menu includes file/folder actions for completed tasks.
                var queueMenu = DarkContextMenuRendererV2();
                var pauseOneMenuItem = new ToolStripMenuItem("暂停此项");
                var retryOneMenuItem = new ToolStripMenuItem("继续/重试此项");
                var openFileMenuItem = new ToolStripMenuItem("打开");
                var openFolderMenuItem = new ToolStripMenuItem("打开下载目录");
                queueMenu.Items.Add(openFileMenuItem);
                queueMenu.Items.Add(openFolderMenuItem);
                queueMenu.Items.Add(new ToolStripSeparator());
                queueMenu.Items.Add(pauseOneMenuItem);
                queueMenu.Items.Add(retryOneMenuItem);
                DownloadQueueItem contextItem = null;
                queueGrid.CellMouseDown += (s, e) =>
                {
                    if (e.Button != MouseButtons.Right) return;
                    contextItem = null;
                    if (e.RowIndex >= 0)
                    {
                        queueGrid.ClearSelection();
                        queueGrid.Rows[e.RowIndex].Selected = true;
                        contextItem = queueGrid.Rows[e.RowIndex].Tag as DownloadQueueItem;
                    }
                    UpdateItemContextActions(contextItem, pauseOneMenuItem, retryOneMenuItem, openFileMenuItem, openFolderMenuItem);
                };
                queueMenu.Opening += (s, e) =>
                {
                    if (contextItem == null || !queueItems.Contains(contextItem))
                        contextItem = queueGrid.SelectedRows.Count > 0
                            ? queueGrid.SelectedRows[0].Tag as DownloadQueueItem : null;
                    UpdateItemContextActions(contextItem, pauseOneMenuItem, retryOneMenuItem, openFileMenuItem, openFolderMenuItem);
                };
                pauseOneMenuItem.Click += (s, e) => PauseQueueItem(contextItem);
                retryOneMenuItem.Click += (s, e) => ResumeQueueItem(contextItem);
                openFileMenuItem.Click += (s, e) => OpenCompletedItem(contextItem, false);
                openFolderMenuItem.Click += (s, e) => OpenCompletedItem(contextItem, true);
                queueGrid.ContextMenuStrip = queueMenu;
                var gridFrame = new Panel
                {
                    Name = "frameQueue", Dock = DockStyle.Fill, BackColor = Theme.Border,
                    Padding = new Padding(Theme.BorderWidth), Margin = Padding.Empty
                };
                gridFrame.Controls.Add(queueGrid);
                queueArea.Controls.Add(gridFrame, 0, 2);
                root.Controls.Add(queueArea, 0, 4);

                var path = new TableLayoutPanel
                {
                    Name = "layoutSavePath", Dock = DockStyle.Fill, ColumnCount = 4,
                    RowCount = 1, Margin = Padding.Empty, Padding = Padding.Empty,
                    BackColor = Theme.Window
                };
                path.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ControlHeight));
                path.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.LabelColumnWidth));
                path.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                path.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.Space8));
                path.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.BrowseButtonWidth));
                path.Controls.Add(new Label
                {
                    Name = "lblSaveTo", Text = "保存到", Dock = DockStyle.Fill,
                    ForeColor = muted, TextAlign = ContentAlignment.MiddleLeft,
                    Margin = Padding.Empty
                }, 0, 0);
                folderBox = InputBox();
                folderBox.Name = "txtOutputFolder";
                folderBox.TextChanged += (s, e) => SaveSettings();
                folderBox.Dock = DockStyle.Fill;
                path.Controls.Add(folderBox, 1, 0);
                browseFolderButton = GhostButton("浏览…", Theme.BrowseButtonWidth);
                browseFolderButton.Name = "btnBrowseFolder";
                browseFolderButton.Dock = DockStyle.Fill;
                browseFolderButton.Click += BrowseFolder;
                path.Controls.Add(browseFolderButton, 3, 0);
                root.Controls.Add(path, 0, 6);

                logBox = new FlatTextArea
                {
                    Name = "txtLog", Dock = DockStyle.Fill, Margin = Padding.Empty,
                    ReadOnly = true, Font = Theme.BodyFont, WordWrap = true
                };
                root.Controls.Add(logBox, 0, 8);

                var footer = new TableLayoutPanel
                {
                    Name = "layoutStatus", Dock = DockStyle.Fill,
                    ColumnCount = 5, RowCount = 1, BackColor = Theme.Window,
                    Margin = Padding.Empty, Padding = Padding.Empty
                };
                footer.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ActionHeight));
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.Space24 * 7));
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.Space16));
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.QueueRunButtonWidth));
                footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 0));
                stateLabel = new Label
                {
                    Name = "lblStatus", Text = "就绪",
                    Dock = DockStyle.Fill, ForeColor = muted,
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoEllipsis = true, Margin = Padding.Empty
                };
                progress = new DarkProgressIndicator
                {
                    Name = "progressQueue", Height = Theme.Space8,
                    Anchor = AnchorStyles.Left | AnchorStyles.Right,
                    Visible = false, Margin = Padding.Empty
                };
                downloadButton = AccentButton("开始队列");
                downloadButton.Name = "btnStartQueue";
                downloadButton.Dock = DockStyle.Fill;
                downloadButton.Enabled = false;
                downloadButton.Click += async (s, e) =>
                {
                    if (downloadRunning) StopQueue();
                    else await DownloadQueue();
                };
                footer.Controls.Add(stateLabel, 0, 0);
                footer.Controls.Add(progress, 1, 0);
                footer.Controls.Add(downloadButton, 3, 0);
                root.Controls.Add(footer, 0, 10);
                AcceptButton = analyzeButton;
            }
            finally { ResumeLayout(true); }
        }

        private static ContextMenuStrip DarkContextMenuRendererV2()
        {
            return dotSharK.UiV2.DarkContextMenuRenderer.CreateMenu();
        }

        private FlatTextBox InputBox()
        {
            return new FlatTextBox
            {
                Font = Theme.BodyFont, Height = Theme.ControlHeight,
                Margin = Padding.Empty
            };
        }
        private PrimaryButton AccentButton(string text)
        {
            return new PrimaryButton
            {
                Text = text, Height = Theme.ActionHeight,
                Font = Theme.BodyFont, Margin = Padding.Empty
            };
        }
        private SecondaryButton GhostButton(string text, int width)
        {
            return new SecondaryButton
            {
                Text = text, Width = width, Height = Theme.ControlHeight,
                Font = Theme.BodyFont, Margin = Padding.Empty
            };
        }

        // Check the clipboard only while the main window is foreground. No background polling.
        private void UpdateClipboardAttention()
        {
            if (IsDisposed || !Visible || Form.ActiveForm != this || !analyzeButton.Enabled)
            {
                ClearClipboardAttention();
                return;
            }

            string candidate = ReadClipboardCandidate();
            if (candidate == null || AddressContainsClipboardLink(candidate, urlBox.Text))
            {
                ClearClipboardAttention();
                return;
            }

            if (!String.Equals(candidate, clipboardCandidateUrl, StringComparison.Ordinal))
            {
                clipboardCandidateUrl = candidate;
                clipboardFlashBright = true;
            }
            else clipboardFlashBright = !clipboardFlashBright;
            analyzeButton.AttentionHighlight = clipboardFlashBright;
        }

        internal static bool AddressContainsClipboardLink(string clipboardUrl, string addressText)
        {
            string address = NormalizeDownloadLink(addressText);
            return clipboardUrl != null && address != null &&
                String.Equals(clipboardUrl, address, StringComparison.Ordinal);
        }

        private void ClearClipboardAttention()
        {
            clipboardCandidateUrl = null;
            clipboardFlashBright = false;
            if (analyzeButton != null && !analyzeButton.IsDisposed)
                analyzeButton.AttentionHighlight = false;
        }

        private static string ReadClipboardCandidate()
        {
            try { return NormalizeDownloadLink(Clipboard.GetText(TextDataFormat.UnicodeText)); }
            catch (ExternalException) { return null; } // Another application holds the clipboard.
            catch (System.Threading.ThreadStateException) { return null; }
        }

        // Accept a single, complete web URL; yt-dlp decides if its site is supported.
        internal static string NormalizeDownloadLink(string raw)
        {
            if (String.IsNullOrWhiteSpace(raw)) return null;
            string candidate = raw.Trim();
            if (candidate.Length > 4096 || candidate.Any(Char.IsWhiteSpace)) return null;
            Uri uri;
            if (!Uri.TryCreate(candidate, UriKind.Absolute, out uri)) return null;
            if ((uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
                String.IsNullOrWhiteSpace(uri.Host) || !String.IsNullOrEmpty(uri.UserInfo))
                return null;
            return candidate;
        }

        private async Task AnalyzeFromButton()
        {
            if (Form.ActiveForm == this)
            {
                string candidate = ReadClipboardCandidate();
                if (candidate != null)
                {
                    urlBox.Text = candidate;
                    urlBox.SelectionStart = urlBox.TextLength;
                    urlBox.SelectionLength = 0;
                }
            }
            ClearClipboardAttention();
            await Analyze();
        }

        private async Task Analyze()
        {
            if (client == null) { OpenConfiguration(this, EventArgs.Empty); if (client == null) return; }
            if (String.IsNullOrWhiteSpace(urlBox.Text)) { SetState("请先粘贴视频网址。"); return; }
            if (!ValidProxy()) return;
            string url = urlBox.Text.Trim();
            Busy(true, "正在识别视频或播放列表…"); logBox.Clear(); analyzeButton.Enabled = false;
            try
            {
                LinkInspection inspection = await client.InspectAsync(url, ytdlpOptions);
                if (inspection.IsPlaylist)
                {
                    Busy(false, "");
                    using (var choice = new PlaylistChoiceDialog(inspection.Playlist.Title, inspection.Playlist.Entries.Count))
                    {
                        if (choice.ShowDialog(this) == DialogResult.OK)
                        {
                            if (choice.DownloadPlaylist) await AnalyzePlaylist(inspection.Playlist);
                            else await AnalyzeSingle(GetSingleVideoTarget(url, inspection.Playlist));
                        }
                    }
                }
                else await AnalyzeSingle(url, inspection.Media);
            }
            catch (Exception ex) { SetState("解析失败 · 查看详情"); logBox.Text = ex.Message; }
            finally { analyzeButton.Enabled = true; Busy(false, stateLabel.Text); UpdateQueueControls(); }
        }

        private async Task AnalyzeSingle(string url, MediaInfo inspected = null)
        {
            Busy(true, "正在解析单个视频的格式…");
            MediaInfo media = inspected ?? await client.AnalyzeAsync(url, ytdlpOptions);
            if (String.IsNullOrWhiteSpace(media.Title)) throw new InvalidOperationException("链接已响应，但未返回标题。");
            Busy(false, "");
            using (var dialog = new SingleVideoDialogV2(media))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) { SetState("已取消选择。"); return; }
                AddQueueItem(new DownloadQueueItem
                {
                    Title = media.Title, Url = String.IsNullOrWhiteSpace(media.WebpageUrl) ? url : media.WebpageUrl,
                    Media = media, Video = dialog.Video, Audio = dialog.Audio, Subtitle = dialog.Subtitle,
                    AudioOnly = dialog.AudioOnly, Container = dialog.OutputContainer, OutputFolder = GetOutputFolder(),
                    OriginPageUrl = urlBox.Text.Trim(),
                    ProgressWeight = media.Duration > 0 ? media.Duration : 1, Status = "等待中"
                });
            }
            SetState("已加入队列 · " + media.Title);
        }

        private async Task AnalyzePlaylist(PlaylistInfo playlist)
        {
            if (playlist.Entries.Count == 0) throw new InvalidOperationException("播放列表里没有可下载的视频。");
            Busy(true, "正在分析播放列表质量选项…");
            MediaInfo sample = await client.AnalyzeAsync(playlist.Entries[0].Url, ytdlpOptions);
            Busy(false, "");
            string playlistFolder = Path.Combine(GetOutputFolder(), SafeFolderName(playlist.Title));
            using (var dialog = new PlaylistSelectionDialogV2(playlist, sample, playlistFolder))
            {
                bool lastPlaylistMaximized = savedPlaylistWindow != null && savedPlaylistWindow.Maximized;
                if (savedPlaylistWindow != null) savedPlaylistWindow.Restore(dialog);
                dialog.Resize += (s, e) =>
                {
                    if (dialog.WindowState != FormWindowState.Minimized)
                        lastPlaylistMaximized = dialog.WindowState == FormWindowState.Maximized;
                };
                dialog.FormClosing += (s, e) =>
                {
                    savedPlaylistWindow = WindowPlacement.Capture(dialog, lastPlaylistMaximized);
                    SaveSettings();
                };
                if (dialog.ShowDialog(this) != DialogResult.OK) { SetState("已取消选择播放列表。"); return; }
                foreach (var item in dialog.QueueItems)
                {
                    item.OriginPageUrl = urlBox.Text.Trim();
                    AddQueueItem(item);
                }
                SetState(String.Format("已加入队列 · {0} 个视频", dialog.QueueItems.Count));
            }
        }

        private async Task DownloadQueue()
        {
            if (downloadRunning || !queueItems.Any(x => x.Status == "等待中" || x.Status == "已取消") || !ValidProxy()) return;
            foreach (var item in queueItems.Where(x => x.Status == "已取消"))
            {
                item.Status = "等待中"; item.PauseRequested = false; UpdateQueueRow(item);
            }
            downloadRunning = true; queueStopRequested = false; queueCancellation = new CancellationTokenSource();
            CancellationToken cancellationToken = queueCancellation.Token;
            analyzeButton.Enabled = false; progress.SetProgress(0); progress.SetIndeterminate(false); progress.Visible = true; logBox.Clear();
            UpdateQueueControls();
            AppendLog(String.Format("[设置] 忽略 yt-dlp.conf；主代理 {0}；地域验证代理 {1}；分片并发 {2}；同时下载 {3}",
                ProxySummary(ytdlpOptions.ProxyEnabled ? ytdlpOptions.Proxy : ""),
                ProxySummary(ytdlpOptions.GeoProxyEnabled ? ytdlpOptions.GeoVerificationProxy : ""),
                ytdlpOptions.ConcurrentFragments, Math.Max(1, Math.Min(6, ytdlpOptions.ConcurrentDownloads))));
            try
            {
                var pending = queueItems.Where(x => x.Status == "等待中").ToList();
                currentRunItems = pending;
                currentRunProgress.Clear();
                foreach (var item in pending) currentRunProgress[item] = 0;
                UpdateQueueProgressDisplay(null, 0);
                using (var slots = new System.Threading.SemaphoreSlim(Math.Max(1, Math.Min(6, ytdlpOptions.ConcurrentDownloads))))
                {
                    Func<DownloadQueueItem, Task> runItem = async item =>
                    {
                        bool acquired = false;
                        try
                        {
                            await slots.WaitAsync(cancellationToken);
                            acquired = true;
                            cancellationToken.ThrowIfCancellationRequested();
                            if (item.PauseRequested || item.Status == "已暂停") return;
                            item.Status = "下载中"; UpdateQueueRow(item); SetState(String.Format("正在下载 · {0}", item.Title));
                            AppendLog("[队列] " + item.Title);
                            try
                            {
                                Directory.CreateDirectory(item.OutputFolder);
                                using (var linkedToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                                {
                                itemCancellation[item] = linkedToken;
                                var itemProgress = new ItemDownloadProgress
                                {
                                    StageCount = ExpectedTransferStages(item)
                                };
                                Action<string> output = lineText =>
                                {
                                    if (!lineText.StartsWith("dotSharKFileJson=", StringComparison.Ordinal))
                                        AppendLog("[" + item.Title + "] " + lineText);
                                    ReportDownloadLine(item, itemProgress, lineText);
                                };
                                if (item.BrowserSilent)
                                    await client.DownloadSilentAsync(item.Url, item.OutputFolder,
                                        ytdlpOptions, output, linkedToken.Token);
                                else if (item.PlaylistItem)
                                    await client.DownloadPlaylistItemAsync(item, ytdlpOptions, output, linkedToken.Token);
                                else
                                    await client.DownloadAsync(item.Media, item.Video, item.Audio, item.AudioOnly,
                                        item.Container, item.OutputFolder, item.Subtitle, ytdlpOptions, output,
                                        linkedToken.Token, item.OriginPageUrl);
                                item.Status = "已完成";
                                }
                                SetItemProgress(item, 1, 100);
                            }
                            catch (OperationCanceledException)
                            {
                                item.Status = item.PauseRequested ? "已暂停" : "已取消";
                                AppendLog("[中断] " + item.Title + " · " + (item.PauseRequested ? "已暂停，可单项继续" : "已停止，可续传"));
                            }
                            catch (Exception ex) { item.Status = "失败"; AppendLog("[失败] " + item.Title + " · " + ex.Message); }
                            finally { itemCancellation.Remove(item); }
                            UpdateQueueRow(item);
                            if (item.Status == "已完成")
                            {
                                string actual = CompletedFileResolver.Resolve(item.FilePath, item.OutputFolder);
                                if (!String.IsNullOrWhiteSpace(actual)) item.FilePath = actual;
                                PersistCompletedQueue();
                            }
                            if (item.BrowserSilent && (item.Status == "已完成" || item.Status == "失败"))
                                SilentDownloadBridge.CompleteJob(item.BrowserJobPath);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            // Items that did not start remain queued for the next manual resume.
                        }
                        finally { if (acquired) slots.Release(); }
                    };
                    var scheduled = new HashSet<DownloadQueueItem>();
                    var running = new List<Task>();
                    while (true)
                    {
                        if (!cancellationToken.IsCancellationRequested && !queueStopRequested)
                        {
                            foreach (var added in queueItems.Where(x => x.Status == "等待中" && !scheduled.Contains(x)).ToList())
                            {
                                scheduled.Add(added);
                                if (!pending.Contains(added))
                                {
                                    pending.Add(added);
                                    currentRunProgress[added] = 0;
                                }
                                running.Add(runItem(added));
                            }
                        }
                        if (running.All(task => task.IsCompleted)) break;
                        // Keep admitting later arrivals while earlier transfers are in flight.
                        await Task.WhenAny(Task.WhenAll(running), Task.Delay(200));
                    }
                    await Task.WhenAll(running);
                }
                int failed = pending.Count(x => x.Status == "失败");
                int completed = pending.Count(x => x.Status == "已完成");
                int canceled = pending.Count(x => x.Status == "已取消");
                if (cancellationToken.IsCancellationRequested)
                    SetState(String.Format("队列已停止 · 完成 {0} · 已取消 {1} · 其余项目保留等待", completed, canceled));
                else
                    SetState(failed == 0 ? String.Format("队列下载完成 · {0} 个视频", completed) : String.Format("队列处理完成 · 成功 {0}，失败 {1}", completed, failed));
            }
            finally
            {
                bool continueNewBrowserJobs = browserAutoRunRequested && !queueStopRequested;
                browserAutoRunRequested = false;
                downloadRunning = false; queueStopRequested = false; progress.Visible = false; currentRunItems.Clear(); currentRunProgress.Clear(); analyzeButton.Enabled = true;
                if (queueCancellation != null) { queueCancellation.Dispose(); queueCancellation = null; }
                UpdateQueueControls();
                if (continueNewBrowserJobs && queueItems.Any(x => x.Status == "等待中"))
                    BeginInvoke((Action)(async () => await DownloadQueue()));
            }
        }

        private void StopQueue()
        {
            if (!downloadRunning || queueStopRequested) return;
            queueStopRequested = true;
            SetState("正在停止队列…");
            AppendLog("[队列] 收到停止请求，正在终止当前 yt-dlp 任务。");
            UpdateQueueControls();
            if (queueCancellation != null) queueCancellation.Cancel();
        }

        private void UpdateItemContextActions(DownloadQueueItem item, ToolStripMenuItem pause, ToolStripMenuItem resume,
            ToolStripMenuItem openFile, ToolStripMenuItem openFolder)
        {
            bool completed = item != null && item.Status == "已完成";
            openFile.Visible = openFolder.Visible = completed;
            openFile.Enabled = openFolder.Enabled = completed;
            pause.Enabled = item != null && (item.Status == "等待中" || item.Status == "下载中");
            resume.Enabled = item != null && (item.Status == "失败" || item.Status == "已暂停" ||
                item.Status == "已取消");
            resume.Text = item != null && (item.Status == "已暂停" || item.Status == "已取消")
                ? "继续此项" : "重试此项";
        }

        private void OpenCompletedItem(DownloadQueueItem item, bool folder)
        {
            if (item == null || item.Status != "已完成") return;
            string path = CompletedFileResolver.Resolve(item.FilePath, item.OutputFolder);
            if (!String.IsNullOrWhiteSpace(path) &&
                !String.Equals(path, item.FilePath, StringComparison.Ordinal))
            {
                item.FilePath = path;
                PersistCompletedQueue();
            }
            if (String.IsNullOrWhiteSpace(path))
            {
                // Never guess which file to launch for legacy completed entries.
                if (folder && Directory.Exists(item.OutputFolder))
                {
                    try { Process.Start(new ProcessStartInfo(item.OutputFolder) { UseShellExecute = true }); }
                    catch (Exception ex) { ShowFileOpenError(ex.Message); }
                    return;
                }
                ShowFileOpenError("未记录最终下载文件路径；旧任务可尝试“打开下载目录”。");
                return;
            }
            if (!File.Exists(path))
            {
                ShowFileOpenError("文件不存在或已被移动：" + Environment.NewLine + path);
                return;
            }
            try
            {
                if (folder)
                    Process.Start(new ProcessStartInfo("explorer.exe")
                    {
                        Arguments = "/select," + (char)34 + path + (char)34,
                        UseShellExecute = true
                    });
                else
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex) { ShowFileOpenError(ex.Message); }
        }

        private void ShowFileOpenError(string message)
        {
            DarkMessageBox.Show(this, message, "dotSharK", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void PauseQueueItem(DownloadQueueItem item)
        {
            if (item == null || !(item.Status == "等待中" || item.Status == "下载中")) return;
            item.PauseRequested = true;
            CancellationTokenSource source;
            if (itemCancellation.TryGetValue(item, out source))
            {
                item.Status = "暂停中";
                source.Cancel();
            }
            else item.Status = "已暂停";
            UpdateQueueRow(item);
            UpdateQueueControls();
            AppendLog("[单项暂停] " + item.Title);
        }

        private void ResumeQueueItem(DownloadQueueItem item)
        {
            if (item == null || !(item.Status == "失败" || item.Status == "已暂停" || item.Status == "已取消"))
                return;
            if (!ValidProxy()) return;
            item.PauseRequested = false;
            item.ProgressPercent = 0;
            item.Status = "等待中";
            UpdateQueueRow(item);
            UpdateQueueControls();
            AppendLog("[继续/重试] " + item.Title);
            if (downloadRunning)
            {
                // The running batch has an immutable snapshot. Run newly resumed
                // jobs in the next batch without restarting the active transfers.
                browserAutoRunRequested = true;
            }
            else { var run = DownloadQueue(); }
        }

        private async Task RetryAllFailed()
        {
            if (downloadRunning || !ValidProxy()) return;
            foreach (var item in queueItems.Where(x => x.Status == "失败")) { item.Status = "等待中"; UpdateQueueRow(item); }
            UpdateQueueControls();
            await DownloadQueue();
        }

        private string GetOutputFolder() { string folder = folderBox.Text.Trim(); return folder.Length == 0 ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads") : folder; }
        private static string SafeFolderName(string name)
        {
            if (String.IsNullOrWhiteSpace(name)) return "播放列表";
            var invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
            var chars = name.Trim().Select(c => invalid.Contains(c) || c == '/' || c == '\\' ? '_' : c).ToArray();
            string safe = new string(chars).Trim().TrimEnd('.');
            if (safe.Length > 100) safe = safe.Substring(0, 100).TrimEnd(' ', '.');
            return safe.Length == 0 ? "播放列表" : safe;
        }

        private sealed class ItemDownloadProgress
        {
            internal readonly object Sync = new object();
            internal int StageCount = 1;
            internal int StageIndex;
            internal double LastPercent;
            internal bool HasPercent;
            internal string AriaGid;
        }

        // The item owns this parser, not the global queue: yt-dlp and aria2c
        // may run concurrently, and even one item can report both formats.
        internal static int ExpectedTransferStages(DownloadQueueItem item)
        {
            if (item == null || item.AudioOnly) return 1;
            if (item.BrowserSilent || item.PlaylistItem) return 2;
            return item.Video != null && !item.Video.HasAudio ? 2 : 1;
        }

        internal static bool TryParseTransferProgress(string line, out double percent, out string ariaGid)
        {
            percent = 0;
            ariaGid = null;
            if (String.IsNullOrWhiteSpace(line)) return false;
            // Prefer yt-dlp's completed stream on a combined line such as:
            // [#fdb514 23MiB/31MiB(74%) ...][download] 100% of 31.88MiB ...
            var native = Regex.Match(line, @"\[download\]\s*(?<value>\d+(?:[.,]\d+)?)%\s+of\b",
                RegexOptions.IgnoreCase);
            // aria2c can report GID, transfer bytes, and progress on every CR.
            var aria = Regex.Match(line,
                @"\[#(?<gid>[0-9a-fA-F]{6,})\s+[^\]\r\n]*?\((?<value>\d+(?:[.,]\d+)?)%\)",
                RegexOptions.IgnoreCase);
            if (aria.Success) ariaGid = aria.Groups["gid"].Value;
            var selected = native.Success ? native : aria;
            if (!selected.Success) return false;
            return Double.TryParse(selected.Groups["value"].Value.Replace(',', '.'),
                NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out percent)
                && percent >= 0 && percent <= 100;
        }

        // yt-dlp output template %(field)j encodes Unicode as JSON escapes
        // before stdout text encoding. Never reconstruct paths from display titles.
        internal static string ParsePrintedMetadata(string lineText, string prefix)
        {
            if (String.IsNullOrWhiteSpace(lineText) ||
                !lineText.StartsWith(prefix, StringComparison.Ordinal)) return null;
            string payload = lineText.Substring(prefix.Length).Trim();
            if (payload.Length < 2 || payload[0] != '"') return null;
            try { return new System.Web.Script.Serialization.JavaScriptSerializer().Deserialize<string>(payload); }
            catch (ArgumentException) { return null; }
            catch (InvalidOperationException) { return null; }
        }

        private void ReportDownloadLine(DownloadQueueItem item, ItemDownloadProgress state, string lineText)
        {
            if (item == null || state == null || String.IsNullOrEmpty(lineText)) return;
            if (lineText.StartsWith("dotSharKFileJson=", StringComparison.Ordinal))
            {
                string filename = ParsePrintedMetadata(lineText, "dotSharKFileJson=");
                if (!String.IsNullOrWhiteSpace(filename))
                {
                    try
                    {
                        if (Path.IsPathRooted(filename) && filename.IndexOfAny(Path.GetInvalidPathChars()) < 0)
                        {
                            item.FilePath = filename;
                            if (item.PlaylistItem && (String.IsNullOrWhiteSpace(item.Title) ||
                                item.Title == "未命名视频" || item.Title.EndsWith("（未取得标题）", StringComparison.Ordinal)))
                                item.Title = Path.GetFileNameWithoutExtension(filename);
                        }
                    }
                    catch (ArgumentException) { }
                }
                return;
            }
            if ((item.BrowserSilent || item.PlaylistItem) && lineText.StartsWith("dotSharKTitleJson=", StringComparison.Ordinal))
            {
                string title = ParsePrintedMetadata(lineText, "dotSharKTitleJson=");
                if (!String.IsNullOrWhiteSpace(title) && IsHandleCreated && !IsDisposed)
                {
                    item.Title = title;
                    try
                    {
                        BeginInvoke((Action)(() =>
                        {
                            if (IsDisposed || !queueItems.Contains(item)) return;
                            UpdateQueueRow(item);
                        }));
                    }
                    catch (InvalidOperationException) { }
                }
                return;
            }

            double percent;
            string ariaGid;
            if (!TryParseTransferProgress(lineText, out percent, out ariaGid))
            {
                var count = Regex.Match(lineText,
                    @"Downloading\s+(\d+)\s+format\(s\):",
                    RegexOptions.IgnoreCase);
                if (count.Success)
                {
                    int stages;
                    if (Int32.TryParse(count.Groups[1].Value, out stages))
                        lock (state.Sync) state.StageCount = Math.Max(1, Math.Min(32, stages));
                }
                return;
            }

            double fraction;
            lock (state.Sync)
            {
                if (!String.IsNullOrWhiteSpace(ariaGid))
                {
                    if (!String.IsNullOrWhiteSpace(state.AriaGid) &&
                        !String.Equals(ariaGid, state.AriaGid, StringComparison.OrdinalIgnoreCase) &&
                        state.StageIndex < state.StageCount - 1)
                    {
                        // New aria2c GID means yt-dlp started the next format
                        // (typically video, then audio), even if its preceding
                        // "Downloading 2 formats" message was suppressed.
                        state.StageIndex++;
                        state.HasPercent = false;
                    }
                    state.AriaGid = ariaGid;
                }
                else if (state.HasPercent && percent + 5 < state.LastPercent &&
                         state.LastPercent >= 70 && state.StageIndex < state.StageCount - 1)
                {
                    // yt-dlp's native downloader uses the same format progress
                    // syntax for consecutive video and audio, but no aria2c GID.
                    state.StageIndex++;
                    state.HasPercent = false;
                }

                state.LastPercent = percent;
                state.HasPercent = true;
                // Never report 100% before yt-dlp exits successfully, because
                // postprocessing and muxing may still be running.
                fraction = Math.Min(0.995,
                    (state.StageIndex + percent / 100.0) / Math.Max(1, state.StageCount));
            }
            SetItemProgress(item, fraction, fraction * 100);
        }

        private void SetItemProgress(DownloadQueueItem item, double fraction, double itemPercent)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke((Action)(() =>
                {
                    if (IsDisposed || !currentRunProgress.ContainsKey(item)) return;
                    double highWater = Math.Max(currentRunProgress[item], Math.Max(0, Math.Min(1, fraction)));
                    currentRunProgress[item] = highWater;
                    item.ProgressPercent = highWater * 100;
                    UpdateQueueRow(item);
                    UpdateQueueProgressDisplay(item, item.ProgressPercent);
                }));
            }
            catch (InvalidOperationException) { }
        }

        private void UpdateQueueProgressDisplay(DownloadQueueItem current, double itemPercent)
        {
            double totalWeight = currentRunItems.Sum(x => Math.Max(1, x.ProgressWeight));
            double completedWeight = currentRunItems.Sum(x => Math.Max(1, x.ProgressWeight) * (currentRunProgress.ContainsKey(x) ? currentRunProgress[x] : 0));
            double queueFraction = totalWeight <= 0 ? 0 : completedWeight / totalWeight;
            progress.SetProgress(queueFraction);
            if (current != null)
                SetState(String.Format("正在下载 · 视频 {0:0.#}% · 队列 {1:0.#}% · {2}", itemPercent, queueFraction * 100, current.Title));
        }
        private static string GetSingleVideoTarget(string url, PlaylistInfo playlist)
        {
            Uri uri;
            if (Uri.TryCreate(url, UriKind.Absolute, out uri))
            {
                if (uri.Host.Equals("youtu.be", StringComparison.OrdinalIgnoreCase)) return url;
                string query = uri.Query.TrimStart('?');
                if (query.Split('&').Any(part => part.Split('=')[0].Equals("v", StringComparison.OrdinalIgnoreCase))) return url;
            }
            return playlist.Entries.Count == 0 ? url : playlist.Entries[0].Url;
        }
        private void AddQueueItem(DownloadQueueItem item)
        {
            queueItems.Add(item);
            string quality = item.BrowserSilent ? (ytdlpOptions.SilentMaxHeight == 0 ? "最高画质 · AV1 优先" :
                ytdlpOptions.SilentMaxHeight + "p 以内 · AV1 优先")
                : item.AudioOnly ? "仅音频" : item.PlaylistItem
                ? (item.TargetHeight > 0 ? item.TargetHeight + "p " + item.TargetCodec : "最高可用")
                : (item.Video == null ? "自动" : item.Video.Label);
            string source = item.BrowserSilent ? "浏览器" : item.PlaylistItem ? "播放列表" : "单视频";
            if (!String.IsNullOrWhiteSpace(item.DisplaySource)) source = item.DisplaySource;
            else item.DisplaySource = source;
            if (!String.IsNullOrWhiteSpace(item.DisplayQuality)) quality = item.DisplayQuality;
            else item.DisplayQuality = quality;
            int row = queueGrid.Rows.Add(item.Title, source, quality, item.Status);
            EnsureTextColumnWidth("source", source, Theme.SourceColumnWidth);
            EnsureQualityWidth(quality);
            EnsureStatusWidth(item.Status);
            queueGrid.Rows[row].Tag = item; queueCountLabel.Text = "下载队列（" + queueItems.Count + "）"; UpdateQueueControls();
        }
        private void UpdateQueueRow(DownloadQueueItem item)
        {
            foreach (DataGridViewRow row in queueGrid.Rows)
                if (ReferenceEquals(row.Tag, item))
                {
                    row.Cells[0].Value = item.Title;
                    string status = item.Status == "下载中" && item.ProgressPercent > 0
                        ? "下载中 " + item.ProgressPercent.ToString("0.#", CultureInfo.InvariantCulture) + "%"
                        : item.Status;
                    row.Cells[3].Value = status;
                    EnsureStatusWidth(status);
                    break;
                }
        }
        private void EnsureQueueColumnWidths()
        {
            if (queueGrid == null || !queueGrid.Columns.Contains("status")) return;
            EnsureTextColumnWidth("source", "播放列表", Theme.SourceColumnWidth);
            EnsureQualityWidth("1080p 以内 · AV1 优先");
            EnsureStatusWidth("下载中 100%");
            foreach (DataGridViewRow row in queueGrid.Rows)
            {
                EnsureTextColumnWidth("source", Convert.ToString(row.Cells["source"].Value), Theme.SourceColumnWidth);
                EnsureQualityWidth(Convert.ToString(row.Cells["quality"].Value));
                EnsureStatusWidth(Convert.ToString(row.Cells["status"].Value));
            }
        }
        private void EnsureTextColumnWidth(string name, string text, int minimum)
        {
            // Use the same GDI flags and 12px left/right insets as DarkPaint.DrawText.
            int required = TextRenderer.MeasureText(text ?? "", queueGrid.Font, Size.Empty,
                TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width + Theme.Space24 + Theme.Space8;
            var column = queueGrid.Columns[name];
            int width = Math.Max(minimum, required);
            if (column.MinimumWidth < width) column.MinimumWidth = width;
            if (column.Width < width) column.Width = width;
        }
        private void EnsureQualityWidth(string quality)
        {
            EnsureTextColumnWidth("quality", quality, Theme.QualityColumnWidth + Theme.Space24 + Theme.Space8);
        }
        private void EnsureStatusWidth(string status)
        {
            EnsureTextColumnWidth("status", status, Theme.StatusColumnWidth);
        }

        private void UpdateQueueControls()
        {
            if (queueCountLabel != null) queueCountLabel.Text = "下载队列（" + queueItems.Count + "）";
            if (downloadButton != null)
            {
                downloadButton.Text = downloadRunning ? (queueStopRequested ? "正在停止…" : "停止队列") : "开始队列";
                downloadButton.Enabled = downloadRunning ? !queueStopRequested : queueItems.Any(x => x.Status == "等待中" || x.Status == "已取消");
            }
            if (removeQueueButton != null) removeQueueButton.Enabled = !downloadRunning && queueGrid.SelectedRows.Count > 0;
            if (clearFinishedButton != null) clearFinishedButton.Enabled = !downloadRunning && queueItems.Any(x => x.Status == "已完成" || x.Status == "失败");
            if (retryFailedButton != null) retryFailedButton.Enabled = !downloadRunning && queueItems.Any(x => x.Status == "失败");
        }
        private void RemoveSelectedQueueItems()
        {
            foreach (DataGridViewRow row in queueGrid.SelectedRows.Cast<DataGridViewRow>().ToList())
            {
                var item = row.Tag as DownloadQueueItem;
                if (item != null && item.Status != "下载中")
                {
                    queueItems.Remove(item);
                    if (item.BrowserSilent) SilentDownloadBridge.CompleteJob(item.BrowserJobPath);
                }
                if (!row.IsNewRow) queueGrid.Rows.Remove(row);
            }
            UpdateQueueControls();
            PersistCompletedQueue();
        }
        private void ClearFinishedQueueItems()
        {
            foreach (DataGridViewRow row in queueGrid.Rows.Cast<DataGridViewRow>().ToList())
            {
                var item = row.Tag as DownloadQueueItem;
                if (item != null && (item.Status == "已完成" || item.Status == "失败"))
                {
                    queueItems.Remove(item);
                    if (item.BrowserSilent) SilentDownloadBridge.CompleteJob(item.BrowserJobPath);
                    queueGrid.Rows.Remove(row);
                }
            }
            UpdateQueueControls();
            PersistCompletedQueue();
        }
        private void AppendLog(string text)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke((Action)(() => { if (IsDisposed) return; logBox.AppendText(text + Environment.NewLine); logBox.SelectionStart = logBox.TextLength; logBox.ScrollToCaret(); })); }
            catch (InvalidOperationException) { }
        }

        private bool ValidProxy()
        {
            if (ytdlpOptions.ProxyEnabled && !ValidProxyUrl(ytdlpOptions.Proxy))
            { SetState("主代理格式不正确。请在“配置 → YT-DLP”中检查代理地址。"); return false; }
            if (ytdlpOptions.GeoProxyEnabled && !ValidProxyUrl(ytdlpOptions.GeoVerificationProxy))
            { SetState("地域验证代理格式不正确。请在“配置 → YT-DLP”中检查代理地址。"); return false; }
            return true;
        }
        private static bool ValidProxyUrl(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return true;
            Uri uri;
            return Uri.TryCreate(value.Trim(), UriKind.Absolute, out uri) &&
                (uri.Scheme == "http" || uri.Scheme == "https" || uri.Scheme == "socks4" || uri.Scheme == "socks4a" || uri.Scheme == "socks5" || uri.Scheme == "socks5h");
        }
        private static string ProxySummary(string value)
        {
            if (String.IsNullOrWhiteSpace(value)) return "未设置";
            Uri uri;
            return Uri.TryCreate(value.Trim(), UriKind.Absolute, out uri) ? uri.Scheme + " 已设置" : "地址无效";
        }
        private void Busy(bool busy, string message)
        {
            analyzeButton.Enabled = !busy;
            if (busy) progress.SetIndeterminate(true);
            progress.Visible = busy;
            if (!busy) UpdateQueueControls();
            Application.UseWaitCursor = busy;
            ApplyWaitCursor(this, busy);
            Cursor.Current = busy ? Cursors.WaitCursor : Cursors.Default;
            if (!String.IsNullOrEmpty(message)) SetState(message);
        }
        private static void ApplyWaitCursor(Control parent, bool busy)
        {
            parent.UseWaitCursor = busy;
            parent.Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
            foreach (Control child in parent.Controls) ApplyWaitCursor(child, busy);
        }
        private void SetState(string message) { if (stateLabel != null) stateLabel.Text = message; }

        private void BrowseFolder(object sender, EventArgs e)
        {
            using (var d = new FolderBrowserDialog { Description = "选择视频保存目录", SelectedPath = Directory.Exists(folderBox.Text) ? folderBox.Text : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads"), ShowNewFolderButton = true })
                if (d.ShowDialog(this) == DialogResult.OK) folderBox.Text = d.SelectedPath;
        }
        private void OpenConfiguration(object sender, EventArgs e)
        {
            using (var d = new ConfigurationDialogV2(ytDlpPath, ffmpegPath, ffprobePath, denoPath, aria2cPath, ytdlpOptions))
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    ytDlpPath = ExistingOrDefault(d.YtDlpPath, "yt-dlp.exe");
                    ffmpegPath = ExistingOrDefault(d.FfmpegPath, "ffmpeg.exe");
                    ffprobePath = ExistingOrDefault(d.FfprobePath, "ffprobe.exe");
                    denoPath = ExistingOrDefault(d.DenoPath, "deno.exe");
                    aria2cPath = ExistingOrDefault(d.Aria2cPath, "aria2c.exe");
                    ytdlpOptions = d.Options;
                    ytdlpOptions.Aria2cPath = aria2cPath;
                    RefreshClient(); SaveSettings(); PersistCompletedQueue();
                    SetState(client == null ? "尚未指定 yt-dlp.exe。" : "配置已保存 · " + Path.GetFileName(ytDlpPath));
                }
        }
        private string ExistingOrDefault(string configured, string fileName)
        {
            if (!String.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
            string local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", fileName);
            if (File.Exists(local)) return local;
            local = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName);
            return File.Exists(local) ? local : "";
        }
        private void RefreshClient() { client = String.IsNullOrWhiteSpace(ytDlpPath) ? null : new YtDlpClient(ytDlpPath, ffmpegPath, ffprobePath, denoPath, aria2cPath); }
        private void LoadSettings()
        {
            loadingSettings = true;
            try
            {
                if (File.Exists(settingsPath))
                    foreach (string l in File.ReadAllLines(settingsPath))
                    {
                        int i = l.IndexOf('='); if (i < 0) continue;
                        string key = l.Substring(0, i), value = Uri.UnescapeDataString(l.Substring(i + 1));
                        if (key == "proxy")
                        {
                            try { ytdlpOptions.Proxy = System.Text.Encoding.UTF8.GetString(System.Security.Cryptography.ProtectedData.Unprotect(Convert.FromBase64String(value), null, System.Security.Cryptography.DataProtectionScope.CurrentUser)); }
                            catch { ytdlpOptions.Proxy = ""; }
                        }
                        else if (key == "geo-proxy")
                        {
                            try { ytdlpOptions.GeoVerificationProxy = System.Text.Encoding.UTF8.GetString(System.Security.Cryptography.ProtectedData.Unprotect(Convert.FromBase64String(value), null, System.Security.Cryptography.DataProtectionScope.CurrentUser)); }
                            catch { ytdlpOptions.GeoVerificationProxy = ""; }
                        }
                        else if (key == "proxy-portable") ytdlpOptions.Proxy = value;
                        else if (key == "geo-proxy-portable") ytdlpOptions.GeoVerificationProxy = value;
                        else if (key == "folder") folderBox.Text = value;
                        else if (key == "yt-dlp") ytDlpPath = value;
                        else if (key == "ffmpeg") ffmpegPath = value;
                        else if (key == "ffprobe") ffprobePath = value;
                        else if (key == "deno") denoPath = value;
                        else if (key == "aria2c") aria2cPath = value;
                        else if (key == "aria2c-sites") ytdlpOptions.Aria2cSites = value;
                        else if (key == "no-proxy-sites") ytdlpOptions.NoProxySites = value;
                        else if (key == "custom-args") ytdlpOptions.CustomArguments = value;
                        else if (key == "keep-completed") ytdlpOptions.KeepCompletedTasks = value == "1";
                        else if (key == "silent-max-height") { int h; if (Int32.TryParse(value, out h) && (h == 0 || h == 4320 || h == 2160 || h == 1080 || h == 720)) ytdlpOptions.SilentMaxHeight = h; }
                        else if (key == "proxy-enabled") ytdlpOptions.ProxyEnabled = value != "0";
                        else if (key == "geo-proxy-enabled") ytdlpOptions.GeoProxyEnabled = value != "0";
                        else if (key == "fragments") { int n; if (Int32.TryParse(value, out n)) ytdlpOptions.ConcurrentFragments = Math.Max(1, Math.Min(32, n)); }
                        else if (key == "downloads") { int n; if (Int32.TryParse(value, out n)) ytdlpOptions.ConcurrentDownloads = Math.Max(1, Math.Min(6, n)); }
                        else if (key == "video-quality-limit") { int n; if (Int32.TryParse(value, out n)) ytdlpOptions.VideoQualityLimit = Math.Max(0, Math.Min(9999, n)); }
                        else if (key == "silent-folder" && !String.IsNullOrWhiteSpace(value)) ytdlpOptions.SilentDownloadFolder = value;
                        else if (key == "main-window") savedMainWindow = WindowPlacement.Parse(value);
                        else if (key == "playlist-window") savedPlaylistWindow = WindowPlacement.Parse(value);
                        else if (key == "cookie-mode") ytdlpOptions.CookieMode = value == "file" ? "file" : "browser";
                        else if (key == "cookie-browser") ytdlpOptions.CookieBrowser = value;
                        else if (key == "cookie-root") ytdlpOptions.CookieProfileRoot = value;
                        else if (key == "cookie-profile") ytdlpOptions.CookieProfile = value;
                        else if (key == "cookie-file") ytdlpOptions.CookieFile = value;
                    }
            }
            catch { }
            if (String.IsNullOrWhiteSpace(folderBox.Text)) folderBox.Text = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads");
            loadingSettings = false;
        }
        private void SaveSettings()
        {
            if (loadingSettings) return;
            try
            {
                savedMainWindow = WindowPlacement.Capture(this, lastMainMaximized);
                File.WriteAllLines(settingsPath, new[] { "proxy-portable=" + Uri.EscapeDataString(ytdlpOptions.Proxy ?? ""),
                    "geo-proxy-portable=" + Uri.EscapeDataString(ytdlpOptions.GeoVerificationProxy ?? ""),
                    "proxy-enabled=" + (ytdlpOptions.ProxyEnabled ? "1" : "0"),
                    "geo-proxy-enabled=" + (ytdlpOptions.GeoProxyEnabled ? "1" : "0"),
                    "folder=" + Uri.EscapeDataString(folderBox == null ? "" : folderBox.Text),
                    "yt-dlp=" + Uri.EscapeDataString(ytDlpPath), "ffmpeg=" + Uri.EscapeDataString(ffmpegPath),
                    "ffprobe=" + Uri.EscapeDataString(ffprobePath), "deno=" + Uri.EscapeDataString(denoPath),
                    "aria2c=" + Uri.EscapeDataString(aria2cPath),
                    "aria2c-sites=" + Uri.EscapeDataString(ytdlpOptions.Aria2cSites ?? ""),
                    "no-proxy-sites=" + Uri.EscapeDataString(ytdlpOptions.NoProxySites ?? ""),
                    "custom-args=" + Uri.EscapeDataString(ytdlpOptions.CustomArguments ?? ""),
                    "keep-completed=" + (ytdlpOptions.KeepCompletedTasks ? "1" : "0"),
                    "silent-max-height=" + ytdlpOptions.SilentMaxHeight,
                    "fragments=" + ytdlpOptions.ConcurrentFragments, "downloads=" + Math.Max(1, Math.Min(6, ytdlpOptions.ConcurrentDownloads)),
                    "video-quality-limit=" + ytdlpOptions.VideoQualityLimit,
                    "silent-folder=" + Uri.EscapeDataString(ytdlpOptions.SilentDownloadFolder ?? ""),
                    "main-window=" + savedMainWindow.Serialize(),
                    "playlist-window=" + (savedPlaylistWindow == null ? "" : savedPlaylistWindow.Serialize()),
                    "cookie-mode=" + ytdlpOptions.CookieMode,
                    "cookie-browser=" + Uri.EscapeDataString(ytdlpOptions.CookieBrowser ?? "firefox"),
                    "cookie-root=" + Uri.EscapeDataString(ytdlpOptions.CookieProfileRoot ?? ""),
                    "cookie-profile=" + Uri.EscapeDataString(ytdlpOptions.CookieProfile ?? ""),
                    "cookie-file=" + Uri.EscapeDataString(ytdlpOptions.CookieFile ?? "") });
            }
            catch { }
        }
        private void LoadCompletedQueue()
        {
            if (!ytdlpOptions.KeepCompletedTasks) return;
            try
            {
                foreach (DownloadQueueItem item in CompletedQueueStore.Load(completedPath)) AddQueueItem(item);
            }
            catch (Exception ex) { AppendLog("[记录] 无法恢复已完成任务：" + ex.Message); }
        }

        private void PersistCompletedQueue()
        {
            try { CompletedQueueStore.Save(completedPath,
                ytdlpOptions.KeepCompletedTasks ? queueItems.Where(x => x.Status == "已完成") :
                    Enumerable.Empty<DownloadQueueItem>()); }
            catch (Exception ex) { AppendLog("[记录] 保存完成任务失败：" + ex.Message); }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveSettings(); PersistCompletedQueue(); base.OnFormClosing(e);
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            clipboardWatchTimer.Stop();
            clipboardWatchTimer.Dispose();
            base.OnFormClosed(e);
        }
    }

}
