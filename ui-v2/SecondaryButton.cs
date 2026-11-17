using System.Drawing;

namespace dotSharK.UiV2
{
    /// <summary>Dark secondary action; inherited Text, Click, Enabled, DialogResult.</summary>
    public sealed class SecondaryButton : DarkButtonBase
    {
        protected override Color NormalColor { get { return Theme.Panel; } }
    }
}