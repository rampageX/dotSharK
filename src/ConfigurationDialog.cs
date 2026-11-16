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
using Microsoft.Win32.SafeHandles;

namespace dotSharK
{
    internal sealed class ConfigurationDialog : DarkSettingsForm
    {
        private readonly Color bg = Color.FromArgb(34, 34, 34), panel = Color.FromArgb(43, 43, 43), field = Color.FromArgb(51, 51, 51), green = Color.FromArgb(96, 185, 28), fg = Color.FromArgb(235, 238, 240), muted = Color.FromArgb(153, 163, 170);
        private readonly TextBox[] toolPaths = new TextBox[4];
        private readonly string[] toolNames = { "yt-dlp.exe", "ffmpeg.exe", "ffprobe.exe", "deno.exe" };
        private TextBox proxyBox, profileRootBox, cookieFileBox;
        private DarkNumericUpDown fragmentsBox;
        private DarkComboBox sourceBox, browserBox, profileBox;
        private Button profileSearchButton;
        private Label profileStatus, cookieFileStatus, profileFoundCheck;
        private int profileSearchGeneration;

        public string YtDlpPath { get { return toolPaths[0].Text.Trim(); } }
        public string FfmpegPath { get { return toolPaths[1].Text.Trim(); } }
        public string FfprobePath { get { return toolPaths[2].Text.Trim(); } }
        public string DenoPath { get { return toolPaths[3].Text.Trim(); } }
        public YtDlpOptions Options { get; private set; }

        public ConfigurationDialog(string ytDlp, string ffmpeg, string ffprobe, string deno, YtDlpOptions options)
        {
            options = options ?? new YtDlpOptions();
            Options = new YtDlpOptions { Proxy = options.Proxy, ConcurrentFragments = options.ConcurrentFragments, CookieMode = options.CookieMode,
                CookieBrowser = options.CookieBrowser, CookieProfileRoot = options.CookieProfileRoot, CookieProfile = options.CookieProfile, CookieFile = options.CookieFile };
            Text = "dotSharK 配置"; StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog; Icon = AppBranding.CreateIcon();
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false; BackColor = bg; ForeColor = fg; Font = new Font("Microsoft YaHei UI", 9F); ClientSize = new Size(790, 540); MinimumSize = new Size(700, 500);
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = bg, Padding = new Padding(16), ColumnCount = 1, RowCount = 2 };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48)); Controls.Add(root);
            var tabs = new DarkTabHost { Dock = DockStyle.Fill };
            var toolsPage = new Panel { BackColor = bg, Padding = new Padding(14) };
            var ytPage = new Panel { BackColor = bg, Padding = new Padding(14) };
            tabs.AddPage("外部工具", toolsPage); tabs.AddPage("YT-DLP", ytPage); root.Controls.Add(tabs, 0, 0);
            BuildToolsPage(toolsPage, new[] { ytDlp, ffmpeg, ffprobe, deno });
            BuildYtPage(ytPage);
            var footer = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, WrapContents = false, BackColor = bg, Padding = new Padding(0, 8, 0, 0) };
            var save = StyledButton("保存", true); save.Width = 98; save.DialogResult = DialogResult.None;
            var cancel = StyledButton("取消", false); cancel.Width = 98; cancel.DialogResult = DialogResult.Cancel; cancel.Margin = new Padding(0, 0, 10, 0);
            save.Click += SaveClicked; footer.Controls.Add(save); footer.Controls.Add(cancel); root.Controls.Add(footer, 0, 1);
            AcceptButton = save; CancelButton = cancel;
        }

        private void BuildToolsPage(Panel page, string[] values)
        {
            var content = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = bg };
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 180)); content.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 5, BackColor = bg };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 116)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 92));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); for (int i = 0; i < 4; i++) grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            grid.Controls.Add(StyledLabel("程序"), 0, 0); grid.Controls.Add(StyledLabel("可执行文件路径（留空时自动检查 dotSharK 目录和 tools 子目录）"), 1, 0);
            for (int i = 0; i < 4; i++)
            {
                int index = i, row = i + 1;
                grid.Controls.Add(StyledLabel(toolNames[i]), 0, row);
                toolPaths[i] = StyledTextBox(values[i]); SetBottomField(toolPaths[i], true, 8); grid.Controls.Add(toolPaths[i], 1, row);
                var browse = StyledButton("浏览…", false); SetBottomField(browse, true, 0); browse.Click += (s, e) => BrowseTool(index); grid.Controls.Add(browse, 2, row);
            }
            content.Controls.Add(grid, 0, 0);
            content.Controls.Add(new Label { Text = "路径保存在 dotSharK.ini 中。YT-DLP 页面中的代理、并发和 Cookie 设置同样由 dotSharK 统一传参。", Dock = DockStyle.Fill, ForeColor = muted, Padding = new Padding(0, 12, 0, 0), AutoEllipsis = true }, 0, 1);
            page.Controls.Add(content);
        }

        private void BuildYtPage(Panel page)
        {
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 9, BackColor = bg, Padding = new Padding(2) };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 104)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 106));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30)); grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            grid.Controls.Add(StyledLabel("代理"), 0, 0);
            proxyBox = StyledTextBox(Options.Proxy); SetBottomField(proxyBox, true, 0); grid.Controls.Add(proxyBox, 1, 0); grid.SetColumnSpan(proxyBox, 2);
            grid.Controls.Add(StyledLabel("分片并发"), 0, 1);
            fragmentsBox = new DarkNumericUpDown { Minimum = 1, Maximum = 32, Value = Math.Max(1, Math.Min(32, Options.ConcurrentFragments)), Width = 116, Height = DarkPalette.ControlHeight, Margin = Padding.Empty, Anchor = AnchorStyles.Left | AnchorStyles.Bottom, BackColor = field, ForeColor = fg, BorderStyle = BorderStyle.FixedSingle, TextAlign = HorizontalAlignment.Center };
            grid.Controls.Add(fragmentsBox, 1, 1);
            grid.Controls.Add(new Label { Text = "下载分片并发数", ForeColor = muted, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 2, 1);

            grid.Controls.Add(StyledLabel("Cookie 来源"), 0, 2);
            sourceBox = StyledCombo(new[] { "浏览器", "Cookie 文件" }); sourceBox.SelectedIndex = Options.CookieMode == "file" ? 1 : 0; sourceBox.Width = 150; sourceBox.Anchor = AnchorStyles.Left | AnchorStyles.Bottom; grid.Controls.Add(sourceBox, 1, 2);
            grid.Controls.Add(StyledLabel("浏览器"), 0, 3);
            var browsers = new[] {
                new BrowserOption { Name="firefox", Label="Firefox" }, new BrowserOption { Name="chrome", Label="Chrome" },
                new BrowserOption { Name="edge", Label="Edge" }, new BrowserOption { Name="brave", Label="Brave" },
                new BrowserOption { Name="chromium", Label="Chromium" }, new BrowserOption { Name="opera", Label="Opera" },
                new BrowserOption { Name="vivaldi", Label="Vivaldi" }, new BrowserOption { Name="safari", Label="Safari" },
                new BrowserOption { Name="whale", Label="Whale" }
            };
            browserBox = StyledCombo(browsers.Cast<object>().ToArray()); browserBox.Width = 190; browserBox.Anchor = AnchorStyles.Left | AnchorStyles.Bottom; grid.Controls.Add(browserBox, 1, 3);
            string browserName = String.IsNullOrWhiteSpace(Options.CookieBrowser) ? "firefox" : Options.CookieBrowser;
            for (int i = 0; i < browserBox.Items.Count; i++) if (((BrowserOption)browserBox.Items[i]).Name == browserName) browserBox.SelectedIndex = i;

            grid.Controls.Add(StyledLabel("搜索目录"), 0, 4);
            profileRootBox = StyledTextBox(Options.CookieProfileRoot); SetBottomField(profileRootBox, true, 8); grid.Controls.Add(profileRootBox, 1, 4);
            var rootButtons = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, BackColor = bg, Height = DarkPalette.ControlHeight, Margin = Padding.Empty, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom };
            rootButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50)); rootButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            var rootBrowse = StyledButton("选择", false); rootBrowse.Dock = DockStyle.Fill; rootBrowse.Margin = new Padding(0, 0, 3, 0); rootBrowse.Click += BrowseProfileRoot;
            profileSearchButton = StyledButton("搜索", true); profileSearchButton.Dock = DockStyle.Fill; profileSearchButton.Margin = new Padding(3, 0, 0, 0); profileSearchButton.Click += async (s, e) => await SearchProfiles();
            rootButtons.Controls.Add(rootBrowse, 0, 0); rootButtons.Controls.Add(profileSearchButton, 1, 0); grid.Controls.Add(rootButtons, 2, 4);

            grid.Controls.Add(StyledLabel("浏览器配置"), 0, 5);
            var profileRow = new TableLayoutPanel { ColumnCount = 2, RowCount = 1, BackColor = bg, Height = DarkPalette.ControlHeight, Margin = Padding.Empty, Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom };
            profileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); profileRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 34));
            profileBox = StyledCombo(new object[0]); profileBox.DropDownStyle = ComboBoxStyle.DropDown; profileBox.Dock = DockStyle.Fill; profileBox.Margin = new Padding(0, 0, 5, 0); profileRow.Controls.Add(profileBox, 0, 0);
            profileFoundCheck = new Label { Text = "", ForeColor = green, Font = new Font("Segoe UI Symbol", 14F, FontStyle.Bold), Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Visible = false, BackColor = bg };
            profileRow.Controls.Add(profileFoundCheck, 1, 0); grid.Controls.Add(profileRow, 1, 5); grid.SetColumnSpan(profileRow, 2);
            if (!String.IsNullOrWhiteSpace(Options.CookieProfile)) { profileBox.Text = Options.CookieProfile; profileBox.Items.Add(Options.CookieProfile); }
            profileStatus = new Label { Text = "默认浏览器配置自动读取；指定目录后可递归搜寻 Cookie 数据库。", ForeColor = muted, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            grid.Controls.Add(profileStatus, 1, 6); grid.SetColumnSpan(profileStatus, 2);
            grid.Controls.Add(StyledLabel("Cookie 文件"), 0, 7);
            cookieFileBox = StyledTextBox(Options.CookieFile); SetBottomField(cookieFileBox, true, 8); grid.Controls.Add(cookieFileBox, 1, 7);
            var cookieBrowse = StyledButton("浏览…", false); SetBottomField(cookieBrowse, true, 0); cookieBrowse.Click += BrowseCookieFile; grid.Controls.Add(cookieBrowse, 2, 7);
            cookieFileStatus = new Label { Text = "浏览器模式默认 Firefox。Cookie 文件需为 Netscape 格式。", ForeColor = muted, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft, AutoEllipsis = true };
            grid.Controls.Add(cookieFileStatus, 1, 8); grid.SetColumnSpan(cookieFileStatus, 2);
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

        private void UpdateCookieMode()
        {
            bool file = sourceBox != null && sourceBox.SelectedIndex == 1;
            browserBox.Enabled = !file; profileRootBox.Enabled = !file; profileBox.Enabled = !file; cookieFileBox.Enabled = file;
        }
        private void SaveClicked(object sender, EventArgs e)
        {
            string proxy = proxyBox.Text.Trim();
            if (proxy.Length > 0)
            {
                Uri uri;
                if (!Uri.TryCreate(proxy, UriKind.Absolute, out uri) || !(uri.Scheme == "http" || uri.Scheme == "https" || uri.Scheme == "socks4" || uri.Scheme == "socks4a" || uri.Scheme == "socks5" || uri.Scheme == "socks5h"))
                { profileStatus.Text = "代理地址格式不正确，请检查协议和端口。"; return; }
            }
            if (sourceBox.SelectedIndex == 1 && !String.IsNullOrWhiteSpace(cookieFileBox.Text) && !File.Exists(cookieFileBox.Text.Trim()))
            { cookieFileStatus.Text = "找不到指定的 Cookie 文件。"; return; }
            if (sourceBox.SelectedIndex == 1 && String.IsNullOrWhiteSpace(cookieFileBox.Text))
            { cookieFileStatus.Text = "请指定 Netscape 格式的 Cookie 文件。"; return; }
            var browser = browserBox.SelectedItem as BrowserOption;
            Options.Proxy = proxy; Options.ConcurrentFragments = (int)fragmentsBox.Value;
            Options.CookieMode = sourceBox.SelectedIndex == 1 ? "file" : "browser";
            Options.CookieBrowser = browser == null ? "firefox" : browser.Name;
            Options.CookieProfileRoot = profileRootBox.Text.Trim(); Options.CookieProfile = profileBox.Text.Trim(); Options.CookieFile = cookieFileBox.Text.Trim();
            DialogResult = DialogResult.OK; Close();
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
        private TextBox StyledTextBox(string text) { return new DarkTextBox { Text = text ?? "", Font = Font, Multiline = false, AutoSize = false, Height = DarkPalette.ControlHeight, Margin = Padding.Empty }; }
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
