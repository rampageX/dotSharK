using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Runtime.InteropServices;

namespace dotSharK
{
    internal sealed class MainForm : Form
    {
        private readonly Color bg = Color.FromArgb(34, 34, 34), panel = Color.FromArgb(43, 43, 43), green = DarkPalette.Accent, muted = Color.FromArgb(153, 163, 170), fg = Color.FromArgb(235, 238, 240), line = Color.FromArgb(65, 65, 65);
        private DarkTextBox urlBox, folderBox, logBox;
        private DarkButton analyzeButton;
        private Button downloadButton, configButton, browseFolderButton;
        private Button removeQueueButton, clearFinishedButton, retryFailedButton;
        private DataGridView queueGrid;
        private Label stateLabel, queueCountLabel;
        private DarkProgressBar progress;
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
                String.Equals(x.Url, url, StringComparison.OrdinalIgnoreCase)))
            {
                SilentDownloadBridge.CompleteJob(jobPath);
                return;
            }

            AddQueueItem(new DownloadQueueItem
            {
                Title = url, Url = url, BrowserSilent = true,
                BrowserJobPath = jobPath,
                OutputFolder = SilentDownloadBridge.SilentFolder,
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

        public MainForm()
        {
            Text = "dotSharK  ·  YouTube Downloader"; StartPosition = FormStartPosition.CenterScreen; MinimumSize = new Size(900, 680); Size = new Size(1120, 820);
            BackColor = bg; ForeColor = fg; Font = new Font("Microsoft YaHei UI", 9F); FormBorderStyle = FormBorderStyle.Sizable; KeyPreview = true; Icon = AppBranding.CreateIcon();
            BuildUi(); LoadSettings();
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
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(22, 16, 22, 11), BackColor = bg };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 151)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            Controls.Add(root);

            var brand = new Panel { Dock = DockStyle.Fill };
            var logo = new Label { Text = "dotSharK", Font = new Font("Microsoft YaHei UI", 19F, FontStyle.Bold), ForeColor = green, AutoSize = true, Location = new Point(0, 0) };
            var tagline = new Label { Text = "做最快的 yt-dlp 桌面外壳", ForeColor = muted, AutoSize = true, Location = new Point(0, 13) };
            logo.SizeChanged += (s, e) => tagline.Left = logo.Right + 10;
            var configHost = new Panel { Dock = DockStyle.Right, Width = 110, Padding = new Padding(0, 9, 0, 9) };
            configButton = GhostButton("配置…", 110); configButton.Dock = DockStyle.Fill; configButton.Click += OpenConfiguration;
            configHost.Controls.Add(configButton);
            brand.Controls.Add(logo); brand.Controls.Add(tagline); tagline.Left = logo.Right + 10; brand.Controls.Add(configHost); root.Controls.Add(brand, 0, 0);

            var entry = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = bg, Padding = new Padding(0, 9, 0, 9) };
            entry.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); entry.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            entry.RowStyles.Add(new RowStyle(SizeType.Absolute, DarkPalette.ControlHeight));
            urlBox = InputBox(); urlBox.Dock = DockStyle.Fill; urlBox.Margin = Padding.Empty; urlBox.Text = "";
            // Stop the prompt immediately when the user pastes the clipboard URL manually.
            urlBox.TextChanged += (s, e) =>
            {
                if (AddressContainsClipboardLink(clipboardCandidateUrl, urlBox.Text))
                    ClearClipboardAttention();
            };
            analyzeButton = AccentButton("分析链接"); analyzeButton.Dock = DockStyle.Fill; analyzeButton.Click += async (s, e) => await AnalyzeFromButton();
            entry.Controls.Add(urlBox, 0, 0); entry.Controls.Add(analyzeButton, 1, 0);
            root.Controls.Add(entry, 0, 1);

            var queueArea = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = bg, Padding = new Padding(0, 8, 0, 1), Margin = Padding.Empty };
            queueArea.RowStyles.Add(new RowStyle(SizeType.Absolute, 36)); queueArea.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var queueToolbar = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, BackColor = bg };
            queueToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); queueToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 128)); queueToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104)); queueToolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112));
            queueCountLabel = new Label { Text = "下载队列（0）", Dock = DockStyle.Fill, ForeColor = fg, Font = new Font(Font, FontStyle.Bold), TextAlign = ContentAlignment.MiddleLeft };
            retryFailedButton = GhostButton("重试所有失败", 128); retryFailedButton.Dock = DockStyle.Fill; retryFailedButton.Click += async (s, e) => await RetryAllFailed();
            removeQueueButton = GhostButton("移除选中", 104); removeQueueButton.Dock = DockStyle.Fill; removeQueueButton.Click += (s, e) => RemoveSelectedQueueItems();
            clearFinishedButton = GhostButton("清理已完成", 112); clearFinishedButton.Dock = DockStyle.Fill; clearFinishedButton.Click += (s, e) => ClearFinishedQueueItems();
            queueToolbar.Controls.Add(queueCountLabel, 0, 0); queueToolbar.Controls.Add(retryFailedButton, 1, 0); queueToolbar.Controls.Add(removeQueueButton, 2, 0); queueToolbar.Controls.Add(clearFinishedButton, 3, 0);
            queueArea.Controls.Add(queueToolbar, 0, 0);
            queueGrid = new DataGridView { Dock = DockStyle.Fill, BackgroundColor = panel, BorderStyle = BorderStyle.None, GridColor = line, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, ReadOnly = true, MultiSelect = true, SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, EnableHeadersVisualStyles = false, ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, ColumnHeadersHeight = 34, RowTemplate = { Height = 36 }, Margin = Padding.Empty };
            queueGrid.AdvancedColumnHeadersBorderStyle.All = DataGridViewAdvancedCellBorderStyle.None;
            queueGrid.ColumnHeadersDefaultCellStyle.BackColor = panel; queueGrid.ColumnHeadersDefaultCellStyle.ForeColor = muted; queueGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = panel;
            queueGrid.DefaultCellStyle.BackColor = panel; queueGrid.DefaultCellStyle.ForeColor = fg; queueGrid.DefaultCellStyle.SelectionBackColor = DarkPalette.Selection; queueGrid.DefaultCellStyle.SelectionForeColor = fg; queueGrid.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
            queueGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "title", HeaderText = "视频", FillWeight = 62 });
            queueGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "source", HeaderText = "来源", FillWeight = 13 });
            queueGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "quality", HeaderText = "质量方案", FillWeight = 16 });
            queueGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "status", HeaderText = "状态", FillWeight = 9 });
            queueGrid.SelectionChanged += (s, e) => UpdateQueueControls();
            var queueMenu = new ContextMenuStrip { BackColor = DarkPalette.Surface, ForeColor = fg, ShowImageMargin = false, Renderer = new DarkContextMenuRenderer() };
            var pauseOneMenuItem = new ToolStripMenuItem("暂停此项");
            var retryOneMenuItem = new ToolStripMenuItem("继续/重试此项");
            queueMenu.Items.Add(pauseOneMenuItem);
            queueMenu.Items.Add(retryOneMenuItem);
            DownloadQueueItem contextItem = null;
            queueGrid.CellMouseDown += (s, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                contextItem = null;
                if (e.RowIndex >= 0)
                {
                    queueGrid.ClearSelection(); queueGrid.Rows[e.RowIndex].Selected = true;
                    contextItem = queueGrid.Rows[e.RowIndex].Tag as DownloadQueueItem;
                }
                UpdateItemContextActions(contextItem, pauseOneMenuItem, retryOneMenuItem);
            };
            queueMenu.Opening += (s, e) =>
            {
                if (contextItem == null || !queueItems.Contains(contextItem))
                    contextItem = queueGrid.SelectedRows.Count > 0
                        ? queueGrid.SelectedRows[0].Tag as DownloadQueueItem : null;
                UpdateItemContextActions(contextItem, pauseOneMenuItem, retryOneMenuItem);
            };
            pauseOneMenuItem.Click += (s, e) => PauseQueueItem(contextItem);
            retryOneMenuItem.Click += (s, e) => ResumeQueueItem(contextItem);
            queueGrid.ContextMenuStrip = queueMenu;
            queueArea.Controls.Add(queueGrid, 0, 1); root.Controls.Add(queueArea, 0, 2);
            NativeDarkTheme.ApplyOnHandleCreated(queueGrid, "DarkMode_Explorer"); NativeDarkTheme.ApplyScrollBarsOnHandleCreated(queueGrid, "DarkMode_Explorer");

            var lower = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = bg, Padding = Padding.Empty, Margin = Padding.Empty };
            lower.RowStyles.Add(new RowStyle(SizeType.Absolute, 43)); lower.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var pathRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = bg, Padding = new Padding(0, 9, 0, 10), Margin = Padding.Empty };
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 66)); pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            pathRow.Controls.Add(new Label { Text = "保存到", ForeColor = muted, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            folderBox = InputBox(); folderBox.Dock = DockStyle.Fill; folderBox.Margin = Padding.Empty; folderBox.TextChanged += (s, e) => SaveSettings(); pathRow.Controls.Add(folderBox, 1, 0);
            browseFolderButton = GhostButton("浏览…", 90); browseFolderButton.Dock = DockStyle.Fill; browseFolderButton.Margin = Padding.Empty; browseFolderButton.Click += BrowseFolder; pathRow.Controls.Add(browseFolderButton, 2, 0);
            lower.Controls.Add(pathRow, 0, 0);
            var logFrame = new Panel { Dock = DockStyle.Fill, BackColor = DarkPalette.Border, Padding = new Padding(1), Margin = Padding.Empty };
            logBox = InputBox(); logBox.Dock = DockStyle.Fill; logBox.Multiline = true; logBox.BorderStyle = BorderStyle.None; logBox.ScrollBars = ScrollBars.Vertical; logBox.ReadOnly = true; logBox.Font = new Font("Consolas", 8.5F); logBox.ForeColor = muted;
            NativeDarkTheme.ApplyOnHandleCreated(logBox, "DarkMode_Explorer"); NativeDarkTheme.ApplyScrollBarsOnHandleCreated(logBox, "DarkMode_Explorer"); logFrame.Controls.Add(logBox);
            lower.Controls.Add(logFrame, 0, 1); root.Controls.Add(lower, 0, 3);

            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = bg, Padding = Padding.Empty, Margin = Padding.Empty };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 176)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 136));
            stateLabel = new Label { Text = "就绪", Dock = DockStyle.Fill, ForeColor = muted, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            progress = new DarkProgressBar { Height = 8, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom, Visible = false, Margin = new Padding(4, 0, 16, 8) };
            stateLabel.Height = DarkPalette.ControlHeight; stateLabel.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom; stateLabel.Margin = Padding.Empty;
            downloadButton = AccentButton("开始队列"); downloadButton.Height = DarkPalette.ControlHeight; downloadButton.Dock = DockStyle.None; downloadButton.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom; downloadButton.Margin = Padding.Empty; downloadButton.Enabled = false;
            downloadButton.Click += async (s, e) => { if (downloadRunning) StopQueue(); else await DownloadQueue(); };
            footer.Controls.Add(stateLabel, 0, 0); footer.Controls.Add(progress, 1, 0); footer.Controls.Add(downloadButton, 2, 0); root.Controls.Add(footer, 0, 4);
            AcceptButton = analyzeButton;
        }
        private DarkTextBox InputBox() { return new DarkTextBox { Font = new Font(Font.FontFamily, 9F), AutoSize = false, Height = DarkPalette.ControlHeight, Margin = Padding.Empty }; }
        private DarkButton AccentButton(string text) { return new DarkButton { Text = text, Primary = true, Font = new Font(Font.FontFamily, 9F, FontStyle.Bold), Height = DarkPalette.ControlHeight, MinimumSize = new Size(0, DarkPalette.ControlHeight) }; }
        private DarkButton GhostButton(string text, int width) { return new DarkButton { Text = text, Width = width, Height = DarkPalette.ControlHeight, MinimumSize = new Size(0, DarkPalette.ControlHeight), Font = Font }; }

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
            using (var dialog = new SingleVideoDialog(media))
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
            using (var dialog = new PlaylistSelectionDialog(playlist, sample, playlistFolder))
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
                    await Task.WhenAll(pending.Select(async item =>
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
                                var itemProgress = new ItemDownloadProgress();
                                Action<string> output = lineText =>
                                {
                                    AppendLog("[" + item.Title + "] " + lineText);
                                    ReportDownloadLine(item, itemProgress, lineText);
                                };
                                if (item.BrowserSilent)
                                    await client.DownloadSilentAsync(item.Url, item.OutputFolder,
                                        SilentDownloadBridge.ArchivePath, ytdlpOptions, output, linkedToken.Token);
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
                            if (item.BrowserSilent && (item.Status == "已完成" || item.Status == "失败"))
                                SilentDownloadBridge.CompleteJob(item.BrowserJobPath);
                        }
                        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                        {
                            // Items that did not start remain queued for the next manual resume.
                        }
                        finally { if (acquired) slots.Release(); }
                    }));
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

        private void UpdateItemContextActions(DownloadQueueItem item, ToolStripMenuItem pause, ToolStripMenuItem resume)
        {
            pause.Enabled = item != null && (item.Status == "等待中" || item.Status == "下载中");
            resume.Enabled = item != null && (item.Status == "失败" || item.Status == "已暂停" ||
                item.Status == "已取消");
            resume.Text = item != null && (item.Status == "已暂停" || item.Status == "已取消")
                ? "继续此项" : "重试此项";
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
            internal int StageCount = 1;
            internal int StageIndex;
            internal double LastPercent;
            internal bool HasPercent;
        }

        private void ReportDownloadLine(DownloadQueueItem item, ItemDownloadProgress state, string lineText)
        {
            if (item == null || state == null || String.IsNullOrEmpty(lineText)) return;
            if (item.BrowserSilent && lineText.StartsWith("dotSharKTitle=", StringComparison.Ordinal))
            {
                string title = lineText.Substring("dotSharKTitle=".Length).Trim();
                if (title.Length > 0 && IsHandleCreated && !IsDisposed)
                {
                    try
                    {
                        BeginInvoke((Action)(() =>
                        {
                            if (IsDisposed || !queueItems.Contains(item)) return;
                            item.Title = title;
                            UpdateQueueRow(item);
                        }));
                    }
                    catch (InvalidOperationException) { }
                }
                return;
            }
            var formatMatch = Regex.Match(lineText, @"Downloading\s+\d+\s+format\(s\):\s*(.+)$", RegexOptions.IgnoreCase);
            if (formatMatch.Success) state.StageCount = Math.Max(1, formatMatch.Groups[1].Value.Count(c => c == '+') + 1);
            var match = Regex.Match(lineText, @"(?:^|\s)(\d+(?:[.,]\d+)?)%\s+of\b", RegexOptions.IgnoreCase);
            if (!match.Success) return;
            double percent;
            if (!Double.TryParse(match.Groups[1].Value.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out percent)) return;
            if (state.HasPercent && percent + 5 < state.LastPercent && state.LastPercent >= 70 && state.StageIndex < state.StageCount - 1)
                state.StageIndex++;
            state.LastPercent = percent;
            state.HasPercent = true;
            double itemFraction = Math.Min(0.995, (state.StageIndex + Math.Max(0, Math.Min(100, percent)) / 100.0) / Math.Max(1, state.StageCount));
            SetItemProgress(item, itemFraction, itemFraction * 100);
        }

        private void SetItemProgress(DownloadQueueItem item, double fraction, double itemPercent)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try
            {
                BeginInvoke((Action)(() =>
                {
                    if (IsDisposed || !currentRunProgress.ContainsKey(item)) return;
                    currentRunProgress[item] = Math.Max(currentRunProgress[item], Math.Max(0, Math.Min(1, fraction)));
                    item.ProgressPercent = itemPercent;
                    UpdateQueueRow(item);
                    UpdateQueueProgressDisplay(item, itemPercent);
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
        private sealed class DarkContextMenuRenderer : ToolStripProfessionalRenderer
        {
            internal DarkContextMenuRenderer() : base(new DarkContextMenuColors()) { RoundedEdges = false; }
            protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
            {
                using (var pen = new Pen(DarkPalette.Border)) e.Graphics.DrawRectangle(pen, 0, 0, e.ToolStrip.Width - 1, e.ToolStrip.Height - 1);
            }
        }
        private sealed class DarkContextMenuColors : ProfessionalColorTable
        {
            public override Color ToolStripDropDownBackground { get { return DarkPalette.Surface; } }
            public override Color MenuBorder { get { return DarkPalette.Border; } }
            public override Color MenuItemSelected { get { return DarkPalette.Selection; } }
            public override Color ImageMarginGradientBegin { get { return DarkPalette.Surface; } }
            public override Color ImageMarginGradientMiddle { get { return DarkPalette.Surface; } }
            public override Color ImageMarginGradientEnd { get { return DarkPalette.Surface; } }
        }
        private void AddQueueItem(DownloadQueueItem item)
        {
            queueItems.Add(item);
            string quality = item.BrowserSilent ? "最高画质 · AV1 优先"
                : item.AudioOnly ? "仅音频" : item.PlaylistItem
                ? (item.TargetHeight > 0 ? item.TargetHeight + "p " + item.TargetCodec : "最高可用")
                : (item.Video == null ? "自动" : item.Video.Label);
            string source = item.BrowserSilent ? "浏览器" : item.PlaylistItem ? "播放列表" : "单视频";
            int row = queueGrid.Rows.Add(item.Title, source, quality, item.Status);
            queueGrid.Rows[row].Tag = item; queueCountLabel.Text = "下载队列（" + queueItems.Count + "）"; UpdateQueueControls();
        }
        private void UpdateQueueRow(DownloadQueueItem item)
        {
            foreach (DataGridViewRow row in queueGrid.Rows)
                if (ReferenceEquals(row.Tag, item))
                {
                    row.Cells[0].Value = item.Title;
                    row.Cells[3].Value = item.Status == "下载中" && item.ProgressPercent > 0
                        ? "下载中 " + item.ProgressPercent.ToString("0.#", CultureInfo.InvariantCulture) + "%"
                        : item.Status;
                    break;
                }
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
            using (var d = new ConfigurationDialog(ytDlpPath, ffmpegPath, ffprobePath, denoPath, aria2cPath, ytdlpOptions))
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    ytDlpPath = ExistingOrDefault(d.YtDlpPath, "yt-dlp.exe");
                    ffmpegPath = ExistingOrDefault(d.FfmpegPath, "ffmpeg.exe");
                    ffprobePath = ExistingOrDefault(d.FfprobePath, "ffprobe.exe");
                    denoPath = ExistingOrDefault(d.DenoPath, "deno.exe");
                    aria2cPath = ExistingOrDefault(d.Aria2cPath, "aria2c.exe");
                    ytdlpOptions = d.Options;
                    ytdlpOptions.Aria2cPath = aria2cPath;
                    RefreshClient(); SaveSettings();
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
                        else if (key == "folder") folderBox.Text = value;
                        else if (key == "yt-dlp") ytDlpPath = value;
                        else if (key == "ffmpeg") ffmpegPath = value;
                        else if (key == "ffprobe") ffprobePath = value;
                        else if (key == "deno") denoPath = value;
                        else if (key == "aria2c") aria2cPath = value;
                        else if (key == "aria2c-sites") ytdlpOptions.Aria2cSites = value;
                        else if (key == "no-proxy-sites") ytdlpOptions.NoProxySites = value;
                        else if (key == "custom-args") ytdlpOptions.CustomArguments = value;
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
                string protectedProxy = Convert.ToBase64String(System.Security.Cryptography.ProtectedData.Protect(System.Text.Encoding.UTF8.GetBytes(ytdlpOptions.Proxy ?? ""), null, System.Security.Cryptography.DataProtectionScope.CurrentUser));
                string protectedGeoProxy = Convert.ToBase64String(System.Security.Cryptography.ProtectedData.Protect(System.Text.Encoding.UTF8.GetBytes(ytdlpOptions.GeoVerificationProxy ?? ""), null, System.Security.Cryptography.DataProtectionScope.CurrentUser));
                savedMainWindow = WindowPlacement.Capture(this, lastMainMaximized);
                File.WriteAllLines(settingsPath, new[] { "proxy=" + protectedProxy,
                    "geo-proxy=" + protectedGeoProxy,
                    "proxy-enabled=" + (ytdlpOptions.ProxyEnabled ? "1" : "0"),
                    "geo-proxy-enabled=" + (ytdlpOptions.GeoProxyEnabled ? "1" : "0"),
                    "folder=" + Uri.EscapeDataString(folderBox == null ? "" : folderBox.Text),
                    "yt-dlp=" + Uri.EscapeDataString(ytDlpPath), "ffmpeg=" + Uri.EscapeDataString(ffmpegPath),
                    "ffprobe=" + Uri.EscapeDataString(ffprobePath), "deno=" + Uri.EscapeDataString(denoPath),
                    "aria2c=" + Uri.EscapeDataString(aria2cPath),
                    "aria2c-sites=" + Uri.EscapeDataString(ytdlpOptions.Aria2cSites ?? ""),
                    "no-proxy-sites=" + Uri.EscapeDataString(ytdlpOptions.NoProxySites ?? ""),
                    "custom-args=" + Uri.EscapeDataString(ytdlpOptions.CustomArguments ?? ""),
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
        protected override void OnFormClosing(FormClosingEventArgs e) { SaveSettings(); base.OnFormClosing(e); }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            clipboardWatchTimer.Stop();
            clipboardWatchTimer.Dispose();
            base.OnFormClosed(e);
        }
    }

}
