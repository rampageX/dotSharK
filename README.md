# dotSharK 1.0.1

dotSharK 是一个轻量的 Windows yt-dlp 图形外壳，使用 **C#、WinForms 和 .NET Framework 4.8** 编写。程序使用暗色界面和绿色强调色，负责展示可下载的视频、音频与字幕选项；实际解析和下载由用户指定的 yt-dlp 完成。

## 运行

- 支持 Windows 10/11，需要 .NET Framework 4.8。
- 解压发布包后运行 `dotSharK.exe`。程序不要求安装 Visual Studio。
- 可执行文件、主界面和配置窗口都使用内嵌的小鲨鱼图标。EXE 属性包含产品、公司和 `1.0.1` 版本信息。

## 首次设置

打开右上角“配置…”：

1. 在“外部工具”页选择 `yt-dlp.exe`、`ffmpeg.exe`、`ffprobe.exe`，以及可选的 `deno.exe`。这些工具由用户单独提供，路径保存在 EXE 同级的 `dotSharK.ini`。
2. 在“YT-DLP”页填写代理、设置分片并发，并选择 Cookie 来源。
3. 使用浏览器 Cookie 时，可选择浏览器并搜索默认配置目录；也可选择 Netscape 格式的 Cookie 文件。Firefox 兼容配置会递归跟随目录联接点搜索（包含 FloorpPortable），多个结果按 Cookie 数据库最近更新时间排序。

代理支持 HTTP(S) 和 SOCKS URL，也支持在 URL 中加入认证信息。代理配置使用当前 Windows 用户的 DPAPI 加密后写入 `dotSharK.ini`。下载目录在主界面自定义；留空时使用 EXE 同级的 `Downloads` 目录。

程序不会读取 yt-dlp 外部配置文件，代理、并发和 Cookie 参数均由 dotSharK 自己传给 yt-dlp。启动时不扫描网络、不检查更新；只有用户点击分析或下载时才会启动 yt-dlp。

## 视频选择

- 显示标题、时长、视频与音频选项、字幕及自动字幕。
- 列表显示分辨率、视频编码、HDR 类型、帧率、源容器和估算大小。
- 仅保留最高的三个不同分辨率档；同一分辨率下的 AV1、VP9 等编码以及 HDR 变体会分别列出。
- 可选视频或仅音频、输出容器、音轨、字幕和下载目录。
- 下载过程中显示 yt-dlp 的进度输出。

## 从源码构建

源码位于 `src`。在安装了 .NET Framework 4.8 Developer Pack 的 Windows 环境中，可使用 .NET SDK 构建：

```powershell
dotnet build src\dotSharK.csproj -c Release
```

构建输出位于 `src\bin\Release\dotSharK.exe`。项目不依赖 Visual Studio IDE。

## 当前版本范围

1.0.1 统一了配置页和主界面的控件间距；为视频格式列表滚动条启用暗色主题并加高下载日志框；底部提示文字和下载按钮底边对齐，细进度条在这一行垂直居中；强调绿统一为 `#25745B`。单链接分析与下载，不包含下载队列、播放列表批量处理、封面预览或代理测试按钮。格式表按“分辨率 + 视频编码 + HDR 类型”显示代表流；容器选项控制输出合并容器，不会过滤格式列表。

yt-dlp 项目与使用说明：<https://github.com/yt-dlp/yt-dlp>
