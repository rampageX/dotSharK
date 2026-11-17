using System;
using System.IO;
using System.Windows.Forms;

namespace dotSharK.UiV2
{
    /// <summary>Independent gallery: settings only. No native messaging, queue or downloads.</summary>
    internal static class SettingsPreviewLauncher
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var options = new YtDlpOptions
            {
                ConcurrentFragments = 2,
                ConcurrentDownloads = 3,
                VideoQualityLimit = 3,
                Proxy = "http://127.0.0.1:7002",
                GeoVerificationProxy = "http://127.0.0.1:7002",
                ProxyEnabled = true,
                GeoProxyEnabled = false,
                CookieMode = "file",
                CookieBrowser = "firefox",
                CookieProfile = "",
                CookieProfileRoot = "",
                CookieFile = "",
                Aria2cSites = "bilibili.com",
                NoProxySites = "bilibili.com",
                CustomArguments = "--retries 20",
                SilentDownloadFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyVideos),
                    "dotSharK")
            };
            using (var form = new ConfigurationDialogV2(
                "yt-dlp.exe", "ffmpeg.exe", "ffprobe.exe", "deno.exe", "aria2c.exe", options))
            {
                form.StartPosition = FormStartPosition.CenterScreen;
                form.ShowDialog();
            }
        }
    }
}
