using System;
using System.Drawing;
using System.Windows.Forms;
using SSHClient.Models;

namespace SSHClient.Forms
{
    public class PeerDialog : Form
    {
        private readonly TextBox _txtName, _txtHost, _txtPort;
        public PeerProfile Result { get; private set; } = new();

        public PeerDialog(PeerProfile? existing = null)
        {
            Text = existing == null ? "New Peer" : "Edit Peer";
            Size = new Size(340, 175);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 2,
                RowCount = 4
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(layout);

            _txtName = AddRow(layout, 0, "Name:");
            _txtHost = AddRow(layout, 1, "Host:");
            _txtPort = AddRow(layout, 2, "Port:");

            var btnOk = new Button { Text = "Save", DialogResult = DialogResult.OK };
            btnOk.Click += BtnOk_Click;
            var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            var btnPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
            btnPanel.Controls.AddRange(new Control[] { btnCancel, btnOk });
            layout.SetColumnSpan(btnPanel, 2);
            layout.Controls.Add(btnPanel, 0, 3);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            if (existing != null) Populate(existing);
            else _txtPort.Text = "9000";
        }

        private static TextBox AddRow(TableLayoutPanel layout, int row, string label)
        {
            layout.Controls.Add(new Label { Text = label, TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, row);
            var txt = new TextBox { Dock = DockStyle.Fill };
            layout.Controls.Add(txt, 1, row);
            return txt;
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_txtHost.Text))
            {
                MessageBox.Show("Host is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }
            if (!int.TryParse(_txtPort.Text, out int port) || port < 1 || port > 65535)
            {
                MessageBox.Show("Port must be 1–65535.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }
            Result = new PeerProfile { Id = Result.Id, Name = _txtName.Text.Trim(), Host = _txtHost.Text.Trim(), Port = port };
        }

        private void Populate(PeerProfile p)
        {
            Result.Id = p.Id;
            _txtName.Text = p.Name;
            _txtHost.Text = p.Host;
            _txtPort.Text = p.Port.ToString();
        }
    }
}
