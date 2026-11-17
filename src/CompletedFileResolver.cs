using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace dotSharK
{
    /// <summary>
    /// Recover real filesystem paths after legacy console encodings have lost
    /// a Unicode character. Only accept a UNIQUE strong match; never guess.
    /// </summary>
    internal static class CompletedFileResolver
    {
        internal static string Resolve(string recordedPath, string downloadFolder)
        {
            if (!String.IsNullOrWhiteSpace(recordedPath) && File.Exists(recordedPath))
                return recordedPath;
            if (String.IsNullOrWhiteSpace(recordedPath)) return null;

            string recordedName, extension;
            try
            {
                recordedName = Path.GetFileName(recordedPath);
                extension = Path.GetExtension(recordedName);
            }
            catch (ArgumentException) { return null; }
            if (String.IsNullOrWhiteSpace(recordedName) || String.IsNullOrEmpty(extension))
                return null;

            var directories = new List<string>();
            try
            {
                string parent = Path.GetDirectoryName(recordedPath);
                if (!String.IsNullOrWhiteSpace(parent)) directories.Add(parent);
            }
            catch (ArgumentException) { }
            if (!String.IsNullOrWhiteSpace(downloadFolder) &&
                !directories.Any(dir => String.Equals(dir, downloadFolder, StringComparison.OrdinalIgnoreCase)))
                directories.Add(downloadFolder);

            // A bracketed video ID is more reliable than a Unicode title.
            // It survives CP936/UTF-8 console fallbacks and yt-dlp renames.
            var id = Regex.Match(recordedName,
                @" \[(?<value>[A-Za-z0-9_-]{6,64})\](?=\.[^.]+$)");
            foreach (string directory in directories)
            {
                if (!Directory.Exists(directory)) continue;
                try
                {
                    var files = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly);
                    string match = null;
                    foreach (string actual in files)
                    {
                        if (!String.Equals(Path.GetExtension(actual), extension,
                            StringComparison.OrdinalIgnoreCase)) continue;
                        string actualName = Path.GetFileName(actual);
                        bool sameId = id.Success && actualName.EndsWith(
                            " [" + id.Groups["value"].Value + "]" + extension,
                            StringComparison.OrdinalIgnoreCase);
                        bool sameUnicodeName = NamesEquivalent(actualName, recordedName);
                        if (!sameId && !sameUnicodeName) continue;
                        if (match != null) return null; // Ambiguous: do not launch an arbitrary file.
                        match = actual;
                    }
                    if (match != null) return match;
                }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                catch (ArgumentException) { }
            }
            return null;
        }

        private static bool NamesEquivalent(string actual, string recorded)
        {
            try
            {
                if (String.Equals(actual.Normalize(NormalizationForm.FormKC),
                    recorded.Normalize(NormalizationForm.FormKC),
                    StringComparison.OrdinalIgnoreCase)) return true;
                // Trademark/copyright symbols can disappear entirely in a lossy
                // text stream. Remove them ONLY for an exact name comparison.
                return String.Equals(StripSymbols(actual).Normalize(NormalizationForm.FormKC),
                    StripSymbols(recorded).Normalize(NormalizationForm.FormKC),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch (ArgumentException) { return false; }
        }

        private static string StripSymbols(string name)
        {
            return name.Replace("\u2122", "").Replace("\u00ae", "").Replace("\u00a9", "");
        }
    }
}
