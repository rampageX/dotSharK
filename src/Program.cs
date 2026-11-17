using System;
using System.Windows.Forms;

namespace dotSharK
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // Chromium passes the extension origin as the native host's first argument.
            // Do not initialize WinForms for either fully silent entry point.
            if (SilentDownloadBridge.IsNativeHostCall(args))
            {
                SilentDownloadBridge.RunNativeHost();
                return;
            }
            bool fromBrowser = args != null && args.Length == 1 && args[0] == "--silent-worker";
            // Normal launches and browser requests use one resident instance and one queue.
            SilentDownloadBridge.RunApplication(!fromBrowser);
        }
    }
}
