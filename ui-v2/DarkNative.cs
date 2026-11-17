using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace dotSharK.UiV2
{
    /// <summary>Win32 interop for nonclient theme and future custom scrolling.</summary>
    internal static class DarkNative
    {
        internal const int DwmUseImmersiveDarkMode = 20;
        internal const int DwmUseImmersiveDarkModeOld = 19;
        internal const int DwmWindowCornerPreference = 33;
        internal const int DwmBorderColor = 34;
        internal const int DwmCaptionColor = 35;
        internal const int DwmTextColor = 36;
        internal const int DwmCornerDoNotRound = 1;

        internal const int SbHorz = 0;
        internal const int SbVert = 1;
        internal const int SbBoth = 3;
        internal const int SifRange = 0x0001;
        internal const int SifPage = 0x0002;
        internal const int SifPos = 0x0004;
        internal const int SifTrackPos = 0x0010;
        internal const int SifAll = SifRange | SifPage | SifPos | SifTrackPos;
        internal const int WmVScroll = 0x0115;
        internal const int WmHScroll = 0x0114;
        internal const int WmMouseWheel = 0x020A;

        [StructLayout(LayoutKind.Sequential)]
        internal struct ScrollInfo
        {
            internal uint cbSize;
            internal uint fMask;
            internal int nMin;
            internal int nMax;
            internal uint nPage;
            internal int nPos;
            internal int nTrackPos;
        }

        [DllImport("dwmapi.dll", ExactSpelling = true, PreserveSig = true)]
        internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute,
            ref int value, int valueSize);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode, ExactSpelling = true,
            PreserveSig = true)]
        internal static extern int SetWindowTheme(IntPtr hwnd, string subAppName,
            string subIdList);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ShowScrollBar(IntPtr hwnd, int bar,
            [MarshalAs(UnmanagedType.Bool)] bool show);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true,
            SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetScrollInfo(IntPtr hwnd, int bar,
            ref ScrollInfo info);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, ExactSpelling = true,
            SetLastError = true)]
        internal static extern int SetScrollInfo(IntPtr hwnd, int bar,
            ref ScrollInfo info, [MarshalAs(UnmanagedType.Bool)] bool redraw);

        [DllImport("user32.dll", EntryPoint = "SendMessageW", CharSet = CharSet.Unicode,
            ExactSpelling = true)]
        internal static extern IntPtr SendMessage(IntPtr hwnd, int message,
            IntPtr wParam, IntPtr lParam);

        internal static ScrollInfo NewScrollInfo()
        {
            return new ScrollInfo
            {
                cbSize = (uint)Marshal.SizeOf(typeof(ScrollInfo)),
                fMask = SifAll
            };
        }

        internal static bool TrySetDwm(IntPtr hwnd, int attribute, int value)
        {
            if (hwnd == IntPtr.Zero) return false;
            try
            {
                return DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int)) >= 0;
            }
            catch (DllNotFoundException) { return false; }
            catch (EntryPointNotFoundException) { return false; }
        }

        internal static bool TrySetExplorerTheme(IntPtr hwnd)
        {
            if (hwnd == IntPtr.Zero) return false;
            try { return SetWindowTheme(hwnd, "DarkMode_Explorer", null) >= 0; }
            catch (DllNotFoundException) { return false; }
            catch (EntryPointNotFoundException) { return false; }
        }

        internal static int ColorRef(Color color)
        {
            return color.R | (color.G << 8) | (color.B << 16);
        }
    }
}
