using System;
using System.Collections.Generic;

namespace dotSharK
{
    internal sealed class YtDlpOptions
    {
        public string Proxy = "";
        public int ConcurrentFragments = 8;
        public string CookieMode = "browser";
        public string CookieBrowser = "firefox";
        public string CookieProfileRoot = "";
        public string CookieProfile = "";
        public string CookieFile = "";
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
