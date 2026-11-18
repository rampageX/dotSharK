using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using dotSharK.UiV2;

internal static class UiV2Stage5Acceptance
{
    private static readonly BindingFlags Flags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    private static Assembly preview;
    private static string output;
    private static int checks;

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            output = args[0];
            Directory.CreateDirectory(output);
            preview = Assembly.LoadFrom(Path.GetFullPath(args[1]));

            using (var settings = MakeSettings())
            {
                Show(settings);
                var tabs = Find<DarkTabStrip>(settings, "tabSettings");
                Check(tabs.Pages.Count == 5, "settings has five pages");
                Check(settings.FormBorderStyle == FormBorderStyle.Sizable, "settings is resizable");
                Check(settings.AutoScaleMode == AutoScaleMode.Dpi, "settings uses DPI autoscaling");
                var save = Find<Button>(settings, "btnSave");
                var cancel = Find<Button>(settings, "btnCancel");
                Check(Object.ReferenceEquals(settings.AcceptButton, save), "settings Enter button");
                Check(Object.ReferenceEquals(settings.CancelButton, cancel), "settings Esc button");
                Check(save.Bounds.Height == Theme.ActionHeight &&
                    cancel.Bounds.Height == Theme.ActionHeight, "settings footer button heights");
                var original = settings.Size;
                settings.Size = settings.MinimumSize;
                Application.DoEvents();
                Check(save.Right <= settings.ClientSize.Width, "minimum size footer fits");
                for (int i = 0; i < 4; i++)
                {
                    tabs.SelectedIndex = i;
                    Application.DoEvents();
                    var content = tabs.Pages[i].Content as ScrollableControl;
                    Check(content != null && !content.AutoScroll, "settings page has no outer AutoScroll " + i);
                    Check(!content.VerticalScroll.Visible, "settings page has no outer vertical scroll " + i);
                    Save(settings, "Settings_Min_" + i + ".png");
                }
                settings.Size = new Size(Math.Max(original.Width, 1040), Math.Max(original.Height, 900));
                Application.DoEvents();
                int footerY = save.Top;
                for (int i = 0; i < 4; i++)
                {
                    tabs.SelectedIndex = i;
                    Application.DoEvents();
                    Check(save.Top == footerY, "settings footer does not jump between pages");
                    Save(settings, "Settings_Large_" + i + ".png");
                }
                Check(Find<FlatTextArea>(settings, "txtAria2cSites") != null &&
                    Find<FlatTextArea>(settings, "txtNoProxySites") != null &&
                    Find<FlatTextArea>(settings, "txtCustomYtDlpArgs") != null,
                    "all three advanced multiline boxes use dark text area");
                Check(All(settings).OfType<ComboBox>().Count() == 0,
                    "settings avoids native ComboBox popup");
                LogChrome("Settings", settings);
            }

            object launcher = null;
            var launch = preview.GetType("dotSharK.UiV2.Stage4PreviewLauncher", true);
            var sample = launch.GetMethod("DemoMedia", Flags).Invoke(launcher, new object[0]);
            var playlistData = launch.GetMethod("DemoPlaylist", Flags).Invoke(launcher, new object[0]);

            using (var main = (Form)launch.GetMethod("CreateMain", Flags).Invoke(null, new object[0]))
            {
                Show(main);
                Check(main.AutoScaleMode == AutoScaleMode.Dpi, "main DPI auto scale");
                var grid = Find<DataGridView>(main, "gridQueue");
                Check(grid.Rows.Count >= 2, "main preview contains queue demo data");
                Check(grid is DarkDataGridView, "main uses dark table");
                Check(Find<FlatTextArea>(main, "txtLog") != null, "main uses dark log editor");
                Save(main, "Main.png");
                CheckMinimum(main, "btnStartQueue", "Main_Min.png");
                LogChrome("Main", main);
            }

            var qualityType = preview.GetType("dotSharK.SingleVideoDialogV2", true);
            using (var video = (Form)Activator.CreateInstance(qualityType, Flags, null,
                new object[] { sample }, null))
            {
                Show(video);
                Check(video.AutoScaleMode == AutoScaleMode.Dpi, "video DPI auto scale");
                var grid = Find<DataGridView>(video, "gridVideoFormats");
                Check(grid.Rows.Count == 7 && grid is DarkDataGridView, "seven demo formats in dark grid");
                Check(Find<DarkComboBox>(video, "cboVideoMode").Items.Count == 2,
                    "video download mode has two entries");
                Check(video.CancelButton != null && video.AcceptButton != null, "video Enter/Esc buttons");
                Save(video, "Quality.png");
                CheckMinimum(video, "btnAddToQueue", "Quality_Min.png");
                LogChrome("Quality", video);
                Find<Button>(video, "btnAddToQueue").PerformClick();
                Check(video.DialogResult == DialogResult.OK, "video quality result is OK");
            }

            var playlistType = preview.GetType("dotSharK.PlaylistSelectionDialogV2", true);
            using (var play = (Form)Activator.CreateInstance(playlistType, Flags, null,
                new object[] { playlistData, sample, Path.GetTempPath() }, null))
            {
                Show(play);
                Check(play.AutoScaleMode == AutoScaleMode.Dpi, "playlist DPI auto scale");
                var grid = Find<DataGridView>(play, "gridPlaylistEntries");
                Check(grid is DarkDataGridView && grid.Rows.Count == 30,
                    "playlist keeps 30 rows and dark grid");
                Check(All(play).OfType<DarkComboBox>().Count() == 5, "playlist has five dark filters");
                Find<Button>(play, "btnSelectNone").PerformClick();
                Application.DoEvents();
                Check(grid.Rows.Cast<DataGridViewRow>().All(r => !Convert.ToBoolean(r.Cells[0].Value)),
                    "playlist select none");
                Find<Button>(play, "btnSelectAll").PerformClick();
                Application.DoEvents();
                Check(grid.Rows.Cast<DataGridViewRow>().All(r => Convert.ToBoolean(r.Cells[0].Value)),
                    "playlist select all");
                Check(play.CancelButton != null && play.AcceptButton != null, "playlist Enter/Esc buttons");
                Save(play, "Playlist.png");
                CheckMinimum(play, "btnAddToQueue", "Playlist_Min.png");
                LogChrome("Playlist", play);
                Find<Button>(play, "btnAddToQueue").PerformClick();
                Check(play.DialogResult == DialogResult.OK, "playlist returns OK");
                var tasks = playlistType.GetProperty("QueueItems", Flags).GetValue(play, null) as System.Collections.ICollection;
                Check(tasks != null && tasks.Count == 30, "playlist generates 30 queue items");
            }

            using (var form = new Form())
            {
                var cb = new DarkComboBox();
                cb.Items.AddRange(new object[] { "视频", "仅音频", "其它" });
                cb.SelectedIndex = 0;
                form.Controls.Add(cb);
                Show(form);
                typeof(DarkComboBox).GetMethod("OnKeyDown", Flags).Invoke(cb,
                    new object[] { new KeyEventArgs(Keys.Down) });
                Check(cb.SelectedIndex == 1, "down key navigates combo");
                typeof(DarkComboBox).GetMethod("OnKeyDown", Flags).Invoke(cb,
                    new object[] { new KeyEventArgs(Keys.Home) });
                Check(cb.SelectedIndex == 0, "home key navigates combo");
                typeof(DarkComboBox).GetMethod("OnKeyDown", Flags).Invoke(cb,
                    new object[] { new KeyEventArgs(Keys.End) });
                Check(cb.SelectedIndex == 2, "end key navigates combo");
                cb.DroppedDown = true;
                Application.DoEvents();
                Check(cb.DroppedDown, "combo popup opens");
                var viewField = typeof(DarkComboBox).GetField("view", Flags);
                var view = viewField.GetValue(cb) as Control;
                Check(view != null, "combo owns custom dark popup view");
                view.GetType().GetMethod("OnKeyDown", Flags).Invoke(view,
                    new object[] { new KeyEventArgs(Keys.Escape) });
                Check(!cb.DroppedDown && form.Visible,
                    "Esc closes combo popup, not its parent dialog");
                cb.DroppedDown = true;
                Application.DoEvents();
                cb.SelectedIndex = 1;
                view = viewField.GetValue(cb) as Control;
                view.GetType().GetMethod("OnKeyDown", Flags).Invoke(view,
                    new object[] { new KeyEventArgs(Keys.Enter) });
                Check(!cb.DroppedDown && cb.SelectedIndex == 1,
                    "Enter commits combo choice and closes popup");
            }

            Console.WriteLine("PASS: " + checks + " Stage 5 Windows UI assertions");
            return 0;
        }
        catch (Exception e)
        {
            Console.Error.WriteLine("FAIL: " + (e.InnerException ?? e).ToString());
            return 1;
        }
    }

    private static Form MakeSettings()
    {
        var optType = preview.GetType("dotSharK.YtDlpOptions", true);
        object opt = Activator.CreateInstance(optType, true);
        optType.GetField("CookieMode", Flags).SetValue(opt, "file");
        var type = preview.GetType("dotSharK.ConfigurationDialogV2", true);
        return (Form)Activator.CreateInstance(type, Flags, null,
            new object[] { "yt-dlp.exe", "ffmpeg.exe", "ffprobe.exe", "deno.exe",
                           "aria2c.exe", opt }, null);
    }

    private static void Check(bool predicate, string msg)
    {
        if (!predicate) throw new InvalidOperationException(msg);
        checks++;
        Console.WriteLine("PASS: " + msg);
    }

    private static void Show(Form form)
    {
        form.StartPosition = FormStartPosition.Manual;
        form.Location = new Point(5, 5);
        form.Show();
        Application.DoEvents();
        Application.DoEvents();
    }

    private static IEnumerable<Control> All(Control c)
    {
        foreach (Control child in c.Controls)
        {
            yield return child;
            foreach (var sub in All(child)) yield return sub;
        }
    }

    private static T Find<T>(Form f, string name) where T : Control
    {
        var item = All(f).OfType<T>().FirstOrDefault(c => c.Name == name);
        if (item == null) throw new InvalidOperationException("Missing control " + name);
        return item;
    }

    private static void Save(Form form, string name)
    {
        Application.DoEvents();
        using (var image = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
        {
            form.DrawToBitmap(image, new Rectangle(Point.Empty, form.ClientSize));
            image.Save(Path.Combine(output, name), ImageFormat.Png);
        }
    }

    private static void CheckMinimum(Form form, string buttonName, string image)
    {
        var old = form.Size;
        form.Size = form.MinimumSize;
        Application.DoEvents();
        var footer = Find<Button>(form, buttonName);
        var rect = footer.RectangleToScreen(footer.ClientRectangle);
        var client = form.RectangleToScreen(form.ClientRectangle);
        Check(rect.Left >= client.Left && rect.Right <= client.Right &&
              rect.Top >= client.Top && rect.Bottom <= client.Bottom,
              form.Text + " minimum-size footer stays inside window");
        Save(form, image);
        form.Size = old;
        Application.DoEvents();
    }

    private static void LogChrome(string id, Form form)
    {
        var dark = form.GetType().GetProperty("DarkCaptionApplied").GetValue(form, null);
        var square = form.GetType().GetProperty("SquareCornersApplied").GetValue(form, null);
        var colors = form.GetType().GetProperty("CaptionColorsApplied").GetValue(form, null);
        Console.WriteLine("INFO: " + id + " DPI=" + form.DeviceDpi +
            " OS=" + Environment.OSVersion.Version +
            " DWM dark=" + dark + " square=" + square + " colors=" + colors);
    }
}