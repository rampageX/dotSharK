using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using dotSharK.UiV2;

internal static class DropdownOverflowRegression
{
    private static readonly BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Directory.CreateDirectory(args[0]);
            using (var form = new Form { Text = "UI V2 Dropdown QA", Width = 600, Height = 440 })
            {
                var combo = new DarkComboBox {
                    Name = "cboDownloadType", Left = 24, Top = 24,
                    Width = 220, Height = 32, MaxDropDownItems = 4
                };
                form.Controls.Add(combo);
                form.Show();
                Application.DoEvents();

                combo.Items.AddRange(new object[] { "视频", "仅音频" });
                Open(combo);
                var shortScroll = Field<DarkScrollBar>(combo, "scrollbar");
                var shortPanel = Field<Panel>(combo, "popupPanel");
                var shortView = Field<Control>(combo, "view");
                Assert(!shortScroll.Visible, "two-item dropdown should NOT show a scrollbar");
                Assert(shortScroll.Maximum == 0, "two-item dropdown should have no scrolling range");
                Assert(shortView.Width == shortPanel.ClientSize.Width,
                    "short dropdown should allocate entire width to items");
                SavePopup(combo, Path.Combine(args[0], "Dropdown_2_Items_NoScrollbar.png"));
                Console.WriteLine("PASS: two-item popup has full-width items and no scrollbar");
                combo.DroppedDown = false;
                Application.DoEvents();

                combo.Items.Clear();
                for (int i = 0; i < 14; i++) combo.Items.Add("Video item " + (i + 1));
                combo.SelectedIndex = 0;
                Open(combo);
                var longScroll = Field<DarkScrollBar>(combo, "scrollbar");
                var longPanel = Field<Panel>(combo, "popupPanel");
                var longView = Field<Control>(combo, "view");
                Assert(longScroll.Visible && longScroll.Enabled, "14 items should show dark scrollbar");
                Assert(longScroll.Width == Theme.ScrollbarWidth, "scrollbar must be theme token width");
                Assert(longScroll.Maximum == 10, "scrollbar maximum should be item count minus visible rows");
                Assert(longView.Right == longScroll.Left, "view must end where scrollbar begins");
                longScroll.Value = 4;
                Assert(longScroll.Value == 4, "scrollbar drag/scroll model must allow offsets");
                SavePopup(combo, Path.Combine(args[0], "Dropdown_14_Items_DarkScrollbar.png"));
                Console.WriteLine("PASS: 14-item popup has 10px dark scrollbar and correct range");
                combo.DroppedDown = false;
                Application.DoEvents();

                combo.Items.Clear();
                for (int i = 0; i < 4; i++) combo.Items.Add("项 " + i);
                combo.MaxDropDownItems = 4;
                Open(combo);
                Assert(!Field<DarkScrollBar>(combo, "scrollbar").Visible,
                    "exactly MaxDropDownItems must not display a scrollbar");
                combo.DroppedDown = false;
                Console.WriteLine("PASS: exactly four items has no scrollbar");
                form.Close();
            }
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex);
            return 1;
        }
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Open(DarkComboBox combo)
    {
        typeof(DarkComboBox).GetMethod("OpenPopup", PrivateInstance).Invoke(combo, null);
        Application.DoEvents();
        Assert(combo.DroppedDown, "popup failed to open");
    }

    private static T Field<T>(DarkComboBox combo, string name) where T : class
    {
        var result = typeof(DarkComboBox).GetField(name, PrivateInstance).GetValue(combo) as T;
        Assert(result != null, name + " missing");
        return result;
    }

    private static void SavePopup(DarkComboBox combo, string path)
    {
        var popup = Field<ToolStripDropDown>(combo, "popup");
        using (var image = new Bitmap(popup.Width, popup.Height))
        {
            popup.DrawToBitmap(image, new Rectangle(Point.Empty, popup.Size));
            image.Save(path, ImageFormat.Png);
        }
    }
}
