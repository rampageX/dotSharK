using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace dotSharK.UiV2
{
    /// <summary>
    /// Isolated three-window sample harness, no browser host/tray or queue
    /// automatically started. UI examples use synthetic rows only.
    /// </summary>
    internal static class Stage4PreviewLauncher
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            string mode = args.Length == 0 ? "" : args[0].ToLowerInvariant();
            if (mode == "main") { Application.Run(CreateMain()); return; }
            if (mode == "video")
            {
                using (var dialog = new SingleVideoDialogV2(DemoMedia()))
                    dialog.ShowDialog();
                return;
            }
            if (mode == "playlist")
            {
                using (var dialog = new PlaylistSelectionDialogV2(
                    DemoPlaylist(), DemoMedia(), Environment.GetFolderPath(
                        Environment.SpecialFolder.MyVideos)))
                    dialog.ShowDialog();
                return;
            }
            Application.Run(new PreviewMenu());
        }

        internal static MainFormV2 CreateMain()
        {
            var form = new MainFormV2();
            var add = typeof(MainFormV2).GetMethod("AddQueueItem",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (add == null) throw new MissingMethodException("AddQueueItem");
            add.Invoke(form, new object[] { new DownloadQueueItem
            {
                Title = "Color Burst HDR Dolby Vision 12K 60FPS",
                Url = "https://www.example.com/watch?v=preview",
                Status = "已完成", BrowserSilent = true,
                OutputFolder = Environment.GetFolderPath(
                    Environment.SpecialFolder.MyVideos)
            } });
            add.Invoke(form, new object[] { new DownloadQueueItem
            {
                Title = "东京七日游 · Tokyo 7 Days",
                Url = "https://www.example.com/watch?v=demo2",
                Status = "已完成", PlaylistItem = true,
                OutputFolder = Environment.GetFolderPath(
                    Environment.SpecialFolder.MyVideos)
            } });
            return form;
        }

        internal static MediaInfo DemoMedia()
        {
            var media = new MediaInfo
            {
                Title = "Color Burst HDR Dolby Vision™ 12K 60FPS",
                Id = "demo"
            };
            media.VideoChoices.AddRange(new[]
            {
                new VideoChoice { Height = 4320, Width = 7680, Codec = "AV1", Fps = 60, DynamicRange = "HDR10", Container = "mp4", ApproxBytes = 5_261_000_000 },
                new VideoChoice { Height = 2160, Width = 3840, Codec = "AV1", Fps = 60, DynamicRange = "HDR10", Container = "mp4", ApproxBytes = 2_680_000_000 },
                new VideoChoice { Height = 2160, Width = 3840, Codec = "VP9", Fps = 60, DynamicRange = "HDR10", Container = "webm", ApproxBytes = 2_990_000_000 },
                new VideoChoice { Height = 2160, Width = 3840, Codec = "VP9", Fps = 60, DynamicRange = "SDR", Container = "webm", ApproxBytes = 2_580_000_000 },
                new VideoChoice { Height = 1440, Width = 2560, Codec = "AV1", Fps = 60, DynamicRange = "HDR10", Container = "mp4", ApproxBytes = 1_480_000_000 },
                new VideoChoice { Height = 1440, Width = 2560, Codec = "VP9", Fps = 60, DynamicRange = "HDR10", Container = "webm", ApproxBytes = 1_710_000_000 },
                new VideoChoice { Height = 1440, Width = 2560, Codec = "VP9", Fps = 60, DynamicRange = "SDR", Container = "webm", ApproxBytes = 1_530_000_000 }
            });
            media.AudioChoices.Add(new AudioChoice { Language = "英语", Codec = "Opus",
                Bitrate = 160, FormatId = "251" });
            media.Subtitles.Add(new SubtitleChoice
            { Language = "zh-CN", Name = "简体中文" });
            return media;
        }

        internal static PlaylistInfo DemoPlaylist()
        {
            var playlist = new PlaylistInfo
            {
                Title = "Mix - Color Burst HDR Dolby Vision™ 12K 60FPS"
            };
            for (int i = 0; i < 30; i++)
                playlist.Entries.Add(new PlaylistEntry
                {
                    Index = i + 1,
                    Title = i % 3 == 0
                        ? "Color Burst HDR Dolby Vision™ 12K 60FPS"
                        : i % 3 == 1
                            ? "日本風景 · 东京七日游 · Amazing 4K HDR Dolby Vision"
                            : "Breathtaking landscape 12K HDR 60FPS Dolby Vision",
                    Url = "https://www.example.com/watch?v=demo" + i,
                    Duration = 75 + (i * 19) % 420
                });
            return playlist;
        }

        private sealed class PreviewMenu : DarkForm
        {
            public PreviewMenu()
            {
                Text = "dotSharK UI V2 · Stage 4 Preview";
                ClientSize = new Size(440, 250);
                MinimumSize = Size; MaximumSize = Size;
                StartPosition = FormStartPosition.CenterScreen;
                var layout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5,
                    Padding = new Padding(Theme.Space24), BackColor = Theme.Window,
                    Margin = Padding.Empty
                };
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ActionHeight));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space16));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ActionHeight));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space16));
                layout.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ActionHeight));
                var main = new PrimaryButton { Text = "主窗口", Dock = DockStyle.Fill };
                var playlist = new SecondaryButton { Text = "播放列表选择", Dock = DockStyle.Fill };
                var quality = new SecondaryButton { Text = "视频质量选择", Dock = DockStyle.Fill };
                main.Click += (s,e) => CreateMain().Show(this);
                playlist.Click += (s,e) =>
                {
                    using (var dlg = new PlaylistSelectionDialogV2(DemoPlaylist(),
                        DemoMedia(), Environment.GetFolderPath(Environment.SpecialFolder.MyVideos)))
                        dlg.ShowDialog(this);
                };
                quality.Click += (s,e) =>
                {
                    using (var dlg = new SingleVideoDialogV2(DemoMedia()))
                        dlg.ShowDialog(this);
                };
                layout.Controls.Add(main, 0, 0);
                layout.Controls.Add(playlist, 0, 2);
                layout.Controls.Add(quality, 0, 4);
                Controls.Add(layout);
            }
        }
    }
}
