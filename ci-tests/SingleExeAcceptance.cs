using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

internal static class SingleExeAcceptance
{
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    static int checks;
    static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
        Console.WriteLine("PASS " + (++checks) + " " + message);
    }
    [STAThread]
    static int Main()
    {
        try
        {
            string root = AppDomain.CurrentDomain.BaseDirectory;
            string exe = Path.Combine(root, "dotSharK.exe");
            Check(!Directory.GetFiles(root, "*.dll", SearchOption.AllDirectories).Any(), "standalone directory has no DLL");
            Assembly app = Assembly.LoadFrom(exe);
            Check(app.GetManifestResourceNames().Contains("dotSharK.Embedded.DotSharK.UiFoundation.dll"), "EXE contains UI resource");
            Type loader = app.GetType("dotSharK.EmbeddedUiAssembly", true);
            loader.GetMethod("Initialize", Flags).Invoke(null, null);
            loader.GetMethod("Initialize", Flags).Invoke(null, null);
            Assembly ui = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "DotSharK.UiFoundation");
            Check(String.IsNullOrEmpty(ui.Location), "UI assembly loaded from memory without extraction");
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var main = (Form)Activator.CreateInstance(app.GetType("dotSharK.MainFormV2"), Flags, null, new object[0], null))
            {
                main.Show(); Application.DoEvents();
                Check(main.Visible && main.Controls.Count > 0, "main UI opens without external DLL");
            }
            var options = Activator.CreateInstance(app.GetType("dotSharK.YtDlpOptions"), true);
            using (var settings = (Form)Activator.CreateInstance(app.GetType("dotSharK.ConfigurationDialogV2"), Flags, null,
                new object[] { "", "", "", "", "", options }, null))
            {
                settings.Show(); Application.DoEvents();
                Check(settings.Visible && settings.Controls.Count > 0, "settings UI opens without external DLL");
            }
            using (var process = Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false, WorkingDirectory = root }))
            {
                try
                {
                    DateTime deadline = DateTime.UtcNow.AddSeconds(15);
                    while (!process.HasExited && process.MainWindowHandle == IntPtr.Zero && DateTime.UtcNow < deadline)
                    { Thread.Sleep(100); process.Refresh(); }
                    Check(!process.HasExited && process.MainWindowHandle != IntPtr.Zero, "actual EXE startup shows window without DLL");
                }
                finally { if (!process.HasExited) { process.Kill(); process.WaitForExit(); } }
            }
            using (var process = Process.Start(new ProcessStartInfo(exe, "--silent-worker") { UseShellExecute = false, WorkingDirectory = root }))
            {
                try
                {
                    Thread.Sleep(1200); process.Refresh();
                    Check(!process.HasExited && process.MainWindowHandle == IntPtr.Zero, "silent worker starts hidden without DLL");
                }
                finally { if (!process.HasExited) { process.Kill(); process.WaitForExit(); } }
            }
            var info = new ProcessStartInfo(exe, "chrome-extension://aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa/")
            {
                UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = root,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            using (var process = Process.Start(info))
            {
                byte[] payload = Encoding.UTF8.GetBytes("{\"url\":\"file:///invalid\"}");
                process.StandardInput.BaseStream.Write(BitConverter.GetBytes(payload.Length), 0, 4);
                process.StandardInput.BaseStream.Write(payload, 0, payload.Length);
                process.StandardInput.Close();
                using (var reader = new BinaryReader(process.StandardOutput.BaseStream, Encoding.UTF8))
                {
                    int length = reader.ReadInt32();
                    string reply = Encoding.UTF8.GetString(reader.ReadBytes(length));
                    Check(reply.Contains("\"ok\":false"), "native messaging returns valid protocol without DLL");
                }
                Check(process.WaitForExit(10000) && process.ExitCode == 0, "native host exits cleanly without GUI");
            }
            Check(!Directory.GetFiles(root, "*.dll", SearchOption.AllDirectories).Any(), "no UI DLL extracted during startup or UI use");
            Console.WriteLine("PASS " + checks + " standalone EXE assertions");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
