using System;
using System.Drawing;
using System.Windows.Forms;
using SSHClient.Services;

namespace SSHClient.Controls
{
    public class ChatPanel : UserControl
    {
        private readonly RichTextBox _log;
        private readonly TextBox _input;
        private readonly Button _btnSend;
        private readonly ChatServer _server;
        private readonly string? _targetPeerId; // null = broadcast to all

        public string PeerId => _targetPeerId ?? "(all)";

        public ChatPanel(ChatServer server, string? targetPeerId, string peerDisplayName)
        {
            _server = server;
            _targetPeerId = targetPeerId;

            _log = new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BackColor = Color.FromArgb(20, 20, 30),
                ForeColor = Color.WhiteSmoke,
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.Vertical
            };

            _input = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.FixedSingle
            };
            _input.KeyDown += Input_KeyDown;

            EditContextMenu.Attach(_log);   // copy / select-all on the read-only chat log
            EditContextMenu.Attach(_input); // cut / copy / paste on the message box

            _btnSend = new Button { Text = "Send", Width = 60, Dock = DockStyle.Right };
            _btnSend.Click += (_, _) => SendMessage();

            var inputBar = new Panel { Dock = DockStyle.Bottom, Height = 30 };
            inputBar.Controls.AddRange(new Control[] { _input, _btnSend });

            var header = new Label
            {
                Text = $"Chat with: {peerDisplayName}",
                Dock = DockStyle.Top,
                Height = 22,
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                ForeColor = Color.SteelBlue,
                BackColor = Color.FromArgb(30, 30, 40)
            };

            Controls.Add(_log);
            Controls.Add(inputBar);
            Controls.Add(header);
            Dock = DockStyle.Fill;

            server.MessageReceived += Server_MessageReceived;
            server.PeerDisconnected += Server_PeerDisconnected;
        }

        private void Server_MessageReceived(object? sender, ChatMessageEventArgs e)
        {
            if (_targetPeerId != null && e.PeerId != _targetPeerId) return;
            AppendMessage(e.Sender, e.Message, Color.LightSkyBlue);
        }

        private void Server_PeerDisconnected(object? sender, ChatPeerEventArgs e)
        {
            if (_targetPeerId != null && e.PeerId != _targetPeerId) return;
            AppendSystem($"{e.DisplayName} disconnected.");
        }

        private void SendMessage()
        {
            var text = _input.Text.Trim();
            if (text.Length == 0) return;
            _input.Clear();

            if (_targetPeerId == null)
                _server.Broadcast(text);
            else
                _server.SendTo(_targetPeerId, text);

            AppendMessage(_server.LocalUsername, text, Color.LightGreen);
        }

        private void Input_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SendMessage();
            }
        }

        /// <summary>Put keyboard focus on the message box so the user can type immediately.</summary>
        public void FocusInput()
        {
            if (!IsHandleCreated) { HandleCreated += (_, _) => BeginInvoke(FocusInput); return; }
            BeginInvoke(() => _input.Focus());
        }

        // When this tab/panel gains focus, redirect it to the message box.
        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            FocusInput();
        }

        private void AppendMessage(string sender, string text, Color nameColor)
        {
            if (InvokeRequired) { BeginInvoke(() => AppendMessage(sender, text, nameColor)); return; }

            var ts = DateTime.Now.ToString("HH:mm");
            _log.SelectionStart = _log.TextLength;
            _log.SelectionColor = Color.Gray;
            _log.AppendText($"[{ts}] ");
            _log.SelectionColor = nameColor;
            _log.AppendText($"{sender}: ");
            _log.SelectionColor = Color.WhiteSmoke;
            _log.AppendText(text + "\n");
            _log.ScrollToCaret();
        }

        public void AppendSystem(string text)
        {
            if (InvokeRequired) { BeginInvoke(() => AppendSystem(text)); return; }
            _log.SelectionStart = _log.TextLength;
            _log.SelectionColor = Color.Gold;
            _log.AppendText($"*** {text}\n");
            _log.ScrollToCaret();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _server.MessageReceived -= Server_MessageReceived;
                _server.PeerDisconnected -= Server_PeerDisconnected;
            }
            base.Dispose(disposing);
        }
    }
}
