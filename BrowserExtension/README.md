# 浏览器右键发送到 dotSharK（统一队列测试版）

适用 Chromium 系浏览器（Chrome / Edge / Catsxp 等），Catsxp 的 Native Messaging 仍以用户本机实测为准。

## 安装

1. 解压完整测试包（含 `dotSharK.exe` 和 `BrowserExtension`）。
2. 浏览器扩展管理页面开启开发者模式，选择「加载已解压的扩展程序」，指向 `BrowserExtension` 文件夹。
3. 复制扩展 ID，在解压目录的 PowerShell 运行：

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File ".\BrowserExtension\Install-NativeHost.ps1" -ExtensionId "填入扩展ID"
   ```

脚本只修改当前用户的注册表和 Native Messaging 清单，无需管理员权限。移动 `dotSharK.exe` 后需重跑安装脚本。

## 使用

在视频页面右键「发送到 dotSharK（最高画质 · AV1 优先）」。

- 浏览器会自动启动 dotSharK（如果还没运行），不弹窗、不抢焦点，只显示托盘图标。
- **所有浏览器任务直接进入原来的 dotSharK 下载队列**，来源列为「浏览器」，质量方案为「最高画质 · AV1 优先」；同时显示标题、进度和完成/失败状态。
- **双击托盘图标**，打开同一个 dotSharK 主界面；再次启动 `dotSharK.exe` 也会要求显示已有主界面，不会另开一个独立下载队列。
- 主界面最小化或点击关闭按钮，缩回托盘；托盘右键可打开浏览器下载目录或退出程序。退出时未完成的浏览器任务保留待下一次启动。
- 手动加入队列与浏览器自动任务共用同一个下载调度器，受「配置 → YT-DLP → 同时下载数」的总并发数限制，不会再叠加一个独立的后台并发。
- 浏览器任务独立保存目录在「配置 → 静默下载」中设置（默认 EXE 同级 `Downloads\Silent`），原来的「保存到」不变。代理、Cookie、yt-dlp/FFmpeg 设置共用。

格式策略：最高分辨率 → 最高帧率 → HDR → 同档优先 AV1 → 码率；自动匹配最佳音频，AV1 不可用时自动回退；默认只下载当前单个视频，不下载整个播放列表。

文件：
- 浏览器任务收件箱：`%LOCALAPPDATA%\dotSharK\SilentQueue\Pending`
- 下载中的持久记录：`%LOCALAPPDATA%\dotSharK\SilentQueue\Active`
- 已下载媒体档案：`%LOCALAPPDATA%\dotSharK\SilentQueue\downloaded.txt`

这是测试版，建议先结束旧版 dotSharK 进程再覆盖 EXE，以免旧版独立后台进程抢任务。浏览器扩展 ID 和 Native Messaging 注册的 EXE 路径不变时无需重新安装。
