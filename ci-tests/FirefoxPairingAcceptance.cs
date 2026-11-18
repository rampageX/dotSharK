using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;
using Microsoft.Win32;

class FirefoxPairingAcceptance
{
    static int Main(string[] args)
    {
        var assembly = Assembly.LoadFrom(Path.GetFullPath(args[0]));
        var type = assembly.GetType("dotSharK.BrowserPairing", true);
        var flags = BindingFlags.Static | BindingFlags.NonPublic;
        var save = type.GetMethod("Save", flags);
        var read = type.GetMethod("Read", flags);
        var all = new SortedDictionary<string, string>(StringComparer.Ordinal) {
            { "dotshark@rampagex.github.io", "Firefox" },
            { "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "Chrome" }
        };
        try {
            save.Invoke(null, new object[] { all });
            string keyPath = @"Software\\Mozilla\\NativeMessagingHosts\\com.rampagex.dotshark";
            string path;
            using (var key = Registry.CurrentUser.OpenSubKey(keyPath)) {
                if (key == null) throw new Exception("Firefox registry key missing");
                path = (string)key.GetValue("");
            }
            var manifest = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
            string[] expected = { "name", "description", "path", "type", "allowed_extensions" };
            if (manifest.Count != expected.Length) throw new Exception("Unsupported Firefox manifest fields");
            foreach (var field in expected) if (!manifest.ContainsKey(field)) throw new Exception("Missing " + field);
            var current = (SortedDictionary<string, string>)read.Invoke(null, null);
            if (current["dotshark@rampagex.github.io"] != "Firefox" || current["aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"] != "Chrome") throw new Exception("Pairing labels were not preserved");
            save.Invoke(null, new object[] { current });
            var again = new JavaScriptSerializer().Deserialize<Dictionary<string, object>>(File.ReadAllText(path));
            if (again.Count != expected.Length) throw new Exception("Re-register reintroduced metadata");
            current.Remove("dotshark@rampagex.github.io");
            save.Invoke(null, new object[] { current });
            var remaining = (SortedDictionary<string, string>)read.Invoke(null, null);
            if (!remaining.ContainsKey("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa") || remaining.ContainsKey("dotshark@rampagex.github.io")) throw new Exception("Removing Firefox changed Chromium registration");
            Console.WriteLine("PASS Firefox manifest schema, labels, repeated save and Chromium coexistence");
            return 0;
        } catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        finally { save.Invoke(null, new object[] { new SortedDictionary<string, string>() }); }
    }
}
