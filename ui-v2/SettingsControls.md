# dotSharK UI V2 — 阶段 3 设置窗口控件清单

该阶段保留原业务方法（SaveClicked / SearchProfiles / SearchDefaultBrowserProfiles / UpdateToolAsync / Browse*）。
四个标签页采用 Theme 令牌及固定底部操作条，内部内容超过高度时滚动。配置窗口 792×572 不缩放。

| 控件名 | 类型 | 所在界面 | 用途 |
|---|---|---|---|
| tabSettings | DarkTabStrip | 设置公共 | 切换四个标签页 |
| btnSave | PrimaryButton | 设置公共 | 验证并保存原 YtDlpOptions |
| btnCancel | SecondaryButton | 设置公共 | 放弃并关闭 |
| txtTool0 | FlatTextBox | 外部工具 | yt-dlp.exe 路径 |
| txtTool1 | FlatTextBox | 外部工具 | ffmpeg.exe 路径 |
| txtTool2 | FlatTextBox | 外部工具 | ffprobe.exe 路径 |
| txtTool3 | FlatTextBox | 外部工具 | deno.exe 路径 |
| txtTool4 | FlatTextBox | 外部工具 | aria2c.exe 路径 |
| btnBrowseTool0～4 | SecondaryButton | 外部工具 | 选择对应 EXE |
| btnUpdateStable0～3 | IconButton | 外部工具 | 稳定版在线更新 |
| btnUpdateNightly0～3 | IconButton | 外部工具 | Nightly/Canary 更新 |
| lblUpdateStatus | Label | 外部工具 | 更新状态提示 |
| txtProxy | FlatTextBox | YT-DLP | 主代理地址 |
| chkProxyEnabled | DarkCheckBox | YT-DLP | 主代理启用开关 |
| txtGeoProxy | FlatTextBox | YT-DLP | 地区验证代理 |
| chkGeoProxyEnabled | DarkCheckBox | YT-DLP | 地区验证代理开关 |
| numFragments | DarkNumeric | YT-DLP | 下载分片并发数 |
| numConcurrentDownloads | DarkNumeric | YT-DLP | 队列同时下载数 |
| cboCookieSource | DarkComboBox | YT-DLP | Cookie 来源 |
| cboBrowser | DarkComboBox | YT-DLP | 浏览器选择 |
| txtProfileRoot | FlatTextBox | YT-DLP | 浏览器 Cookie 搜索目录 |
| btnBrowseProfileRoot | SecondaryButton | YT-DLP | 选择搜索目录 |
| btnSearchProfiles | PrimaryButton | YT-DLP | 搜索 Cookie 配置 |
| cboBrowserProfile | DarkComboBox（可编辑） | YT-DLP | 选择/手动输入浏览器配置 |
| lblProfileFound | Label | YT-DLP | 配置发现勾选图标 |
| lblProfileStatus | Label | YT-DLP | Cookie 配置搜索状态 |
| txtCookieFile | FlatTextBox | YT-DLP | Netscape Cookie 文件 |
| btnBrowseCookieFile | SecondaryButton | YT-DLP | 选择 Cookie 文件 |
| numVideoQualityLimit | DarkNumeric | YT-DLP | 质量档位限制 |
| txtAria2cSites | FlatTextArea | 高级下载 | aria2c 使用站点 |
| txtNoProxySites | FlatTextArea | 高级下载 | 不使用代理的站点 |
| txtCustomYtDlpArgs | FlatTextArea | 高级下载 | 自定义 yt-dlp 命令行 |
| txtSilentFolder | FlatTextBox | 静默下载 | 浏览器下载目录 |
| btnBrowseSilentFolder | SecondaryButton | 静默下载 | 选择浏览器下载目录 |

## 仍待阶段 5 的系统限制

- OpenFileDialog、FolderBrowserDialog 是 Windows 系统对话框；没有承诺其所有系统版本能够暗黑。
- 现有 UpdatePromptForm 用暗色基类及新按钮，但不是本次四个设置主标签页的布局验收对象。
- 100% / 125% / 150% 高 DPI 真实显示待用户设备视觉验收。

## 编译（Windows .NET Framework 4.8 / C# 7.3）

```powershell
dotnet build ui-v2\DotSharK.UiFoundation.csproj -c Release
dotnet build ui-v2\UiV2SettingsPreview.csproj -c Release
```

输出 UiV2SettingsPreview.exe 是独立设置预览程序，不会启动 dotSharK 下载队列。
