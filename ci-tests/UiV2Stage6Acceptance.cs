using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using dotSharK.UiV2;

internal static class UiV2Stage6Acceptance
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static Assembly app;
    static string screenshots;
    static int checks;

    [STAThread]
    static int Main(string[] args)
    {
        try
        {
            if (args.Length != 5) throw new ArgumentException("Expected screenshots, integrated EXE, sample MP4, yt-dlp, ffmpeg");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            screenshots = Path.GetFullPath(args[0]);
            Directory.CreateDirectory(screenshots);
            app = Assembly.LoadFrom(Path.GetFullPath(args[1]));
            Check(Type("MainFormV2") != null && Type("SilentTrayContext") != null, "integrated V2 and tray are in production EXE");
            var bridge = Type("SilentDownloadBridge");
            string expectedRoot = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SilentQueue");
            Check((string)bridge.GetField("DataRoot", F).GetValue(null) == expectedRoot, "portable queue anchored to EXE directory");
            Check((string)Type("SilentTrayContext").GetField("UiLog", F).GetValue(null) == Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs", "tray-ui.log"), "portable tray log anchored to EXE directory");
            var media = Media();
            var playlist = Playlist();
            using (var main = NewForm("MainFormV2"))
            {
                Show(main);
                Check(Find<DataGridView>(main, "gridQueue") is DarkDataGridView, "production queue uses V2 dark grid");
                Check(Find<FlatTextArea>(main, "txtLog") != null, "production log uses V2 dark editor");
                Check(Find<FlatTextArea>(main, "txtLog").Font.FontFamily.Name == Theme.BodyFont.FontFamily.Name,
                    "log Chinese font matches UI");
                var queue = Find<DataGridView>(main, "gridQueue");
                Check(queue.Columns["source"].Width < queue.Columns["status"].Width,
                    "source compact and status readable");
                Check(queue.ContextMenuStrip.Items.Cast<ToolStripItem>().Any(x => x.Text == "打开下载目录"),
                    "completed menu includes open folder");
                string qualityText = "1080p 以内 · AV1 优先";
                Font originalFont = queue.Font;
                foreach (float scale in new[] { 1f, 1.25f, 1.5f })
                {
                    using (var scaledFont = new Font(originalFont.FontFamily, originalFont.Size * scale))
                    {
                        queue.Font = scaledFont;
                        main.GetType().GetMethod("EnsureQualityWidth", F).Invoke(main, new object[] { qualityText });
                        int measured = TextRenderer.MeasureText(qualityText, queue.Font, Size.Empty,
                            TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width;
                        Check(queue.Columns["quality"].Width - Theme.Space24 >= measured,
                            "quality text and custom paint insets fit at font scale " + scale);
                        foreach (string[] sample in new[] { new[] { "source", "播放列表" }, new[] { "status", "下载中 100%" } })
                        {
                            int textWidth = TextRenderer.MeasureText(sample[1], queue.Font, Size.Empty,
                                TextFormatFlags.NoPrefix | TextFormatFlags.SingleLine).Width;
                            Check(queue.Columns[sample[0]].Width - Theme.Space24 >= textWidth,
                                sample[0] + " full text fits at font scale " + scale);
                        }
                        Check(queue.Columns["title"].AutoSizeMode == DataGridViewAutoSizeColumnMode.Fill,
                            "video receives all remaining width at font scale " + scale);
                        queue.Font = originalFont;
                    }
                }
                Save(main, "Main.png");
                var portableOptions = Field(main, "ytdlpOptions");
                string proxy = "socks5://user:p@ss@127.0.0.1:1080";
                Field(portableOptions, "Proxy", proxy);
                main.GetType().GetMethod("SaveSettings", F).Invoke(main, null);
                Check(File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dotSharK.ini")).Contains("proxy-portable=" + Uri.EscapeDataString(proxy)), "proxy saved without user-bound encryption");
                Field(portableOptions, "Proxy", "");
                main.GetType().GetMethod("LoadSettings", F).Invoke(main, null);
                Check((string)Field(portableOptions, "Proxy") == proxy, "portable proxy roundtrips special characters");
                Field(portableOptions, "Proxy", "");

            }
            using (var settings = Settings())
            {
                Show(settings);
                var tabs = Find<DarkTabStrip>(settings, "tabSettings");
                Check(tabs.Pages.Count == 5, "five integrated settings pages");
                for (int i = 0; i < 4; ++i)
                {
                    tabs.SelectedIndex = i;
                    Application.DoEvents();
                    Save(settings, "Settings_" + i + ".png");
                    Check(!((ScrollableControl)tabs.Pages[i].Content).AutoScroll, "settings page " + i + " has no outer scrolling");
                }
                Check(Find<Button>(settings, "btnSave") != null, "configuration has save action");
                Check(Find<DarkCheckBox>(settings, "chkKeepCompleted") != null,
                    "advanced settings has completed-history option");
                Check(Find<DarkCheckBox>(settings, "chkKeepCompleted").Width > 250 &&
                    Find<DarkCheckBox>(settings, "chkKeepCompleted").Text == "重启后保留已完成任务",
                    "advanced history checkbox caption has full visible width");
                Check(Find<DarkCheckBox>(settings, "chkKeepCompleted").ForeColor == Theme.SecondaryText,
                    "completed history caption matches settings secondary text");
                var qualityCaption = Find<Label>(settings, "lblVideoQualityLimitCaption");
                Check(qualityCaption != null && qualityCaption.Text == "视频质量限制 前：" &&
                    qualityCaption.Width >= TextRenderer.MeasureText(qualityCaption.Text, Theme.BodyFont).Width,
                    "video quality caption is one fully visible string before the spinner");
                Check(Find<DarkComboBox>(settings, "cboSilentMaxResolution").SelectedIndex == 0,
                    "silent quality defaults to highest");
            }
            using (var quality = (Form)Activator.CreateInstance(Type("SingleVideoDialogV2"), F, null, new[] { media }, null))
            {
                Show(quality);
                Check(Find<DataGridView>(quality, "gridVideoFormats").Rows.Count == 7, "integrated quality renders format rows");
                Save(quality, "Quality.png");
                Find<Button>(quality, "btnAddToQueue").PerformClick();
                Check(quality.DialogResult == DialogResult.OK, "quality chooser returns selected result");
            }
            using (var list = (Form)Activator.CreateInstance(Type("PlaylistSelectionDialogV2"), F, null, new[] { playlist, media, Path.GetTempPath() }, null))
            {
                Show(list);
                var grid = Find<DataGridView>(list, "gridPlaylistEntries");
                Check(grid.Rows.Count == 30, "integrated playlist has 30 entries");
                Find<Button>(list, "btnSelectNone").PerformClick();
                Check(grid.Rows.Cast<DataGridViewRow>().All(x => !Convert.ToBoolean(x.Cells[0].Value)), "playlist none works");
                Find<Button>(list, "btnSelectAll").PerformClick();
                Check(grid.Rows.Cast<DataGridViewRow>().All(x => Convert.ToBoolean(x.Cells[0].Value)), "playlist all works");
                Save(list, "Playlist.png");
                Find<Button>(list, "btnAddToQueue").PerformClick();
                Check(list.DialogResult == DialogResult.OK, "playlist accepted");
                Check(((ICollection)list.GetType().GetProperty("QueueItems", F).GetValue(list, null)).Count == 30,
                    "playlist returns 30 real queue model items");
            }
            CheckConfigRoundtrip();
            CheckCompletedStorage();
            CheckSilentResolutionSelectors();
            CheckCommandLine();
            CheckUnicodeFilePaths();
            CheckNativeHost();
            CheckDownloads(args[2], args[3], args[4]);
            CheckTray();
            Console.WriteLine("PASS " + checks + " integrated assertions");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("FAIL " + (e.InnerException ?? e));
            return 1;
        }
    }

    static Type Type(string name) { return app.GetType("dotSharK." + name, true); }
    static object New(string name) { return Activator.CreateInstance(Type(name), true); }
    static Form NewForm(string name) { return (Form)New(name); }
    static object Field(object obj, string name) { return obj.GetType().GetField(name, F).GetValue(obj); }
    static void Field(object obj, string name, object value) { obj.GetType().GetField(name, F).SetValue(obj, value); }
    static object Invoke(object obj, string method, params object[] values)
    {
        return obj.GetType().GetMethod(method, F).Invoke(obj, values);
    }
    static object Static(string type, string method, params object[] values)
    {
        return Type(type).GetMethod(method, F).Invoke(null, values);
    }
    static void Check(bool ok, string name)
    {
        if (!ok) throw new InvalidOperationException(name);
        Console.WriteLine("PASS " + (++checks) + " " + name);
    }
    static T Find<T>(Control root, string name) where T : Control
    {
        if (root.Name == name && root is T) return (T)root;
        foreach (Control child in root.Controls)
        {
            var found = Find<T>(child, name);
            if (found != null) return found;
        }
        return null;
    }
    static void Show(Form form)
    {
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(5, 5);
        form.Show();
        Application.DoEvents();
        Application.DoEvents();
    }
    static void Save(Form form, string name)
    {
        using (var picture = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
        {
            form.DrawToBitmap(picture, new Rectangle(Point.Empty, form.ClientSize));
            picture.Save(Path.Combine(screenshots, name), ImageFormat.Png);
        }
    }
    static object Media()
    {
        var media = New("MediaInfo");
        Field(media, "Title", "UI V2 integrated real program");
        Field(media, "WebpageUrl", "https://example.com/video");
        var choices = (IList)Field(media, "VideoChoices");
        for (int i = 0; i < 7; i++)
        {
            var v = New("VideoChoice");
            Field(v, "Height", 1080 - i * 120);
            Field(v, "Width", 1920 - i * 200);
            Field(v, "Fps", 30.0);
            Field(v, "Codec", i % 2 == 0 ? "AV1" : "H.264");
            Field(v, "DynamicRange", i % 3 == 0 ? "HDR10" : "SDR");
            Field(v, "FormatId", "test" + i);
            Field(v, "Container", "mp4");
            Field(v, "ApproxBytes", 1000000.0);
            Field(v, "HasAudio", true);
            choices.Add(v);
        }
        return media;
    }
    static object Playlist()
    {
        var list = New("PlaylistInfo");
        Field(list, "Title", "UI V2 integrated playlist");
        var entries = (IList)Field(list, "Entries");
        for (int i = 1; i <= 30; ++i)
        {
            var item = New("PlaylistEntry");
            Field(item, "Index", i);
            Field(item, "Id", "test" + i);
            Field(item, "Title", "Test episode " + i);
            Field(item, "Url", "https://example.com/watch?v=" + i);
            Field(item, "Duration", 120.0);
            entries.Add(item);
        }
        return list;
    }
    static Form Settings()
    {
        var options = New("YtDlpOptions");
        Field(options, "CookieMode", "browser");
        return (Form)Activator.CreateInstance(Type("ConfigurationDialogV2"), F, null,
            new object[] { "yt-dlp.exe", "ffmpeg.exe", "ffprobe.exe", "deno.exe", "aria2c.exe", options }, null);
    }
    static void CheckConfigRoundtrip()
    {
        using (var form = Settings())
        {
            Show(form);
            Find<FlatTextBox>(form, "txtProxy").Text = "socks5://127.0.0.1:1080";
            Find<DarkCheckBox>(form, "chkProxyEnabled").Checked = true;
            Find<FlatTextBox>(form, "txtGeoProxy").Text = "http://127.0.0.1:3128";
            Find<DarkCheckBox>(form, "chkGeoProxyEnabled").Checked = false;
            Find<DarkComboBox>(form, "cboCookieSource").SelectedIndex = 0;
            Find<FlatTextArea>(form, "txtAria2cSites").Text = "youtube.com";
            Find<FlatTextArea>(form, "txtNoProxySites").Text = "example.org";
            Find<FlatTextArea>(form, "txtCustomYtDlpArgs").Text = "--retries 3";
            Find<DarkCheckBox>(form, "chkKeepCompleted").Checked = true;
            Find<DarkComboBox>(form, "cboSilentMaxResolution").SelectedIndex = 3;
            Find<Button>(form, "btnSave").PerformClick();
            Check(form.DialogResult == DialogResult.OK, "settings save returns OK");
            var opt = form.GetType().GetProperty("Options", F).GetValue(form, null);
            Check((string)Field(opt, "Proxy") == "socks5://127.0.0.1:1080" && (bool)Field(opt, "ProxyEnabled"),
                "socks5 proxy persists");
            Check(!(bool)Field(opt, "GeoProxyEnabled") && (string)Field(opt, "GeoVerificationProxy") == "http://127.0.0.1:3128",
                "disabled geographic proxy remains saved");
            Check((string)Field(opt, "Aria2cSites") == "youtube.com" && (string)Field(opt, "NoProxySites") == "example.org",
                "advanced site filters persist");
            Check((string)Field(opt, "CookieMode") == "browser" && (string)Field(opt, "CustomArguments") == "--retries 3",
                "browser cookie mode and custom yt-dlp arguments persist");
            Check((bool)Field(opt, "KeepCompletedTasks"), "completed-history checkbox saves");
            Check((int)Field(opt, "SilentMaxHeight") == 1080, "1080p silent cap saves");
        }
    }
    static void CheckCompletedStorage()
    {
        string path = Path.Combine(Path.GetTempPath(), "dotSharK-completed-test-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            var item = New("DownloadQueueItem");
            Field(item, "Status", "已完成");
            Field(item, "Title", "Chinese 测试 video");
            Field(item, "DisplaySource", "浏览器");
            Field(item, "DisplayQuality", "1080p 以内");
            Field(item, "FilePath", @"C:\Videos\finished.mp4");
            Field(item, "OutputFolder", @"C:\Videos");
            var listType = typeof(List<>).MakeGenericType(Type("DownloadQueueItem"));
            var list = (IList)Activator.CreateInstance(listType);
            list.Add(item);
            Static("CompletedQueueStore", "Save", path, list);
            Check(File.Exists(path), "completed history is written to disk");
            var restored = (IList)Static("CompletedQueueStore", "Load", path);
            Check(restored.Count == 1 && (string)Field(restored[0], "Title") == "Chinese 测试 video",
                "completed row restores after restart");
            Check((string)Field(restored[0], "FilePath") == @"C:\Videos\finished.mp4" &&
                (string)Field(restored[0], "DisplaySource") == "浏览器",
                "completed output path and source restore");
            Static("CompletedQueueStore", "Save", path, Activator.CreateInstance(listType));
            Check(!File.Exists(path), "clearing completed history deletes disk snapshot");
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    static void CheckSilentResolutionSelectors()
    {
        var opt = New("YtDlpOptions");
        var limits = new[] { 0, 4320, 2160, 1080, 720 };
        foreach (int limit in limits)
        {
            Field(opt, "SilentMaxHeight", limit);
            string selector = (string)Static("YtDlpClient", "SilentFormatSelector", opt);
            Check(limit == 0 ? selector == "bv*+ba/b" :
                selector.Contains("[height<=" + limit + "]") &&
                !selector.Contains("[height>" + limit + "]"), "silent resolution selector " + limit);
        }
    }

    static void CheckCommandLine()
    {
        var opt = New("YtDlpOptions");
        Field(opt, "CookieMode", "file"); Field(opt, "CookieFile", @"C:\test\cookie.txt");
        Field(opt, "ProxyEnabled", true); Field(opt, "Proxy", "socks5://127.0.0.1:1090");
        Field(opt, "GeoProxyEnabled", false);
        var client = Activator.CreateInstance(Type("YtDlpClient"), F, null,
            new object[] { "yt-dlp.exe", "", "", "", "" }, null);
        var args = ((IEnumerable<string>)Invoke(client, "CommonArguments", opt, true)).ToArray();
        Check(args.Contains("--cookies") && args.Contains(@"C:\test\cookie.txt"), "cookie-file argument preserved");
        Check(args.Contains("--proxy") && args.Contains("socks5://127.0.0.1:1090"), "proxy argument preserved");
        Check(!args.Contains("--geo-verification-proxy"), "disabled geographic proxy omitted");
        Field(opt, "CookieMode", "browser"); Field(opt, "CookieBrowser", "firefox");
        Field(opt, "CookieProfile", "default-release");
        args = ((IEnumerable<string>)Invoke(client, "CommonArguments", opt, false)).ToArray();
        Check(args.Contains("--cookies-from-browser") && args.Contains("firefox:default-release"),
            "browser cookie profile argument preserved");
        Check((bool)Static("YtDlpClient", "MatchesAriaSite",
                "https://www.youtube.com/watch?v=123", "youtube.com"), "aria2c subdomain routing preserved");
    }
    static void CheckUnicodeFilePaths()
    {
        string directory = Path.Combine(Path.GetTempPath(), "dotSharK-Unicode-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            const string realName = "Color Burst HDR Dolby Vision™ 12K 60FPS [y9n6HkftavM].webm";
            string actual = Path.Combine(directory, realName);
            File.WriteAllBytes(actual, new byte[] { 1, 2, 3 });
            string missingMark = Path.Combine(directory,
                "Color Burst HDR Dolby Vision 12K 60FPS [y9n6HkftavM].webm");
            Check((string)Static("CompletedFileResolver", "Resolve", actual, directory) == actual,
                "exact Unicode output path remains unchanged");
            Check((string)Static("CompletedFileResolver", "Resolve", missingMark, directory) == actual,
                "legacy missing trademark sign recovers actual downloaded video by stable ID");
            string missingId = Path.Combine(directory,
                "Color Burst HDR Dolby Vision 12K 60FPS.webm");
            string actualNoId = Path.Combine(directory, "Color Burst HDR Dolby Vision™ 12K 60FPS.webm");
            File.WriteAllBytes(actualNoId, new byte[] { 1 });
            Check((string)Static("CompletedFileResolver", "Resolve", missingId, directory) == actualNoId,
                "legacy missing trademark without video ID resolves unique file name");
            string notMatching = Path.Combine(directory, "completely unrelated video.webm");
            Check(Static("CompletedFileResolver", "Resolve", notMatching, directory) == null,
                "resolver refuses unrelated existing videos");
            string titleJson = "dotSharKTitleJson=\"Color Burst HDR Dolby Vision\\u2122 12K 60FPS\"";
            Check((string)Static("MainFormV2", "ParsePrintedMetadata", titleJson, "dotSharKTitleJson=") ==
                "Color Burst HDR Dolby Vision™ 12K 60FPS", "escaped JSON title preserves trademark");
            var escapedPath = new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(actual)
                .Replace("™", "\\u2122");
            string decoded = (string)Static("MainFormV2", "ParsePrintedMetadata",
                "dotSharKFileJson=" + escapedPath, "dotSharKFileJson=");
            Check(decoded == actual, "yt-dlp JSON final filename roundtrips trademark and Windows path");
        }
        finally { Directory.Delete(directory, true); }
    }

    static void CheckNativeHost()
    {
        Check((bool)Static("SilentDownloadBridge", "IsNativeHostCall",
            (object)new[] { "chrome-extension://test/" }), "native messaging entry point recognized");
        Check((string)Static("SilentDownloadBridge", "ValidatePageUrl",
            "https://example.com/watch?v=1") != null, "native messaging HTTP URL accepted");
        Check(Static("SilentDownloadBridge", "ValidatePageUrl",
            "file:///C:/secret.txt") == null, "native messaging local files rejected");
    }
    static void Pump(Task task, string name)
    {
        var deadline = DateTime.UtcNow.AddSeconds(120);
        while (!task.IsCompleted && DateTime.UtcNow < deadline)
        {
            Application.DoEvents();
            Thread.Sleep(30);
        }
        if (!task.IsCompleted) throw new TimeoutException(name);
        task.GetAwaiter().GetResult();
    }
    static void CheckDownloads(string fixture, string ytDlp, string ffmpeg)
    {
        Check(File.Exists(fixture) && new FileInfo(fixture).Length > 500, "real MP4 fixture present");
        Check(File.Exists(ytDlp), "real yt-dlp executable present");
        var folder = Path.Combine(Path.GetTempPath(), "dotSharK-S6-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        using (var server = new LocalServer(File.ReadAllBytes(fixture)))
        {
            var opt = New("YtDlpOptions");
            Field(opt, "CookieMode", "file"); Field(opt, "CookieFile", "");
            Field(opt, "ProxyEnabled", false); Field(opt, "GeoProxyEnabled", false);
            var client = Activator.CreateInstance(Type("YtDlpClient"), F, null,
                new object[] { ytDlp, ffmpeg, "", "", "" }, null);
            var silent = Path.Combine(folder, "Silent");
            Directory.CreateDirectory(silent);
            var download = (Task)Invoke(client, "DownloadSilentAsync", server.Url, silent,
                opt,
                (Action<string>)(line => Console.WriteLine("yt-dlp: " + line)),
                CancellationToken.None);
            Pump(download, "silent yt-dlp local HTTP transfer");
            Check(Directory.GetFiles(silent, "*.mp4").Length == 1, "real silent download wrote MP4");
            string firstFile = Directory.GetFiles(silent, "*.mp4")[0];
            File.Delete(firstFile);
            var repeatLog = new List<string>();
            var repeat = (Task)Invoke(client, "DownloadSilentAsync", server.Url, silent, opt,
                (Action<string>)(line => { lock (repeatLog) repeatLog.Add(line); }), CancellationToken.None);
            Pump(repeat, "download same URL after deleting file");
            Check(Directory.GetFiles(silent, "*.mp4").Length == 1, "same URL downloads again after file deletion");
            Check(!File.Exists(Path.Combine(silent, "downloaded.txt")), "no download archive created");
            Check(repeatLog.Any(line => line.Contains("[download]")), "normal yt-dlp download diagnostics remain visible");
            using (var main = NewForm("MainFormV2"))
            {
                Show(main);
                Field(main, "client", client);
                Field(main, "ytdlpOptions", opt);
                var item = New("DownloadQueueItem");
                Field(item, "Title", "Local MP4 real download");
                Field(item, "Url", server.Url);
                Field(item, "OutputFolder", Path.Combine(folder, "Queue"));
                Directory.CreateDirectory((string)Field(item, "OutputFolder"));
                Field(item, "Status", "等待中");
                Field(item, "PlaylistItem", true);
                Field(item, "Container", "MP4");
                Invoke(main, "AddQueueItem", item);
                Field(opt, "ConcurrentDownloads", 3);
                server.AllowBody.Reset();
                Task queueRun = (Task)Invoke(main, "DownloadQueue");
                var later = new List<object>();
                try
                {
                    for (int i = 0; i < 3; i++)
                    {
                        var added = New("DownloadQueueItem");
                        Field(added, "Title", "Later arrival " + i);
                        Field(added, "Url", server.Url);
                        Field(added, "OutputFolder", Path.Combine(folder, "Later" + i));
                        Field(added, "Status", "等待中");
                        Field(added, "PlaylistItem", true);
                        Field(added, "Container", "MP4");
                        Invoke(main, "AddQueueItem", added);
                        later.Add(added);
                    }
                    var deadline = DateTime.UtcNow.AddSeconds(15);
                    while (DateTime.UtcNow < deadline &&
                        later.Count(x => (string)Field(x, "Status") == "下载中") < 2)
                    { Application.DoEvents(); Thread.Sleep(25); }
                    Check((string)Field(item, "Status") == "下载中" &&
                        later.Count(x => (string)Field(x, "Status") == "下载中") == 2,
                        "later arrivals fill two idle slots before initial transfer completes");
                    Check((string)Field(later[2], "Status") == "等待中",
                        "fourth item respects concurrency limit of three");
                }
                finally { server.AllowBody.Set(); }
                Pump(queueRun, "integrated dynamic queue local HTTP transfer");
                Check(later.All(x => (string)Field(x, "Status") == "已完成"),
                    "all later arrivals complete in same queue run");
                Check((string)Field(item, "Status") == "已完成",
                    "actual integrated download queue finishes");
                Check(Directory.GetFiles((string)Field(item, "OutputFolder"), "*.mp4").Length > 0,
                    "actual integrated queue outputs MP4");
                Check(File.Exists((string)Field(item, "FilePath")),
                    "yt-dlp after_move returns actual playable output path");
                Save(main, "Main_RealDownload.png");
            }
        }
    }
    static void CheckTray()
    {
        var ctx = (ApplicationContext)Activator.CreateInstance(Type("SilentTrayContext"), F, null,
            new object[] { false }, null);
        try
        {
            var main = (Form)Field(ctx, "mainWindow");
            Check(main.GetType().Name == "MainFormV2", "tray owns actual V2 main window");
            Invoke(ctx, "OpenMainWindow"); Application.DoEvents();
            Check(main.Visible, "tray opens V2 window");
            Invoke(ctx, "HideMainWindow", "Stage6Test"); Application.DoEvents();
            Check(!main.Visible, "tray hides without closing V2");
            Invoke(ctx, "OpenMainWindow"); Application.DoEvents();
            Check(main.Visible, "tray restores V2 window");
        }
        finally { ctx.ExitThread(); ctx.Dispose(); }
    }
    sealed class LocalServer : IDisposable
    {
        readonly TcpListener listener;
        readonly Thread worker;
        readonly byte[] body;
        volatile bool stopping;
        internal readonly ManualResetEventSlim AllowBody = new ManualResetEventSlim(true);
        internal string Url { get; private set; }
        internal LocalServer(byte[] bytes)
        {
            body = bytes;
            listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            Url = "http://127.0.0.1:" + port + "/clip.mp4";
            worker = new Thread(Loop); worker.IsBackground = true; worker.Start();
        }
        void Loop()
        {
            while (!stopping)
            {
                try
                {
                    using (var c = listener.AcceptTcpClient())
                    using (var stream = c.GetStream())
                    {
                        stream.ReadTimeout = 10000;
                        var reader = new StreamReader(stream, Encoding.ASCII, false, 1024, true);
                        string head = reader.ReadLine();
                        if (head == null) continue;
                        while (!String.IsNullOrEmpty(reader.ReadLine())) { }
                        var h = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: video/mp4\r\n" +
                            "Content-Length: " + body.Length + "\r\nAccept-Ranges: bytes\r\nConnection: close\r\n\r\n");
                        stream.Write(h, 0, h.Length);
                        if (!head.StartsWith("HEAD ", StringComparison.OrdinalIgnoreCase))
                        {
                            AllowBody.Wait();
                            stream.Write(body, 0, body.Length);
                        }
                        stream.Flush();
                    }
                }
                catch { if (!stopping) Thread.Sleep(30); }
            }
        }
        public void Dispose() { stopping = true; AllowBody.Set(); listener.Stop(); worker.Join(1500); }
    }
}
