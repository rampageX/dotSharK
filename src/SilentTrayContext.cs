using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace dotSharK
{
    // One WinForms message loop / MainForm / tray icon. In particular, never
    // change ShowInTaskbar after the handle is created: WinForms recreates
    // top-level handles in response, invalidating queued UI callbacks.
    internal sealed class SilentTrayContext : ApplicationContext
    {
        private const int SW_SHOW = 5;
        private const int SW_SHOWMAXIMIZED = 3;
        private const int SW_RESTORE = 9;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool ShowWindow(IntPtr hWnd, int command);
        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        private readonly NotifyIcon notification;
        private readonly Icon trayIcon;
        private readonly ContextMenuStrip menu;
        private readonly System.Windows.Forms.Timer refreshTimer;
        private readonly MainFormV2 mainWindow;
        private bool exiting;
        private bool hiding;
        private bool checkRestore;
        private FormWindowState mainRestoreState;

        private static readonly string UiLog = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Logs", "tray-ui.log");

        internal SilentTrayContext(bool showMain)
        {
            trayIcon = AppBranding.CreateIcon();
            mainWindow = new MainFormV2();
            mainRestoreState = mainWindow.WindowState == FormWindowState.Maximized
                ? FormWindowState.Maximized : FormWindowState.Normal;

            mainWindow.HandleCreated += (s, e) => TraceState("HandleCreated");
            mainWindow.HandleDestroyed += (s, e) => TraceState("HandleDestroyed");
            mainWindow.TrayMinimizeRequested += (s, e) => HideMainWindow("SC_MINIMIZE");
            mainWindow.FormClosing += OnMainWindowClosing;
            mainWindow.Resize += (s, e) =>
            {
                if (mainWindow.IsDisposed || exiting || hiding) return;
                if (mainWindow.WindowState == FormWindowState.Minimized)
                {
                    // Fallback for programmatic WindowState changes and Win+M.
                    // Native minimize already animates; hide after the message
                    // finishes, never toggle ShowInTaskbar or recreate the HWND.
                    if (mainWindow.IsHandleCreated)
                        mainWindow.BeginInvoke((Action)(() =>
                        {
                            if (!exiting && !hiding && !mainWindow.IsDisposed &&
                                mainWindow.Visible && mainWindow.WindowState == FormWindowState.Minimized)
                            {
                                TraceState("Native minimize fallback");
                                HideMainWindow("WM_SIZE_MINIMIZED");
                            }
                        }));
                }
                else mainRestoreState = mainWindow.WindowState;
            };

            // Keep a stable HWND even when the first launch comes from a
            // browser and the main window starts out hidden.
            var hwnd = mainWindow.Handle;

            menu = new ContextMenuStrip
            {
                BackColor = DarkPalette.Surface,
                ForeColor = DarkPalette.Foreground,
                ShowImageMargin = false
            };
            var openMain = new ToolStripMenuItem("打开 dotSharK");
            openMain.Click += (s, e) => OpenMainWindow();
            var openFolder = new ToolStripMenuItem("打开浏览器下载目录");
            openFolder.Click += (s, e) => OpenDownloadFolder();
            var exit = new ToolStripMenuItem("退出 dotSharK");
            exit.Click += async (s, e) => await StopAndExitAsync(exit);
            menu.Items.Add(openMain);
            menu.Items.Add(openFolder);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exit);

            notification = new NotifyIcon
            {
                Icon = trayIcon,
                Text = "dotSharK · 统一下载队列",
                ContextMenuStrip = menu,
                Visible = true
            };
            notification.MouseDoubleClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) OpenMainWindow();
            };

            refreshTimer = new System.Windows.Forms.Timer { Interval = 450 };
            refreshTimer.Tick += (s, e) =>
            {
                if (exiting) return;
                ProcessIncomingJobs();
                if (SilentDownloadBridge.ConsumeShowMainRequest()) OpenMainWindow();
                if (checkRestore)
                {
                    checkRestore = false;
                    CheckRestoreVisibility();
                }
                UpdateTooltip();
            };
            refreshTimer.Start();
            TraceState("Tray ready");
            if (showMain) OpenMainWindow();
        }

        private void ProcessIncomingJobs()
        {
            foreach (var request in SilentDownloadBridge.ClaimPendingJobs())
                mainWindow.EnqueueBrowserUrl(request.Url, request.JobPath);
        }

        private void OnMainWindowClosing(object sender, FormClosingEventArgs e)
        {
            if (exiting || e.CloseReason != CloseReason.UserClosing) return;
            e.Cancel = true;
            HideMainWindow("SC_CLOSE");
        }

        private void HideMainWindow(string reason)
        {
            if (exiting || hiding || mainWindow.IsDisposed || !mainWindow.Visible) return;
            hiding = true;
            try
            {
                if (mainWindow.WindowState != FormWindowState.Minimized)
                    mainRestoreState = mainWindow.WindowState;
                TraceState("Hide requested " + reason);

                // AnimateWindow(AW_BLEND) can expose an uncomposed Win32
                // nonclient frame on dark WinForms windows (Aero-like flash).
                // One direct hide is both compositor-safe and handle-stable.
                mainWindow.Hide();
                checkRestore = false;
                TraceState("Hidden in tray");
            }
            finally { hiding = false; }
        }

        private void OpenMainWindow()
        {
            if (exiting || mainWindow.IsDisposed) return;
            if (hiding)
            {
                // A fast tray double-click during a hide transition must not
                // be lost; defer until that transition finishes.
                if (mainWindow.IsHandleCreated)
                    mainWindow.BeginInvoke((Action)(() => OpenMainWindow()));
                return;
            }
            TraceState("Restore requested");
            FormWindowState desiredState = mainRestoreState;
            try
            {
                // Apply the desired state BEFORE revealing the window.
                // Previously a maximized form passed through Normal -> Show
                // -> Maximized, briefly painting the normal caption/frame.
                if (mainWindow.WindowState != desiredState)
                    mainWindow.WindowState = desiredState;
                EnsureOnScreen();
                if (!mainWindow.Visible) mainWindow.Show();

                IntPtr hwnd = mainWindow.Handle;
                if (IsIconic(hwnd))
                    ShowWindow(hwnd, desiredState == FormWindowState.Maximized
                        ? SW_SHOWMAXIMIZED : SW_RESTORE);
                else if (!IsWindowVisible(hwnd))
                    ShowWindow(hwnd, desiredState == FormWindowState.Maximized
                        ? SW_SHOWMAXIMIZED : SW_SHOW);
                mainRestoreState = desiredState;

                // The tray double-click is a user gesture and is eligible
                // for foreground activation. No TopMost trick or handle reset.
                BringWindowToTop(hwnd);
                mainWindow.BringToFront();
                mainWindow.Activate();
                SetForegroundWindow(hwnd);
                checkRestore = true;
                TraceState("Restore attempted");
            }
            catch (Exception ex)
            {
                TraceState("Restore ERROR " + ex);
            }
        }

        private void CheckRestoreVisibility()
        {
            if (exiting || mainWindow.IsDisposed) return;
            IntPtr hwnd = mainWindow.IsHandleCreated ? mainWindow.Handle : IntPtr.Zero;
            if (hwnd == IntPtr.Zero || !IsWindow(hwnd) || !mainWindow.Visible ||
                !IsWindowVisible(hwnd) || IsIconic(hwnd))
            {
                TraceState("Restore verification failed; native repair");
                // Repair an unexpectedly hidden or iconic Win32 window once.
                // Do not force focus if the user has since switched apps.
                if (mainWindow.WindowState == FormWindowState.Minimized)
                    mainWindow.WindowState = FormWindowState.Normal;
                if (!mainWindow.Visible) mainWindow.Show();
                hwnd = mainWindow.Handle;
                ShowWindow(hwnd, IsIconic(hwnd) ? SW_RESTORE : SW_SHOW);
                TraceState("Restore repair completed");
            }
            else TraceState("Restore verification OK");
        }

        private void EnsureOnScreen()
        {
            if (mainWindow.WindowState != FormWindowState.Normal) return;
            Rectangle bounds = mainWindow.Bounds;
            foreach (Screen screen in Screen.AllScreens)
            {
                Rectangle overlap = Rectangle.Intersect(bounds, screen.WorkingArea);
                if (overlap.Width >= 80 && overlap.Height >= 60) return;
            }
            Screen primary = Screen.PrimaryScreen;
            if (primary == null) return;
            Rectangle work = primary.WorkingArea;
            int x = work.Left + Math.Max(0, (work.Width - bounds.Width) / 2);
            int y = work.Top + Math.Max(0, (work.Height - bounds.Height) / 2);
            mainWindow.Location = new Point(x, y);
            TraceState("Recentered off-screen window");
        }

        private void TraceState(string action)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(UiLog));
                var info = new FileInfo(UiLog);
                if (info.Exists && info.Length > 150 * 1024)
                {
                    string rotated = UiLog + ".old";
                    if (File.Exists(rotated)) File.Delete(rotated);
                    File.Move(UiLog, rotated);
                }
                IntPtr hwnd = mainWindow != null && mainWindow.IsHandleCreated
                    ? mainWindow.Handle : IntPtr.Zero;
                string line = String.Format(
                    "{0:yyyy-MM-dd HH:mm:ss.fff} {1} | HWND={2} Win32Visible={3} IsIconic={4} Foreground={5} ManagedVisible={6} State={7} ShowInTaskbar={8} Bounds={9}",
                    DateTime.Now, action, hwnd.ToInt64().ToString("X"),
                    hwnd != IntPtr.Zero && IsWindowVisible(hwnd),
                    hwnd != IntPtr.Zero && IsIconic(hwnd),
                    GetForegroundWindow().ToInt64().ToString("X"),
                    mainWindow != null && mainWindow.Visible,
                    mainWindow != null ? mainWindow.WindowState.ToString() : "N/A",
                    mainWindow != null && mainWindow.ShowInTaskbar,
                    mainWindow != null ? mainWindow.Bounds.ToString() : "N/A");
                File.AppendAllText(UiLog, line + Environment.NewLine);
            }
            catch { /* UI diagnostics must never block activation. */ }
        }

        private void UpdateTooltip()
        {
            int active = mainWindow.ActiveDownloadCount;
            int pending = mainWindow.PendingDownloadCount + SilentDownloadBridge.PendingFileCount;
            string caption = "dotSharK · " + (active > 0 ? "下载中 " + active : "就绪");
            if (pending > 0) caption += " · 等待 " + pending;
            if (!String.Equals(caption, notification.Text, StringComparison.Ordinal))
                notification.Text = caption;
        }

        private static void OpenDownloadFolder()
        {
            try
            {
                string folder = SilentDownloadBridge.SilentFolder;
                Directory.CreateDirectory(folder);
                Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show("无法打开浏览器下载目录：" + ex.Message,
                    "dotSharK", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private async Task StopAndExitAsync(ToolStripMenuItem exitItem)
        {
            if (exiting) return;
            exiting = true;
            exitItem.Enabled = false;
            refreshTimer.Stop();
            TraceState("Exit requested");
            await mainWindow.CancelQueueForExitAsync();
            ExitThread();
        }

        protected override void ExitThreadCore()
        {
            exiting = true;
            refreshTimer.Stop();
            notification.Visible = false;
            if (!mainWindow.IsDisposed) mainWindow.Close();
            base.ExitThreadCore();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                refreshTimer.Dispose();
                notification.Dispose();
                menu.Dispose();
                trayIcon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
