using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using SSHClient.Models;

namespace SSHClient.Forms
{
    public class ConnectionDialog : Form
    {
        private TextBox txtName, txtHost, txtPort, txtUser, txtPassword, txtKeyPath;
        private RadioButton rdoPassword, rdoKey;
        private Button btnBrowseKey, btnOk, btnCancel;
        public ConnectionProfile Result { get; private set; } = new();

        public ConnectionDialog(ConnectionProfile? existing = null)
        {
            Text = existing == null ? "New Connection" : "Edit Connection";
            Size = new Size(420, 360);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;

            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(12),
                ColumnCount = 2,
                RowCount = 9,
                AutoSize = true
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(layout);

            txtName = AddRow(layout, 0, "Name:");
            txtHost = AddRow(layout, 1, "Host:");
            txtPort = AddRow(layout, 2, "Port:");
            txtUser = AddRow(layout, 3, "Username:");

            // Auth type
            var lblAuth = new Label { Text = "Auth:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
            layout.Controls.Add(lblAuth, 0, 4);
            var authPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
            rdoPassword = new RadioButton { Text = "Password", Checked = true };
            rdoKey = new RadioButton { Text = "Private Key" };
            rdoPassword.CheckedChanged += (_, _) => UpdateAuthMode();
            authPanel.Controls.AddRange(new Control[] { rdoPassword, rdoKey });
            layout.Controls.Add(authPanel, 1, 4);

            txtPassword = AddRow(layout, 5, "Password:");
            txtPassword.PasswordChar = '●';

            var lblKey = new Label { Text = "Key File:", TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
            layout.Controls.Add(lblKey, 0, 6);
            var keyPanel = new Panel { Dock = DockStyle.Fill };
            txtKeyPath = new TextBox { Dock = DockStyle.Fill, ReadOnly = true };
            btnBrowseKey = new Button { Text = "...", Width = 30, Dock = DockStyle.Right };
            btnBrowseKey.Click += BrowseKey_Click;
            keyPanel.Controls.AddRange(new Control[] { txtKeyPath, btnBrowseKey });
            layout.Controls.Add(keyPanel, 1, 6);

            // Buttons
            var btnPanel = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                AutoSize = true
            };
            btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel };
            btnOk = new Button { Text = "Connect", DialogResult = DialogResult.OK };
            btnOk.Click += BtnOk_Click;
            btnPanel.Controls.AddRange(new Control[] { btnCancel, btnOk });
            layout.SetColumnSpan(btnPanel, 2);
            layout.Controls.Add(btnPanel, 0, 8);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            if (existing != null) Populate(existing);
            else txtPort.Text = "22";

            UpdateAuthMode();
        }

        private static TextBox AddRow(TableLayoutPanel layout, int row, string label)
        {
            var lbl = new Label { Text = label, TextAlign = ContentAlignment.MiddleRight, Dock = DockStyle.Fill };
            var txt = new TextBox { Dock = DockStyle.Fill };
            layout.Controls.Add(lbl, 0, row);
            layout.Controls.Add(txt, 1, row);
            return txt;
        }

        private void UpdateAuthMode()
        {
            bool pw = rdoPassword.Checked;
            txtPassword.Enabled = pw;
            txtKeyPath.Enabled = !pw;
            btnBrowseKey.Enabled = !pw;
        }

        private void BrowseKey_Click(object? sender, EventArgs e)
        {
            using var dlg = new OpenFileDialog { Filter = "Key Files|*.pem;*.ppk;*|All Files|*.*" };
            if (dlg.ShowDialog() == DialogResult.OK)
                txtKeyPath.Text = dlg.FileName;
        }

        private void BtnOk_Click(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtHost.Text))
            {
                MessageBox.Show("Host is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }
            if (!int.TryParse(txtPort.Text, out int port) || port < 1 || port > 65535)
            {
                MessageBox.Show("Port must be 1–65535.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
                return;
            }

            Result = new ConnectionProfile
            {
                Id = Result.Id,
                Name = txtName.Text.Trim(),
                Host = txtHost.Text.Trim(),
                Port = port,
                Username = txtUser.Text.Trim(),
                Password = rdoPassword.Checked ? txtPassword.Text : "",
                PrivateKeyPath = rdoKey.Checked ? txtKeyPath.Text : "",
                UseKeyAuth = rdoKey.Checked
            };
        }

        private void Populate(ConnectionProfile p)
        {
            Result.Id = p.Id;
            txtName.Text = p.Name;
            txtHost.Text = p.Host;
            txtPort.Text = p.Port.ToString();
            txtUser.Text = p.Username;
            txtPassword.Text = p.Password;
            txtKeyPath.Text = p.PrivateKeyPath;
            rdoKey.Checked = p.UseKeyAuth;
            rdoPassword.Checked = !p.UseKeyAuth;
        }
    }
}
