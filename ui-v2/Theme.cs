using System;
using System.Drawing;
using System.Windows.Forms;

namespace dotSharK.UiV2
{
    /// <summary>
    /// UI V2 design tokens. Measurements are 96-DPI logical pixels, and all
    /// component layouts must use these shared tokens rather than literals.
    /// .NET Framework 4.8 / C# 7.3; no external dependencies.
    /// </summary>
    public static class Theme
    {
        public const int ReferenceDpi = 96;
        public const int BorderWidth = 1;
        public const int Space4 = 4;
        public const int Space8 = 8;
        public const int Space12 = 12;
        public const int Space16 = 16;
        public const int Space24 = 24;
        public const int CornerRadius = 0;

        public const int ControlHeight = 32;
        public const int ActionHeight = 36;
        public const int TableHeaderHeight = 36;
        public const int TableRowHeight = 36;
        public const int TabHeight = 40;
        public const int ScrollbarWidth = 10;
        public const int CheckboxSize = 16;
        public const int ArrowRegionWidth = 32;
        public const int NumericArrowHeight = 16;
        public const int IconButtonSize = 32;
        public const int LabelColumnWidth = 96;
        public const int NumericWidth = 80;
        public const int NumericArrowWidth = 24;
        public const int FullNumericWidth = 96;
        public const int BrowseButtonWidth = 80;
        public const int FooterButtonWidth = 88;
        public const int AnalyzeButtonWidth = 96;
        public const int QueueRunButtonWidth = 120;
        public const int SourceColumnWidth = 120;
        public const int QualityColumnWidth = 160;
        public const int StatusColumnWidth = 100;

        public const int BodyFontPx = 13;
        public const int SmallFontPx = 12;
        public const int HeadingFontPx = 14;
        public const int MonoFontPx = 12;

        // From the current accepted dotSharK source palette. Backgrounds and
        // brand green are retained; secondary/success text is brighter.
        public static readonly Color Window = Color.FromArgb(0x22, 0x22, 0x22);
        public static readonly Color Panel = Color.FromArgb(0x2B, 0x2B, 0x2B);
        public static readonly Color Input = Color.FromArgb(0x33, 0x33, 0x33);
        public static readonly Color PrimaryText = Color.FromArgb(0xEB, 0xEE, 0xF0);
        public static readonly Color SecondaryText = Color.FromArgb(0xA8, 0xB3, 0xB7);
        public static readonly Color MutedText = Color.FromArgb(0x99, 0xA3, 0xAA);
        public static readonly Color Accent = Color.FromArgb(0x25, 0x74, 0x5B);
        public static readonly Color AccentHover = Color.FromArgb(0x2A, 0x7A, 0x60);
        public static readonly Color AccentPressed = Color.FromArgb(0x21, 0x68, 0x4F);
        public static readonly Color AccentSelection = Color.FromArgb(0x2C, 0x41, 0x39);
        public static readonly Color AccentFocus = Color.FromArgb(0x4D, 0xD4, 0x9B);
        public static readonly Color SuccessText = Color.FromArgb(0x53, 0xD6, 0xA0);
        public static readonly Color Warning = Color.FromArgb(0xE6, 0xB1, 0x3D);
        public static readonly Color Error = Color.FromArgb(0xF0, 0x7A, 0x75);
        public static readonly Color Border = Color.FromArgb(0x41, 0x41, 0x41);
        public static readonly Color Divider = Color.FromArgb(0x38, 0x38, 0x38);
        public static readonly Color Hover = Color.FromArgb(0x38, 0x38, 0x38);
        public static readonly Color Pressed = Color.FromArgb(0x27, 0x27, 0x27);
        public static readonly Color DisabledText = Color.FromArgb(0x78, 0x80, 0x84);
        public static readonly Color ScrollTrack = Color.FromArgb(0x29, 0x29, 0x29);
        public static readonly Color ScrollThumb = Color.FromArgb(0x55, 0x5B, 0x5B);
        public static readonly Color ScrollThumbHover = Color.FromArgb(0x70, 0x7A, 0x76);
        public static readonly Color ScrollThumbDrag = Color.FromArgb(0x7C, 0x98, 0x88);

        public static readonly Font BodyFont = new Font("Microsoft YaHei UI", BodyFontPx,
            FontStyle.Regular, GraphicsUnit.Pixel);
        public static readonly Font SmallFont = new Font("Microsoft YaHei UI", SmallFontPx,
            FontStyle.Regular, GraphicsUnit.Pixel);
        public static readonly Font HeadingFont = new Font("Microsoft YaHei UI", HeadingFontPx,
            FontStyle.Bold, GraphicsUnit.Pixel);
        public static readonly Font MonoFont = new Font("Consolas", MonoFontPx,
            FontStyle.Regular, GraphicsUnit.Pixel);

        // Sizable settings window: enough room for the longest (YT-DLP) page
        // without any page-level scrollbar, and no restrictive maximum size.
        public static readonly Size SettingsClientSize = new Size(864, 760);
        public static readonly Size SettingsMinimumSize = new Size(840, 760);
        public static readonly Size MainMinimumSize = new Size(900, 680);
        public static readonly Size PlaylistMinimumSize = new Size(960, 640);
        public static readonly Size QualityMinimumSize = new Size(760, 560);
        public static readonly Size DialogMinimumSize = new Size(520, 360);

        // Use ONLY for explicit physical-pixel measurements outside WinForms'
        // automatic DPI scaling; do not scale the same value twice.
        public static int DevicePixels(Control control, int logicalPixels)
        {
            if (control == null) throw new ArgumentNullException("control");
            return (int)Math.Round(logicalPixels * control.DeviceDpi / (double)ReferenceDpi,
                MidpointRounding.AwayFromZero);
        }
    }
}
