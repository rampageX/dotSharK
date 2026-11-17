using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace dotSharK
{
    // One application, one MainForm, one download queue, one tray icon.
    // The browser merely drops requests into the inbox, which this UI thread drains.
    internal sealed class SilentTrayContext : ApplicationContext
    {
        private readonly NotifyIcon notification;
        private readonly Icon trayIcon;
        private readonly ContextMenuStrip menu;
        private readonly System.Windows.Forms.Timer refreshTimer;
        private readonly MainForm mainWindow;
        private bool exiting;
        private FormWindowState mainRestoreState = FormWindowState.Normal;

        internal SilentTrayContext(bool showMain)
        {
            trayIcon = AppBranding.CreateIcon();
            mainWindow = new MainForm();
            // Ensure BeginInvoke and progress updates work even before Show().
            var handle = mainWindow.Handle;
            mainWindow.FormClosing += OnMainWindowClosing;
            mainWindow.Resize += (s, e) =>
            {
                if (mainWindow.IsDisposed || exiting) return;
                if (mainWindow.WindowState == FormWindowState.Minimized)
                {
                    mainWindow.ShowInTaskbar = false;
                    mainWindow.Hide();
                }
                else mainRestoreState = mainWindow.WindowState;
            };

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
                UpdateTooltip();
            };
            refreshTimer.Start();
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
            mainWindow.ShowInTaskbar = false;
            mainWindow.Hide();
        }

        private void OpenMainWindow()
        {
            if (exiting || mainWindow.IsDisposed) return;
            mainWindow.ShowInTaskbar = true;
            if (mainWindow.WindowState == FormWindowState.Minimized)
                mainWindow.WindowState = mainRestoreState;
            mainWindow.Show();
            mainWindow.BringToFront();
            mainWindow.Activate();
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
