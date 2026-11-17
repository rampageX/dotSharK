using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace dotSharK
{
    // The browser-launched worker owns the tray icon. It keeps its own queue alive
    // without displaying a window, and opens the ordinary main form on demand.
    internal sealed class SilentTrayContext : ApplicationContext
    {
        private readonly CancellationTokenSource shutdown = new CancellationTokenSource();
        private readonly NotifyIcon notification;
        private readonly Icon trayIcon;
        private readonly ContextMenuStrip menu;
        private readonly System.Windows.Forms.Timer refreshTimer;
        private readonly Task worker;
        private MainForm mainWindow;
        private SilentStatusDialog statusWindow;
        private bool exiting;
        private FormWindowState mainRestoreState = FormWindowState.Normal;

        internal SilentTrayContext()
        {
            trayIcon = AppBranding.CreateIcon();
            menu = new ContextMenuStrip
            {
                BackColor = DarkPalette.Surface,
                ForeColor = DarkPalette.Foreground,
                ShowImageMargin = false
            };
            var openMain = new ToolStripMenuItem("打开 dotSharK 主界面");
            openMain.Click += (s, e) => OpenMainWindow();
            var openStatus = new ToolStripMenuItem("查看静默下载状态…");
            openStatus.Click += (s, e) => OpenStatusWindow();
            var openFolder = new ToolStripMenuItem("打开静默下载目录");
            openFolder.Click += (s, e) => OpenDownloadFolder();
            var exit = new ToolStripMenuItem("退出 dotSharK 后台");
            exit.Click += async (s, e) => await StopAndExitAsync(exit);
            menu.Items.Add(openMain);
            menu.Items.Add(openStatus);
            menu.Items.Add(openFolder);
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(exit);
            notification = new NotifyIcon
            {
                Icon = trayIcon,
                Text = "dotSharK · 静默下载队列",
                ContextMenuStrip = menu,
                Visible = true
            };
            notification.MouseDoubleClick += (s, e) =>
            {
                if (e.Button == MouseButtons.Left) OpenMainWindow();
            };
            refreshTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            refreshTimer.Tick += (s, e) => RefreshTooltip();
            refreshTimer.Start();
            worker = Task.Run(() => SilentDownloadBridge.RunWorkerLoop(shutdown.Token));
        }

        private void OpenMainWindow()
        {
            if (exiting) return;
            if (mainWindow == null || mainWindow.IsDisposed)
            {
                mainWindow = new MainForm();
                mainWindow.FormClosed += (s, e) => mainWindow = null;
                mainWindow.Resize += (s, e) =>
                {
                    if (mainWindow == null || mainWindow.IsDisposed) return;
                    if (mainWindow.WindowState == FormWindowState.Minimized)
                        mainWindow.Hide(); // Minimize to notification area, not the taskbar.
                    else mainRestoreState = mainWindow.WindowState;
                };
                mainWindow.Show();
                mainRestoreState = mainWindow.WindowState == FormWindowState.Maximized
                    ? FormWindowState.Maximized : FormWindowState.Normal;
            }
            else
            {
                if (mainWindow.WindowState == FormWindowState.Minimized)
                    mainWindow.WindowState = mainRestoreState;
                mainWindow.Show();
                mainWindow.BringToFront();
                mainWindow.Activate();
            }
        }

        private void OpenStatusWindow()
        {
            if (exiting) return;
            if (statusWindow == null || statusWindow.IsDisposed)
            {
                statusWindow = new SilentStatusDialog();
                statusWindow.FormClosed += (s, e) => statusWindow = null;
                statusWindow.Show();
            }
            else
            {
                if (statusWindow.WindowState == FormWindowState.Minimized)
                    statusWindow.WindowState = FormWindowState.Normal;
                statusWindow.Show();
                statusWindow.BringToFront();
                statusWindow.Activate();
            }
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
                MessageBox.Show("无法打开静默下载目录：" + ex.Message, "dotSharK",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void RefreshTooltip()
        {
            if (notification == null || !notification.Visible) return;
            string info = SilentDownloadBridge.ActiveUrl != null ? "正在下载" : "就绪";
            int waiting = SilentDownloadBridge.PendingCount;
            string caption = "dotSharK · " + info + (waiting > 0 ? " · 排队 " + waiting : "");
            if (!String.Equals(caption, notification.Text, StringComparison.Ordinal))
                notification.Text = caption;
        }

        private async Task StopAndExitAsync(ToolStripMenuItem exitItem)
        {
            if (exiting) return;
            exiting = true;
            exitItem.Enabled = false;
            shutdown.Cancel();
            // Give yt-dlp time to stop and return the current job to the pending queue.
            await Task.WhenAny(worker, Task.Delay(5000));
            ExitThread();
        }

        protected override void ExitThreadCore()
        {
            shutdown.Cancel();
            refreshTimer.Stop();
            notification.Visible = false;
            if (statusWindow != null && !statusWindow.IsDisposed) statusWindow.Close();
            if (mainWindow != null && !mainWindow.IsDisposed) mainWindow.Close();
            base.ExitThreadCore();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                shutdown.Cancel();
                refreshTimer.Dispose();
                notification.Dispose();
                menu.Dispose();
                trayIcon.Dispose();
                shutdown.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
