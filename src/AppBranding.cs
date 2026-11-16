using System;
using System.Drawing;
using System.IO;
using System.Reflection;

namespace dotSharK
{
    internal static class AppBranding
    {
        private static readonly byte[] iconData = LoadIconData();

        internal static Icon CreateIcon()
        {
            using (var stream = new MemoryStream(iconData, false)) return new Icon(stream);
        }

        private static byte[] LoadIconData()
        {
            using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("dotSharK.Resources.dotShark.ico"))
            {
                if (stream == null) throw new InvalidOperationException("The dotSharK application icon resource is missing.");
                using (var memory = new MemoryStream()) { stream.CopyTo(memory); return memory.ToArray(); }
            }
        }
    }
}
