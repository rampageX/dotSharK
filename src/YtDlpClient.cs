using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
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
        public YtDlpClient(string path, string ffmpeg = "", string ffprobe = "", string deno = "")
        { ExecutablePath = path; FfmpegPath = ffmpeg ?? ""; FfprobePath = ffprobe ?? ""; DenoPath = deno ?? ""; }

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
            ProcessResult result = await RunCaptureAsync(args);
            if (result.ExitCode != 0 || String.IsNullOrWhiteSpace(result.Stdout))
                throw new InvalidOperationException(UsefulError(result.Stderr, result.ExitCode));

            object parsed;
            try { parsed = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue, RecursionLimit = 256 }.DeserializeObject(result.Stdout); }
            catch (Exception ex) { throw new InvalidOperationException("yt-dlp 返回的数据无法解析：" + ex.Message); }
            var root = parsed as Dictionary<string, object>;
            if (root == null) throw new InvalidOperationException("没有收到可识别的视频信息。");

            var info = new MediaInfo
            {
                Id = Str(root, "id"), Title = Str(root, "title"), Description = Str(root, "description"),
                Thumbnail = Str(root, "thumbnail"), WebpageUrl = Str(root, "webpage_url"), Duration = Num(root, "duration")
            };
            var formats = ObjList(root, "formats");
            var videos = formats.Where(f => Str(f, "vcodec") != "" && Str(f, "vcodec") != "none" && Int(f, "height") > 0).ToList();
            var topHeights = videos.Select(f => Int(f, "height")).Distinct().OrderByDescending(x => x).Take(3).ToList();

            // Keep one representative per resolution + codec + HDR class. Container is intentionally
            // not part of the key, so MP4/MKV preferences cannot hide VP9/AV1 alternatives.
            foreach (var group in videos.Where(f => topHeights.Contains(Int(f, "height")))
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
            string container, string folder, SubtitleChoice subtitle, YtDlpOptions options, Action<string> output)
        {
            var args = CommonArguments(options, true);
            args.AddRange(new[] { "--no-playlist", "--newline", "--progress", "-P", folder, "-o", "%(title)s.%(ext)s" });
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
            var result = await RunStreamingAsync(args, output);
            if (result.ExitCode != 0) throw new InvalidOperationException(UsefulError(result.Stderr, result.ExitCode));
        }

        private List<string> CommonArguments(YtDlpOptions options, bool downloading)
        {
            options = options ?? new YtDlpOptions();
            var args = new List<string> { "--ignore-config" };
            if (!String.IsNullOrWhiteSpace(options.Proxy)) { args.Add("--proxy"); args.Add(options.Proxy.Trim()); }
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

        private async Task<ProcessResult> RunCaptureAsync(List<string> args)
        {
            using (var p = Start(args))
            {
                Task<string> stdout = p.StandardOutput.ReadToEndAsync(), stderr = p.StandardError.ReadToEndAsync();
                await Task.Run(() => p.WaitForExit());
                return new ProcessResult { ExitCode = p.ExitCode, Stdout = await stdout, Stderr = await stderr };
            }
        }

        private async Task<ProcessResult> RunStreamingAsync(List<string> args, Action<string> output)
        {
            using (var p = Start(args))
            {
                var error = new StringBuilder();
                p.OutputDataReceived += (s, e) => { if (e.Data != null && output != null) output(e.Data); };
                p.ErrorDataReceived += (s, e) => { if (e.Data != null) lock (error) error.AppendLine(e.Data); };
                p.BeginOutputReadLine(); p.BeginErrorReadLine();
                await Task.Run(() => p.WaitForExit());
                return new ProcessResult { ExitCode = p.ExitCode, Stderr = error.ToString() };
            }
        }

        private Process Start(List<string> args)
        {
            if (!File.Exists(ExecutablePath)) throw new FileNotFoundException("找不到 yt-dlp.exe。请将它放入程序目录的 tools 文件夹，或通过“选择程序”指定。", ExecutablePath);
            var psi = new ProcessStartInfo { FileName = ExecutablePath, Arguments = String.Join(" ", args.Select(Quote)), UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8, WorkingDirectory = Path.GetDirectoryName(ExecutablePath) };
            var extraPath = new List<string>();
            if (!String.IsNullOrWhiteSpace(FfmpegPath) && File.Exists(FfmpegPath)) extraPath.Add(Path.GetDirectoryName(FfmpegPath));
            if (!String.IsNullOrWhiteSpace(FfprobePath) && File.Exists(FfprobePath)) extraPath.Add(Path.GetDirectoryName(FfprobePath));
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
