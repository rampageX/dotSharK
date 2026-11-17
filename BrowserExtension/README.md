# 浏览器右键发送到 dotSharK（便携版）

适用 Chromium 系浏览器（Chrome / Edge / Catsxp 等），Catsxp 的 Native Messaging 仍以用户本机实测为准。

## 安装

1. 解压完整便携包（含 `dotSharK.exe` 和 `BrowserExtension`）。
2. 浏览器扩展管理页面开启开发者模式，选择「加载已解压的扩展程序」，指向 `BrowserExtension` 文件夹。
3. 复制扩展 ID，在解压目录的 PowerShell 运行：

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File ".\BrowserExtension\Install-NativeHost.ps1" -ExtensionId "填入扩展ID"
   ```

脚本只修改当前用户的注册表和 Native Messaging 清单，无需管理员权限。移动 `dotSharK.exe` 后需重跑安装脚本。

## 使用

在视频页面空白处、视频上，或视频缩略图对应的**网页链接**上右键，选择「发送到 dotSharK（静默下载）」。

- **右键网页链接时发送链接目标地址**（而不是当前列表页地址）；支持有真实 `href` 的缩略图链接。
- **右键页面或视频时发送当前网页地址**，不直接把 `blob:` / `m3u8` 等媒体流地址交给下载器。
- 纯 JavaScript 点击跳转、没有真实链接地址的缩略图无法由浏览器的“链接右键”直接取得目标网址。

- 浏览器会自动启动 dotSharK（如果还没运行），不弹窗、不抢焦点，只显示托盘图标。
- **所有浏览器任务直接进入原来的 dotSharK 下载队列**，来源列为「浏览器」，质量方案按「配置 → 静默下载」所选分辨率上限显示（默认最高）；同时显示标题、进度和完成/失败状态。
- **双击托盘图标**，打开同一个 dotSharK 主界面；再次启动 `dotSharK.exe` 也会要求显示已有主界面，不会另开一个独立下载队列。
- 主界面最小化或点击关闭按钮，缩回托盘；托盘右键可打开浏览器下载目录或退出程序。退出时未完成的浏览器任务保留待下一次启动。
- 手动加入队列与浏览器自动任务共用同一个下载调度器，受「配置 → YT-DLP → 同时下载数」的总并发数限制，不会再叠加一个独立的后台并发。
- 浏览器任务独立保存目录在「配置 → 静默下载」中设置（默认 EXE 同级 `Downloads\Silent`），原来的「保存到」不变。代理、Cookie、yt-dlp/FFmpeg 设置共用。

格式策略：在所选上限内优先最高分辨率 → 最高帧率 → HDR → 同档优先 AV1 → 码率；自动匹配最佳音频，AV1 不可用时自动回退；默认只下载当前单个视频，不下载整个播放列表。

扩展升级：如果原扩展目录不变，将新版 `background.js` 与 `manifest.json` 覆盖到原扩展目录，然后到浏览器扩展管理页点击「重新加载」。扩展 ID 和 EXE 位置未变化时，无需重新注册 Native Messaging。

文件：
- 浏览器任务收件箱：`EXE 同级的 SilentQueue\Pending`
- 下载中的持久记录：`EXE 同级的 SilentQueue\Active`
- 不使用已下载档案拦截重复下载，删除文件后可重新下载，也可选择其它分辨率。

这是测试版，建议先结束旧版 dotSharK 进程再覆盖 EXE，以免旧版独立后台进程抢任务。浏览器扩展 ID 和 Native Messaging 注册的 EXE 路径不变时无需重新安装。


便携版：任务写入 EXE 同级 `SilentQueue`；托盘诊断写入 `Logs/tray-ui.log`；Native Host 清单写入 `BrowserExtension/NativeMessaging`。不自动迁移旧 AppData 记录。移动目录或换电脑后重新运行注册脚本，浏览器集成仍需要当前用户的注册表入口。
