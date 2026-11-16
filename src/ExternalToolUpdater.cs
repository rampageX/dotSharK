using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace dotSharK
{
    internal static class ExternalToolUpdater
    {
        private static readonly string[] ToolNames = { "yt-dlp.exe", "ffmpeg.exe", "ffprobe.exe", "deno.exe" };
        private const string YtDlpStable = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
        private const string YtDlpNightly = "https://github.com/yt-dlp/yt-dlp-nightly-builds/releases/latest/download/yt-dlp.exe";
        private const string FfmpegStable = "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";
        private const string FfmpegNightly = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
        private const string DenoStable = "https://github.com/denoland/deno/releases/latest/download/deno-x86_64-pc-windows-msvc.zip";
        private const string DenoCanaryLatest = "https://dl.deno.land/canary-x86_64-pc-windows-msvc-latest.txt";
        private const string GyanReleaseVersion = "https://www.gyan.dev/ffmpeg/builds/release-version";

        internal sealed class UpdateCheckResult
        {
            internal string CurrentVersion;
            internal string RemoteVersion;
            internal bool IsCurrentOrNewer;
        }

        internal static string GetTargetPath(int toolIndex, string configuredPath)
        {
            if (toolIndex < 0 || toolIndex >= ToolNames.Length) throw new ArgumentOutOfRangeException("toolIndex");
            if (String.IsNullOrWhiteSpace(configuredPath))
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", ToolNames[toolIndex]);
            string path = configuredPath.Trim();
            return Path.IsPathRooted(path) ? Path.GetFullPath(path) : Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path));
        }

        internal static async Task<UpdateCheckResult> CheckAsync(int toolIndex, bool nightly, string targetPath, string proxy)
        {
            if (toolIndex < 0 || toolIndex >= ToolNames.Length) throw new ArgumentOutOfRangeException("toolIndex");
            string current = "未安装";
            if (File.Exists(targetPath))
            {
                try { current = await GetVersionAsync(targetPath, toolIndex); }
                catch (Exception ex) { current = "无法读取：" + ex.Message; }
            }
            string remote = await GetRemoteVersionAsync(toolIndex, nightly, proxy);
            return new UpdateCheckResult
            {
                CurrentVersion = current,
                RemoteVersion = remote,
                IsCurrentOrNewer = IsCurrentOrNewer(toolIndex, nightly, current, remote)
            };
        }

        private static async Task<string> GetRemoteVersionAsync(int toolIndex, bool nightly, string proxy)
        {
            if (toolIndex == 0)
            {
                string repo = nightly ? "yt-dlp/yt-dlp-nightly-builds" : "yt-dlp/yt-dlp";
                string endpoint = nightly ? "/releases?per_page=1" : "/releases/latest";
                string json = await DownloadTextAsync("https://api.github.com/repos/" + repo + endpoint, proxy, true);
                return "yt-dlp " + JsonString(json, "tag_name");
            }
            if (toolIndex == 1 || toolIndex == 2)
            {
                if (!nightly)
                    return "FFmpeg " + (await DownloadTextAsync(GyanReleaseVersion, proxy, false)).Trim();
                string json = await DownloadTextAsync("https://api.github.com/repos/BtbN/FFmpeg-Builds/releases/tags/latest", proxy, true);
                Match asset = Regex.Match(json, "ffmpeg-N-(\\d+-g[0-9a-fA-F]+)-win64-gpl\\.zip", RegexOptions.IgnoreCase);
                if (!asset.Success) throw new InvalidDataException("BtbN 未返回可识别的 FFmpeg Nightly 版本号。");
                return "FFmpeg N-" + asset.Groups[1].Value;
            }
            if (!nightly)
            {
                string json = await DownloadTextAsync("https://api.github.com/repos/denoland/deno/releases/latest", proxy, true);
                return "Deno " + JsonString(json, "tag_name").TrimStart('v', 'V');
            }
            string commit = (await DownloadTextAsync(DenoCanaryLatest, proxy, false)).Trim().TrimStart('\uFEFF');
            if (commit.Length < 7 || commit.Any(c => !Uri.IsHexDigit(c))) throw new InvalidDataException("Deno Canary 返回了无效版本号。");
            return "Deno Canary " + commit;
        }

        private static bool IsCurrentOrNewer(int toolIndex, bool nightly, string current, string remote)
        {
            if (String.IsNullOrWhiteSpace(current) || current == "未安装" || current.StartsWith("无法读取：", StringComparison.Ordinal)) return false;
            string localToken = null, remoteToken = null;
            if (toolIndex == 0)
            {
                Match local = Regex.Match(current, @"\d+(?:\.\d+)+(?:[-+][A-Za-z0-9.-]+)?");
                Match target = Regex.Match(remote, @"\d+(?:\.\d+)+(?:[-+][A-Za-z0-9.-]+)?");
                if (local.Success && target.Success) { localToken = local.Value; remoteToken = target.Value; }
            }
            else if (toolIndex == 1 || toolIndex == 2)
            {
                if (nightly)
                {
                    Match local = Regex.Match(current, @"N-(\d+)-g([0-9a-fA-F]+)", RegexOptions.IgnoreCase);
                    Match target = Regex.Match(remote, @"N-(\d+)-g([0-9a-fA-F]+)", RegexOptions.IgnoreCase);
                    if (local.Success && target.Success)
                    {
                        int localBuild, remoteBuild;
                        if (Int32.TryParse(local.Groups[1].Value, out localBuild) && Int32.TryParse(target.Groups[1].Value, out remoteBuild))
                            return localBuild >= remoteBuild;
                        return String.Equals(local.Value, target.Value, StringComparison.OrdinalIgnoreCase);
                    }
                }
                else
                {
                    Match local = Regex.Match(current, @"version\s+(\d+(?:\.\d+)+)", RegexOptions.IgnoreCase);
                    Match target = Regex.Match(remote, @"\d+(?:\.\d+)+");
                    if (local.Success && target.Success) { localToken = local.Groups[1].Value; remoteToken = target.Value; }
                }
            }
            else if (nightly)
            {
                Match local = Regex.Match(current, @"(?:\+|-)([0-9a-fA-F]{7,})\b");
                Match target = Regex.Match(remote, @"([0-9a-fA-F]{7,})$");
                if (local.Success && target.Success) return local.Groups[1].Value.StartsWith(target.Groups[1].Value, StringComparison.OrdinalIgnoreCase) || target.Groups[1].Value.StartsWith(local.Groups[1].Value, StringComparison.OrdinalIgnoreCase);
            }
            else
            {
                Match local = Regex.Match(current, @"deno\s+(\d+(?:\.\d+)+)", RegexOptions.IgnoreCase);
                Match target = Regex.Match(remote, @"\d+(?:\.\d+)+");
                if (local.Success && target.Success) { localToken = local.Groups[1].Value; remoteToken = target.Value; }
            }
            if (localToken == null || remoteToken == null) return String.Equals(current.Trim(), remote.Trim(), StringComparison.OrdinalIgnoreCase);
            int comparison = CompareNumericVersions(localToken, remoteToken);
            return comparison >= 0;
        }

        private static int CompareNumericVersions(string left, string right)
        {
            string[] a = Regex.Matches(left, @"\d+").Cast<Match>().Select(m => m.Value).ToArray();
            string[] b = Regex.Matches(right, @"\d+").Cast<Match>().Select(m => m.Value).ToArray();
            for (int i = 0; i < Math.Max(a.Length, b.Length); i++)
            {
                long x = i < a.Length ? Int64.Parse(a[i]) : 0, y = i < b.Length ? Int64.Parse(b[i]) : 0;
                if (x != y) return x.CompareTo(y);
            }
            return String.Compare(left, right, StringComparison.OrdinalIgnoreCase);
        }

        private static string JsonString(string json, string property)
        {
            Match match = Regex.Match(json ?? "", "\\\"" + Regex.Escape(property) + "\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"");
            if (!match.Success) throw new InvalidDataException("远端版本信息缺少 " + property + " 字段。");
            return match.Groups[1].Value;
        }

        private static async Task<string> DownloadTextAsync(string url, string proxy, bool githubApi)
        {
            string path = Path.Combine(Path.GetTempPath(), "dotSharK-check-" + Guid.NewGuid().ToString("N") + ".txt");
            try
            {
                string extra = githubApi ? " --header \"Accept: application/vnd.github+json\" --header \"User-Agent: dotSharK\"" : "";
                await DownloadAsync(url, path, proxy, extra);
                return File.ReadAllText(path, Encoding.UTF8);
            }
            finally { try { if (File.Exists(path)) File.Delete(path); } catch { } }
        }

        private static async Task<string> GetVersionAsync(string path, int toolIndex)
        {
            string arguments = toolIndex == 1 || toolIndex == 2 ? "-version" : "--version";
            ProcessResult result = await RunProcessAsync(path, arguments, Path.GetDirectoryName(path));
            string output = (result.Stdout + Environment.NewLine + result.Stderr).Trim();
            if (result.ExitCode != 0 || output.Length == 0) throw new InvalidDataException("版本命令未返回有效信息。");
            return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "未知";
        }

        internal static async Task<string> UpdateAsync(int toolIndex, bool nightly, string targetPath, string proxy)
        {
            if (toolIndex < 0 || toolIndex >= ToolNames.Length) throw new ArgumentOutOfRangeException("toolIndex");
            string tempRoot = Path.Combine(Path.GetTempPath(), "dotSharK-update-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);
            try
            {
                string downloadPath;
                string candidatePath;
                if (toolIndex == 0)
                {
                    downloadPath = Path.Combine(tempRoot, "yt-dlp.exe");
                    await DownloadAsync(nightly ? YtDlpNightly : YtDlpStable, downloadPath, proxy);
                    candidatePath = downloadPath;
                }
                else if (toolIndex == 1 || toolIndex == 2)
                {
                    downloadPath = Path.Combine(tempRoot, "ffmpeg.zip");
                    await DownloadAsync(nightly ? FfmpegNightly : FfmpegStable, downloadPath, proxy);
                    candidatePath = ExtractZipExecutable(downloadPath, tempRoot, ToolNames[toolIndex], true);
                }
                else
                {
                    string packageUrl = DenoStable;
                    if (nightly)
                    {
                        string marker = Path.Combine(tempRoot, "deno-canary.txt");
                        await DownloadAsync(DenoCanaryLatest, marker, proxy);
                        string commit = File.ReadAllText(marker, Encoding.UTF8).Trim();
                        if (commit.Length < 7 || commit.Any(c => !Uri.IsHexDigit(c))) throw new InvalidDataException("Deno Canary 返回了无效版本号。");
                        packageUrl = "https://dl.deno.land/canary/" + commit + "/deno-x86_64-pc-windows-msvc.zip";
                    }
                    downloadPath = Path.Combine(tempRoot, "deno.zip");
                    await DownloadAsync(packageUrl, downloadPath, proxy);
                    candidatePath = ExtractZipExecutable(downloadPath, tempRoot, "deno.exe", false);
                }

                string version = await ValidateExecutableAsync(candidatePath, toolIndex);
                InstallAtomically(candidatePath, targetPath);
                return version;
            }
            finally
            {
                try { Directory.Delete(tempRoot, true); } catch { }
            }
        }

        private static async Task DownloadAsync(string url, string destination, string proxy)
        {
            await DownloadAsync(url, destination, proxy, "");
        }

        private static async Task DownloadAsync(string url, string destination, string proxy, string extraArguments)
        {
            string curl = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "curl.exe");
            if (!File.Exists(curl)) throw new FileNotFoundException("找不到 Windows 自带的 curl.exe，无法在线更新。", curl);
            var args = new StringBuilder("--fail --location --silent --show-error --connect-timeout 30 --retry 2 --output ");
            args.Append(Quote(destination));
            args.Append(" --proxy ").Append(Quote(String.IsNullOrWhiteSpace(proxy) ? "" : proxy.Trim()));
            if (!String.IsNullOrWhiteSpace(extraArguments)) args.Append(extraArguments);
            args.Append(' ').Append(Quote(url));
            ProcessResult result = await RunProcessAsync(curl, args.ToString(), "");
            if (result.ExitCode != 0) throw new InvalidOperationException("下载失败：" + (String.IsNullOrWhiteSpace(result.Stderr) ? "curl.exe 退出代码 " + result.ExitCode : result.Stderr.Trim()));
            var file = new FileInfo(destination);
            if (!file.Exists || file.Length == 0) throw new InvalidDataException("下载文件为空，未替换现有组件。");
        }

        private static string ExtractZipExecutable(string archivePath, string tempRoot, string exeName, bool requireBinFolder)
        {
            using (var archive = ZipFile.OpenRead(archivePath))
            {
                string suffix = "/" + exeName;
                var entry = archive.Entries.FirstOrDefault(x =>
                {
                    string name = x.FullName.Replace('\\', '/');
                    bool isExecutable = name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) ||
                        (!requireBinFolder && String.Equals(name, exeName, StringComparison.OrdinalIgnoreCase));
                    return isExecutable && (!requireBinFolder || name.IndexOf("/bin/", StringComparison.OrdinalIgnoreCase) >= 0);
                });
                if (entry == null || entry.Length == 0 || entry.Length > 250L * 1024 * 1024)
                    throw new InvalidDataException("下载包中没有找到有效的 " + exeName + "。");
                string path = Path.Combine(tempRoot, "candidate-" + exeName);
                using (Stream input = entry.Open())
                using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) input.CopyTo(output);
                return path;
            }
        }

        private static async Task<string> ValidateExecutableAsync(string path, int toolIndex)
        {
            string arguments = toolIndex == 1 || toolIndex == 2 ? "-version" : "--version";
            ProcessResult result = await RunProcessAsync(path, arguments, Path.GetDirectoryName(path));
            string output = (result.Stdout + Environment.NewLine + result.Stderr).Trim();
            if (result.ExitCode != 0 || output.Length == 0)
                throw new InvalidDataException("下载的文件未通过版本检查，现有组件未更改。" + (output.Length == 0 ? "" : Environment.NewLine + output));
            return output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "更新成功";
        }

        private static void InstallAtomically(string candidatePath, string targetPath)
        {
            string directory = Path.GetDirectoryName(targetPath);
            if (String.IsNullOrWhiteSpace(directory)) throw new InvalidOperationException("目标目录无效。");
            Directory.CreateDirectory(directory);
            string staging = Path.Combine(directory, "." + Path.GetFileName(targetPath) + ".new-" + Guid.NewGuid().ToString("N"));
            string backup = staging + ".bak";
            File.Copy(candidatePath, staging, false);
            try
            {
                if (File.Exists(targetPath))
                {
                    try { File.Replace(staging, targetPath, backup, true); }
                    catch (PlatformNotSupportedException) { ReplaceWithRollback(staging, targetPath, backup); }
                    catch (IOException) { ReplaceWithRollback(staging, targetPath, backup); }
                    if (File.Exists(backup)) File.Delete(backup);
                }
                else File.Move(staging, targetPath);
            }
            finally
            {
                if (File.Exists(staging)) File.Delete(staging);
                if (File.Exists(backup)) File.Delete(backup);
            }
        }

        private static void ReplaceWithRollback(string staging, string target, string backup)
        {
            if (File.Exists(backup)) File.Delete(backup);
            File.Move(target, backup);
            try { File.Move(staging, target); }
            catch
            {
                if (!File.Exists(target) && File.Exists(backup)) File.Move(backup, target);
                throw;
            }
        }

        private static async Task<ProcessResult> RunProcessAsync(string fileName, string arguments, string workingDirectory)
        {
            var info = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                WorkingDirectory = String.IsNullOrWhiteSpace(workingDirectory) ? Environment.SystemDirectory : workingDirectory
            };
            using (var process = new Process { StartInfo = info })
            {
                process.Start();
                Task<string> stdout = process.StandardOutput.ReadToEndAsync();
                Task<string> stderr = process.StandardError.ReadToEndAsync();
                await Task.Run(() => process.WaitForExit());
                return new ProcessResult { ExitCode = process.ExitCode, Stdout = await stdout, Stderr = await stderr };
            }
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? "").Replace("\"", "\\\"") + "\"";
        }

        private sealed class ProcessResult
        {
            internal int ExitCode;
            internal string Stdout, Stderr;
        }
    }
}
