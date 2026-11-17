using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;

namespace dotSharK
{
    // Persist only finished task labels and local destinations; no cookie,
    // proxy credential or signed source URL is stored in this sidecar file.
    internal static class CompletedQueueStore
    {
        private sealed class Record
        {
            public string Title { get; set; }
            public string Source { get; set; }
            public string Quality { get; set; }
            public string OutputFolder { get; set; }
            public string FilePath { get; set; }
        }

        internal static List<DownloadQueueItem> Load(string path)
        {
            var result = new List<DownloadQueueItem>();
            if (!File.Exists(path)) return result;
            var records = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }
                .Deserialize<List<Record>>(File.ReadAllText(path, Encoding.UTF8));
            if (records == null) return result;
            foreach (var record in records)
            {
                if (record == null || String.IsNullOrWhiteSpace(record.Title)) continue;
                result.Add(new DownloadQueueItem
                {
                    Title = record.Title, DisplaySource = record.Source,
                    DisplayQuality = record.Quality, OutputFolder = record.OutputFolder,
                    FilePath = record.FilePath, ProgressPercent = 100, Status = "已完成"
                });
            }
            return result;
        }

        internal static void Save(string path, IEnumerable<DownloadQueueItem> completed)
        {
            var records = completed.Where(item => item != null && item.Status == "已完成")
                .Select(item => new Record
                {
                    Title = item.Title, Source = item.DisplaySource,
                    Quality = item.DisplayQuality, OutputFolder = item.OutputFolder,
                    FilePath = item.FilePath
                }).ToList();
            if (records.Count == 0)
            {
                if (File.Exists(path)) File.Delete(path);
                return;
            }
            string temp = path + ".tmp";
            string json = new JavaScriptSerializer { MaxJsonLength = Int32.MaxValue }.Serialize(records);
            File.WriteAllText(temp, json, new UTF8Encoding(false));
            if (File.Exists(path))
            {
                try { File.Replace(temp, path, null); }
                catch (PlatformNotSupportedException) { File.Copy(temp, path, true); File.Delete(temp); }
            }
            else File.Move(temp, path);
        }
    }
}
