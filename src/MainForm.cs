using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace dotSharK
{
    internal sealed class MainForm : Form
    {
        private readonly Color bg = Color.FromArgb(34, 34, 34), panel = Color.FromArgb(43, 43, 43), field = Color.FromArgb(51, 51, 51), green = Color.FromArgb(96, 185, 28), muted = Color.FromArgb(153, 163, 170), fg = Color.FromArgb(235, 238, 240), line = Color.FromArgb(65, 65, 65);
        private DarkTextBox urlBox, folderBox, logBox;
        private Button analyzeButton, downloadButton, configButton, browseFolderButton;
        private DarkComboBox modeBox, containerBox, audioBox, subtitleBox;
        private DataGridView formatsGrid;
        private Label titleLabel, detailsLabel, stateLabel;
        private ProgressBar progress;
        private MediaInfo current;
        private VideoChoice selectedVideo;
        private AudioChoice selectedAudio;
        private SubtitleChoice selectedSubtitle;
        private YtDlpClient client;
        private YtDlpOptions ytdlpOptions = new YtDlpOptions();
        private string ytDlpPath = "", ffmpegPath = "", ffprobePath = "", denoPath = "";
        private bool loadingSettings;
        private readonly string settingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dotSharK.ini");

        public MainForm()
        {
            Text = "dotSharK  ·  YouTube Downloader"; StartPosition = FormStartPosition.CenterScreen; MinimumSize = new Size(900, 680); Size = new Size(1120, 820);
            BackColor = bg; ForeColor = fg; Font = new Font("Microsoft YaHei UI", 9F); FormBorderStyle = FormBorderStyle.Sizable; KeyPreview = true; Icon = AppBranding.CreateIcon();
            BuildUi(); LoadSettings();
            ytDlpPath = ExistingOrDefault(ytDlpPath, "yt-dlp.exe");
            ffmpegPath = ExistingOrDefault(ffmpegPath, "ffmpeg.exe");
            ffprobePath = ExistingOrDefault(ffprobePath, "ffprobe.exe");
            denoPath = ExistingOrDefault(denoPath, "deno.exe");
            RefreshClient();
            SetState(client == null ? "请先在“配置…”中指定 yt-dlp.exe。" : "就绪 · " + Path.GetFileName(client.ExecutablePath));
        }

        private void BuildUi()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(22, 16, 22, 18), BackColor = bg };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54));
            Controls.Add(root);

            var brand = new Panel { Dock = DockStyle.Fill };
            var logo = new Label { Text = "dotSharK", Font = new Font("Microsoft YaHei UI", 19F, FontStyle.Bold), ForeColor = green, AutoSize = true, Location = new Point(0, 0) };
            var tagline = new Label { Text = "轻快的 yt-dlp 桌面外壳", ForeColor = muted, AutoSize = true, Location = new Point(126, 13) };
            var configHost = new Panel { Dock = DockStyle.Right, Width = 110, Padding = new Padding(0, 9, 0, 9) };
            configButton = GhostButton("配置…", 110); configButton.Dock = DockStyle.Fill; configButton.Click += OpenConfiguration;
            configHost.Controls.Add(configButton);
            brand.Controls.Add(logo); brand.Controls.Add(tagline); brand.Controls.Add(configHost); root.Controls.Add(brand, 0, 0);

            var entry = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = bg, Padding = new Padding(0, 9, 0, 9) };
            entry.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); entry.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104));
            entry.RowStyles.Add(new RowStyle(SizeType.Absolute, DarkPalette.ControlHeight));
            urlBox = InputBox(); urlBox.Dock = DockStyle.Fill; urlBox.Margin = Padding.Empty; urlBox.Text = "";
            analyzeButton = AccentButton("分析链接"); analyzeButton.Dock = DockStyle.Fill; analyzeButton.Click += async (s, e) => await Analyze();
            entry.Controls.Add(urlBox, 0, 0); entry.Controls.Add(analyzeButton, 1, 0);
            root.Controls.Add(entry, 0, 1);

            var metadata = new Panel { Dock = DockStyle.Fill, BackColor = panel, Padding = new Padding(16, 12, 16, 10) };
            titleLabel = new Label { Text = "等待分析视频", Dock = DockStyle.Top, Height = 27, Font = new Font(Font.FontFamily, 12F, FontStyle.Bold), ForeColor = fg, AutoEllipsis = true };
            detailsLabel = new Label { Text = "解析后会显示标题、时长、分辨率、编码、动态范围、帧率与字幕信息。", Dock = DockStyle.Fill, ForeColor = muted, AutoEllipsis = true };
            metadata.Controls.Add(detailsLabel); metadata.Controls.Add(titleLabel); root.Controls.Add(metadata, 0, 2);

            var body = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = bg, Padding = new Padding(0, 10, 0, 8) };
            body.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); body.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); body.RowStyles.Add(new RowStyle(SizeType.Absolute, 122));
            var settings = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 8, RowCount = 1, BackColor = bg, Padding = new Padding(0, 9, 0, 9) };
            float[] widths = { 6, 18, 6, 18, 7, 22, 7, 16 };
            foreach (float width in widths) settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, width));
            settings.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            modeBox = DropDown(new[] { "视频", "仅音频" }); containerBox = DropDown(new[] { "MKV", "MP4", "WebM" });
            audioBox = DropDown(new[] { "默认音轨" }); subtitleBox = DropDown(new[] { "不下载字幕" });
            AddSetting(settings, 0, "下载"); AddSetting(settings, 2, "容器"); AddSetting(settings, 4, "音轨"); AddSetting(settings, 6, "字幕");
            settings.Controls.Add(modeBox, 1, 0); settings.Controls.Add(containerBox, 3, 0); settings.Controls.Add(audioBox, 5, 0); settings.Controls.Add(subtitleBox, 7, 0);
            modeBox.SelectedIndexChanged += (s, e) => UpdateMode(); subtitleBox.SelectedIndexChanged += (s, e) => { selectedSubtitle = subtitleBox.SelectedItem as SubtitleChoice; };
            body.Controls.Add(settings, 0, 0);

            formatsGrid = new DataGridView { Dock = DockStyle.Fill, BackgroundColor = panel, BorderStyle = BorderStyle.None, GridColor = line, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, ReadOnly = true, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, EnableHeadersVisualStyles = false, ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None, RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.None, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, ColumnHeadersHeight = 34, RowTemplate = { Height = 40 }, Visible = true };
            formatsGrid.AdvancedColumnHeadersBorderStyle.All = DataGridViewAdvancedCellBorderStyle.None;
            formatsGrid.ColumnHeadersDefaultCellStyle.BackColor = panel; formatsGrid.ColumnHeadersDefaultCellStyle.ForeColor = muted; formatsGrid.ColumnHeadersDefaultCellStyle.SelectionBackColor = panel;
            formatsGrid.DefaultCellStyle.BackColor = panel; formatsGrid.DefaultCellStyle.ForeColor = fg; formatsGrid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(51, 75, 39); formatsGrid.DefaultCellStyle.SelectionForeColor = fg; formatsGrid.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
            formatsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "resolution", HeaderText = "分辨率", FillWeight = 24 });
            formatsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "codec", HeaderText = "编码 / HDR", FillWeight = 33 });
            formatsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "fps", HeaderText = "帧率", FillWeight = 16 });
            formatsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "container", HeaderText = "源格式", FillWeight = 15 });
            formatsGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "size", HeaderText = "估算大小", FillWeight = 22 });
            formatsGrid.CellPainting += (s, e) =>
            {
                if (e.RowIndex != -1 || e.ColumnIndex < 0) return;
                using (var brush = new SolidBrush(panel)) e.Graphics.FillRectangle(brush, e.CellBounds);
                bool sorted = formatsGrid.SortedColumn == formatsGrid.Columns[e.ColumnIndex] && formatsGrid.SortOrder != SortOrder.None;
                Rectangle textBounds = Rectangle.Inflate(e.CellBounds, -9, 0); if (sorted) textBounds.Width -= 16;
                TextRenderer.DrawText(e.Graphics, Convert.ToString(e.FormattedValue), formatsGrid.ColumnHeadersDefaultCellStyle.Font ?? formatsGrid.Font,
                    textBounds, muted, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                using (var pen = new Pen(line))
                {
                    e.Graphics.DrawLine(pen, e.CellBounds.Left, e.CellBounds.Bottom - 1, e.CellBounds.Right - 1, e.CellBounds.Bottom - 1);
                    e.Graphics.DrawLine(pen, e.CellBounds.Right - 1, e.CellBounds.Top + 6, e.CellBounds.Right - 1, e.CellBounds.Bottom - 6);
                }
                if (sorted)
                {
                    int x = e.CellBounds.Right - 13, y = e.CellBounds.Top + e.CellBounds.Height / 2;
                    Point[] glyph = formatsGrid.SortOrder == SortOrder.Ascending ? new[] { new Point(x - 4, y + 2), new Point(x + 4, y + 2), new Point(x, y - 2) } : new[] { new Point(x - 4, y - 2), new Point(x + 4, y - 2), new Point(x, y + 2) };
                    using (var brush = new SolidBrush(muted)) e.Graphics.FillPolygon(brush, glyph);
                }
                e.Handled = true;
            };
            formatsGrid.CellClick += (s, e) => { if (e.RowIndex >= 0) selectedVideo = formatsGrid.Rows[e.RowIndex].Tag as VideoChoice; };
            formatsGrid.SelectionChanged += (s, e) => { if (formatsGrid.CurrentRow != null) selectedVideo = formatsGrid.CurrentRow.Tag as VideoChoice; };
            body.Controls.Add(formatsGrid, 0, 1);

            var lower = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = bg };
            lower.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); lower.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var pathRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = bg };
            pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 75)); pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); pathRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 96));
            pathRow.Controls.Add(new Label { Text = "保存到", ForeColor = muted, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            folderBox = InputBox(); folderBox.Dock = DockStyle.Fill; folderBox.Margin = new Padding(0, 8, 0, 0); folderBox.TextChanged += (s, e) => SaveSettings(); pathRow.Controls.Add(folderBox, 1, 0);
            browseFolderButton = GhostButton("浏览…", 90); browseFolderButton.Dock = DockStyle.Fill; browseFolderButton.Margin = new Padding(0, 8, 0, 0); browseFolderButton.Click += BrowseFolder; pathRow.Controls.Add(browseFolderButton, 2, 0);
            lower.Controls.Add(pathRow, 0, 0);
            var logFrame = new Panel { Dock = DockStyle.Fill, BackColor = DarkPalette.Border, Padding = new Padding(1), Margin = Padding.Empty };
            logBox = InputBox(); logBox.Dock = DockStyle.Fill; logBox.Multiline = true; logBox.BorderStyle = BorderStyle.None; logBox.ScrollBars = ScrollBars.Vertical; logBox.ReadOnly = true; logBox.Font = new Font("Consolas", 8.5F); logBox.ForeColor = muted;
            NativeDarkTheme.ApplyOnHandleCreated(logBox, "DarkMode_Explorer"); logFrame.Controls.Add(logBox);
            lower.Controls.Add(logFrame, 0, 1); body.Controls.Add(lower, 0, 2); root.Controls.Add(body, 0, 3);
            NativeDarkTheme.ApplyOnHandleCreated(formatsGrid, "DarkMode_Explorer");

            var footer = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = bg, Padding = new Padding(0, 8, 0, 0) };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 176)); footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 136));
            stateLabel = new Label { Text = "就绪", Dock = DockStyle.Fill, ForeColor = muted, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            progress = new ProgressBar { Dock = DockStyle.Fill, Style = ProgressBarStyle.Marquee, MarqueeAnimationSpeed = 24, Visible = false, Margin = new Padding(4, 10, 16, 10) };
            downloadButton = AccentButton("下载"); downloadButton.Dock = DockStyle.Fill; downloadButton.Margin = new Padding(0, 11, 0, 11); downloadButton.Enabled = false; downloadButton.Click += async (s, e) => await Download();
            footer.Controls.Add(stateLabel, 0, 0); footer.Controls.Add(progress, 1, 0); footer.Controls.Add(downloadButton, 2, 0); root.Controls.Add(footer, 0, 4);
            AcceptButton = analyzeButton;
        }

        private static void AddSetting(TableLayoutPanel row, int column, string label)
        {
            row.Controls.Add(new Label { Text = label, ForeColor = DarkPalette.Muted, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true, Margin = new Padding(0, 0, 4, 0) }, column, 0);
        }
        private DarkComboBox DropDown(string[] values)
        {
            var c = new DarkComboBox { Dock = DockStyle.Fill, Height = DarkPalette.ControlHeight, Margin = new Padding(0, 0, 8, 0) };
            c.Items.AddRange(values.Cast<object>().ToArray()); c.SelectedIndex = 0; return c;
        }
        private DarkTextBox InputBox() { return new DarkTextBox { Font = new Font(Font.FontFamily, 9F), AutoSize = false, Height = DarkPalette.ControlHeight, Margin = Padding.Empty }; }
        private DarkButton AccentButton(string text) { return new DarkButton { Text = text, Primary = true, Font = new Font(Font.FontFamily, 9F, FontStyle.Bold), Height = DarkPalette.ControlHeight, MinimumSize = new Size(0, DarkPalette.ControlHeight) }; }
        private DarkButton GhostButton(string text, int width) { return new DarkButton { Text = text, Width = width, Height = DarkPalette.ControlHeight, MinimumSize = new Size(0, DarkPalette.ControlHeight), Font = Font }; }

        private async Task Analyze()
        {
            if (client == null) { OpenConfiguration(this, EventArgs.Empty); if (client == null) return; }
            if (String.IsNullOrWhiteSpace(urlBox.Text)) { SetState("请先粘贴视频网址。"); return; }
            if (!ValidProxy()) return;
            Busy(true, "正在解析视频信息…"); formatsGrid.Rows.Clear(); logBox.Clear(); downloadButton.Enabled = false;
            try
            {
                current = await client.AnalyzeAsync(urlBox.Text.Trim(), ytdlpOptions);
                titleLabel.Text = String.IsNullOrWhiteSpace(current.Title) ? "未获取到标题" : current.Title;
                detailsLabel.Text = String.Format("时长  {0}     可用视频选项  {1}     音轨  {2}     字幕  {3}（含自动字幕）",
                    TimeSpan.FromSeconds(Math.Max(0, current.Duration)).ToString(current.Duration >= 3600 ? @"h\:mm\:ss" : @"mm\:ss"), current.VideoChoices.Count, current.AudioChoices.Count, current.Subtitles.Count);
                foreach (var v in current.VideoChoices)
                {
                    int i = formatsGrid.Rows.Add(v.Height + "p", v.Codec + (v.DynamicRange == "SDR" ? "" : "  ·  " + v.DynamicRange), v.Fps > 0 ? v.Fps.ToString("0.##") + " fps" : "—", v.Container.ToUpperInvariant(), YtDlpClient.FormatBytes(v.ApproxBytes));
                    formatsGrid.Rows[i].Tag = v;
                }
                if (formatsGrid.Rows.Count > 0) { formatsGrid.Rows[0].Selected = true; selectedVideo = formatsGrid.Rows[0].Tag as VideoChoice; }
                RefreshChoices();
                if (String.IsNullOrWhiteSpace(current.Title)) throw new InvalidOperationException("链接已响应，但未返回标题。请查看详情并检查链接。");
                downloadButton.Enabled = formatsGrid.Rows.Count > 0 || current.AudioChoices.Count > 0;
                SetState("解析完成 · 同一分辨率的不同视频编码会分别列出。" + (String.IsNullOrWhiteSpace(ytdlpOptions.Proxy) ? "" : " 代理已启用。"));
            }
            catch (Exception ex) { titleLabel.Text = "解析失败"; detailsLabel.Text = ex.Message; SetState("解析失败 · 查看详情"); logBox.Text = ex.Message; }
            finally { Busy(false, stateLabel.Text); }
        }

        private void RefreshChoices()
        {
            audioBox.Items.Clear(); audioBox.Items.Add("默认音轨");
            if (current != null) foreach (var a in current.AudioChoices) audioBox.Items.Add(a);
            audioBox.SelectedIndex = 0; selectedAudio = null;
            subtitleBox.Items.Clear(); subtitleBox.Items.Add("不下载字幕");
            if (current != null) foreach (var sub in current.Subtitles) subtitleBox.Items.Add(sub);
            subtitleBox.SelectedIndex = 0; selectedSubtitle = null;
        }
        private void UpdateMode()
        {
            bool audioOnly = modeBox.SelectedIndex == 1;
            formatsGrid.Enabled = !audioOnly;
            if (audioBox != null) { audioBox.SelectedItem = "默认音轨"; }
        }

        private async Task Download()
        {
            if (current == null || !ValidProxy()) return;
            string folder = folderBox.Text.Trim(); if (folder.Length == 0) folder = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            try { Directory.CreateDirectory(folder); } catch (Exception ex) { SetState("保存目录不可用：" + ex.Message); return; }
            bool onlyAudio = modeBox.SelectedIndex == 1;
            if (!onlyAudio && selectedVideo == null) { SetState("请选择一个视频格式。"); return; }
            selectedAudio = audioBox.SelectedItem as AudioChoice;
            selectedSubtitle = subtitleBox.SelectedItem as SubtitleChoice;
            var selectedContainer = containerBox.SelectedItem.ToString();
            Busy(true, "正在下载…"); logBox.Clear(); downloadButton.Enabled = false;
            try
            {
                await client.DownloadAsync(current, selectedVideo, selectedAudio, onlyAudio, selectedContainer, folder, selectedSubtitle, ytdlpOptions, lineText =>
                { if (!IsDisposed && IsHandleCreated) BeginInvoke((Action)(() => { logBox.AppendText(lineText + Environment.NewLine); logBox.SelectionStart = logBox.TextLength; logBox.ScrollToCaret(); })); });
                SetState("下载完成 · " + folder);
            }
            catch (Exception ex) { SetState("下载失败"); logBox.AppendText(Environment.NewLine + ex.Message); }
            finally { Busy(false, stateLabel.Text); downloadButton.Enabled = current != null; }
        }

        private bool ValidProxy()
        {
            string value = ytdlpOptions.Proxy.Trim(); if (value.Length == 0) return true;
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || !(uri.Scheme == "http" || uri.Scheme == "https" || uri.Scheme == "socks4" || uri.Scheme == "socks4a" || uri.Scheme == "socks5" || uri.Scheme == "socks5h"))
            { SetState("代理格式不正确。请在“配置 → YT-DLP”中检查代理地址。"); return false; }
            return true;
        }
        private void Busy(bool busy, string message)
        {
            analyzeButton.Enabled = !busy;
            downloadButton.Enabled = !busy && current != null && (formatsGrid.Rows.Count > 0 || current.AudioChoices.Count > 0);
            progress.Visible = busy;
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
            using (var d = new ConfigurationDialog(ytDlpPath, ffmpegPath, ffprobePath, denoPath, ytdlpOptions))
                if (d.ShowDialog(this) == DialogResult.OK)
                {
                    ytDlpPath = d.YtDlpPath; ffmpegPath = d.FfmpegPath; ffprobePath = d.FfprobePath; denoPath = d.DenoPath; ytdlpOptions = d.Options;
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
        private void RefreshClient() { client = String.IsNullOrWhiteSpace(ytDlpPath) ? null : new YtDlpClient(ytDlpPath, ffmpegPath, ffprobePath, denoPath); }
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
                        else if (key == "folder") folderBox.Text = value;
                        else if (key == "yt-dlp") ytDlpPath = value;
                        else if (key == "ffmpeg") ffmpegPath = value;
                        else if (key == "ffprobe") ffprobePath = value;
                        else if (key == "deno") denoPath = value;
                        else if (key == "fragments") { int n; if (Int32.TryParse(value, out n)) ytdlpOptions.ConcurrentFragments = Math.Max(1, Math.Min(32, n)); }
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
                File.WriteAllLines(settingsPath, new[] { "proxy=" + protectedProxy,
                    "folder=" + Uri.EscapeDataString(folderBox == null ? "" : folderBox.Text),
                    "yt-dlp=" + Uri.EscapeDataString(ytDlpPath), "ffmpeg=" + Uri.EscapeDataString(ffmpegPath),
                    "ffprobe=" + Uri.EscapeDataString(ffprobePath), "deno=" + Uri.EscapeDataString(denoPath),
                    "fragments=" + ytdlpOptions.ConcurrentFragments, "cookie-mode=" + ytdlpOptions.CookieMode,
                    "cookie-browser=" + Uri.EscapeDataString(ytdlpOptions.CookieBrowser ?? "firefox"),
                    "cookie-root=" + Uri.EscapeDataString(ytdlpOptions.CookieProfileRoot ?? ""),
                    "cookie-profile=" + Uri.EscapeDataString(ytdlpOptions.CookieProfile ?? ""),
                    "cookie-file=" + Uri.EscapeDataString(ytdlpOptions.CookieFile ?? "") });
            }
            catch { }
        }
        protected override void OnFormClosing(FormClosingEventArgs e) { SaveSettings(); base.OnFormClosing(e); }
    }

}
