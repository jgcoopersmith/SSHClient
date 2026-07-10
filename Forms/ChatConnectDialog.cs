using System;
using System.Drawing;
using System.Windows.Forms;

namespace SSHClient.Forms
{
    public class ChatConnectDialog : Form
    {
        private readonly TextBox _txtHost, _txtPort;
        public string Host => _txtHost.Text.Trim();
        public int Port => int.TryParse(_txtPort.Text, out var p) ? p : 0;

        public ChatConnectDialog()
        {
            Text = "Connect to Chat Peer";
            Size = new Size(340, 160);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 2,
                RowCount = 3
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(layout);

            layout.Controls.Add(new Label { Text = "Host:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 0);
            _txtHost = new TextBox { Dock = DockStyle.Fill };
            layout.Controls.Add(_txtHost, 1, 0);

            layout.Controls.Add(new Label { Text = "Port:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill }, 0, 1);
            _txtPort = new TextBox { Dock = DockStyle.Fill };
            layout.Controls.Add(_txtPort, 1, 1);

            var btnOk = new Button { Text = "Connect", DialogResult = DialogResult.OK };
            btnOk.Click += BtnOk_Click;
            var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            var btnPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill };
            btnPanel.Controls.AddRange(new Control[] { btnCancel, btnOk });
            layout.SetColumnSpan(btnPanel, 2);
            layout.Controls.Add(btnPanel, 0, 2);

            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_txtHost.Text) || Port <= 0)
            {
                MessageBox.Show("Enter a valid host and port.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        }
    }
}
