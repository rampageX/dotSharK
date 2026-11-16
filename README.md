# dotSharK 1.0.2

dotSharK 是一个轻量的 Windows yt-dlp 图形外壳，使用 **C#、WinForms 和 .NET Framework 4.8** 编写。程序使用暗色界面和绿色强调色，实际解析与下载由用户选择的 yt-dlp 完成。

## 运行

- 支持 Windows 10/11，需要 .NET Framework 4.8。
- 解压发布包后运行 `dotSharK.exe`，不需要安装 Visual Studio。
- 主界面、配置窗口和 EXE 都使用内嵌的小鲨鱼图标。

## 配置

在“配置…”中设置外部工具与 yt-dlp 选项：

- 可指定 `yt-dlp.exe`、`ffmpeg.exe`、`ffprobe.exe` 和 `deno.exe` 的路径。路径留空时，程序检查 EXE 同级目录和 `tools` 子目录。
- 外部工具页提供稳定版、Nightly/Canary 版本检查和更新。更新前会显示本地版本与远端版本，可选择更新、强制更新或取消。更新下载使用主代理；留空路径时写入 EXE 同级 `tools` 子目录。
- YT-DLP 页可设置 HTTP(S)/SOCKS 代理、地域验证代理、分片并发、同时下载数和 Cookie 来源。
- Cookie 可从浏览器配置或 Netscape 格式的 Cookie 文件读取。浏览器配置支持递归搜索；找到多个配置时优先选择 Cookie 数据库最新的一个。
- 代理配置使用当前 Windows 用户的 DPAPI 加密后保存在 EXE 同级 `dotSharK.ini`。
- 下载目录可自定义；留空时保存到 EXE 同级的 `Downloads` 目录。

程序不读取 yt-dlp 外部配置文件，代理、并发、Cookie 与其他下载参数由 dotSharK 传给 yt-dlp。

## 视频与播放列表

- 视频格式列表显示分辨率、视频编码、HDR 类型、帧率、源容器与估算大小；同一分辨率的 AV1、VP9 等编码会分别列出。
- 仅展示最高的三个不同分辨率档，同时保留该档位下不同编码与动态范围的选项。
- 检测到播放列表时，可选择下载单个视频或整个列表；列表项目可勾选，并统一设置质量、音轨、容器和字幕。
- 播放列表下载会自动建立同名子目录。
- 主界面显示下载队列、单项与队列实际进度；可停止队列、重试失败项目、右键单项重试或清理已完成项目。

## 从源码构建

源码位于 `src`。在安装了 .NET Framework 4.8 Developer Pack 的 Windows 环境中，使用 .NET SDK 构建：

```powershell
dotnet build src\dotSharK.csproj -c Release
```

构建输出位于 `src\bin\Release\dotSharK.exe`。项目不依赖 Visual Studio IDE。

## 1.0.2 更新内容

- 新增下载队列与播放列表批量选择、统一质量方案和自动创建播放列表子目录。
- 增加队列停止、失败重试、单项右键重试，以及按 yt-dlp 实际输出更新的下载进度。
- 外部工具页加入稳定版、Nightly/Canary 版本检查与更新，支持使用配置的代理和自定义工具路径。
- 修复启动时下拉框和工具地址框自动选中文本导致的橙红选区底色。
- 将保存目录行垂直居中在队列框与日志框之间，间距各 10px。
- 保留暗色滚动条、统一控件高度和绿色强调色。

yt-dlp 项目与使用说明：<https://github.com/yt-dlp/yt-dlp>
