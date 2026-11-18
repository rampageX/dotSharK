using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace dotSharK
{
    internal sealed class YtDlpClient
    {
        public string ExecutablePath { get; private set; }
        public string FfmpegPath { get; private set; }
        public string FfprobePath { get; private set; }
        public string DenoPath { get; private set; }
        public string Aria2cPath { get; private set; }
        public YtDlpClient(string path, string ffmpeg = "", string ffprobe = "", string deno = "", string aria2c = "")
        {
            ExecutablePath = path; FfmpegPath = ffmpeg ?? ""; FfprobePath = ffprobe ?? "";
            DenoPath = deno ?? ""; Aria2cPath = aria2c ?? "";
        }

        public static string FindExecutable()
        {
            string beside = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "yt-dlp.exe");
            string root = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "yt-dlp.exe");
            if (File.Exists(beside)) return beside;
            if (File.Exists(root)) return root;
            return "";
        }

        public async Task<MediaInfo> AnalyzeAsync(string url, YtDlpOptions options)
        {
            var args = CommonArguments(options, false);
            args.AddRange(new[] { "--dump-single-json", "--no-warnings", "--no-playlist", "--skip-download", "--no-progress", url });
            ApplyCustomOverrides(args, options);
            ApplyNoProxySite(args, options, url, false);
            ProcessResult result = await RunCaptureAsync(args);
            if (result.ExitCode != 0 || String.IsNullOrWhiteSpace(result.Stdout))
                throw new InvalidOperationException(UsefulError(result.Stderr, result.ExitCode));
            return ParseMediaInfo(ParseJson(result.Stdout), options == null ? 3 : options.VideoQualityLimit);
        }

        public async Task<LinkInspection> InspectAsync(string url, YtDlpOptions options)
        {
            var args = CommonArguments(options, false);
            args.AddRange(new[] { "--dump-single-json", "--flat-playlist", "--no-warnings", "--skip-download", "--no-progress", url });
            ApplyCustomOverrides(args, options);
            ApplyNoProxySite(args, options, url, false);
            ProcessResult result = await RunCaptureAsync(args);
            if (result.ExitCode != 0 || String.IsNullOrWhiteSpace(result.Stdout))
                throw new InvalidOperationException(UsefulError(result.Stderr, result.ExitCode));
            var root = ParseJson(result.Stdout);
            var rawEntries = ObjList(root, "entries");
            bool isPlaylist = Str(root, "_type").Equals("playlist", StringComparison.OrdinalIgnoreCase) || rawEntries.Count > 0;
            if (!isPlaylist) return new LinkInspection { Media = ParseMediaInfo(root, options == null ? 3 : options.VideoQualityLimit) };

            var playlist = new PlaylistInfo { Title = Str(root, "title") };
            Uri playlistUri;
            bool bilibili = Uri.TryCreate(url, UriKind.Absolute, out playlistUri) &&
                (playlistUri.Host.Equals("bilibili.com", StringComparison.OrdinalIgnoreCase) ||
                 playlistUri.Host.EndsWith(".bilibili.com", StringComparison.OrdinalIgnoreCase) ||
                 playlistUri.Host.Equals("b23.tv", StringComparison.OrdinalIgnoreCase));
            var missingTitles = new List<PlaylistEntry>();
            int fallbackIndex = 0;
            foreach (var entry in rawEntries)
            {
                if (entry == null) continue;
                fallbackIndex++;
                string id = Str(entry, "id");
                // Flat Bilibili entries carry the individual part URL (including ?p=)
                // in url. Prefer it over a generic webpage URL when available.
                string flatUrl = Str(entry, "url");
                Uri partUri;
                string entryUrl = bilibili && Uri.TryCreate(flatUrl, UriKind.Absolute, out partUri) &&
                    (partUri.Scheme == Uri.UriSchemeHttp || partUri.Scheme == Uri.UriSchemeHttps)
                    ? flatUrl : Str(entry, "webpage_url");
                if (String.IsNullOrWhiteSpace(entryUrl)) entryUrl = Str(entry, "original_url");
                if (String.IsNullOrWhiteSpace(entryUrl)) entryUrl = Str(entry, "url");
                if (!bilibili && !String.IsNullOrWhiteSpace(id) && (String.IsNullOrWhiteSpace(entryUrl) || !entryUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)))
                    entryUrl = "https://www.youtube.com/watch?v=" + Uri.EscapeDataString(id);
                if (String.IsNullOrWhiteSpace(entryUrl)) continue;
                string title = Str(entry, "title");
                if (bilibili && String.IsNullOrWhiteSpace(title)) title = Str(entry, "part");
                var item = new PlaylistEntry
                {
                    Index = Int(entry, "playlist_index") > 0 ? Int(entry, "playlist_index") : fallbackIndex,
                    Id = id, Title = String.IsNullOrWhiteSpace(title) ? (bilibili ? "第 " + fallbackIndex + " 集（未取得标题）" : id.Length == 0 ? "未命名视频" : id) : title,
                    Url = entryUrl, Duration = Num(entry, "duration")
                };
                playlist.Entries.Add(item);
                if (bilibili && String.IsNullOrWhiteSpace(title)) missingTitles.Add(item);
            }
            if (missingTitles.Count > 0)
            {
                using (var slots = new SemaphoreSlim(3))
                {
                    await Task.WhenAll(missingTitles.Select(async item =>
                    {
                        await slots.WaitAsync();
                        try
                        {
                            var media = await AnalyzeAsync(item.Url, options);
                            if (!String.IsNullOrWhiteSpace(media.Title)) item.Title = media.Title;
                            if (media.Duration > 0) item.Duration = media.Duration;
                        }
                        catch (InvalidOperationException) { /* Leave the selectable entry in the list. */ }
                        finally { slots.Release(); }
                    }));
                }
            }
            if (playlist.Entries.Count == 0) throw new InvalidOperationException("检测到播放列表，但没有取得可下载的视频项目。");
            return new LinkInspection { Playlist = playlist };
        }

        private static Dictionary<string, object> ParseJson(string json)
        {
            object parsed;
            try { parsed = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue, RecursionLimit = 256 }.DeserializeObject(json); }
            catch (Exception ex) { throw new InvalidOperationException("yt-dlp 返回的数据无法解析：" + ex.Message); }
            var root = parsed as Dictionary<string, object>;
            if (root == null) throw new InvalidOperationException("没有收到可识别的视频信息。");
            return root;
        }

        private static MediaInfo ParseMediaInfo(Dictionary<string, object> root, int videoQualityLimit)
        {
            var info = new MediaInfo
            {
                Id = Str(root, "id"), Title = Str(root, "title"), Description = Str(root, "description"),
                Thumbnail = Str(root, "thumbnail"), WebpageUrl = Str(root, "webpage_url"), Duration = Num(root, "duration")
            };
            var formats = ObjList(root, "formats");
            var videos = formats.Where(f => Str(f, "vcodec") != "" && Str(f, "vcodec") != "none" && Int(f, "height") > 0).ToList();
            // Count distinct resolution heights, not codecs or HDR variants.
            // 0 disables the display limit; yt-dlp always returns the complete format list.
            var topHeights = videos.Select(f => Int(f, "height")).Distinct().OrderByDescending(x => x);
            var allowedHeights = new HashSet<int>(videoQualityLimit > 0 ? topHeights.Take(videoQualityLimit) : topHeights);

            // Keep one representative per resolution + codec + HDR class. Container is intentionally
            // not part of the key, so MP4/MKV preferences cannot hide VP9/AV1 alternatives.
            foreach (var group in videos.Where(f => allowedHeights.Contains(Int(f, "height")))
                .GroupBy(f => Int(f, "height") + "|" + CodecName(Str(f, "vcodec")) + "|" + Range(f)))
            {
                var f = group.OrderByDescending(x => Num(x, "tbr")).ThenByDescending(x => Num(x, "filesize") > 0 ? Num(x, "filesize") : Num(x, "filesize_approx")).First();
                info.VideoChoices.Add(new VideoChoice
                {
                    FormatId = Str(f, "format_id"), Height = Int(f, "height"), Width = Int(f, "width"),
                    Codec = CodecName(Str(f, "vcodec")), DynamicRange = Range(f), Fps = Num(f, "fps"),
                    Container = Str(f, "ext"), ApproxBytes = Num(f, "filesize") > 0 ? Num(f, "filesize") : Num(f, "filesize_approx"),
                    HasAudio = Str(f, "acodec") != "" && Str(f, "acodec") != "none"
                });
            }
            info.VideoChoices = info.VideoChoices.OrderByDescending(x => x.Height).ThenBy(x => x.Codec).ThenByDescending(x => x.DynamicRange == "SDR" ? 0 : 1).ToList();

            var audios = formats.Where(f => (Str(f, "vcodec") == "" || Str(f, "vcodec") == "none") && Str(f, "acodec") != "" && Str(f, "acodec") != "none")
                .GroupBy(f => Str(f, "language") + "|" + CodecName(Str(f, "acodec")));
            foreach (var group in audios)
            {
                var f = group.OrderByDescending(x => Num(x, "abr")).First();
                info.AudioChoices.Add(new AudioChoice { FormatId = Str(f, "format_id"), Language = Str(f, "language"), Codec = CodecName(Str(f, "acodec")), Container = Str(f, "ext"), ApproxBytes = Num(f, "filesize") > 0 ? Num(f, "filesize") : Num(f, "filesize_approx"), Bitrate = Num(f, "abr") });
            }
            info.AudioChoices = info.AudioChoices.OrderBy(x => x.Language).ThenByDescending(x => x.Bitrate).ToList();

            AddSubtitles(info.Subtitles, root, "subtitles", false);
            AddSubtitles(info.Subtitles, root, "automatic_captions", true);
            return info;
        }

        public async Task DownloadAsync(MediaInfo info, VideoChoice video, AudioChoice audio, bool audioOnly,
            string container, string folder, SubtitleChoice subtitle, YtDlpOptions options, Action<string> output,
            CancellationToken cancellationToken, string originPageUrl = null)
        {
            var args = CommonArguments(options, true);
            args.AddRange(new[] { "--no-playlist", "--newline", "--progress", "-P", folder, "-o", "%(title)s.%(ext)s",
                "--print", "after_move:dotSharKFileJson=%(filepath)j" });
            // If both tools are configured, PATH below lets ffmpeg and ffprobe live in
            // separate folders. With only ffmpeg configured, yt-dlp can find a sibling probe.
            if (!String.IsNullOrWhiteSpace(FfmpegPath) && String.IsNullOrWhiteSpace(FfprobePath)) { args.Add("--ffmpeg-location"); args.Add(FfmpegPath); }
            string format;
            if (audioOnly) format = audio == null ? "bestaudio/best" : audio.FormatId;
            else
            {
                if (video == null) throw new InvalidOperationException("请先选择一个视频格式。");
                format = video.HasAudio ? video.FormatId : video.FormatId + "+" + (audio == null ? "bestaudio" : audio.FormatId);
            }
            args.Add("-f"); args.Add(format);
            if (!String.IsNullOrWhiteSpace(container)) { args.Add("--merge-output-format"); args.Add(container.ToLowerInvariant()); }
            if (subtitle != null)
            {
                args.Add(subtitle.Automatic ? "--write-auto-subs" : "--write-subs");
                args.Add("--sub-langs"); args.Add(subtitle.Language);
                args.Add("--sub-format"); args.Add("srt/best");
            }
            args.Add(info.WebpageUrl);
            AddAria2cAndOverrides(args, options, originPageUrl ?? info.WebpageUrl);
            var result = await RunStreamingAsync(args, output, cancellationToken);
            if (result.ExitCode != 0) throw new InvalidOperationException(UsefulError(result.Stderr, result.ExitCode));
        }

        // Preserve FPS, HDR, codec, bitrate and audio preference; cap only height.
        internal static string SilentFormatSelector(YtDlpOptions options)
        {
            int height = options == null ? 0 : options.SilentMaxHeight;
            if (height != 4320 && height != 2160 && height != 1080 && height != 720)
                return "bv*+ba/b";
            return "bv*[height<=" + height + "]+ba/b[height<=" + height + "]";
        }

        // Browser download only; normal queued video quality is unchanged.
        internal async Task DownloadSilentAsync(string url, string folder,
            YtDlpOptions options, Action<string> output, CancellationToken cancellationToken)
        {
            var args = CommonArguments(options, true);
            args.AddRange(new[] {
                "--no-playlist", "--playlist-items", "1", "--newline", "--progress",
                "-P", folder, "-o", "%(title)s [%(id)s].%(ext)s",
                "--print", "before_dl:dotSharKTitleJson=%(title)j",
                "--print", "after_move:dotSharKFileJson=%(filepath)j",
                "-f", SilentFormatSelector(options), "--format-sort-force",
                "-S", "res,fps,hdr,vcodec,br"
            });
            if (!String.IsNullOrWhiteSpace(FfmpegPath) && String.IsNullOrWhiteSpace(FfprobePath))
            {
                args.Add("--ffmpeg-location");
                args.Add(FfmpegPath);
            }
            args.Add(url);
            AddAria2cAndOverrides(args, options, url);
            // --print implies quiet; keep archive skips and extractor diagnostics visible.
            args.Insert(args.Count - 1, "--no-quiet");
            string finalPath = null;
            Action<string> observe = line =>
            {
                const string prefix = "dotSharKFileJson=";
                if (line.StartsWith(prefix, StringComparison.Ordinal))
                {
                    try { finalPath = new JavaScriptSerializer().Deserialize<string>(line.Substring(prefix.Length)); }
                    catch (ArgumentException) { }
                }
                if (output != null) output(line);
            };
            if (output != null) output("[执行] 浏览器任务开始；已启用完整过程日志。");
            var result = await RunStreamingAsync(args, observe, cancellationToken);
            if (output != null) output("[退出] yt-dlp 退出码 " + result.ExitCode);
            if (result.ExitCode != 0)
                throw new InvalidOperationException(UsefulError(result.Stderr, result.ExitCode));
            if (String.IsNullOrWhiteSpace(finalPath) || !File.Exists(finalPath))
                throw new InvalidOperationException("yt-dlp 正常退出，但未返回实际存在的输出文件；请查看过程日志。");
        }

        public async Task DownloadPlaylistItemAsync(DownloadQueueItem item, YtDlpOptions options, Action<string> output, CancellationToken cancellationToken)
        {
            if (item == null || String.IsNullOrWhiteSpace(item.Url)) throw new InvalidOperationException("播放列表项目网址为空。");
            var args = CommonArguments(options, true);
            args.AddRange(new[] { "--no-playlist", "--newline", "--progress", "-P", item.OutputFolder, "-o", "%(title)s [%(id)s].%(ext)s",
                "--print", "before_dl:dotSharKTitleJson=%(title)j",
                "--print", "after_move:dotSharKFileJson=%(filepath)j" });
            if (!String.IsNullOrWhiteSpace(FfmpegPath) && String.IsNullOrWhiteSpace(FfprobePath)) { args.Add("--ffmpeg-location"); args.Add(FfmpegPath); }
            args.Add("-f"); args.Add(PlaylistFormatSelector(item));
            if (!String.IsNullOrWhiteSpace(item.Container)) { args.Add("--merge-output-format"); args.Add(item.Container.ToLowerInvariant()); }
            if (!String.IsNullOrWhiteSpace(item.SubtitleLanguage))
            {
                args.Add(item.AutomaticSubtitle ? "--write-auto-subs" : "--write-subs");
                args.Add("--sub-langs"); args.Add(item.SubtitleLanguage);
                args.Add("--sub-format"); args.Add("srt/best");
            }
            args.Add(item.Url);
            AddAria2cAndOverrides(args, options, String.IsNullOrWhiteSpace(item.OriginPageUrl) ? item.Url : item.OriginPageUrl);
            var result = await RunStreamingAsync(args, output, cancellationToken);
            if (result.ExitCode != 0) throw new InvalidOperationException(UsefulError(result.Stderr, result.ExitCode));
        }

        private static string PlaylistFormatSelector(DownloadQueueItem item)
        {
            string audio = "bestaudio";
            if (item.Audio != null)
            {
                string language = String.IsNullOrWhiteSpace(item.Audio.Language) ? "" : "[language^=" + item.Audio.Language + "]";
                string codec = AudioCodecFilter(item.Audio.Codec);
                audio = "bestaudio" + language + codec + "/bestaudio" + language + "/bestaudio";
            }
            if (item.AudioOnly) return audio + "/bestaudio/best";

            string height = item.TargetHeight > 0 ? "[height<=" + item.TargetHeight.ToString(CultureInfo.InvariantCulture) + "]" : "";
            string codecFilter = VideoCodecFilter(item.TargetCodec);
            string rangeFilter = String.IsNullOrWhiteSpace(item.TargetDynamicRange) || item.TargetDynamicRange.Equals("SDR", StringComparison.OrdinalIgnoreCase)
                ? "" : "[dynamic_range^=" + item.TargetDynamicRange + "]";
            string preferred = "bestvideo" + height + codecFilter + rangeFilter;
            string sameCodec = "bestvideo" + height + codecFilter;
            string anyCodec = "bestvideo" + height;
            string progressive = "best" + height;
            return preferred + "+" + audio + "/" + sameCodec + "+" + audio + "/" + anyCodec + "+" + audio + "/" + progressive + "/best";
        }

        private static string VideoCodecFilter(string codec)
        {
            if (String.IsNullOrWhiteSpace(codec)) return "";
            if (codec == "AV1") return "[vcodec^=av01]";
            if (codec == "VP9") return "[vcodec^=vp09]";
            if (codec == "VP8") return "[vcodec^=vp08]";
            if (codec == "H.264") return "[vcodec^=avc1]";
            if (codec == "HEVC") return "[vcodec^=hev1]";
            return "";
        }

        private static string AudioCodecFilter(string codec)
        {
            if (String.IsNullOrWhiteSpace(codec)) return "";
            if (codec == "AAC") return "[acodec^=mp4a]";
            if (codec == "Opus") return "[acodec^=opus]";
            if (codec == "Vorbis") return "[acodec^=vorbis]";
            if (codec == "AC-3") return "[acodec^=ac-3]";
            if (codec == "E-AC-3") return "[acodec^=ec-3]";
            if (codec == "FLAC") return "[acodec^=flac]";
            return "";
        }

        private List<string> CommonArguments(YtDlpOptions options, bool downloading)
        {
            options = options ?? new YtDlpOptions();
            var args = new List<string> { "--ignore-config" };
            if (options.ProxyEnabled && !String.IsNullOrWhiteSpace(options.Proxy)) { args.Add("--proxy"); args.Add(options.Proxy.Trim()); }
            if (options.GeoProxyEnabled && !String.IsNullOrWhiteSpace(options.GeoVerificationProxy)) { args.Add("--geo-verification-proxy"); args.Add(options.GeoVerificationProxy.Trim()); }
            if (options.CookieMode == "file")
            {
                if (!String.IsNullOrWhiteSpace(options.CookieFile)) { args.Add("--cookies"); args.Add(options.CookieFile.Trim()); }
                else args.Add("--no-cookies");
            }
            else
            {
                string browser = String.IsNullOrWhiteSpace(options.CookieBrowser) ? "firefox" : options.CookieBrowser.Trim();
                string profile = options.CookieProfile;
                args.Add("--cookies-from-browser");
                args.Add(browser + (String.IsNullOrWhiteSpace(profile) ? "" : ":" + profile));
            }
            AddRuntimeOptions(args);
            if (downloading && options.ConcurrentFragments > 0)
            { args.Add("--concurrent-fragments"); args.Add(options.ConcurrentFragments.ToString()); }
            return args;
        }

        private void AddRuntimeOptions(List<string> args)
        {
            if (!String.IsNullOrWhiteSpace(DenoPath)) { args.Add("--js-runtimes"); args.Add("deno:" + DenoPath); }
        }

        // Match only the host of the original input page (not yt-dlp's CDN media URL).
        internal static bool MatchesAriaSite(string pageUrl, string sites)
        {
            if (String.IsNullOrWhiteSpace(pageUrl) || String.IsNullOrWhiteSpace(sites)) return false;
            Uri uri;
            if (!Uri.TryCreate(pageUrl.Trim(), UriKind.Absolute, out uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
                return false;
            string host = uri.IdnHost.TrimEnd('.').ToLowerInvariant();
            foreach (string raw in Regex.Split(sites.Trim(), @"\s+"))
            {
                string name = raw.Trim().TrimEnd('.').ToLowerInvariant();
                if (name.StartsWith("*.", StringComparison.Ordinal)) name = name.Substring(2);
                if (name.Length == 0) continue;
                if (host == name || host.EndsWith("." + name, StringComparison.Ordinal))
                    return true;
            }
            return false;
        }

        internal static void ValidateAriaSites(string sites)
        {
            if (String.IsNullOrWhiteSpace(sites)) return;
            foreach (string raw in Regex.Split(sites.Trim(), @"\s+"))
            {
                string host = raw.StartsWith("*.", StringComparison.Ordinal) ? raw.Substring(2) : raw;
                if (host.Length > 253 || !Regex.IsMatch(host,
                    @"^(?=.{1,253}$)[a-zA-Z0-9-]+(?:\.[a-zA-Z0-9-]+)+$") ||
                    host.Split('.').Any(x => x.StartsWith("-", StringComparison.Ordinal) ||
                                              x.EndsWith("-", StringComparison.Ordinal)))
                    throw new ArgumentException("站点规则必须填写域名（例如 youtube.com 或 *.youtube.com），不能包含 URL、端口或其它通配符：" + raw);
            }
        }

        [DllImport("shell32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CommandLineToArgvW(string commandLine, out int argumentCount);
        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr pointer);

        // Native Windows command-line parsing, including quoted values with spaces.
        // Never invokes cmd.exe, so settings cannot be interpreted as shell commands.
        internal static List<string> ParseCustomArguments(string text)
        {
            var result = new List<string>();
            if (String.IsNullOrWhiteSpace(text)) return result;
            int count = 0;
            IntPtr argv = CommandLineToArgvW("dotSharK.exe " + text.Trim(), out count);
            if (argv == IntPtr.Zero) throw new ArgumentException("自定义 yt-dlp 参数解析失败。");
            try
            {
                for (int i = 1; i < count; i++)
                {
                    string value = Marshal.PtrToStringUni(Marshal.ReadIntPtr(argv, i * IntPtr.Size));
                    if (value != null) result.Add(value);
                }
            }
            finally { LocalFree(argv); }
            // Each group must start with an option. Its following non-option token
            // may be a native yt-dlp value (paths, URLs, codec expressions, etc.).
            bool hasOption = false;
            for (int i = 0; i < result.Count; i++)
            {
                string value = result[i];
                if (value == "--" || value.IndexOf('\0') >= 0)
                    throw new ArgumentException("自定义命令不允许位置参数分隔符 -- 或 NUL 字符。");
                if (value.StartsWith("-", StringComparison.Ordinal) && value.Length > 1)
                    hasOption = true;
                else
                {
                    if (!hasOption || i == 0 || !(result[i - 1].StartsWith("-", StringComparison.Ordinal)))
                        throw new ArgumentException("自定义命令只允许 yt-dlp 选项及其参数值，不能直接附加视频 URL：" + value);
                    hasOption = false;
                }
            }
            return result;
        }

        private static string CanonicalSwitch(string arg)
        {
            if (String.IsNullOrEmpty(arg) || arg[0] != '-') return "";
            string flag = arg.StartsWith("--", StringComparison.Ordinal)
                ? arg.Split('=')[0] : arg.Length >= 2 ? arg.Substring(0, 2) : arg;
            switch (flag)
            {
                case "-f": return "--format";
                case "-S": return "--format-sort";
                case "-P": return "--paths";
                case "-o": return "--output";
                case "-N": return "--concurrent-fragments";
                case "-r": return "--limit-rate";
                case "-R": return "--retries";
                case "-c": return "--continue";
                case "--external-downloader": return "--downloader";
                case "--external-downloader-args": return "--downloader-args";
                case "--no-continue": return "--continue";
                case "--no-progress": return "--progress";
                case "--no-part": return "--part";
                case "--no-cookies": return "--cookies";
                case "--cookies-from-browser": return "--cookies";
                case "--write-auto-subs": return "--write-subs";
                case "--no-playlist": return "--yes-playlist";
                default: return flag;
            }
        }

        private static bool HasValue(string arg)
        {
            if (String.Equals(arg, "--no-cookies", StringComparison.OrdinalIgnoreCase)) return false;
            string key = CanonicalSwitch(arg);
            return key == "--format" || key == "--format-sort" || key == "--paths" || key == "--output" ||
                key == "--proxy" || key == "--geo-verification-proxy" || key == "--cookies" ||
                key == "--ffmpeg-location" || key == "--sub-langs" || key == "--sub-format" ||
                key == "--merge-output-format" || key == "--download-archive" ||
                key == "--playlist-items" || key == "--print" || key == "--downloader" ||
                key == "--downloader-args" || key == "--concurrent-fragments" ||
                key == "--js-runtimes" || key == "--retries" || key == "--limit-rate";
        }

        private void AddAria2cAndOverrides(List<string> args, YtDlpOptions options, string originPageUrl)
        {
            if (options != null && MatchesAriaSite(originPageUrl, options.Aria2cSites))
            {
                string path = String.IsNullOrWhiteSpace(Aria2cPath) ? options.Aria2cPath : Aria2cPath;
                if (String.IsNullOrWhiteSpace(path) || !File.Exists(path))
                    throw new FileNotFoundException("网址命中 aria2c 规则，但 aria2c.exe 路径无效。请到「配置 → 外部工具」检查。", path);
                args.Insert(args.Count - 1, path);
                args.Insert(args.Count - 2, "--downloader");
            }
            ApplyCustomOverrides(args, options);
            // Domain-specific bypass takes precedence over even custom --proxy.
            ApplyNoProxySite(args, options, originPageUrl, SelectsAria2c(args));
        }

        private static bool SelectsAria2c(List<string> args)
        {
            for (int i = 0; i < args.Count - 1; i++)
            {
                string value = null;
                if (args[i] == "--downloader" && i + 1 < args.Count - 1)
                    value = args[i + 1];
                else if (args[i].StartsWith("--downloader=", StringComparison.OrdinalIgnoreCase))
                    value = args[i].Substring("--downloader=".Length);
                if (value != null && value.IndexOf("aria2c", StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        internal static bool ApplyNoProxySite(List<string> args, YtDlpOptions options, string originPageUrl, bool usesAria2c)
        {
            if (options == null || !MatchesAriaSite(originPageUrl, options.NoProxySites))
                return false;

            // Explicit empty --proxy disables inherited proxy environment variables.
            // Remove both UI and custom proxy arguments, preserve their saved values.
            for (int i = 0; i < args.Count - 1;)
            {
                string key = CanonicalSwitch(args[i]);
                if (key == "--proxy" || key == "--geo-verification-proxy")
                {
                    int count = args[i].Contains("=") ? 1 : Math.Min(2, args.Count - 1 - i);
                    args.RemoveRange(i, count);
                }
                else i++;
            }
            args.Insert(args.Count - 1, "--proxy");
            args.Insert(args.Count - 1, "");

            if (usesAria2c)
            {
                // aria2c can independently read proxies from aria2.conf or the
                // environment. Explicit child command-line options override both.
                const string direct = " --all-proxy= --http-proxy= --https-proxy= --ftp-proxy=";
                bool combined = false;
                for (int i = 0; i < args.Count - 1; i++)
                {
                    if (args[i] == "--downloader-args" && i + 1 < args.Count - 1 &&
                        args[i + 1].StartsWith("aria2c:", StringComparison.OrdinalIgnoreCase))
                    {
                        args[i + 1] += direct;
                        combined = true;
                    }
                    else if (args[i].StartsWith("--downloader-args=aria2c:", StringComparison.OrdinalIgnoreCase))
                    {
                        args[i] += direct;
                        combined = true;
                    }
                }
                if (!combined)
                {
                    args.Insert(args.Count - 1, "--downloader-args");
                    args.Insert(args.Count - 1, "aria2c:" + direct.TrimStart());
                }
            }
            return true;
        }

        private static void ApplyCustomOverrides(List<string> args, YtDlpOptions options)
        {
            var user = ParseCustomArguments(options == null ? "" : options.CustomArguments);
            if (user.Count == 0) return;
            if (args.Count == 0) return;
            string url = args[args.Count - 1];
            args.RemoveAt(args.Count - 1);
            var overrideFlags = new HashSet<string>(user.Select(CanonicalSwitch).Where(x => x.Length > 0),
                StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < args.Count;)
            {
                string key = CanonicalSwitch(args[i]);
                if (!overrideFlags.Contains(key)) { i += HasValue(args[i]) ? 2 : 1; continue; }
                int remove = HasValue(args[i]) && i + 1 < args.Count ? 2 : 1;
                args.RemoveRange(i, remove);
            }
            args.AddRange(user);
            args.Add(url);
        }

        // yt-dlp can be distributed with a native Windows launcher which
        // writes CP936/GB18030 bytes despite PYTHONIOENCODING. Decode stdout
        // and stderr from raw bytes, not DataReceived's fixed UTF-8 decoder:
        // a lossy decoder would permanently replace Chinese chars with U+FFFD.
        private static readonly Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private static readonly Encoding WindowsChinese =
            Encoding.GetEncoding(54936, EncoderFallback.ExceptionFallback,
                DecoderFallback.ExceptionFallback);

        internal static string DecodeToolOutput(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0) return "";
            string text;
            try { text = StrictUtf8.GetString(bytes); }
            catch (DecoderFallbackException)
            {
                try { text = WindowsChinese.GetString(bytes); }
                catch (DecoderFallbackException) { text = Encoding.Default.GetString(bytes); }
            }
            if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1);
            // Strip colored aria2c progress escape sequences from log display.
            return Regex.Replace(text, @"\x1B\[[0-9;]*[ -/]*[@-~]", "");
        }

        private static async Task<string> ReadCaptureBytesAsync(Stream stream)
        {
            using (var memory = new MemoryStream())
            {
                await stream.CopyToAsync(memory).ConfigureAwait(false);
                return DecodeToolOutput(memory.ToArray());
            }
        }

        private static async Task ReadOutputLinesAsync(Stream stream, Action<string> onLine)
        {
            var bytes = new byte[8192];
            using (var line = new MemoryStream())
            {
                Action emit = () =>
                {
                    if (line.Length == 0) return;
                    string value = DecodeToolOutput(line.ToArray());
                    line.SetLength(0);
                    if (value.Length != 0 && onLine != null) onLine(value);
                };
                int n;
                while ((n = await stream.ReadAsync(bytes, 0, bytes.Length).ConfigureAwait(false)) > 0)
                {
                    for (int i = 0; i < n; i++)
                    {
                        // Handle both yt-dlp's newline progress and aria2c's CR.
                        if (bytes[i] == 10 || bytes[i] == 13) emit();
                        else line.WriteByte(bytes[i]);
                        // Prevent unbounded growth if a child emits no newlines.
                        if (line.Length >= 1024 * 1024) emit();
                    }
                }
                emit();
            }
        }

        private async Task<ProcessResult> RunCaptureAsync(List<string> args)
        {
            using (var p = Start(args))
            {
                Task<string> stdout = ReadCaptureBytesAsync(p.StandardOutput.BaseStream);
                Task<string> stderr = ReadCaptureBytesAsync(p.StandardError.BaseStream);
                await Task.Run(() => p.WaitForExit());
                return new ProcessResult
                {
                    ExitCode = p.ExitCode, Stdout = await stdout, Stderr = await stderr
                };
            }
        }

        private async Task<ProcessResult> RunStreamingAsync(List<string> args, Action<string> output,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using (var p = Start(args))
            {
                var error = new StringBuilder();
                Task outRead = ReadOutputLinesAsync(p.StandardOutput.BaseStream, output);
                Task errRead = ReadOutputLinesAsync(p.StandardError.BaseStream, line =>
                {
                    lock (error) error.AppendLine(line);
                    if (output != null) output(line);
                });
                using (cancellationToken.Register(() => Task.Run(() => KillProcessTree(p))))
                    await Task.Run(() => p.WaitForExit());
                await Task.WhenAll(outRead, errRead);
                cancellationToken.ThrowIfCancellationRequested();
                return new ProcessResult { ExitCode = p.ExitCode, Stderr = error.ToString() };
            }
        }

        private static void KillProcessTree(Process process)
        {
            try
            {
                if (process == null || process.HasExited) return;
                var psi = new ProcessStartInfo
                {
                    FileName = "taskkill.exe",
                    Arguments = "/PID " + process.Id.ToString(CultureInfo.InvariantCulture) + " /T /F",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                using (var killer = Process.Start(psi))
                {
                    if (killer != null && !killer.WaitForExit(5000)) killer.Kill();
                }
            }
            catch
            {
                try { if (process != null && !process.HasExited) process.Kill(); }
                catch { }
            }
        }

        private Process Start(List<string> args)
        {
            if (!File.Exists(ExecutablePath)) throw new FileNotFoundException("找不到 yt-dlp.exe。请将它放入程序目录的 tools 文件夹，或通过“选择程序”指定。", ExecutablePath);
            var psi = new ProcessStartInfo { FileName = ExecutablePath, Arguments = String.Join(" ", args.Select(Quote)), UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = new UTF8Encoding(false), StandardErrorEncoding = new UTF8Encoding(false), WorkingDirectory = Path.GetDirectoryName(ExecutablePath) };
            // yt-dlp embeds Python. Without these, Windows GUI launches may emit
            // GBK/ACP bytes that become replacement glyphs under our UTF-8 reader.
            psi.EnvironmentVariables["PYTHONIOENCODING"] = "utf-8:replace";
            psi.EnvironmentVariables["PYTHONUTF8"] = "1";
            var extraPath = new List<string>();
            if (!String.IsNullOrWhiteSpace(FfmpegPath) && File.Exists(FfmpegPath)) extraPath.Add(Path.GetDirectoryName(FfmpegPath));
            if (!String.IsNullOrWhiteSpace(FfprobePath) && File.Exists(FfprobePath)) extraPath.Add(Path.GetDirectoryName(FfprobePath));
            if (!String.IsNullOrWhiteSpace(Aria2cPath) && File.Exists(Aria2cPath))
                extraPath.Add(Path.GetDirectoryName(Aria2cPath));
            if (extraPath.Count > 0) psi.EnvironmentVariables["PATH"] = String.Join(Path.PathSeparator.ToString(), extraPath.Distinct()) + Path.PathSeparator + Environment.GetEnvironmentVariable("PATH");
            var p = new Process { StartInfo = psi, EnableRaisingEvents = true };
            p.Start(); return p;
        }

        private static string Quote(string value)
        {
            if (value == null) return "\"\"";
            var b = new StringBuilder("\""); int slashes = 0;
            foreach (char c in value)
            {
                if (c == '\\') { slashes++; continue; }
                if (c == '"') { b.Append('\\', slashes * 2 + 1); b.Append('"'); slashes = 0; continue; }
                b.Append('\\', slashes); slashes = 0; b.Append(c);
            }
            b.Append('\\', slashes * 2); b.Append('"'); return b.ToString();
        }

        private static void AddSubtitles(List<SubtitleChoice> into, Dictionary<string, object> root, string key, bool automatic)
        {
            var dict = root.ContainsKey(key) ? root[key] as Dictionary<string, object> : null;
            if (dict == null) return;
            foreach (var item in dict)
            {
                if (into.Any(x => x.Language == item.Key && x.Automatic == automatic)) continue;
                var entry = (item.Value as object[])?.OfType<Dictionary<string, object>>().FirstOrDefault();
                into.Add(new SubtitleChoice { Language = item.Key, Name = entry == null ? item.Key : Str(entry, "name"), Automatic = automatic });
            }
        }
        private static List<Dictionary<string, object>> ObjList(Dictionary<string, object> root, string key) { return root.ContainsKey(key) ? (root[key] as object[])?.OfType<Dictionary<string, object>>().ToList() ?? new List<Dictionary<string, object>>() : new List<Dictionary<string, object>>(); }
        private static string Str(Dictionary<string, object> d, string k) { object v; return d != null && d.TryGetValue(k, out v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) : ""; }
        private static double Num(Dictionary<string, object> d, string k) { double n; return Double.TryParse(Str(d, k), NumberStyles.Any, CultureInfo.InvariantCulture, out n) ? n : 0; }
        private static int Int(Dictionary<string, object> d, string k) { return (int)Num(d, k); }
        private static string Range(Dictionary<string, object> f)
        {
            string r = Str(f, "dynamic_range");
            if (String.IsNullOrEmpty(r) || r.Equals("SDR", StringComparison.OrdinalIgnoreCase))
            {
                string note = (Str(f, "format_note") + " " + Str(f, "format")).ToUpperInvariant();
                if (note.Contains("DOLBY VISION")) return "DOLBY VISION";
                if (note.Contains("HDR10+")) return "HDR10+";
                if (note.Contains("HDR10")) return "HDR10";
                if (note.Contains("HLG")) return "HLG";
                if (note.Contains("HDR")) return "HDR";
                return "SDR";
            }
            return r.ToUpperInvariant();
        }
        private static string CodecName(string codec)
        {
            if (String.IsNullOrEmpty(codec) || codec == "none") return "未知";
            string c = codec.ToLowerInvariant();
            if (c.StartsWith("av01") || c.Contains("av1")) return "AV1";
            if (c.StartsWith("vp09") || c.StartsWith("vp9")) return "VP9";
            if (c.StartsWith("vp08") || c.StartsWith("vp8")) return "VP8";
            if (c.StartsWith("avc1") || c.Contains("h264")) return "H.264";
            if (c.StartsWith("hev1") || c.StartsWith("hvc1") || c.Contains("hevc")) return "HEVC";
            if (c.StartsWith("av01")) return "AV1";
            if (c.StartsWith("mp4a")) return "AAC";
            if (c.StartsWith("opus")) return "Opus";
            if (c.StartsWith("vorbis")) return "Vorbis";
            if (c.StartsWith("ac-3")) return "AC-3";
            if (c.StartsWith("ec-3")) return "E-AC-3";
            if (c.StartsWith("flac")) return "FLAC";
            return codec.Split('.')[0].ToUpperInvariant();
        }
        private static string UsefulError(string stderr, int code) { string s = (stderr ?? "").Trim(); return s.Length > 0 ? s : "yt-dlp 退出，代码 " + code + "。"; }
        public static string FormatBytes(double bytes) { if (bytes <= 0) return "大小未知"; string[] units = { "B", "KB", "MB", "GB" }; int n = 0; while (bytes >= 1024 && n < units.Length - 1) { bytes /= 1024; n++; } return bytes.ToString("0.#") + " " + units[n]; }
        private sealed class ProcessResult { public int ExitCode; public string Stdout, Stderr; }
    }
}
