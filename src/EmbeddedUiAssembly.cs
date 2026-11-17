using System;
using System.IO;
using System.Reflection;

namespace dotSharK
{
    internal static class EmbeddedUiAssembly
    {
        private static readonly object Sync = new object();
        private static Assembly ui;

        internal static void Initialize()
        {
            lock (Sync)
            {
                if (ui != null) return;
                using (Stream resource = typeof(EmbeddedUiAssembly).Assembly.GetManifestResourceStream("dotSharK.Embedded.DotSharK.UiFoundation.dll"))
                {
                    if (resource == null) throw new InvalidOperationException("Embedded UI assembly is missing.");
                    using (var bytes = new MemoryStream())
                    {
                        resource.CopyTo(bytes);
                        ui = Assembly.Load(bytes.ToArray());
                    }
                }
                AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            }
        }

        private static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            return String.Equals(new AssemblyName(args.Name).Name, "DotSharK.UiFoundation", StringComparison.OrdinalIgnoreCase)
                ? ui : null;
        }
    }
}
