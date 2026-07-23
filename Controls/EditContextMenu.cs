using System;
using System.Windows.Forms;

namespace SSHClient.Controls
{
    /// <summary>
    /// Attaches a right-click Cut/Copy/Paste/Select-All menu to a text control.
    /// Works for both TextBox and RichTextBox (both derive from TextBoxBase).
    /// Read-only controls (terminal output, chat log) get Copy/Select-All only.
    /// </summary>
    public static class EditContextMenu
    {
        public static void Attach(TextBoxBase control)
        {
            var menu = new ContextMenuStrip();

            var cut = new ToolStripMenuItem("Cut", null, (_, _) => control.Cut())
            {
                ShortcutKeyDisplayString = "Ctrl+X"
            };
            var copy = new ToolStripMenuItem("Copy", null, (_, _) => control.Copy())
            {
                ShortcutKeyDisplayString = "Ctrl+C"
            };
            var paste = new ToolStripMenuItem("Paste", null, (_, _) => control.Paste())
            {
                ShortcutKeyDisplayString = "Ctrl+V"
            };
            var selectAll = new ToolStripMenuItem("Select All", null, (_, _) => control.SelectAll())
            {
                ShortcutKeyDisplayString = "Ctrl+A"
            };

            menu.Items.AddRange(new ToolStripItem[]
            {
                cut, copy, paste, new ToolStripSeparator(), selectAll
            });

            // Enable/disable items based on current state each time the menu opens.
            menu.Opening += (_, _) =>
            {
                bool hasSelection = control.SelectionLength > 0;
                bool canPaste = !control.ReadOnly && Clipboard.ContainsText();
                bool hasText = control.TextLength > 0;

                cut.Enabled = hasSelection && !control.ReadOnly;
                copy.Enabled = hasSelection;
                paste.Enabled = canPaste;
                selectAll.Enabled = hasText;
            };

            control.ContextMenuStrip = menu;

            // Ctrl+A isn't wired by default on multiline TextBox/RichTextBox — add it so the
            // menu shortcut hint is truthful and keyboard users get select-all too.
            control.KeyDown += (_, e) =>
            {
                if (e.Control && e.KeyCode == Keys.A)
                {
                    control.SelectAll();
                    e.SuppressKeyPress = true;
                }
            };
        }
    }
}
