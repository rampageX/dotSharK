using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace dotSharK
{
    // The browser host only stores incoming URLs; the ONE WinForms application
    // owns the tray icon, the main-window queue and all yt-dlp jobs.
    internal static class SilentDownloadBridge
    {
        private static readonly string DataRoot = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "SilentQueue");
        private static readonly string Pending = Path.Combine(DataRoot, "Pending");
        private static readonly string Active = Path.Combine(DataRoot, "Active");
        private static readonly string ShowRequest = Path.Combine(DataRoot, "show-main.request");
        private static readonly string InstanceMutex = CreateInstanceMutex();

        private static string CreateInstanceMutex()
        {
            // Separate portable copies must not consume each other's queue or show requests.
            string root = Path.GetFullPath(AppDomain.CurrentDomain.BaseDirectory).TrimEnd(Path.DirectorySeparatorChar).ToUpperInvariant();
            using (SHA256 sha = SHA256.Create())
                return @"Local\dotSharK.UnifiedQueue." + BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(root))).Replace("-", "");
        }

        internal sealed class BrowserRequest
        {
            internal string Url;
            internal string JobPath;
        }


        internal static bool IsNativeHostCall(string[] args)
        {
            return args != null && args.Length > 0 &&
                (args[0].StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase) ||
                 (args.Length >= 2 && args[1] == BrowserPairing.FirefoxId &&
                  String.Equals(Path.GetFileName(args[0]), BrowserPairing.Host + ".json", StringComparison.OrdinalIgnoreCase)));
        }

        internal static void RunNativeHost(bool firefox)
        {
            object response;
            try
            {
                string json = ReadNativeMessage(Console.OpenStandardInput());
                var request = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(json);
                object value;
                if (request != null && request.TryGetValue("action", out value) && (value as string) == "ping")
                {
                    WriteNativeMessage(Console.OpenStandardOutput(), "{\"ok\":true}");
                    return;
                }
                string url = request != null && request.TryGetValue("url", out value) ? value as string : null;
                url = ValidatePageUrl(url);
                if (url == null) throw new InvalidDataException("必须发送单个 HTTP/HTTPS 网页链接。");
                bool duplicate = Enqueue(url);
                bool launched = StartHostProcess(firefox);
                response = new { ok = launched, duplicate = duplicate,
                    error = launched ? "" : "任务已保存，但 dotSharK 后台进程启动失败。" };
            }
            catch (Exception ex)
            {
                response = new { ok = false, duplicate = false, error = ex.Message };
            }
            try { WriteNativeMessage(Console.OpenStandardOutput(), new JavaScriptSerializer().Serialize(response)); }
            catch { }
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
            if (length < 2 || length > 65536)
                throw new InvalidDataException("浏览器消息大小无效：" + BitConverter.ToString(size));
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
                key = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(url)))
                    .Replace("-", "").ToLowerInvariant();
            string name = key + ".job";
            if (File.Exists(Path.Combine(Pending, name)) || File.Exists(Path.Combine(Active, name)))
                return true;
            string tmp = Path.Combine(Pending, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                File.WriteAllText(tmp, url, new UTF8Encoding(false));
                try { File.Move(tmp, Path.Combine(Pending, name)); }
                catch (IOException)
                {
                    if (File.Exists(Path.Combine(Pending, name)) ||
                        File.Exists(Path.Combine(Active, name))) return true;
                    throw;
                }
            }
            finally { try { if (File.Exists(tmp)) File.Delete(tmp); } catch { } }
            return false;
        }

        private static bool StartHostProcess(bool firefox)
        {
            try
            {
                if (firefox) return StartDetachedWorker();
                var info = new ProcessStartInfo
                {
                    FileName = Process.GetCurrentProcess().MainModule.FileName,
                    Arguments = "--silent-worker",
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                using (Process p = Process.Start(info)) return p != null;
            }
            catch { return false; }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct StartupInfo
        {
            public int cb; public string reserved, desktop, title;
            public int x, y, xSize, ySize, xCountChars, yCountChars, fillAttribute, flags;
            public short showWindow, reserved2; public IntPtr reservedPtr, stdInput, stdOutput, stdError;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessInformation { public IntPtr process, thread; public int processId, threadId; }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateProcess(string app, StringBuilder command, IntPtr processAttributes,
            IntPtr threadAttributes, bool inheritHandles, uint flags, IntPtr environment, string directory,
            ref StartupInfo startup, out ProcessInformation process);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);

        private static bool StartDetachedWorker()
        {
            string exe = Process.GetCurrentProcess().MainModule.FileName;
            var startup = new StartupInfo { cb = Marshal.SizeOf(typeof(StartupInfo)) };
            ProcessInformation process;
            // Firefox owns the native host's job; the resident queue must survive its exit.
            if (!CreateProcess(exe, new StringBuilder("\"" + exe + "\" --silent-worker"), IntPtr.Zero,
                IntPtr.Zero, false, 0x01000000 | 0x08000000, IntPtr.Zero,
                AppDomain.CurrentDomain.BaseDirectory, ref startup, out process)) return false;
            CloseHandle(process.thread); CloseHandle(process.process); return true;
        }

        internal static void RunApplication(bool showMain)
        {
            Directory.CreateDirectory(Pending);
            Directory.CreateDirectory(Active);
            using (var mutex = new Mutex(false, InstanceMutex))
            {
                bool acquired = false;
                try
                {
                    try { acquired = mutex.WaitOne(0); }
                    catch (AbandonedMutexException) { acquired = true; }
                    if (!acquired)
                    {
                        // A normal launch while tray is already alive must restore that
                        // SAME main window, not create an unresponsive second window.
                        if (showMain) RequestShowMain();
                        return;
                    }

                    RecoverInterruptedJobs();
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);
                    using (var tray = new SilentTrayContext(showMain))
                        Application.Run(tray);
                }
                finally { if (acquired) mutex.ReleaseMutex(); }
            }
        }

        internal static void RequestShowMain()
        {
            try
            {
                Directory.CreateDirectory(DataRoot);
                File.WriteAllText(ShowRequest, "show");
            }
            catch { }
        }

        internal static bool ConsumeShowMainRequest()
        {
            try
            {
                if (!File.Exists(ShowRequest)) return false;
                File.Delete(ShowRequest);
                return true;
            }
            catch { return false; }
        }

        internal static List<BrowserRequest> ClaimPendingJobs()
        {
            Directory.CreateDirectory(Pending);
            Directory.CreateDirectory(Active);
            var result = new List<BrowserRequest>();
            foreach (string file in Directory.GetFiles(Pending, "*.job")
                .OrderBy(path => File.GetCreationTimeUtc(path)))
            {
                string active = Path.Combine(Active, Path.GetFileName(file));
                try
                {
                    File.Move(file, active);
                    string url = ValidatePageUrl(File.ReadAllText(active, Encoding.UTF8));
                    if (url == null) { File.Delete(active); continue; }
                    result.Add(new BrowserRequest { Url = url, JobPath = active });
                }
                catch (IOException) { /* Another request/worker may have claimed the file. */ }
                catch (UnauthorizedAccessException) { }
            }
            return result;
        }

        internal static void CompleteJob(string jobPath)
        {
            if (String.IsNullOrWhiteSpace(jobPath)) return;
            try { if (File.Exists(jobPath)) File.Delete(jobPath); }
            catch { }
        }

        // A cancelled or interrupted job stays in Active until this application
        // closes. On the next launch it is restored to Pending exactly once.
        private static void RecoverInterruptedJobs()
        {
            foreach (string file in Directory.GetFiles(Active, "*.job"))
            {
                string pending = Path.Combine(Pending, Path.GetFileName(file));
                try { if (File.Exists(pending)) File.Delete(file); else File.Move(file, pending); }
                catch (IOException) { }
            }
        }

        internal static int PendingFileCount
        {
            get
            {
                try { return Directory.GetFiles(Pending, "*.job").Length; }
                catch { return 0; }
            }
        }

        internal static string SilentFolder
        {
            get
            {
                string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads", "Silent");
                string settings = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "dotSharK.ini");
                try
                {
                    if (!File.Exists(settings)) return folder;
                    foreach (string line in File.ReadAllLines(settings, Encoding.UTF8))
                    {
                        if (!line.StartsWith("silent-folder=", StringComparison.Ordinal)) continue;
                        string value = Uri.UnescapeDataString(line.Substring("silent-folder=".Length));
                        if (!String.IsNullOrWhiteSpace(value) && Path.IsPathRooted(value))
                            folder = Path.GetFullPath(value);
                        break;
                    }
                }
                catch { }
                return folder;
            }
        }
    }
}
