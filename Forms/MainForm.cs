using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using SSHClient.Controls;
using SSHClient.Models;
using SSHClient.Services;

namespace SSHClient.Forms
{
    public class MainForm : Form
    {
        // SSH
        private readonly ListBox _profileList;
        private readonly TabControl _tabs;
        private readonly Button _btnNew, _btnEdit, _btnDelete, _btnConnect, _btnSftp;
        private List<ConnectionProfile> _profiles;

        // Chat
        private readonly ChatServer _chatServer;
        private readonly Label _lblListenPort;
        private readonly Button _btnChatConnect;
        private int _chatTabCounter = 0;

        public MainForm()
        {
            Text = "SSH Client";
            Size = new Size(1060, 700);
            MinimumSize = new Size(700, 480);
            StartPosition = FormStartPosition.CenterScreen;

            _profiles = ConnectionManager.Load();

            // ── Left panel ────────────────────────────────────────────
            // SplitContainer keeps the chat section always visible regardless of height.
            var leftSplit = new SplitContainer
            {
                Dock = DockStyle.Left,
                Width = 230,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 320,
                Panel2MinSize = 100,    // chat section never gets smaller than this
                FixedPanel = FixedPanel.Panel2,
                BorderStyle = BorderStyle.None
            };

            // ── Top half: SSH connections ──
            var lblSsh = new Label
            {
                Text = "SSH Connections",
                Dock = DockStyle.Top,
                Height = 22,
                Font = new Font(Font, FontStyle.Bold)
            };

            _profileList = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
            _profileList.DoubleClick += (_, _) => OpenTerminal();

            _btnNew = new Button { Text = "+", Width = 30 };
            _btnEdit = new Button { Text = "✎", Width = 30 };
            _btnDelete = new Button { Text = "✕", Width = 30 };
            _btnConnect = new Button { Text = "Connect", Width = 66 };
            _btnSftp = new Button { Text = "SFTP", Width = 50 };

            _btnNew.Click += (_, _) => NewProfile();
            _btnEdit.Click += (_, _) => EditProfile();
            _btnDelete.Click += (_, _) => DeleteProfile();
            _btnConnect.Click += (_, _) => OpenTerminal();
            _btnSftp.Click += (_, _) => OpenSftp();

            var sshBtnBar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 32, AutoSize = false };
            sshBtnBar.Controls.AddRange(new Control[] { _btnNew, _btnEdit, _btnDelete, _btnConnect, _btnSftp });

            leftSplit.Panel1.Padding = new Padding(4);
            leftSplit.Panel1.Controls.Add(_profileList);
            leftSplit.Panel1.Controls.Add(lblSsh);
            leftSplit.Panel1.Controls.Add(sshBtnBar);

            // ── Bottom half: Chat (always visible) ──
            var lblChat = new Label
            {
                Text = "Chat",
                Dock = DockStyle.Top,
                Height = 22,
                Font = new Font(Font, FontStyle.Bold)
            };

            _lblListenPort = new Label
            {
                Text = "Starting...",
                Dock = DockStyle.Top,
                Height = 18,
                ForeColor = Color.Gray,
                Font = new Font(Font.FontFamily, 8f)
            };

            _btnChatConnect = new Button { Text = "Connect to Peer", Dock = DockStyle.Bottom, Height = 28 };
            _btnChatConnect.Click += (_, _) => ConnectToPeer();

            leftSplit.Panel2.Padding = new Padding(4);
            leftSplit.Panel2.Controls.Add(_btnChatConnect);
            leftSplit.Panel2.Controls.Add(_lblListenPort);
            leftSplit.Panel2.Controls.Add(lblChat);

            // ── Tabs ──────────────────────────────────────────────────
            _tabs = new TabControl { Dock = DockStyle.Fill };
            _tabs.MouseClick += Tabs_MouseClick;

            var splitter = new Splitter { Dock = DockStyle.Left, Width = 4 };

            Controls.Add(_tabs);
            Controls.Add(splitter);
            Controls.Add(leftSplit);

            RefreshProfileList();

            // Start chat server
            _chatServer = new ChatServer(Environment.UserName);
            _chatServer.Start();
            _lblListenPort.Text = $"Listening on port {_chatServer.ListenPort}";
            _chatServer.PeerConnected += ChatServer_PeerConnected;
        }

        // ── Chat ──────────────────────────────────────────────────────

        private void ChatServer_PeerConnected(object? sender, ChatPeerEventArgs e)
        {
            // Incoming connection from a peer — open a chat tab on the UI thread
            BeginInvoke(() => OpenChatTab(e.PeerId, e.DisplayName));
        }

        private void ConnectToPeer()
        {
            using var dlg = new ChatConnectDialog();
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            try
            {
                var peer = _chatServer.ConnectTo(dlg.Host, dlg.Port);
                // The HELLO handshake runs async; PeerConnected will fire and open the tab.
                // Wait briefly so the tab is ready; if the event fires before we return, fine.
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not connect:\n{ex.Message}", "Chat", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenChatTab(string peerId, string displayName)
        {
            // Avoid duplicate tabs for the same peer
            foreach (TabPage existing in _tabs.TabPages)
            {
                if (existing.Tag is ChatPanel cp && cp.PeerId == peerId)
                {
                    _tabs.SelectedTab = existing;
                    return;
                }
            }

            var panel = new ChatPanel(_chatServer, peerId, displayName);
            panel.AppendSystem($"Connected to {displayName}.");

            var tab = new TabPage($"💬 {displayName}") { Tag = panel, Padding = new Padding(0) };
            tab.Controls.Add(panel);
            _tabs.TabPages.Add(tab);
            _tabs.SelectedTab = tab;
            _chatTabCounter++;
        }

        // ── Tabs ──────────────────────────────────────────────────────

        private void Tabs_MouseClick(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right) return;
            for (int i = 0; i < _tabs.TabPages.Count; i++)
            {
                if (!_tabs.GetTabRect(i).Contains(e.Location)) continue;
                var tab = _tabs.TabPages[i];
                var menu = new ContextMenuStrip();
                menu.Items.Add("Close", null, (_, _) => CloseTab(tab));
                menu.Show(_tabs, e.Location);
                break;
            }
        }

        private void CloseTab(TabPage tab)
        {
            if (tab.Tag is TerminalPanel tp) tp.Disconnect();
            _tabs.TabPages.Remove(tab);
            tab.Dispose();
        }

        // ── SSH ───────────────────────────────────────────────────────

        private void RefreshProfileList()
        {
            _profileList.Items.Clear();
            foreach (var p in _profiles)
                _profileList.Items.Add(p);
        }

        private ConnectionProfile? SelectedProfile =>
            _profileList.SelectedItem as ConnectionProfile;

        private void NewProfile()
        {
            using var dlg = new ConnectionDialog();
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            _profiles.Add(dlg.Result);
            ConnectionManager.Save(_profiles);
            RefreshProfileList();
            _profileList.SelectedItem = dlg.Result;
        }

        private void EditProfile()
        {
            if (SelectedProfile == null) return;
            using var dlg = new ConnectionDialog(SelectedProfile);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            var idx = _profiles.IndexOf(SelectedProfile);
            _profiles[idx] = dlg.Result;
            ConnectionManager.Save(_profiles);
            RefreshProfileList();
        }

        private void DeleteProfile()
        {
            if (SelectedProfile == null) return;
            if (MessageBox.Show($"Delete '{SelectedProfile}'?", "Confirm",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _profiles.Remove(SelectedProfile);
            ConnectionManager.Save(_profiles);
            RefreshProfileList();
        }

        private void OpenTerminal()
        {
            var profile = SelectedProfile;
            if (profile == null)
            {
                using var dlg = new ConnectionDialog();
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                profile = dlg.Result;
            }

            var terminal = new TerminalPanel(profile);
            var tab = new TabPage(profile.ToString()) { Tag = terminal, Padding = new Padding(0) };
            tab.Controls.Add(terminal);
            _tabs.TabPages.Add(tab);
            _tabs.SelectedTab = tab;
            terminal.Connect();
        }

        private void OpenSftp()
        {
            var profile = SelectedProfile;
            if (profile == null)
            {
                using var dlg = new ConnectionDialog();
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                profile = dlg.Result;
            }
            new SftpBrowserForm(profile).Show(this);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            _chatServer.Dispose();
            base.OnFormClosing(e);
        }
    }
}
