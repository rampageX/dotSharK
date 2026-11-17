using System.Drawing;

namespace dotSharK.UiV2
{
    /// <summary>Green primary action; inherited Text, Click, Enabled, DialogResult.</summary>
    public sealed class PrimaryButton : DarkButtonBase
    {
        public PrimaryButton() { Height = Theme.ActionHeight; }
        protected override Color NormalColor { get { return Theme.Accent; } }
        protected override Color HoverColor { get { return Theme.AccentHover; } }
        protected override Color PressedColor { get { return Theme.AccentPressed; } }
        protected override Color OutlineColor { get { return Theme.Accent; } }
    }
}