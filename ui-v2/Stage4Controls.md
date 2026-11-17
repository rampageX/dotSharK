# dotSharK UI V2 — 阶段 4 界面/控件映射

状态：独立 UI 编译与截图测试，尚未合并到原始 dotSharK。
.NET Framework 4.8 / C# 7.3；布局仅使用 TableLayoutPanel，统一 Theme 令牌。

## 主窗口 MainFormV2

| 名称 | 类型 | 用途 |
|---|---|---|
| layoutMainRoot | TableLayoutPanel | 主窗口固定 24px 外边距，队列自适应 |
| layoutHeader | TableLayoutPanel | Logo、副标题、配置按钮 |
| lblLogo | Label | 保留 dotSharK Logo |
| lblTagline | Label | 原副标题 |
| btnSettings | SecondaryButton | 配置 |
| layoutAddress | TableLayoutPanel | 输入链接行 |
| txtUrl | FlatTextBox | 输入待解析网址 |
| btnAnalyze | PrimaryButton | 分析链接，保留剪贴板 URL 提醒 |
| layoutQueue | TableLayoutPanel | 队列区域 |
| lblQueueCount | Label | 下载队列数量 |
| btnRetryAll | SecondaryButton | 重试所有失败 |
| btnRemoveSelected | SecondaryButton | 移除选中 |
| btnClearFinished | SecondaryButton | 清理已完成 |
| gridQueue | DarkDataGridView | 下载列表：视频/来源/质量方案/状态 |
| txtOutputFolder | FlatTextBox | 输出路径 |
| btnBrowseFolder | SecondaryButton | 浏览保存目录 |
| txtLog | FlatTextArea | 只读日志，暗黑细滚动条 |
| lblStatus | Label | 队列状态 |
| progressQueue | DarkProgressIndicator | 队列处理进度 |
| btnStartQueue | PrimaryButton | 开始/停止队列 |

## 播放列表选择 PlaylistSelectionDialogV2

| 名称 | 类型 | 用途 |
|---|---|---|
| lblPlaylistTitle | Label | 播放列表标题 |
| lblPlaylistDescription | Label | 视频数量/统一质量说明 |
| cboPlaylistMode | DarkComboBox | 视频/仅音频 |
| cboPlaylistQuality | DarkComboBox | 统一视频质量 |
| cboPlaylistAudio | DarkComboBox | 音轨 |
| cboPlaylistContainer | DarkComboBox | 容器 |
| cboPlaylistSubtitle | DarkComboBox | 字幕 |
| btnSelectAll | SecondaryButton | 全选 |
| btnSelectNone | SecondaryButton | 全不选 |
| gridPlaylistEntries | DarkDataGridView | 自绘绿色复选框/序号/视频标题/时长/状态 |
| btnCancel | SecondaryButton | 取消 |
| btnAddToQueue | PrimaryButton | 批量生成原有 DownloadQueueItem |

## 选择视频质量 SingleVideoDialogV2

| 名称 | 类型 | 用途 |
|---|---|---|
| lblVideoTitle | Label | 视频标题 |
| cboVideoMode | DarkComboBox | 下载模式 |
| cboContainer | DarkComboBox | 输出容器 |
| cboAudio | DarkComboBox | 音轨 |
| cboSubtitle | DarkComboBox | 字幕 |
| gridVideoFormats | DarkDataGridView | 分辨率/编码 HDR/帧率/源格式/估算大小 |
| btnCancel | SecondaryButton | 取消 |
| btnAddToQueue | PrimaryButton | 返回 VideoChoice |

三窗体的 UI 源码在 ui-v2/MainFormV2.cs 与 ui-v2/SelectionDialogsV2.cs；
原业务源文件 src/MainForm.cs、src/SelectionDialogs.cs 和正式版 main 未修改。
预览程序的样本行不属于实际任务队列。

阶段 5 待办：正式集成到入口与托盘上下文、系统选择器暗黑化折中、真实 100%/125%/150% DPI 对照、无障碍与 keyboard 回归。
