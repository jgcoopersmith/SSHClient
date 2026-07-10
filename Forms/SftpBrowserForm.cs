using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using Renci.SshNet;
using Renci.SshNet.Sftp;
using SSHClient.Models;

namespace SSHClient.Forms
{
    public class SftpBrowserForm : Form
    {
        private readonly SftpClient _sftp;
        private readonly ListView _listView;
        private readonly TextBox _txtPath;
        private readonly Button _btnUp, _btnRefresh, _btnDownload, _btnUpload, _btnDelete;
        private string _currentPath = "/";

        public SftpBrowserForm(ConnectionProfile profile)
        {
            Text = $"SFTP — {profile}";
            Size = new Size(720, 520);
            StartPosition = FormStartPosition.CenterParent;

            AuthenticationMethod auth = profile.UseKeyAuth
                ? new PrivateKeyAuthenticationMethod(profile.Username, new PrivateKeyFile(profile.PrivateKeyPath))
                : (AuthenticationMethod)new PasswordAuthenticationMethod(profile.Username, profile.Password);

            _sftp = new SftpClient(new ConnectionInfo(profile.Host, profile.Port, profile.Username, auth));

            // Toolbar
            var toolbar = new ToolStrip();
            _btnUp = new Button { Text = "↑ Up", Width = 60 };
            _btnRefresh = new Button { Text = "Refresh", Width = 65 };
            _btnDownload = new Button { Text = "Download", Width = 75 };
            _btnUpload = new Button { Text = "Upload", Width = 65 };
            _btnDelete = new Button { Text = "Delete", Width = 60 };
            _btnUp.Click += (_, _) => NavigateUp();
            _btnRefresh.Click += (_, _) => Refresh(_currentPath);
            _btnDownload.Click += (_, _) => Download();
            _btnUpload.Click += (_, _) => Upload();
            _btnDelete.Click += (_, _) => Delete();

            var btnBar = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 32, AutoSize = false };
            btnBar.Controls.AddRange(new Control[] { _btnUp, _btnRefresh, _btnDownload, _btnUpload, _btnDelete });

            _txtPath = new TextBox { Dock = DockStyle.Top, ReadOnly = true, BackColor = SystemColors.Control };

            _listView = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                MultiSelect = true,
                Font = new Font("Consolas", 9.5f)
            };
            _listView.Columns.Add("Name", 300);
            _listView.Columns.Add("Size", 100);
            _listView.Columns.Add("Modified", 180);
            _listView.DoubleClick += ListView_DoubleClick;

            Controls.Add(_listView);
            Controls.Add(_txtPath);
            Controls.Add(btnBar);

            Load += (_, _) => ConnectAndLoad();
        }

        private void ConnectAndLoad()
        {
            try
            {
                _sftp.Connect();
                Refresh(_currentPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"SFTP connection failed:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Close();
            }
        }

        private void Refresh(string path)
        {
            try
            {
                _currentPath = path;
                _txtPath.Text = path;
                _listView.Items.Clear();

                var entries = new List<ISftpFile>(_sftp.ListDirectory(path));
                entries.Sort((a, b) =>
                {
                    if (a.IsDirectory != b.IsDirectory) return a.IsDirectory ? -1 : 1;
                    return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
                });

                foreach (var entry in entries)
                {
                    if (entry.Name == ".") continue;
                    var item = new ListViewItem(entry.Name);
                    item.SubItems.Add(entry.IsDirectory ? "<DIR>" : FormatSize(entry.Length));
                    item.SubItems.Add(entry.LastWriteTime.ToString("yyyy-MM-dd HH:mm"));
                    item.Tag = entry;
                    if (entry.IsDirectory) item.ForeColor = Color.DodgerBlue;
                    _listView.Items.Add(item);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error listing directory:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void NavigateUp()
        {
            var parent = Path.GetDirectoryName(_currentPath.TrimEnd('/'))?.Replace('\\', '/');
            Refresh(string.IsNullOrEmpty(parent) ? "/" : parent);
        }

        private void ListView_DoubleClick(object? sender, EventArgs e)
        {
            if (_listView.SelectedItems.Count == 0) return;
            if (_listView.SelectedItems[0].Tag is ISftpFile entry && entry.IsDirectory)
                Refresh(entry.FullName);
        }

        private void Download()
        {
            if (_listView.SelectedItems.Count == 0) return;
            using var dlg = new FolderBrowserDialog { Description = "Choose download folder" };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            foreach (ListViewItem item in _listView.SelectedItems)
            {
                if (item.Tag is not ISftpFile entry || entry.IsDirectory) continue;
                var localPath = Path.Combine(dlg.SelectedPath, entry.Name);
                using var fs = File.Create(localPath);
                _sftp.DownloadFile(entry.FullName, fs);
            }
            MessageBox.Show("Download complete.", "SFTP", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void Upload()
        {
            using var dlg = new OpenFileDialog { Multiselect = true, Title = "Select files to upload" };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            foreach (var localPath in dlg.FileNames)
            {
                var remotePath = _currentPath.TrimEnd('/') + "/" + Path.GetFileName(localPath);
                using var fs = File.OpenRead(localPath);
                _sftp.UploadFile(fs, remotePath);
            }
            Refresh(_currentPath);
        }

        private void Delete()
        {
            if (_listView.SelectedItems.Count == 0) return;
            if (MessageBox.Show($"Delete {_listView.SelectedItems.Count} item(s)?", "Confirm",
                MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;

            foreach (ListViewItem item in _listView.SelectedItems)
            {
                if (item.Tag is not ISftpFile entry) continue;
                if (entry.IsDirectory) _sftp.DeleteDirectory(entry.FullName);
                else _sftp.DeleteFile(entry.FullName);
            }
            Refresh(_currentPath);
        }

        private static string FormatSize(long bytes)
        {
            if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
            if (bytes >= 1_048_576) return $"{bytes / 1_048_576.0:F1} MB";
            if (bytes >= 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes} B";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { _sftp?.Disconnect(); _sftp?.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
