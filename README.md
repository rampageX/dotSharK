# dotSharK · 原型 0.1

dotSharK 是一个启动轻、暗色界面、绿色强调色的 Windows 桌面原型，使用 **.NET Framework 4.8 WinForms**，由 yt-dlp 负责站点解析和下载。

## 启动

1. 本包附带 `dotSharK.exe`，Windows 10/11 已有 .NET Framework 4.8 即可启动。
2. 点右上角“配置…”：在“外部工具”页指定 `yt-dlp.exe`、`ffmpeg.exe`、`ffprobe.exe`、`deno.exe`；在“YT-DLP”页设置代理、并发和 Cookie 来源。
3. 粘贴视频链接，点“分析链接”。

未指定下载目录时，文件保存到主程序同级 `Downloads`；也可以在界面中改为任意目录。程序不在启动时扫描网络或检查更新；只有用户点击分析/下载时才启动 yt-dlp。

## 当前原型已有

- 解析标题、时长、可用视频/音频流、字幕与自动字幕。
- 只显示最高的三个不同分辨率档；每档再按视频编码和动态范围分组，因此同分辨率的 AV1、VP9 等不会因为 MP4/MKV 选项而互相隐藏。
- 视频格式表显示分辨率、编码、HDR 标记、帧率、源容器与 yt-dlp 提供的大小估算。
- 可选择视频/仅音频、输出容器、音轨、字幕与保存目录。
- 所有调用均忽略 yt-dlp 外部配置文件；代理、并发及 Cookie 设置只由 dotSharK 传入。
- Cookie 默认从 Firefox 读取；可切换其它浏览器，也可指定 Netscape 格式 Cookie 文件。选定搜索目录后，可递归寻找浏览器 Cookie 数据库并选择对应配置目录。
- 代理设置支持 HTTP(S) 与 SOCKS URL，可在 URL 中包含认证信息。代理地址保存在 EXE 同级的 `dotSharK.ini`，并通过当前 Windows 用户的 DPAPI 加密。
- 下载时显示 yt-dlp 的进度输出。

## 尚未做

这是第一版可交互原型：没有下载队列、播放列表批量处理、封面预览和代理测试按钮。当前按每个“分辨率 + 视频编码 + HDR 类型”选一条代表流；容器下拉框只控制输出合并容器，不过滤可见格式。外部工具通过 yt-dlp 的 ffmpeg 与 JavaScript runtime 参数及进程搜索路径指定。

yt-dlp 官方项目与命令说明：<https://github.com/yt-dlp/yt-dlp>
