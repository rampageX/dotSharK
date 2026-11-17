using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace dotSharK
{
    // Chrome Native Messaging uses the existing dotSharK.exe as its host.
    // Jobs are persisted in the user's profile and consumed by one background worker.
    // Neither entry point creates a WinForms window.
    internal static class SilentDownloadBridge
    {
        private static readonly string DataRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "dotSharK", "SilentQueue");
        private static readonly string Pending = Path.Combine(DataRoot, "Pending");
        private static readonly string Active = Path.Combine(DataRoot, "Active");
        private static readonly string LogPath = Path.Combine(DataRoot, "silent-download.log");
        private static readonly string ArchivePath = Path.Combine(DataRoot, "downloaded.txt");
        private const string MutexName = @"Local\dotSharK.SilentDownloadQueue.1";

        internal static bool IsNativeHostCall(string[] args)
        {
            return args != null && args.Length > 0 &&
                args[0].StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase);
        }

        internal static void RunNativeHost()
        {
            object response;
            try
            {
                string json = ReadNativeMessage(Console.OpenStandardInput());
                var request = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
                object value;
                string url = request != null && request.TryGetValue("url", out value) ? value as string : null;
                url = ValidatePageUrl(url);
                if (url == null) throw new InvalidDataException("必须发送单个 HTTP/HTTPS 网页链接。");
                bool duplicate = Enqueue(url);
                bool launched = StartWorker();
                response = new { ok = launched, duplicate = duplicate, error = launched ? "" : "任务已保存，但后台下载进程启动失败。" };
            }
            catch (Exception ex)
            {
                response = new { ok = false, duplicate = false, error = ex.Message };
            }
            try { WriteNativeMessage(Console.OpenStandardOutput(), new JavaScriptSerializer().Serialize(response)); }
            catch { } // The browser may already have closed the connection.
        }

        internal static string ValidatePageUrl(string raw)
        {
            if (String.IsNullOrWhiteSpace(raw)) return null;
            string text = raw.Trim();
            if (text.Length > 4096 || text.Any(Char.IsWhiteSpace)) return null;
            Uri url;
            if (!Uri.TryCreate(text, UriKind.Absolute, out url) ||
                (url.Scheme != Uri.UriSchemeHttp && url.Scheme != Uri.UriSchemeHttps) ||
                String.IsNullOrWhiteSpace(url.Host) || !String.IsNullOrEmpty(url.UserInfo))
                return null;
            return text;
        }

        internal static string ReadNativeMessage(Stream stream)
        {
            byte[] size = ReadExactly(stream, 4);
            if (!BitConverter.IsLittleEndian) Array.Reverse(size);
            int length = BitConverter.ToInt32(size, 0);
            if (length < 2 || length > 65536) throw new InvalidDataException("浏览器消息大小无效。");
            return Encoding.UTF8.GetString(ReadExactly(stream, length));
        }

        private static void WriteNativeMessage(Stream stream, string json)
        {
            byte[] payload = Encoding.UTF8.GetBytes(json);
            byte[] length = BitConverter.GetBytes(payload.Length);
            if (!BitConverter.IsLittleEndian) Array.Reverse(length);
            stream.Write(length, 0, length.Length);
            stream.Write(payload, 0, payload.Length);
            stream.Flush();
        }

        private static byte[] ReadExactly(Stream stream, int count)
        {
            byte[] bytes = new byte[count];
            int read = 0, n;
            while (read < count && (n = stream.Read(bytes, read, count - read)) > 0) read += n;
            if (read != count) throw new EndOfStreamException("浏览器消息不完整。");
            return bytes;
        }

        private static bool Enqueue(string url)
        {
            Directory.CreateDirectory(Pending);
            Directory.CreateDirectory(Active);
            string key;
            using (SHA256 sha = SHA256.Create())
                key = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(url))).Replace("-", "").ToLowerInvariant();
            string name = key + ".job";
            if (File.Exists(Path.Combine(Pending, name)) || File.Exists(Path.Combine(Active, name))) return true;
            string tmp = Path.Combine(Pending, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(tmp, url, new UTF8Encoding(false));
                try { File.Move(tmp, Path.Combine(Pending, name)); }
                catch (IOException)
                {
                    if (File.Exists(Path.Combine(Pending, name))) return true;
                    throw;
                }
            }
            finally { try { if (File.Exists(tmp)) File.Delete(tmp); } catch { } }
            return false;
        }

        private static bool StartWorker()
        {
            try
            {
                var info = new ProcessStartInfo
                {
                    FileName = Process.GetCurrentProcess().MainModule.FileName,
                    Arguments = "--silent-worker",
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (Process worker = Process.Start(info)) return worker != null;
            }
            catch (Exception ex)
            {
                WriteLog("后台进程启动失败：" + ex.Message);
                return false;
            }
        }

        internal static void RunWorker()
        {
            Directory.CreateDirectory(Pending);
            Directory.CreateDirectory(Active);
            using (var mutex = new Mutex(false, MutexName))
            {
                bool acquired = false;
                try
                {
                    try { acquired = mutex.WaitOne(0); }
                    catch (AbandonedMutexException) { acquired = true; }
                    if (!acquired) return;

                    // Own a persistent notification-area icon; no taskbar button or
                    // unsolicited main window is created until the user opens one.
                    System.Windows.Forms.Application.EnableVisualStyles();
                    System.Windows.Forms.Application.SetCompatibleTextRenderingDefault(false);
                    using (var tray = new SilentTrayContext())
                        System.Windows.Forms.Application.Run(tray);
                }
                catch (Exception ex) { WriteLog("后台托盘异常：" + ex); }
                finally { if (acquired) mutex.ReleaseMutex(); }
            }
        }

        internal static void RunWorkerLoop(CancellationToken cancellationToken)
        {
            RecoverInterruptedJobs();
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    string job = Directory.GetFiles(Pending, "*.job")
                        .OrderBy(path => File.GetCreationTimeUtc(path)).FirstOrDefault();
                    if (job == null)
                    {
                        cancellationToken.WaitHandle.WaitOne(350);
                        continue;
                    }
                    string running = Path.Combine(Active, Path.GetFileName(job));
                    try { File.Move(job, running); }
                    catch (IOException) { continue; }
                    ProcessJobAsync(running, cancellationToken).GetAwaiter().GetResult();
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    WriteLog("队列处理异常：" + ex);
                    cancellationToken.WaitHandle.WaitOne(500);
                }
            }
        }

        internal static int PendingCount
        {
            get
            {
                try { return Directory.Exists(Pending) ? Directory.GetFiles(Pending, "*.job").Length : 0; }
                catch { return 0; }
            }
        }

        internal static string ActiveUrl
        {
            get
            {
                try
                {
                    if (!Directory.Exists(Active)) return null;
                    string file = Directory.GetFiles(Active, "*.job").FirstOrDefault();
                    return file == null ? null : ValidatePageUrl(File.ReadAllText(file, Encoding.UTF8));
                }
                catch { return null; }
            }
        }

        internal static string ReadRecentLog()
        {
            try
            {
                if (!File.Exists(LogPath)) return "";
                using (var stream = new FileStream(LogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
                {
                    // Cap UI log I/O even after thousands of downloads.
                    int length = (int)Math.Min(32768L, stream.Length);
                    if (length == 0) return "";
                    stream.Seek(-length, SeekOrigin.End);
                    byte[] bytes = new byte[length];
                    int offset = 0;
                    while (offset < length)
                    {
                        int read = stream.Read(bytes, offset, length - offset);
                        if (read == 0) break;
                        offset += read;
                    }
                    string raw = Encoding.UTF8.GetString(bytes, 0, offset);
                    string[] lines = raw.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    return String.Join(Environment.NewLine, lines.Skip(Math.Max(0, lines.Length - 70)));
                }
            }
            catch { return ""; }
        }

        internal static string SilentFolder
        {
            get
            {
                try
                {
                    YtDlpOptions options;
                    string folder, ytDlp, ffmpeg, ffprobe, deno;
                    LoadConfiguration(out options, out folder, out ytDlp, out ffmpeg, out ffprobe, out deno);
                    return folder;
                }
                catch { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads", "Silent"); }
            }
        }

        private static void RecoverInterruptedJobs()
        {
            foreach (string active in Directory.GetFiles(Active, "*.job"))
            {
                string pending = Path.Combine(Pending, Path.GetFileName(active));
                try { if (File.Exists(pending)) File.Delete(active); else File.Move(active, pending); }
                catch (IOException ex) { WriteLog("恢复未完成任务失败：" + ex.Message); }
            }
        }

        private static async Task ProcessJobAsync(string job, CancellationToken cancellationToken)
        {
            string url = "";
            bool interrupted = false;
            try
            {
                url = ValidatePageUrl(File.ReadAllText(job, Encoding.UTF8));
                if (url == null) throw new InvalidDataException("队列任务中的网址无效。");
                YtDlpOptions options;
                string folder, ytDlp, ffmpeg, ffprobe, deno;
                LoadConfiguration(out options, out folder, out ytDlp, out ffmpeg, out ffprobe, out deno);
                if (String.IsNullOrWhiteSpace(ytDlp) || !File.Exists(ytDlp))
                    throw new FileNotFoundException("未配置可用的 yt-dlp.exe；请在 dotSharK 主窗口配置。");
                Directory.CreateDirectory(folder);
                WriteLog("[开始] " + url);
                var client = new YtDlpClient(ytDlp, ffmpeg, ffprobe, deno);
                await client.DownloadSilentAsync(url, folder, ArchivePath, options,
                    line => { if (!String.IsNullOrWhiteSpace(line)) WriteLog("[yt-dlp] " + line); },
                    cancellationToken);
                WriteLog("[完成] " + url);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                interrupted = true;
                WriteLog("[中断，保留待续] " + url);
            }
            catch (Exception ex) { WriteLog("[失败] " + url + " · " + ex.Message); }
            finally
            {
                try
                {
                    if (interrupted)
                    {
                        string target = Path.Combine(Pending, Path.GetFileName(job));
                        if (File.Exists(target)) File.Delete(job);
                        else File.Move(job, target);
                    }
                    else File.Delete(job);
                }
                catch (Exception ex) { WriteLog("保存任务状态失败：" + ex.Message); }
            }
        }

        private static void LoadConfiguration(out YtDlpOptions options, out string folder,
            out string ytDlp, out string ffmpeg, out string ffprobe, out string deno)
        {
            options = new YtDlpOptions();
            folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads", "Silent");
            ytDlp = ffmpeg = ffprobe = deno = "";
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dotSharK.ini");
            if (File.Exists(path))
            {
                foreach (string line in File.ReadAllLines(path, Encoding.UTF8))
                {
                    int split = line.IndexOf('=');
                    if (split < 0) continue;
                    string key = line.Substring(0, split);
                    string value;
                    try { value = Uri.UnescapeDataString(line.Substring(split + 1)); }
                    catch { continue; }
                    int n;
                    switch (key)
                    {
                        case "silent-folder": if (!String.IsNullOrWhiteSpace(value)) folder = value; break;
                        case "yt-dlp": ytDlp = value; break;
                        case "ffmpeg": ffmpeg = value; break;
                        case "ffprobe": ffprobe = value; break;
                        case "deno": deno = value; break;
                        case "fragments": if (Int32.TryParse(value, out n)) options.ConcurrentFragments = Math.Max(1, Math.Min(32, n)); break;
                        case "proxy": options.Proxy = Unprotect(value); break;
                        case "geo-proxy": options.GeoVerificationProxy = Unprotect(value); break;
                        case "cookie-mode": options.CookieMode = value == "file" ? "file" : "browser"; break;
                        case "cookie-browser": options.CookieBrowser = value; break;
                        case "cookie-profile": options.CookieProfile = value; break;
                        case "cookie-root": options.CookieProfileRoot = value; break;
                        case "cookie-file": options.CookieFile = value; break;
                    }
                }
            }
            ytDlp = FindTool(ytDlp, "yt-dlp.exe");
            ffmpeg = FindTool(ffmpeg, "ffmpeg.exe");
            ffprobe = FindTool(ffprobe, "ffprobe.exe");
            deno = FindTool(deno, "deno.exe");
        }

        private static string FindTool(string configured, string name)
        {
            if (!String.IsNullOrWhiteSpace(configured) && File.Exists(configured)) return configured;
            string tools = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", name);
            if (File.Exists(tools)) return tools;
            string beside = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, name);
            return File.Exists(beside) ? beside : "";
        }

        private static string Unprotect(string text)
        {
            try
            {
                byte[] data = Convert.FromBase64String(text);
                return Encoding.UTF8.GetString(ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser));
            }
            catch { return ""; }
        }

        private static void WriteLog(string message)
        {
            try
            {
                Directory.CreateDirectory(DataRoot);
                File.AppendAllText(LogPath,
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message + Environment.NewLine,
                    new UTF8Encoding(false));
            }
            catch { }
        }
    }
}
