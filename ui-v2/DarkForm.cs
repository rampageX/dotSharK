using System;
using System.Drawing;
using System.Windows.Forms;

namespace dotSharK.UiV2
{
    /// <summary>
    /// Base class for all V2 windows. Does not modify business handlers.
    /// All child control painting / custom focus rings are implemented in phase 2.
    /// .NET Framework 4.8, C# 7.3.
    /// </summary>
    public class DarkForm : Form
    {
        public bool DarkCaptionApplied { get; private set; }
        public bool SquareCornersApplied { get; private set; }
        public bool CaptionColorsApplied { get; private set; }

        public DarkForm()
        {
            SuspendLayout();
            try
            {
                AutoScaleMode = AutoScaleMode.Dpi;
                Font = Theme.BodyFont;
                BackColor = Theme.Window;
                ForeColor = Theme.PrimaryText;
                MinimumSize = Theme.DialogMinimumSize;
                StartPosition = FormStartPosition.CenterParent;
                KeyPreview = true;
                SetStyle(ControlStyles.UserPaint |
                         ControlStyles.AllPaintingInWmPaint |
                         ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.ResizeRedraw, true);
                DoubleBuffered = true;
            }
            finally { ResumeLayout(false); }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyWindowChrome();
        }

        // WinForms will automatically dispatch Enter/Esc through the dialog's
        // AcceptButton and CancelButton. Override only Escape to keep the
        // behavior consistent in bespoke focusable controls; do not close the
        // main window when a dialog's CancelButton is null.
        protected override bool ProcessDialogKey(Keys keyData)
        {
            if (keyData == Keys.Escape && CancelButton != null)
            {
                CancelButton.PerformClick();
                return true;
            }
            return base.ProcessDialogKey(keyData);
        }

        protected void AssignDialogButtons(IButtonControl accept, IButtonControl cancel)
        {
            AcceptButton = accept;
            CancelButton = cancel;
        }

        protected void ApplyWindowChrome()
        {
            if (!IsHandleCreated || IsDisposed) return;

            DarkCaptionApplied = DarkNative.TrySetDwm(Handle,
                DarkNative.DwmUseImmersiveDarkMode, 1);
            if (!DarkCaptionApplied)
                DarkCaptionApplied = DarkNative.TrySetDwm(Handle,
                    DarkNative.DwmUseImmersiveDarkModeOld, 1);

            // Unsupported attributes fail harmlessly on older Windows builds.
            // No window style, ShowInTaskbar or native handle recreation.
            SquareCornersApplied = DarkNative.TrySetDwm(Handle,
                DarkNative.DwmWindowCornerPreference,
                DarkNative.DwmCornerDoNotRound);

            bool background = DarkNative.TrySetDwm(Handle,
                DarkNative.DwmCaptionColor, DarkNative.ColorRef(Theme.Window));
            bool outline = DarkNative.TrySetDwm(Handle,
                DarkNative.DwmBorderColor, DarkNative.ColorRef(Theme.Border));
            bool text = DarkNative.TrySetDwm(Handle,
                DarkNative.DwmTextColor, DarkNative.ColorRef(Theme.PrimaryText));
            CaptionColorsApplied = background && outline && text;
        }
    }
}
