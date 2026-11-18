using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace dotSharK
{
    internal static class BrowserPairing
    {
        internal const string Host = "com.rampagex.dotshark";
        internal const string FirefoxId = "dotshark@rampagex.github.io";
        private static string FirefoxManifest => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BrowserExtension", "NativeMessaging", "firefox", Host + ".json");
        private const string FirefoxKey = @"Software\Mozilla\NativeMessagingHosts\" + Host;
        private const string Origin = "chrome-extension://";
        private static readonly string[] Keys = { @"Software\Google\Chrome", @"Software\Chromium", @"Software\Microsoft\Edge" };
        private static string Manifest => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "BrowserExtension", "NativeMessaging", Host + ".json");
        internal static bool ValidId(string id) => Regex.IsMatch(id ?? "", @"\A[a-p]{32}\z");

        internal static SortedDictionary<string, string> Read()
        {
            var entries = new SortedDictionary<string, string>(StringComparer.Ordinal);
            if (File.Exists(FirefoxManifest))
            {
                var f = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(FirefoxManifest));
                object ids;
                if (f.TryGetValue("allowed_extensions", out ids) && ids is System.Collections.IEnumerable)
                    foreach (var id in (System.Collections.IEnumerable)ids)
                        if (String.Equals(id as string, FirefoxId, StringComparison.Ordinal)) entries[FirefoxId] = f.ContainsKey("dotshark_browser_label") ? Convert.ToString(f["dotshark_browser_label"]) : "Firefox / Floorp";
            }
            if (!File.Exists(Manifest)) return entries;
            var json = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(Manifest));
            object raw;
            if (!json.TryGetValue("allowed_origins", out raw)) return entries;
            var origins = raw as System.Collections.IEnumerable;
            if (origins == null) return entries;
            var labels = json.ContainsKey("dotshark_browser_labels") ? json["dotshark_browser_labels"] as Dictionary<string, object> : null;
            foreach (var item in origins)
            {
                var origin = item as string;
                if (origin == null || !origin.StartsWith(Origin, StringComparison.Ordinal) || !origin.EndsWith("/", StringComparison.Ordinal)) continue;
                string id = origin.Substring(Origin.Length, origin.Length - Origin.Length - 1);
                if (ValidId(id)) entries[id] = labels != null && labels.TryGetValue(id, out raw) ? Convert.ToString(raw) : "旧注册（未标注）";
            }
            return entries;
        }

        private static void SaveFirefox(SortedDictionary<string, string> entries)
        {
            string label;
            if (!entries.TryGetValue(FirefoxId, out label))
            {
                using (var key = Registry.CurrentUser.OpenSubKey(FirefoxKey))
                    if (key != null && String.Equals(key.GetValue("") as string, FirefoxManifest, StringComparison.OrdinalIgnoreCase))
                        Registry.CurrentUser.DeleteSubKey(FirefoxKey, false);
                if (File.Exists(FirefoxManifest)) File.Delete(FirefoxManifest);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(FirefoxManifest));
            string json = new JavaScriptSerializer().Serialize(new {
                name = Host, description = "dotSharK silent video download queue",
                path = Assembly.GetExecutingAssembly().Location, type = "stdio",
                allowed_extensions = new[] { FirefoxId }, dotshark_browser_label = label
            });
            string temp = FirefoxManifest + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try {
                File.WriteAllText(temp, json, new UTF8Encoding(false));
                if (File.Exists(FirefoxManifest)) File.Replace(temp, FirefoxManifest, null);
                else File.Move(temp, FirefoxManifest);
            } finally { if (File.Exists(temp)) File.Delete(temp); }
            using (var key = Registry.CurrentUser.CreateSubKey(FirefoxKey))
                key.SetValue("", FirefoxManifest, RegistryValueKind.String);
        }

        internal static void Save(SortedDictionary<string, string> entries)
        {
            SaveFirefox(entries);
            var chromeEntries = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in entries) if (ValidId(pair.Key)) chromeEntries.Add(pair.Key, pair.Value);
            entries = chromeEntries;
            if (entries.Count == 0)
            {
                foreach (string browser in Keys)
                {
                    string keyPath = browser + @"\NativeMessagingHosts\" + Host;
                    using (var key = Registry.CurrentUser.OpenSubKey(keyPath))
                        if (key != null && String.Equals(key.GetValue("") as string, Manifest, StringComparison.OrdinalIgnoreCase))
                            Registry.CurrentUser.DeleteSubKey(keyPath, false);
                }
                File.Delete(Manifest);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(Manifest));
            string json = new JavaScriptSerializer().Serialize(new {
                name = Host, description = "dotSharK silent video download queue",
                path = Assembly.GetExecutingAssembly().Location, type = "stdio",
                allowed_origins = entries.Keys.Select(id => Origin + id + "/").ToArray(),
                dotshark_browser_labels = entries
            });
            string temp = Manifest + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllText(temp, json, new UTF8Encoding(false));
                if (File.Exists(Manifest)) File.Replace(temp, Manifest, null);
                else File.Move(temp, Manifest);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
            foreach (string browser in Keys)
                using (var key = Registry.CurrentUser.CreateSubKey(browser + @"\NativeMessagingHosts\" + Host))
                    key.SetValue("", Manifest, RegistryValueKind.String);
        }
    }
}
