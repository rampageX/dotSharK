using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace dotSharK
{
    internal sealed class PlaylistChoiceDialog : Form
    {
        internal bool DownloadPlaylist { get; private set; }

        internal PlaylistChoiceDialog(string title, int count)
        {
            Text = "选择下载范围"; StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false; MinimizeBox = false; ShowInTaskbar = false; ClientSize = new Size(480, 190);
            BackColor = DarkPalette.Background; ForeColor = DarkPalette.Foreground; Font = new Font("Microsoft YaHei UI", 9F); Icon = AppBranding.CreateIcon();
            var heading = new Label { Text = "检测到播放列表", Font = new Font(Font, FontStyle.Bold), ForeColor = DarkPalette.Foreground, AutoEllipsis = true, Location = new Point(22, 20), Size = new Size(430, 26) };
            var details = new Label { Text = String.Format("{0}\r\n共 {1} 个视频。请选择本次下载范围。", title, count), ForeColor = DarkPalette.Muted, AutoEllipsis = true, Location = new Point(22, 52), Size = new Size(430, 42) };
            var current = new DarkButton { Text = "下载当前视频", Width = 142, Height = DarkPalette.ControlHeight, Location = new Point(22, 128) };
            var playlist = new DarkButton { Text = "下载整个播放列表", Primary = true, Width = 160, Height = DarkPalette.ControlHeight, Location = new Point(176, 128) };
            var cancel = new DarkButton { Text = "取消", Width = 96, Height = DarkPalette.ControlHeight, Location = new Point(346, 128) };
            current.Click += (s, e) => { DownloadPlaylist = false; DialogResult = DialogResult.OK; Close(); };
            playlist.Click += (s, e) => { DownloadPlaylist = true; DialogResult = DialogResult.OK; Close(); };
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            Controls.AddRange(new Control[] { heading, details, current, playlist, cancel }); CancelButton = cancel;
        }
    }

    internal sealed class SingleVideoDialog : Form
    {
        private readonly MediaInfo media;
        private readonly DataGridView videoGrid;
        private readonly DarkComboBox modeBox, containerBox, audioBox, subtitleBox;

        internal VideoChoice Video { get; private set; }
        internal AudioChoice Audio { get { return audioBox.SelectedItem as AudioChoice; } }
        internal SubtitleChoice Subtitle { get { return subtitleBox.SelectedItem as SubtitleChoice; } }
        internal bool AudioOnly { get { return modeBox.SelectedIndex == 1; } }
        internal string OutputContainer { get { return Convert.ToString(containerBox.SelectedItem); } }

        internal SingleVideoDialog(MediaInfo info)
        {
            media = info;
            Text = "选择视频质量"; StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(820, 580); Size = new Size(940, 680); BackColor = DarkPalette.Background; ForeColor = DarkPalette.Foreground;
            Font = new Font("Microsoft YaHei UI", 9F); Icon = AppBranding.CreateIcon();
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, Padding = new Padding(20, 16, 20, 16), BackColor = DarkPalette.Background };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 54)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 44)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 46));
            Controls.Add(root);
            var heading = new Label { Text = info.Title, Dock = DockStyle.Fill, ForeColor = DarkPalette.Foreground, Font = new Font(Font, FontStyle.Bold), AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft };
            root.Controls.Add(heading, 0, 0);

            var settings = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 8, RowCount = 1, BackColor = DarkPalette.Background };
            float[] widths = { 6, 18, 6, 18, 6, 22, 6, 18 }; foreach (float w in widths) settings.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, w));
            modeBox = MakeCombo(new object[] { "视频", "仅音频" }); containerBox = MakeCombo(new object[] { "MKV", "MP4", "WebM" });
            audioBox = MakeCombo(new object[] { "默认音轨" }); foreach (var a in info.AudioChoices) audioBox.Items.Add(a); audioBox.SelectedIndex = 0;
            subtitleBox = MakeCombo(new object[] { "不下载字幕" }); foreach (var sub in info.Subtitles) subtitleBox.Items.Add(sub); subtitleBox.SelectedIndex = 0;
            AddLabel(settings, 0, "下载"); AddLabel(settings, 2, "容器"); AddLabel(settings, 4, "音轨"); AddLabel(settings, 6, "字幕");
            settings.Controls.Add(modeBox, 1, 0); settings.Controls.Add(containerBox, 3, 0); settings.Controls.Add(audioBox, 5, 0); settings.Controls.Add(subtitleBox, 7, 0);
            root.Controls.Add(settings, 0, 1);

            videoGrid = MakeGrid();
            videoGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "分辨率", FillWeight = 20 });
            videoGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "编码 / HDR", FillWeight = 32 });
            videoGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "帧率", FillWeight = 16 });
            videoGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "源格式", FillWeight = 14 });
            videoGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "估算大小", FillWeight = 18 });
            foreach (var v in info.VideoChoices)
            {
                int row = videoGrid.Rows.Add(v.Height + "p", v.Codec + (v.DynamicRange == "SDR" ? "" : " · " + v.DynamicRange), v.Fps > 0 ? v.Fps.ToString("0.##") + " fps" : "—", (v.Container ?? "").ToUpperInvariant(), YtDlpClient.FormatBytes(v.ApproxBytes));
                videoGrid.Rows[row].Tag = v;
            }
            if (videoGrid.Rows.Count > 0) { videoGrid.Rows[0].Selected = true; Video = videoGrid.Rows[0].Tag as VideoChoice; }
            videoGrid.SelectionChanged += (s, e) => { if (videoGrid.CurrentRow != null) Video = videoGrid.CurrentRow.Tag as VideoChoice; };
            modeBox.SelectedIndexChanged += (s, e) => videoGrid.Enabled = !AudioOnly;
            root.Controls.Add(videoGrid, 0, 2);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, BackColor = DarkPalette.Background, Padding = new Padding(0, 8, 0, 0) };
            var add = new DarkButton { Text = "加入队列", Primary = true, Width = 120, Height = DarkPalette.ControlHeight };
            var cancel = new DarkButton { Text = "取消", Width = 100, Height = DarkPalette.ControlHeight };
            add.Click += (s, e) => { if (!AudioOnly && Video == null) { MessageBox.Show(this, "请选择一个视频格式。", "dotSharK", MessageBoxButtons.OK, MessageBoxIcon.Information); return; } DialogResult = DialogResult.OK; Close(); };
            cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            buttons.Controls.Add(add); buttons.Controls.Add(cancel); root.Controls.Add(buttons, 0, 3); AcceptButton = add; CancelButton = cancel;
        }

        private static DarkComboBox MakeCombo(IEnumerable<object> values)
        {
            var combo = new DarkComboBox { Dock = DockStyle.Fill, Height = DarkPalette.ControlHeight, Margin = new Padding(0, 0, 8, 0) };
            combo.Items.AddRange(values.ToArray()); combo.SelectedIndex = 0; return combo;
        }
        private static void AddLabel(TableLayoutPanel row, int col, string text) { row.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, ForeColor = DarkPalette.Muted, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 0, 4, 0) }, col, 0); }
        internal static DataGridView MakeGrid()
        {
            var grid = new DataGridView { Dock = DockStyle.Fill, BackgroundColor = DarkPalette.Surface, BorderStyle = BorderStyle.None, GridColor = DarkPalette.Border, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, ReadOnly = true, MultiSelect = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, RowHeadersVisible = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, EnableHeadersVisualStyles = false, ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, ColumnHeadersHeight = 34, RowTemplate = { Height = 36 } };
            grid.AdvancedColumnHeadersBorderStyle.All = DataGridViewAdvancedCellBorderStyle.None;
            grid.ColumnHeadersDefaultCellStyle.BackColor = DarkPalette.Surface; grid.ColumnHeadersDefaultCellStyle.ForeColor = DarkPalette.Muted;
            grid.DefaultCellStyle.BackColor = DarkPalette.Surface; grid.DefaultCellStyle.ForeColor = DarkPalette.Foreground; grid.DefaultCellStyle.SelectionBackColor = DarkPalette.Selection; grid.DefaultCellStyle.SelectionForeColor = DarkPalette.Foreground; grid.DefaultCellStyle.Padding = new Padding(8, 0, 8, 0);
            NativeDarkTheme.ApplyOnHandleCreated(grid, "DarkMode_Explorer"); NativeDarkTheme.ApplyScrollBarsOnHandleCreated(grid, "DarkMode_Explorer");
            return grid;
        }
    }

    internal sealed class PlaylistSelectionDialog : Form
    {
        private sealed class QualityProfile
        {
            internal VideoChoice Choice;
            public override string ToString() { return Choice == null ? "最高可用（自动适配）" : Choice.Label; }
        }

        private readonly PlaylistInfo playlist;
        private readonly MediaInfo sample;
        private readonly DataGridView entriesGrid;
        private readonly DarkComboBox modeBox, qualityBox, audioBox, containerBox, subtitleBox;
        internal List<DownloadQueueItem> QueueItems { get; private set; }

        internal PlaylistSelectionDialog(PlaylistInfo list, MediaInfo sampleMedia, string folder)
        {
            playlist = list; sample = sampleMedia;
            Text = "播放列表 · 选择视频并加入队列"; StartPosition = FormStartPosition.CenterParent; FormBorderStyle = FormBorderStyle.Sizable;
            MinimumSize = new Size(900, 620); Size = new Size(1080, 760); BackColor = DarkPalette.Background; ForeColor = DarkPalette.Foreground;
            Font = new Font("Microsoft YaHei UI", 9F); Icon = AppBranding.CreateIcon();
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(20, 16, 20, 16), BackColor = DarkPalette.Background };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 32)); root.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
            Controls.Add(root);
            var heading = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = DarkPalette.Background };
            heading.RowStyles.Add(new RowStyle(SizeType.Absolute, 27)); heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            heading.Controls.Add(new Label { Text = String.IsNullOrWhiteSpace(list.Title) ? "播放列表" : list.Title, Dock = DockStyle.Fill, Font = new Font(Font, FontStyle.Bold), ForeColor = DarkPalette.Foreground, AutoEllipsis = true }, 0, 0);
            heading.Controls.Add(new Label { Text = String.Format("已解析 {0} 个视频。统一质量设置会应用于所选视频；目标格式不可用时 yt-dlp 会自动回退。", list.Entries.Count), Dock = DockStyle.Fill, ForeColor = DarkPalette.Muted, AutoEllipsis = true }, 0, 1);
            root.Controls.Add(heading, 0, 0);

            var options = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 10, RowCount = 1, BackColor = DarkPalette.Background };
            float[] widths = { 4, 8, 12, 20, 4, 15, 4, 12, 4, 17 }; foreach (float w in widths) options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, w));
            options.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            modeBox = MakeCombo(new object[] { "视频", "仅音频" });
            modeBox.ShowFocusAccent = false;
            modeBox.ClearSelectionOnFocus = true;
            qualityBox = MakeCombo(Enumerable.Empty<object>()); qualityBox.Items.Add(new QualityProfile { Choice = null });
            if (sampleMedia != null) foreach (var v in sampleMedia.VideoChoices) qualityBox.Items.Add(new QualityProfile { Choice = v });
            qualityBox.SelectedIndex = qualityBox.Items.Count > 1 ? 1 : 0;
            audioBox = MakeCombo(new object[] { "默认音轨" }); if (sampleMedia != null) foreach (var a in sampleMedia.AudioChoices) audioBox.Items.Add(a); audioBox.SelectedIndex = 0;
            containerBox = MakeCombo(new object[] { "MKV", "MP4", "WebM" });
            subtitleBox = MakeCombo(new object[] { "不下载字幕" }); if (sampleMedia != null) foreach (var sub in sampleMedia.Subtitles) subtitleBox.Items.Add(sub); subtitleBox.SelectedIndex = 0;
            AddOptionLabel(options, 0, "下载"); AddOptionLabel(options, 2, "统一视频质量"); AddOptionLabel(options, 4, "音轨"); AddOptionLabel(options, 6, "容器"); AddOptionLabel(options, 8, "字幕");
            options.Controls.Add(modeBox, 1, 0); options.Controls.Add(qualityBox, 3, 0); options.Controls.Add(audioBox, 5, 0); options.Controls.Add(containerBox, 7, 0); options.Controls.Add(subtitleBox, 9, 0);
            modeBox.SelectedIndexChanged += (s, e) => { qualityBox.Enabled = modeBox.SelectedIndex == 0; };
            var listActions = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = DarkPalette.Background, Padding = Padding.Empty };
            listActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 78)); listActions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90)); listActions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            var all = new DarkButton { Text = "全选", Dock = DockStyle.Fill, Height = DarkPalette.ControlHeight, Margin = new Padding(0, 2, 4, 2) };
            var none = new DarkButton { Text = "全不选", Dock = DockStyle.Fill, Height = DarkPalette.ControlHeight, Margin = new Padding(0, 2, 8, 2) };
            all.Click += (s, e) => SetAll(true); none.Click += (s, e) => SetAll(false);
            listActions.Controls.Add(all, 0, 0); listActions.Controls.Add(none, 1, 0);
            listActions.Controls.Add(new Label { Text = "勾选要加入下载队列的视频", Dock = DockStyle.Fill, ForeColor = DarkPalette.Muted, TextAlign = ContentAlignment.MiddleLeft }, 2, 0);
            root.Controls.Add(options, 0, 1); root.Controls.Add(listActions, 0, 2);

            entriesGrid = SingleVideoDialog.MakeGrid(); entriesGrid.ReadOnly = false; entriesGrid.MultiSelect = true; entriesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            var check = new DataGridViewCheckBoxColumn { HeaderText = "下载", FillWeight = 6, ReadOnly = false, TrueValue = true, FalseValue = false, FlatStyle = FlatStyle.Standard };
            check.HeaderCell.Style.BackColor = DarkPalette.Surface; check.HeaderCell.Style.ForeColor = DarkPalette.Muted; check.HeaderCell.Style.SelectionBackColor = DarkPalette.Surface; check.HeaderCell.Style.SelectionForeColor = DarkPalette.Muted;
            entriesGrid.Columns.Add(check);
            entriesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "序号", FillWeight = 8, ReadOnly = true });
            entriesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "视频标题", FillWeight = 68, ReadOnly = true });
            entriesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "时长", FillWeight = 10, ReadOnly = true });
            entriesGrid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "状态", FillWeight = 8, ReadOnly = true });
            foreach (var entry in list.Entries)
            {
                string duration = entry.Duration > 0 ? TimeSpan.FromSeconds(entry.Duration).ToString(entry.Duration >= 3600 ? @"h\:mm\:ss" : @"mm\:ss") : "—";
                int row = entriesGrid.Rows.Add(true, entry.Index, entry.Title, duration, "待加入"); entriesGrid.Rows[row].Tag = entry;
            }
            root.Controls.Add(entriesGrid, 0, 3);

            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, BackColor = DarkPalette.Background, Padding = new Padding(0, 8, 0, 0) };
            var add = new DarkButton { Text = "加入队列", Primary = true, Width = 120, Height = DarkPalette.ControlHeight };
            var cancel = new DarkButton { Text = "取消", Width = 100, Height = DarkPalette.ControlHeight };
            add.Click += (s, e) => BuildQueue(folder); cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            buttons.Controls.Add(add); buttons.Controls.Add(cancel); root.Controls.Add(buttons, 0, 4); AcceptButton = add; CancelButton = cancel; ActiveControl = entriesGrid;
        }

        private void BuildQueue(string folder)
        {
            entriesGrid.EndEdit();
            var selected = entriesGrid.Rows.Cast<DataGridViewRow>().Where(r => Convert.ToBoolean(r.Cells[0].Value)).Select(r => r.Tag as PlaylistEntry).Where(x => x != null).ToList();
            if (selected.Count == 0) { MessageBox.Show(this, "请至少勾选一个视频。", "dotSharK", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
            var profile = qualityBox.SelectedItem as QualityProfile;
            var choice = profile == null ? null : profile.Choice;
            var audio = audioBox.SelectedItem as AudioChoice;
            var subtitle = subtitleBox.SelectedItem as SubtitleChoice;
            QueueItems = selected.Select(entry => new DownloadQueueItem
            {
                Title = entry.Title, Url = entry.Url, OutputFolder = folder, PlaylistItem = true,
                ProgressWeight = entry.Duration > 0 ? entry.Duration : 1,
                AudioOnly = modeBox.SelectedIndex == 1, Video = choice, Audio = audio,
                TargetHeight = choice == null ? 0 : choice.Height,
                TargetCodec = choice == null ? "" : choice.Codec,
                TargetDynamicRange = choice == null ? "" : choice.DynamicRange,
                Container = Convert.ToString(containerBox.SelectedItem),
                SubtitleLanguage = subtitle == null ? "" : subtitle.Language,
                AutomaticSubtitle = subtitle != null && subtitle.Automatic,
                Status = "等待中"
            }).ToList();
            DialogResult = DialogResult.OK; Close();
        }

        private void SetAll(bool value) { foreach (DataGridViewRow row in entriesGrid.Rows) row.Cells[0].Value = value; }
        private static DarkComboBox MakeCombo(IEnumerable<object> values)
        {
            var combo = new DarkComboBox { Dock = DockStyle.Fill, Height = DarkPalette.ControlHeight, Margin = new Padding(0, 0, 8, 0) };
            combo.Items.AddRange(values.ToArray()); combo.SelectedIndex = combo.Items.Count == 0 ? -1 : 0; return combo;
        }
        private static void AddOptionLabel(TableLayoutPanel row, int col, string text) { row.Controls.Add(new Label { Text = text, Dock = DockStyle.Fill, ForeColor = DarkPalette.Muted, TextAlign = ContentAlignment.MiddleLeft, Margin = new Padding(0, 0, 4, 0), AutoEllipsis = true }, col, 0); }
    }
}
