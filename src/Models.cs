using System;
using System.Collections.Generic;
using System.IO;

namespace dotSharK
{
    internal sealed class YtDlpOptions
    {
        public string Proxy = "";
        public string GeoVerificationProxy = "";
        public bool ProxyEnabled = true;
        public bool GeoProxyEnabled = true;
        public string Aria2cPath = "";
        public string Aria2cSites = "";
        public string NoProxySites = "";
        public string CustomArguments = "";
        public int ConcurrentFragments = 8;
        public int ConcurrentDownloads = 3;
        public int VideoQualityLimit = 3; // Distinct resolution tiers; 0 = show all.
        public string SilentDownloadFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Downloads", "Silent");
        public string CookieMode = "browser";
        public string CookieBrowser = "firefox";
        public string CookieProfileRoot = "";
        public string CookieProfile = "";
        public string CookieFile = "";
        public bool KeepCompletedTasks;
        public int SilentMaxHeight; // 0=highest; otherwise 4320/2160/1080/720
    }

    internal sealed class BrowserOption
    {
        public string Name, Label;
        public override string ToString() { return Label; }
    }

    internal sealed class MediaInfo
    {
        public string Id, Title, Description, Thumbnail, WebpageUrl;
        public double Duration;
        public List<VideoChoice> VideoChoices = new List<VideoChoice>();
        public List<AudioChoice> AudioChoices = new List<AudioChoice>();
        public List<SubtitleChoice> Subtitles = new List<SubtitleChoice>();
    }

    internal sealed class LinkInspection
    {
        public MediaInfo Media;
        public PlaylistInfo Playlist;
        public bool IsPlaylist { get { return Playlist != null && Playlist.Entries.Count > 0; } }
    }

    internal sealed class PlaylistInfo
    {
        public string Title;
        public List<PlaylistEntry> Entries = new List<PlaylistEntry>();
    }

    internal sealed class PlaylistEntry
    {
        public int Index;
        public string Id, Title, Url;
        public double Duration;
    }

    internal sealed class DownloadQueueItem
    {
        public string Title, Url, Container, SubtitleLanguage, OutputFolder, Status;
        public string FilePath, DisplaySource, DisplayQuality;
        public double ProgressWeight = 1;
        public double ProgressPercent;
        public MediaInfo Media;
        public bool AudioOnly, AutomaticSubtitle, PlaylistItem, BrowserSilent, ArchiveSkipped;
        public string BrowserJobPath;
        public bool PauseRequested;
        public string OriginPageUrl;
        public VideoChoice Video;
        public AudioChoice Audio;
        public SubtitleChoice Subtitle;
        public int TargetHeight;
        public string TargetCodec, TargetDynamicRange;
    }

    internal sealed class VideoChoice
    {
        public string FormatId, Codec, Container, DynamicRange;
        public int Height, Width;
        public double Fps, ApproxBytes;
        public bool HasAudio;
        public string Label
        {
            get
            {
                string size = ApproxBytes > 0 ? "  ·  " + YtDlpClient.FormatBytes(ApproxBytes) : "";
                return String.Format("{0}p  ·  {1}  ·  {2}{3}{4}", Height, Codec, Fps > 0 ? Fps.ToString("0.##") + " fps" : "帧率未知",
                    String.IsNullOrEmpty(DynamicRange) || DynamicRange == "SDR" ? "" : "  ·  " + DynamicRange, size);
            }
        }
    }

    internal sealed class AudioChoice
    {
        public string FormatId, Language, Codec, Container;
        public double ApproxBytes, Bitrate;
        public string Label { get { return (String.IsNullOrEmpty(Language) ? "默认音轨" : Language) + "  ·  " + Codec + (Bitrate > 0 ? "  ·  " + Bitrate.ToString("0") + " kbps" : ""); } }
        public override string ToString() { return Label; }
    }

    internal sealed class SubtitleChoice
    {
        public string Language, Name;
        public bool Automatic;
        public string Label { get { return (Automatic ? "自动字幕 · " : "字幕 · ") + (String.IsNullOrEmpty(Name) ? Language : Name + " (" + Language + ")"); } }
        public override string ToString() { return Label; }
    }
}
