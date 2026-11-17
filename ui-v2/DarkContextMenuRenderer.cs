using System;
using System.Drawing;
using System.Windows.Forms;

namespace dotSharK.UiV2
{
    /// <summary>Square, shadowless dark rendering for context and dropdown menus.</summary>
    public sealed class DarkContextMenuRenderer : ToolStripProfessionalRenderer
    {
        private sealed class Colors : ProfessionalColorTable
        {
            public override Color MenuBorder { get { return Theme.Border; } }
            public override Color ToolStripDropDownBackground { get { return Theme.Panel; } }
            public override Color MenuItemSelected { get { return Theme.AccentSelection; } }
            public override Color MenuItemBorder { get { return Theme.Border; } }
            public override Color MenuItemSelectedGradientBegin { get { return Theme.AccentSelection; } }
            public override Color MenuItemSelectedGradientEnd { get { return Theme.AccentSelection; } }
            public override Color ImageMarginGradientBegin { get { return Theme.Panel; } }
            public override Color ImageMarginGradientMiddle { get { return Theme.Panel; } }
            public override Color ImageMarginGradientEnd { get { return Theme.Panel; } }
            public override Color SeparatorDark { get { return Theme.Divider; } }
            public override Color SeparatorLight { get { return Theme.Divider; } }
        }
        public DarkContextMenuRenderer() : base(new Colors()) { RoundedEdges = false; }
        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            var rect = new Rectangle(Point.Empty, e.Item.Size);
            using (var fill = new SolidBrush(e.Item.Selected ? Theme.AccentSelection : Theme.Panel))
                e.Graphics.FillRectangle(fill, rect);
        }
        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        { e.Graphics.Clear(Theme.Panel); }
        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        { DarkPaint.Border(e.Graphics, new Rectangle(Point.Empty, e.ToolStrip.Size), Theme.Border); }
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            using (var p = new Pen(Theme.Divider))
                e.Graphics.DrawLine(p, Theme.Space8, e.Item.Height / 2,
                    Math.Max(Theme.Space8, e.Item.Width - Theme.Space8), e.Item.Height / 2);
        }
        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? Theme.PrimaryText : Theme.DisabledText;
            base.OnRenderItemText(e);
        }
        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = Theme.SecondaryText;
            base.OnRenderArrow(e);
        }
        public static ContextMenuStrip CreateMenu()
        {
            return new ContextMenuStrip
            {
                Renderer = new DarkContextMenuRenderer(),
                BackColor = Theme.Panel,
                ForeColor = Theme.PrimaryText,
                ShowImageMargin = false,
                DropShadowEnabled = false,
                Padding = Padding.Empty
            };
        }
        public static void Apply(ContextMenuStrip menu)
        {
            if (menu == null) return;
            menu.Renderer = new DarkContextMenuRenderer();
            menu.BackColor = Theme.Panel;
            menu.ForeColor = Theme.PrimaryText;
            menu.DropShadowEnabled = false;
        }
    }
}