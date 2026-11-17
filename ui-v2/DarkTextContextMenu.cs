using System;
using System.Windows.Forms;

namespace dotSharK.UiV2
{
    /// <summary>Shared dark right-click editor menu: Cut / Copy / Paste / Select All.</summary>
    public static class DarkTextContextMenu
    {
        public static ContextMenuStrip Create(TextBoxBase editor)
        {
            if (editor == null) throw new ArgumentNullException("editor");
            var menu = DarkContextMenuRenderer.CreateMenu();
            var cut = new ToolStripMenuItem("剪切");
            var copy = new ToolStripMenuItem("复制");
            var paste = new ToolStripMenuItem("粘贴");
            var selectAll = new ToolStripMenuItem("全选");
            cut.Click += (s, e) => editor.Cut();
            copy.Click += (s, e) => editor.Copy();
            paste.Click += (s, e) => editor.Paste();
            selectAll.Click += (s, e) => editor.SelectAll();
            menu.Items.AddRange(new ToolStripItem[] { cut, copy, paste,
                new ToolStripSeparator(), selectAll });
            menu.Opening += (s, e) =>
            {
                cut.Enabled = !editor.ReadOnly && editor.SelectionLength > 0;
                copy.Enabled = editor.SelectionLength > 0;
                paste.Enabled = !editor.ReadOnly && Clipboard.ContainsText();
                selectAll.Enabled = editor.TextLength > 0;
            };
            editor.ContextMenuStrip = menu;
            editor.Disposed += (s, e) => menu.Dispose();
            return menu;
        }
    }
}