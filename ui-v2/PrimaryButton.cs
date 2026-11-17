using System.Drawing;

namespace dotSharK.UiV2
{
    /// <summary>Green primary action; inherited Text, Click, Enabled, DialogResult.</summary>
    public sealed class PrimaryButton : DarkButtonBase
    {
        private bool attentionHighlight;
        // Preserve MainForm's clipboard-alert behavior without changing the
        // analysis event handler or the dark button implementation.
        public bool AttentionHighlight
        {
            get { return attentionHighlight; }
            set { if (attentionHighlight != value) { attentionHighlight = value; Invalidate(); } }
        }
        public PrimaryButton() { Height = Theme.ActionHeight; }
        protected override Color NormalColor { get { return attentionHighlight ? Theme.AccentFocus : Theme.Accent; } }
        protected override Color HoverColor { get { return Theme.AccentHover; } }
        protected override Color PressedColor { get { return Theme.AccentPressed; } }
        protected override Color OutlineColor { get { return Theme.Accent; } }
    }
}