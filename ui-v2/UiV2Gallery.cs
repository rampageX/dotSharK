using System;
using System.Drawing;
using System.Windows.Forms;

namespace dotSharK.UiV2
{
    /// <summary>Standalone WinForms manual visual/keyboard/DPI smoke gallery.</summary>
    internal static class UiV2Gallery
    {
        [STAThread]
        private static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Gallery());
        }

        private sealed class Gallery : DarkForm
        {
            private readonly DarkToolTip tip = new DarkToolTip();
            internal Gallery()
            {
                Text = "dotSharK · UI V2 控件验证";
                MinimumSize = Theme.QualityMinimumSize;
                ClientSize = Theme.MainMinimumSize;
                var tabs = new DarkTabStrip { Dock = DockStyle.Fill, Margin = Padding.Empty };
                tabs.AddPage("表单控件", FormPage());
                tabs.AddPage("数据列表", TablePage());
                tabs.AddPage("文本日志", LogPage());
                tabs.AddPage("提示与菜单", PopupsPage());
                Controls.Add(tabs);
            }
            private Control FormPage()
            {
                var root = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill, AutoScroll = false, RowCount = 5,
                    ColumnCount = 2, Margin = Padding.Empty,
                    Padding = new Padding(Theme.Space24), BackColor = Theme.Window
                };
                root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.LabelColumnWidth));
                root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                for (int i = 0; i < 5; i++) root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ControlHeight + Theme.Space12));
                var text = new FlatTextBox { Dock = DockStyle.Fill, Text = "支持中文输入与剪贴板", Margin = Padding.Empty };
                var combo = new DarkComboBox { Dock = DockStyle.Fill, Margin = Padding.Empty };
                combo.Items.AddRange(new object[] { "最高画质 · AV1 优先", "1080p · HEVC", "720p · H.264", "仅音频" });
                combo.SelectedIndex = 0;
                var number = new DarkNumeric { Minimum = 1, Maximum = 12, Value = 3, Dock = DockStyle.Left };
                var check = new DarkCheckBox { Text = "启用", Checked = true, Dock = DockStyle.Fill };
                var buttons = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
                    WrapContents = false, Margin = Padding.Empty, Padding = Padding.Empty
                };
                buttons.Controls.Add(new PrimaryButton { Text = "分析链接", Width = Theme.AnalyzeButtonWidth });
                buttons.Controls.Add(new SecondaryButton { Text = "浏览…", Width = Theme.BrowseButtonWidth, Margin = new Padding(Theme.Space8, 0, 0, 0) });
                buttons.Controls.Add(new TextButton { Text = "取消", Width = Theme.FooterButtonWidth, Margin = new Padding(Theme.Space8, 0, 0, 0) });
                buttons.Controls.Add(new IconButton { Nightly = false, Margin = new Padding(Theme.Space8, 0, 0, 0) });
                buttons.Controls.Add(new IconButton { Nightly = true, Margin = new Padding(Theme.Space8, 0, 0, 0) });
                AddRow(root, 0, "链接", text);
                AddRow(root, 1, "视频格式", combo);
                AddRow(root, 2, "同时下载", number);
                AddRow(root, 3, "主代理", check);
                AddRow(root, 4, "操作", buttons);
                tip.SetToolTip(combo, "下拉层不使用系统原生弹窗，可用键盘选择。 ");
                tip.SetToolTip(number, "支持鼠标滚轮与长按连续调整");
                return root;
            }
            private static void AddRow(TableLayoutPanel table, int row, string title, Control item)
            {
                var label = new Label
                {
                    Text = title, ForeColor = Theme.SecondaryText,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Dock = DockStyle.Fill, Margin = Padding.Empty
                };
                table.Controls.Add(label, 0, row);
                table.Controls.Add(item, 1, row);
            }
            private static Control TablePage()
            {
                var root = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
                    Margin = Padding.Empty, Padding = new Padding(Theme.Space24)
                };
                root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
                root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));
                var grid = new DarkDataGridView { Dock = DockStyle.Fill, EmptyText = "暂无下载任务", Margin = Padding.Empty };
                grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "下载", Width = Theme.Space24 * 3, AutoSizeMode = DataGridViewAutoSizeColumnMode.None });
                grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "视频", FillWeight = 70 });
                grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "状态", FillWeight = 30 });
                for (int i = 0; i < 36; i++) grid.Rows.Add(i % 2 == 0, "视频 " + i + " · 画质测试", "等待中");
                var list = new DarkListView { Dock = DockStyle.Fill, EmptyText = "暂无数据", GreenCheckBoxes = true, Margin = Padding.Empty };
                list.Columns.Add("下载", Theme.Space24 * 3);
                list.Columns.Add("文件", Theme.Space24 * 8);
                for (int i = 0; i < 36; i++)
                {
                    var item = new ListViewItem("") { Checked = i % 3 == 0 };
                    item.SubItems.Add("样本 " + i);
                    list.Items.Add(item);
                }
                root.Controls.Add(grid, 0, 0);
                root.Controls.Add(list, 1, 0);
                return root;
            }
            private static Control LogPage()
            {
                var root = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2,
                    Margin = Padding.Empty, Padding = new Padding(Theme.Space24)
                };
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space24 * 5));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                var top = new FlatTextArea { Dock = DockStyle.Fill, Text = "youtube.com\r\nbilibili.com\r\nexample.org", Margin = Padding.Empty };
                var log = new FlatTextArea { Dock = DockStyle.Fill, ReadOnly = true, WordWrap = false, Margin = Padding.Empty };
                for (int i = 0; i < 90; i++) log.AppendText("[download] 第 " + i + " 行：纯暗黑日志滚动验证\r\n");
                log.ScrollToCaret();
                root.Controls.Add(top, 0, 0);
                root.Controls.Add(log, 0, 1);
                return root;
            }
            private static Control PopupsPage()
            {
                var root = new FlowLayoutPanel
                {
                    Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight,
                    Margin = Padding.Empty, Padding = new Padding(Theme.Space24)
                };
                var msg = new SecondaryButton { Text = "打开暗色消息框", Width = Theme.Space24 * 7 };
                msg.Click += (s, e) => DarkMessageBox.Show(msg.FindForm(),
                    "这是自绘暗黑消息框。它保留 Enter/Esc 按钮与 DialogResult。", "dotSharK",
                    MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
                var context = new SecondaryButton { Text = "打开暗色菜单", Width = Theme.Space24 * 7 };
                var menu = DarkContextMenuRenderer.CreateMenu();
                menu.Items.Add("复制"); menu.Items.Add("粘贴"); menu.Items.Add("全选");
                context.Click += (s, e) => menu.Show(context, 0, context.Height);
                root.Controls.Add(msg);
                root.Controls.Add(context);
                return root;
            }
            protected override void Dispose(bool disposing)
            {
                if (disposing) tip.Dispose();
                base.Dispose(disposing);
            }
        }
    }
}