# 浏览器右键发送到 dotSharK（测试版）

适用 Chromium 系浏览器（Chrome / Edge / Catsxp 等）。Catsxp 的 Native Messaging 注册表兼容性仍需本机实测。

## 安装步骤

1. 解压 dotSharK 测试包，保留 `dotSharK.exe` 和 `BrowserExtension` 目录。
2. 在浏览器扩展管理页（如 `chrome://extensions` 或 `edge://extensions`）开启开发者模式。
3. 点击「加载已解压的扩展程序」，选择 `BrowserExtension` 文件夹。
4. 在扩展详情中复制 32 位扩展 ID（由字母 a–p 组成）。
5. 在 PowerShell 运行：

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File ".\BrowserExtension\Install-NativeHost.ps1" -ExtensionId "填入扩展ID"
   ```

安装脚本只写入当前用户 HKCU 注册表及本地 Native Messaging 清单，不需要管理员权限。
**不要移动 dotSharK.exe 的位置**；移动后需要重新运行安装脚本。

## 使用

在视频网页空白处或播放器上右键 → 「发送到 dotSharK（最高画质 · AV1 优先）」。
无需提前打开 dotSharK 主窗口，第一次发送后会出现**系统托盘鲨鱼图标**，后台独立队列依次下载，每次处理一个任务。托盘图标持续保留，方便查看任务。**双击托盘图标打开 dotSharK 主界面**，点击界面右上角「静默下载」查看当前 URL、实时进度、排队数量和下载日志；也可右键托盘直接选择「查看静默下载状态」。右键托盘还可打开独立下载目录或退出后台（正在下载时停止并保留任务供下次继续）。

视频排序优先级：**最高分辨率 → 最高帧率 → HDR → AV1（同档） → 码率**。自动选择最佳音频。
找不到 AV1 时自动选择其他编码。静默下载目录在 dotSharK「配置 → 静默下载」里单独设置（默认 EXE 同级 `Downloads\\Silent`）；主界面「保存到」只影响手动下载，两个目录互不干扰。代理、Cookie、外部工具仍共用现有设置。
默认不下载整个播放列表；每次最多处理一个视频。不会转码 HDR→SDR。
已完成的媒体使用 yt-dlp download archive 自动跳过重复下载。

- 静默队列目录：`%LOCALAPPDATA%\dotSharK\SilentQueue`
- 详细日志：`%LOCALAPPDATA%\dotSharK\SilentQueue\silent-download.log`
- 已下载媒体清单：`%LOCALAPPDATA%\dotSharK\SilentQueue\downloaded.txt`

这是**独立后台队列**，不会混进主窗口的手动下载列表；点击主界面的「静默下载」可查看状态。浏览器右键时不会主动弹出主窗口或抢占焦点。
失败写入日志，不弹窗。浏览器扩展遇到 Native Messaging 错误时可在扩展的 Service Worker 控制台检查。
