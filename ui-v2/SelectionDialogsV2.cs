using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using dotSharK.UiV2;
using V2Combo = dotSharK.UiV2.DarkComboBox;

namespace dotSharK
{
    internal sealed class SingleVideoDialogV2 : DarkForm
    {
        private readonly MediaInfo media;
        private readonly DataGridView videoGrid;
        private readonly V2Combo modeBox, containerBox, audioBox, subtitleBox;

        internal VideoChoice Video { get; private set; }
        internal AudioChoice Audio { get { return audioBox.SelectedItem as AudioChoice; } }
        internal SubtitleChoice Subtitle { get { return subtitleBox.SelectedItem as SubtitleChoice; } }
        internal bool AudioOnly { get { return modeBox.SelectedIndex == 1; } }
        internal string OutputContainer { get { return Convert.ToString(containerBox.SelectedItem); } }

        internal SingleVideoDialogV2(MediaInfo info)
        {
            media = info;
            SuspendLayout();
            try
            {
                Text = "选择视频质量";
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.Sizable;
                MinimumSize = Theme.QualityMinimumSize;
                Size = new Size(940, 680);
                BackColor = Theme.Window; ForeColor = Theme.PrimaryText;
                Font = Theme.BodyFont;
                Icon = AppBranding.CreateIcon();

                var root = new TableLayoutPanel
                {
                    Name = "layoutVideoQuality", Dock = DockStyle.Fill,
                    ColumnCount = 1, RowCount = 7,
                    BackColor = Theme.Window,
                    Margin = Padding.Empty, Padding = new Padding(Theme.Space24)
                };
                root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ControlHeight));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space16));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ControlHeight));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space16));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space16));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute,
                    Theme.ActionHeight + Theme.Space16 * 2 + Theme.BorderWidth));
                Controls.Add(root);
                root.Controls.Add(new Label
                {
                    Name = "lblVideoTitle", Text = info.Title,
                    Dock = DockStyle.Fill, Font = Theme.HeadingFont,
                    ForeColor = Theme.PrimaryText, AutoEllipsis = true,
                    TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty
                }, 0, 0);

                modeBox = MakeCombo(new object[] { "视频", "仅音频" });
                modeBox.Name = "cboVideoMode";
                containerBox = MakeCombo(new object[] { "MKV", "MP4", "WebM" });
                containerBox.Name = "cboContainer";
                audioBox = MakeCombo(new object[] { "默认音轨" });
                audioBox.Name = "cboAudio";
                foreach (var track in info.AudioChoices) audioBox.Items.Add(track);
                audioBox.SelectedIndex = 0;
                subtitleBox = MakeCombo(new object[] { "不下载字幕" });
                subtitleBox.Name = "cboSubtitle";
                foreach (var sub in info.Subtitles) subtitleBox.Items.Add(sub);
                subtitleBox.SelectedIndex = 0;

                var filters = new TableLayoutPanel
                {
                    Name = "layoutVideoFilters", ColumnCount = 7, RowCount = 1,
                    Dock = DockStyle.Fill, BackColor = Theme.Window,
                    Margin = Padding.Empty, Padding = Padding.Empty
                };
                filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
                filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.Space8));
                filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
                filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.Space8));
                filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
                filters.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.Space8));
                filters.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
                filters.Controls.Add(FilterGroup("下载", modeBox, Theme.Space12 * 3), 0, 0);
                filters.Controls.Add(FilterGroup("容器", containerBox, Theme.Space12 * 3), 2, 0);
                filters.Controls.Add(FilterGroup("音轨", audioBox, Theme.Space12 * 3), 4, 0);
                filters.Controls.Add(FilterGroup("字幕", subtitleBox, Theme.Space12 * 3), 6, 0);
                root.Controls.Add(filters, 0, 2);

                videoGrid = MakeGrid();
                videoGrid.Name = "gridVideoFormats";
                videoGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "resolution", HeaderText = "分辨率", Width = Theme.SourceColumnWidth,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.None
                });
                videoGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "codec", HeaderText = "编码 / HDR",
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                    MinimumWidth = Theme.Space24 * 4
                });
                videoGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "fps", HeaderText = "帧率", Width = Theme.StatusColumnWidth,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.None
                });
                videoGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "source", HeaderText = "源格式", Width = Theme.StatusColumnWidth,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.None
                });
                var sizeCol = new DataGridViewTextBoxColumn
                {
                    Name = "estimate", HeaderText = "估算大小",
                    Width = Theme.SourceColumnWidth,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.None
                };
                sizeCol.HeaderCell.Style.Alignment = DataGridViewContentAlignment.MiddleRight;
                sizeCol.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                videoGrid.Columns.Add(sizeCol);
                foreach (var v in info.VideoChoices)
                {
                    int row = videoGrid.Rows.Add(v.Height + "p",
                        v.Codec + (v.DynamicRange == "SDR" ? "" : " · " + v.DynamicRange),
                        v.Fps > 0 ? v.Fps.ToString("0.##") + " fps" : "—",
                        (v.Container ?? "").ToUpperInvariant(), YtDlpClient.FormatBytes(v.ApproxBytes));
                    videoGrid.Rows[row].Tag = v;
                }
                if (videoGrid.Rows.Count > 0)
                {
                    videoGrid.Rows[0].Selected = true;
                    Video = videoGrid.Rows[0].Tag as VideoChoice;
                }
                videoGrid.SelectionChanged += (s, e) =>
                {
                    if (videoGrid.CurrentRow != null)
                        Video = videoGrid.CurrentRow.Tag as VideoChoice;
                };
                modeBox.SelectedIndexChanged += (s, e) => videoGrid.Enabled = !AudioOnly;
                root.Controls.Add(videoGrid, 0, 4);

                var cancel = new SecondaryButton
                {
                    Name = "btnCancel", Text = "取消", Dock = DockStyle.Fill,
                    DialogResult = DialogResult.Cancel, Margin = Padding.Empty
                };
                var add = new PrimaryButton
                {
                    Name = "btnAddToQueue", Text = "加入队列", Dock = DockStyle.Fill,
                    DialogResult = DialogResult.None, Margin = Padding.Empty
                };
                add.Click += (s, e) =>
                {
                    if (!AudioOnly && Video == null)
                    {
                        DarkMessageBox.Show(this, "请选择一个视频格式。", "dotSharK",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        return;
                    }
                    DialogResult = DialogResult.OK;
                    Close();
                };
                cancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
                root.Controls.Add(Footer(cancel, add), 0, 6);
                AssignDialogButtons(add, cancel);
            }
            finally { ResumeLayout(true); }
        }

        internal static V2Combo MakeCombo(IEnumerable<object> values)
        {
            var combo = new V2Combo
            {
                Dock = DockStyle.Fill, Height = Theme.ControlHeight,
                Font = Theme.BodyFont, Margin = Padding.Empty
            };
            combo.Items.AddRange(values.ToArray());
            combo.SelectedIndex = combo.Items.Count == 0 ? -1 : 0;
            return combo;
        }
        internal static TableLayoutPanel FilterGroup(string title, Control field, int labelWidth)
        {
            var group = new TableLayoutPanel
            {
                ColumnCount = 3, RowCount = 1, Dock = DockStyle.Fill,
                Margin = Padding.Empty, Padding = Padding.Empty,
                BackColor = Theme.Window
            };
            group.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, labelWidth));
            group.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.Space8));
            group.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            group.Controls.Add(new Label
            {
                Text = title, ForeColor = Theme.SecondaryText,
                TextAlign = ContentAlignment.MiddleLeft,
                Dock = DockStyle.Fill, Margin = Padding.Empty,
                Font = Theme.BodyFont
            }, 0, 0);
            field.Dock = DockStyle.Fill;
            field.Margin = Padding.Empty;
            group.Controls.Add(field, 2, 0);
            return group;
        }
        internal static TableLayoutPanel Footer(Button cancel, Button add)
        {
            var footer = new TableLayoutPanel
            {
                Name = "layoutDialogFooter", Dock = DockStyle.Fill,
                Margin = Padding.Empty, Padding = Padding.Empty,
                BackColor = Theme.Window, ColumnCount = 4, RowCount = 4
            };
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.FooterButtonWidth));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.Space8));
            footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.FooterButtonWidth));
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.BorderWidth));
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space16));
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ActionHeight));
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space16));
            var divider = new Panel
            {
                BackColor = Theme.Divider, Dock = DockStyle.Fill, Margin = Padding.Empty
            };
            footer.Controls.Add(divider, 0, 0);
            footer.SetColumnSpan(divider, 4);
            cancel.Height = add.Height = Theme.ActionHeight;
            cancel.Margin = add.Margin = Padding.Empty;
            cancel.Dock = add.Dock = DockStyle.Fill;
            footer.Controls.Add(cancel, 1, 2);
            footer.Controls.Add(add, 3, 2);
            return footer;
        }
        internal static DataGridView MakeGrid()
        {
            return new DarkDataGridView
            {
                Dock = DockStyle.Fill, Margin = Padding.Empty,
                ReadOnly = true, MultiSelect = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None
            };
        }
    }

    internal sealed class PlaylistSelectionDialogV2 : DarkForm
    {
        private sealed class QualityProfile
        {
            internal VideoChoice Choice;
            public override string ToString() { return Choice == null ? "最高可用（自动适配）" : Choice.Label; }
        }

        private readonly PlaylistInfo playlist;
        private readonly MediaInfo sample;
        private readonly DataGridView entriesGrid;
        private readonly V2Combo modeBox, qualityBox, audioBox, containerBox, subtitleBox;
        internal List<DownloadQueueItem> QueueItems { get; private set; }

        internal PlaylistSelectionDialogV2(PlaylistInfo list, MediaInfo sampleMedia, string folder)
        {
            playlist = list;
            sample = sampleMedia;
            SuspendLayout();
            try
            {
                Text = "播放列表 · 选择视频并加入队列";
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.Sizable;
                MinimumSize = Theme.PlaylistMinimumSize;
                Size = new Size(1200, 760);
                BackColor = Theme.Window; ForeColor = Theme.PrimaryText;
                Font = Theme.BodyFont;
                Icon = AppBranding.CreateIcon();

                var root = new TableLayoutPanel
                {
                    Name = "layoutPlaylistRoot", Dock = DockStyle.Fill,
                    ColumnCount = 1, RowCount = 9,
                    Margin = Padding.Empty, Padding = new Padding(Theme.Space24),
                    BackColor = Theme.Window
                };
                root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute,
                    Theme.ControlHeight + Theme.Space24 * 2));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space16));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ControlHeight));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space12));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ControlHeight));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space16));
                root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.Space16));
                root.RowStyles.Add(new RowStyle(SizeType.Absolute,
                    Theme.ActionHeight + Theme.Space16 * 2 + Theme.BorderWidth));
                Controls.Add(root);

                var heading = new TableLayoutPanel
                {
                    Name = "layoutPlaylistTitle", Dock = DockStyle.Fill,
                    ColumnCount = 1, RowCount = 2, Margin = Padding.Empty,
                    Padding = Padding.Empty, BackColor = Theme.Window
                };
                heading.RowStyles.Add(new RowStyle(SizeType.Absolute, Theme.ControlHeight));
                heading.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
                heading.Controls.Add(new Label
                {
                    Name = "lblPlaylistTitle",
                    Text = String.IsNullOrWhiteSpace(list.Title) ? "播放列表" : list.Title,
                    Dock = DockStyle.Fill, Font = Theme.HeadingFont,
                    ForeColor = Theme.PrimaryText, AutoEllipsis = true,
                    TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty
                }, 0, 0);
                heading.Controls.Add(new Label
                {
                    Name = "lblPlaylistDescription",
                    Text = String.Format("已解析 {0} 个视频。统一质量设置会应用于所选视频；目标格式不可用时 yt-dlp 会自动回退。", list.Entries.Count),
                    Dock = DockStyle.Fill, Font = Theme.SmallFont,
                    ForeColor = Theme.SecondaryText,
                    TextAlign = ContentAlignment.MiddleLeft, Margin = Padding.Empty
                }, 0, 1);
                root.Controls.Add(heading, 0, 0);

                modeBox = MakeCombo(new object[] { "视频", "仅音频" });
                modeBox.Name = "cboPlaylistMode";
                qualityBox = MakeCombo(Enumerable.Empty<object>());
                qualityBox.Name = "cboPlaylistQuality";
                qualityBox.Items.Add(new QualityProfile { Choice = null });
                if (sampleMedia != null)
                    foreach (var v in sampleMedia.VideoChoices)
                        qualityBox.Items.Add(new QualityProfile { Choice = v });
                qualityBox.SelectedIndex = qualityBox.Items.Count > 1 ? 1 : 0;
                audioBox = MakeCombo(new object[] { "默认音轨" });
                audioBox.Name = "cboPlaylistAudio";
                if (sampleMedia != null)
                    foreach (var track in sampleMedia.AudioChoices) audioBox.Items.Add(track);
                audioBox.SelectedIndex = 0;
                containerBox = MakeCombo(new object[] { "MKV", "MP4", "WebM" });
                containerBox.Name = "cboPlaylistContainer";
                subtitleBox = MakeCombo(new object[] { "不下载字幕" });
                subtitleBox.Name = "cboPlaylistSubtitle";
                if (sampleMedia != null)
                    foreach (var sub in sampleMedia.Subtitles) subtitleBox.Items.Add(sub);
                subtitleBox.SelectedIndex = 0;

                // Five groups with weights 1:2:1:1:1, four 16px gutters.
                var options = new TableLayoutPanel
                {
                    Name = "layoutPlaylistFilters", Dock = DockStyle.Fill,
                    ColumnCount = 9, RowCount = 1,
                    Margin = Padding.Empty, Padding = Padding.Empty,
                    BackColor = Theme.Window
                };
                float baseUnit = 100f / 6f;
                for (int i = 0; i < 5; i++)
                {
                    options.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,
                        i == 1 ? baseUnit * 2 : baseUnit));
                    if (i < 4)
                        options.ColumnStyles.Add(new ColumnStyle(
                            SizeType.Absolute, Theme.Space16));
                }
                options.Controls.Add(SingleVideoDialogV2.FilterGroup("下载", modeBox,
                    Theme.Space12 * 3), 0, 0);
                options.Controls.Add(SingleVideoDialogV2.FilterGroup("统一视频质量", qualityBox,
                    Theme.Space24 * 4), 2, 0);
                options.Controls.Add(SingleVideoDialogV2.FilterGroup("音轨", audioBox,
                    Theme.Space12 * 3), 4, 0);
                options.Controls.Add(SingleVideoDialogV2.FilterGroup("容器", containerBox,
                    Theme.Space12 * 3), 6, 0);
                options.Controls.Add(SingleVideoDialogV2.FilterGroup("字幕", subtitleBox,
                    Theme.Space12 * 3), 8, 0);
                root.Controls.Add(options, 0, 2);
                modeBox.SelectedIndexChanged += (s, e) =>
                    qualityBox.Enabled = modeBox.SelectedIndex == 0;

                var actions = new TableLayoutPanel
                {
                    Name = "layoutPlaylistActions", Dock = DockStyle.Fill,
                    ColumnCount = 5, RowCount = 1,
                    BackColor = Theme.Window, Margin = Padding.Empty,
                    Padding = Padding.Empty
                };
                actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.BrowseButtonWidth));
                actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.Space8));
                actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.BrowseButtonWidth));
                actions.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Theme.Space16));
                actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
                var all = new SecondaryButton
                {
                    Name = "btnSelectAll", Text = "全选", Dock = DockStyle.Fill,
                    Margin = Padding.Empty
                };
                var none = new SecondaryButton
                {
                    Name = "btnSelectNone", Text = "全不选", Dock = DockStyle.Fill,
                    Margin = Padding.Empty
                };
                all.Click += (s, e) => SetAll(true);
                none.Click += (s, e) => SetAll(false);
                actions.Controls.Add(all, 0, 0);
                actions.Controls.Add(none, 2, 0);
                actions.Controls.Add(new Label
                {
                    Text = "勾选要加入下载队列的视频",
                    Font = Theme.SmallFont, ForeColor = Theme.SecondaryText,
                    Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft,
                    Margin = Padding.Empty
                }, 4, 0);
                root.Controls.Add(actions, 0, 4);

                entriesGrid = SingleVideoDialogV2.MakeGrid();
                entriesGrid.Name = "gridPlaylistEntries";
                entriesGrid.ReadOnly = false;
                entriesGrid.MultiSelect = true;
                entriesGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
                var check = new DataGridViewCheckBoxColumn
                {
                    Name = "checked", HeaderText = "下载",
                    Width = Theme.Space16 * 4,
                    ReadOnly = false, TrueValue = true, FalseValue = false,
                    FlatStyle = FlatStyle.Flat,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.None
                };
                entriesGrid.Columns.Add(check);
                entriesGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "order", HeaderText = "序号", Width = Theme.Space16 * 4,
                    ReadOnly = true, AutoSizeMode = DataGridViewAutoSizeColumnMode.None
                });
                entriesGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "title", HeaderText = "视频标题", ReadOnly = true,
                    MinimumWidth = Theme.Space24 * 4,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill
                });
                entriesGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "duration", HeaderText = "时长",
                    Width = Theme.Space24 * 4, ReadOnly = true,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.None
                });
                entriesGrid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = "status", HeaderText = "状态",
                    Width = Theme.Space24 * 4, ReadOnly = true,
                    AutoSizeMode = DataGridViewAutoSizeColumnMode.None
                });
                foreach (var entry in list.Entries)
                {
                    string duration = entry.Duration > 0
                        ? TimeSpan.FromSeconds(entry.Duration).ToString(
                            entry.Duration >= 3600 ? @"h\:mm\:ss" : @"mm\:ss")
                        : "—";
                    int row = entriesGrid.Rows.Add(true, entry.Index, entry.Title,
                        duration, "待加入");
                    entriesGrid.Rows[row].Tag = entry;
                }
                // Space toggles the checkbox from any cell of the current row;
                // when the checkbox itself owns focus, its native handler acts.
                entriesGrid.KeyDown += (s, e) =>
                {
                    if (e.KeyCode != Keys.Space || entriesGrid.CurrentCell == null
                        || entriesGrid.CurrentCell.ColumnIndex == 0) return;
                    int i = entriesGrid.CurrentCell.RowIndex;
                    if (i < 0) return;
                    var cell = entriesGrid.Rows[i].Cells[0];
                    cell.Value = !Convert.ToBoolean(cell.Value);
                    entriesGrid.InvalidateRow(i);
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                };
                root.Controls.Add(entriesGrid, 0, 6);

                var add = new PrimaryButton
                {
                    Name = "btnAddToQueue", Text = "加入队列", Dock = DockStyle.Fill,
                    Margin = Padding.Empty
                };
                var cancel = new SecondaryButton
                {
                    Name = "btnCancel", Text = "取消", Dock = DockStyle.Fill,
                    Margin = Padding.Empty, DialogResult = DialogResult.Cancel
                };
                add.Click += (s, e) => BuildQueue(folder);
                cancel.Click += (s, e) =>
                    { DialogResult = DialogResult.Cancel; Close(); };
                root.Controls.Add(SingleVideoDialogV2.Footer(cancel, add), 0, 8);
                AssignDialogButtons(add, cancel);
                ActiveControl = entriesGrid;
            }
            finally { ResumeLayout(true); }
        }

        private void BuildQueue(string folder)
        {
            entriesGrid.EndEdit();
            var selected = entriesGrid.Rows.Cast<DataGridViewRow>().Where(r => Convert.ToBoolean(r.Cells[0].Value)).Select(r => r.Tag as PlaylistEntry).Where(x => x != null).ToList();
            if (selected.Count == 0) { DarkMessageBox.Show(this, "请至少勾选一个视频。", "dotSharK", MessageBoxButtons.OK, MessageBoxIcon.Information); return; }
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
        private static V2Combo MakeCombo(IEnumerable<object> values)
        {
            return SingleVideoDialogV2.MakeCombo(values);
        }
    }
}
