# dotSharK UI V2 — 阶段 2：自绘控件

基线：`a82f382cac294b411b130178e14602b6479faff5`（`backup/2026-11-17-tray-ui-accepted`）
开发分支：`feature/ui-v2-redesign`

## 构建和运行（Windows / .NET Framework 4.8 / C# 7.3）

```powershell
dotnet build ui-v2\DotSharK.UiFoundation.csproj -c Release --nologo
dotnet build ui-v2\UiV2Gallery.csproj -c Release --nologo
.\ui-v2\bin\Release\UiV2Gallery.exe
```

只依赖 .NET Framework 4.8 自带的 `System.Windows.Forms`、`System.Drawing` 等程序集；不需要 NuGet。
本阶段的 UI Foundation 是隔离的类库，**没有接入现有 dotSharK 主程序**。`UiV2Gallery.exe` 是独立控件演示程序，不具备下载功能。

## 控件与接口

| 文件 | 类型 | 保留/新增的常用接口 |
|---|---|---|
| `DarkScrollBar.cs` | `DarkScrollBar : Control` | `Minimum`、`Maximum`、`Value`、`ValueChanged`、`LargeChange`、`SmallChange`、`Scroll` |
| `FlatTextBox.cs` | `FlatTextBox : UserControl` | `Text`、`TextChanged`、`ReadOnly`、`SelectionStart`、`SelectionLength`、`SelectedText`、`SelectAll`、`Paste`、`AppendText` |
| `FlatTextArea.cs` | `FlatTextArea : FlatTextBox` | 多行 `Text`、`Lines`、`WordWrap`、`AcceptsReturn`、`ScrollToCaret`，10px 暗色滑块 |
| `DarkComboBox.cs` | `DarkComboBox : Control` | `Items`、`SelectedIndex`、`SelectedItem`、`Text`、`SelectedIndexChanged`、`SelectionChangeCommitted`、`DroppedDown`、`MaxDropDownItems` |
| `DarkNumeric.cs` | `DarkNumeric : UserControl` | `Minimum`、`Maximum`、`Increment`、`DecimalPlaces`、`Value`、`ValueChanged`、`ReadOnly` |
| `DarkCheckBox.cs` | `DarkCheckBox : Control` | `Checked`、`CheckState`、`CheckedChanged`、`CheckStateChanged`、`AutoCheck`、`ThreeState` |
| `DarkButtonBase.cs` + `PrimaryButton.cs`、`SecondaryButton.cs`、`TextButton.cs` | Button 派生 | `Text`、`Enabled`、`Click`、`DialogResult`、键盘焦点 |
| `IconButton.cs` | `IconButton : DarkButtonBase` | `Nightly`、`Click`、`Enabled`，GDI+ 环形箭头 |
| `DarkTabStrip.cs` | `DarkTabStrip : UserControl` | `AddPage`、`Pages`、`SelectedPage`、`SelectedIndex`、`SelectedIndexChanged`、Ctrl+Tab |
| `DarkListView.cs` | `DarkListView : ListView` | `Items`、`Columns`、`SelectedItems`、owner draw 标题/行、绿色复选框 |
| `DarkDataGridView.cs` | `DarkDataGridView : DataGridView` | **原项目实际使用**的 `Rows`、`Columns`、`Cells`、`Tag`、`CellClick`、`CellValueChanged` 保留 |
| `DarkToolTip.cs` | `DarkToolTip : Component` | `SetToolTip`、`GetToolTip`、`Show`、`Hide`、延时属性；内部封装 `ToolTip`（框架原类 sealed） |
| `DarkContextMenuRenderer.cs` | 自绘菜单 Renderer | `DarkContextMenuRenderer.CreateMenu()`、`Apply(menu)` |
| `DarkTextContextMenu.cs` | 文本菜单工厂 | 剪切 / 复制 / 粘贴 / 全选，自动按 `ReadOnly` 与选区启停 |
| `DarkMessageBox.cs` | 暗色对话框 | `DarkMessageBox.Show(owner,text,caption,buttons,icon)`，框架 4.8 常用按钮组合与 `DialogResult` |

`Theme.cs`、`DarkForm.cs`、`DarkNative.cs` 继承阶段 1。`DarkPaint.cs` 提供整像素边框和 `TextRenderer` 文本绘制。

## 行为说明

- **下拉框**：完全避开原生 ComboBox 弹出层；通过 `ToolStripDropDown + PopupView + DarkScrollBar` 绘制。键盘上下 / Home / End / Enter / Esc / 首字母、鼠标滚轮均有效。
- **多行文本**：继续用 Windows 原生 `TextBox` 负责编辑/中文 IME/撤销，禁用原生滚动条，用 `NativeWindow` 监听滚动消息，外部同步自绘滑块，无轮询计时器。
- **列表**：保留 DataGridView 原生数据结构和事件、按行着色，专用绿色复选框绘制；ListView 提供独立兼容实现。两个列表都不启用原生浅色滚动条。
- **消息框与菜单**：均使用自绘的方角外观；所有键盘焦点框为绿色 1px。
- **UI 缩放**：嵌入演示 EXE 的 `app.manifest` 声明 `PerMonitorV2`。控件仍需在实际 100% / 125% / 150% 双屏组合上人工复核尺寸与字体；不能把 CI 的默认 DPI 测试算作已验证所有缩放比例。

## 阶段边界

此包只包含阶段 2 控件及独立运行的演示窗体，不替换原主窗口或下载业务；现有配置、队列、yt-dlp、aria2c 和托盘逻辑均未变更。
阶段 3、4 再接入配置页和三个业务窗体。系统级文件/目录选择器，以及 Windows 文本选区颜色等平台限制，会在阶段 5 如实列出折中项。