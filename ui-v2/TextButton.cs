using System.Drawing;

namespace dotSharK.UiV2
{
    /// <summary>Flat text-only button with focus border; inherited Click/Enabled.</summary>
    public sealed class TextButton : DarkButtonBase
    {
        protected override Color NormalColor { get { return Theme.Window; } }
        protected override Color OutlineColor { get { return Theme.Window; } }
        protected override Color TextColor { get { return Theme.SecondaryText; } }
    }
}