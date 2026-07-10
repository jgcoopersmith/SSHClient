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
        private readonly ListBox _peerList;
        private readonly Button _btnPeerNew, _btnPeerEdit, _btnPeerDelete, _btnPeerConnect;
        private List<PeerProfile> _peers;

        public MainForm()
        {
            Text = "SSH Client";
            Size = new Size(1060, 700);
            MinimumSize = new Size(700, 480);
            StartPosition = FormStartPosition.CenterScreen;

            _profiles = ConnectionManager.Load();
            _peers = PeerManager.Load();

            // ── Left panel ────────────────────────────────────────────
            var leftSplit = new SplitContainer
            {
                Dock = DockStyle.Left,
                Width = 230,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 320,
                Panel2MinSize = 160,
                FixedPanel = FixedPanel.None,
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

            // ── Bottom half: Peer chat connections ──
            var lblChat = new Label
            {
                Text = "Chat Peers",
                Dock = DockStyle.Top,
                Height = 22,
                Font = new Font(Font, FontStyle.Bold)
            };

            _lblListenPort = new Label
            {
                Text = "Starting...",
                Dock = DockStyle.Top,
                Height = 16,
                ForeColor = Color.Gray,
                Font = new Font(Font.FontFamily, 8f)
            };

            _peerList = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
            _peerList.DoubleClick += (_, _) => ConnectToPeer();

            _btnPeerNew = new Button { Text = "+", Width = 30 };
            _btnPeerEdit = new Button { Text = "✎", Width = 30 };
            _btnPeerDelete = new Button { Text = "✕", Width = 30 };
            _btnPeerConnect = new Button { Text = "Chat", Width = 50 };

            _btnPeerNew.Click += (_, _) => NewPeer();
            _btnPeerEdit.Click += (_, _) => EditPeer();
            _btnPeerDelete.Click += (_, _) => DeletePeer();
            _btnPeerConnect.Click += (_, _) => ConnectToPeer();

            var peerBtnBar = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 32, AutoSize = false };
            peerBtnBar.Controls.AddRange(new Control[] { _btnPeerNew, _btnPeerEdit, _btnPeerDelete, _btnPeerConnect });

            leftSplit.Panel2.Padding = new Padding(4);
            leftSplit.Panel2.Controls.Add(_peerList);
            leftSplit.Panel2.Controls.Add(_lblListenPort);
            leftSplit.Panel2.Controls.Add(lblChat);
            leftSplit.Panel2.Controls.Add(peerBtnBar);

            // ── Tabs ──────────────────────────────────────────────────
            _tabs = new TabControl { Dock = DockStyle.Fill };
            _tabs.MouseClick += Tabs_MouseClick;

            var splitter = new Splitter { Dock = DockStyle.Left, Width = 4 };

            Controls.Add(_tabs);
            Controls.Add(splitter);
            Controls.Add(leftSplit);

            RefreshProfileList();
            RefreshPeerList();

            // Start chat server
            _chatServer = new ChatServer(Environment.UserName);
            _chatServer.Start();
            _lblListenPort.Text = $"Listening on port {_chatServer.ListenPort}";
            _chatServer.PeerConnected += ChatServer_PeerConnected;
        }

        // ── Chat ──────────────────────────────────────────────────────

        private void ChatServer_PeerConnected(object? sender, ChatPeerEventArgs e)
        {
            BeginInvoke(() => OpenChatTab(e.PeerId, e.DisplayName));
        }

        private void ConnectToPeer()
        {
            var peer = SelectedPeer;
            if (peer == null)
            {
                // Quick-connect without saving
                using var dlg = new PeerDialog();
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                DoConnect(dlg.Result.Host, dlg.Result.Port);
                return;
            }
            DoConnect(peer.Host, peer.Port);
        }

        private void DoConnect(string host, int port)
        {
            try
            {
                _chatServer.ConnectTo(host, port);
                // PeerConnected fires async and opens the tab
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not connect:\n{ex.Message}", "Chat", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OpenChatTab(string peerId, string displayName)
        {
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
        }

        // ── Peer CRUD ─────────────────────────────────────────────────

        private void RefreshPeerList()
        {
            _peerList.Items.Clear();
            foreach (var p in _peers)
                _peerList.Items.Add(p);
        }

        private PeerProfile? SelectedPeer => _peerList.SelectedItem as PeerProfile;

        private void NewPeer()
        {
            using var dlg = new PeerDialog();
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            _peers.Add(dlg.Result);
            PeerManager.Save(_peers);
            RefreshPeerList();
            _peerList.SelectedItem = dlg.Result;
        }

        private void EditPeer()
        {
            if (SelectedPeer == null) return;
            using var dlg = new PeerDialog(SelectedPeer);
            if (dlg.ShowDialog(this) != DialogResult.OK) return;
            var idx = _peers.IndexOf(SelectedPeer);
            _peers[idx] = dlg.Result;
            PeerManager.Save(_peers);
            RefreshPeerList();
        }

        private void DeletePeer()
        {
            if (SelectedPeer == null) return;
            if (MessageBox.Show($"Delete '{SelectedPeer}'?", "Confirm",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            _peers.Remove(SelectedPeer);
            PeerManager.Save(_peers);
            RefreshPeerList();
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
