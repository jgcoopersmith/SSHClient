using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Renci.SshNet;
using Renci.SshNet.Common;
using SSHClient.Models;

namespace SSHClient.Controls
{
    public class TerminalPanel : UserControl
    {
        private readonly RichTextBox _output;
        private readonly TextBox _input;
        private readonly Button _btnSend;
        private SshClient? _client;
        private ShellStream? _shell;
        private bool _autoScroll = true;

        public ConnectionProfile Profile { get; }

        public TerminalPanel(ConnectionProfile profile)
        {
            Profile = profile;

            _output = new RichTextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                ForeColor = Color.LightGreen,
                Font = new Font("Consolas", 10f),
                ReadOnly = true,
                ScrollBars = RichTextBoxScrollBars.Vertical,
                WordWrap = false
            };

            _input = new TextBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.White,
                Font = new Font("Consolas", 10f),
                BorderStyle = BorderStyle.FixedSingle
            };
            _input.KeyDown += Input_KeyDown;
            _output.VScroll += Output_VScroll;

            _btnSend = new Button { Text = "Send", Width = 60, Dock = DockStyle.Right };
            _btnSend.Click += (_, _) => SendCommand();

            var inputBar = new Panel { Dock = DockStyle.Bottom, Height = 28 };
            inputBar.Controls.AddRange(new Control[] { _input, _btnSend });

            Controls.Add(_output);
            Controls.Add(inputBar);
            Dock = DockStyle.Fill;
        }

        public void Connect()
        {
            try
            {
                AuthenticationMethod auth = Profile.UseKeyAuth
                    ? new PrivateKeyAuthenticationMethod(Profile.Username,
                        new PrivateKeyFile(Profile.PrivateKeyPath))
                    : (AuthenticationMethod)new PasswordAuthenticationMethod(Profile.Username, Profile.Password);

                var connInfo = new ConnectionInfo(Profile.Host, Profile.Port, Profile.Username, auth);
                _client = new SshClient(connInfo);
                _client.Connect();

                _shell = _client.CreateShellStream("xterm", 220, 50, 0, 0, 8192);
                _shell.DataReceived += Shell_DataReceived;
                _shell.ErrorOccurred += Shell_ErrorOccurred;

                AppendOutput($"[Connected to {Profile.Host}:{Profile.Port}]\r\n", Color.Cyan);
            }
            catch (Exception ex)
            {
                AppendOutput($"[Connection failed: {ex.Message}]\r\n", Color.Red);
            }
        }

        private void Shell_DataReceived(object? sender, ShellDataEventArgs e)
        {
            // Fires on a background SSH.NET thread — strip ANSI escapes before appending
            var text = StripAnsi(Encoding.UTF8.GetString(e.Data));
            AppendOutput(text, Color.LightGreen);
        }

        private void Shell_ErrorOccurred(object? sender, ExceptionEventArgs e)
        {
            AppendOutput($"\r\n[Error: {e.Exception.Message}]\r\n", Color.Red);
        }

        private void SendCommand()
        {
            if (_shell == null || _input.Text.Length == 0) return;
            var text = _input.Text;
            _input.Clear();
            var bytes = Encoding.UTF8.GetBytes(text + "\n");
            _shell.Write(bytes, 0, bytes.Length);
            _shell.Flush();
        }

        private void Input_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SendCommand();
            }
        }

        private void Output_VScroll(object? sender, EventArgs e)
        {
            // Detect whether the user is at the bottom; update auto-scroll accordingly.
            // GetPositionFromCharIndex returns the pixel position of the last character.
            // If it falls within the visible client area we're at (or near) the bottom.
            var lastPos = _output.GetPositionFromCharIndex(Math.Max(0, _output.TextLength - 1));
            _autoScroll = lastPos.Y <= _output.ClientSize.Height;
        }

        private void AppendOutput(string text, Color color)
        {
            if (InvokeRequired) { BeginInvoke(() => AppendOutput(text, color)); return; }
            _output.SelectionStart = _output.TextLength;
            _output.SelectionLength = 0;
            _output.SelectionColor = color;
            _output.AppendText(text);
            if (_autoScroll)
                _output.ScrollToCaret();
        }

        // Remove ANSI/VT100 escape sequences so the RichTextBox doesn't get garbled
        private static string StripAnsi(string input)
        {
            var sb = new System.Text.StringBuilder(input.Length);
            int i = 0;
            while (i < input.Length)
            {
                if (input[i] == '\x1B' && i + 1 < input.Length && input[i + 1] == '[')
                {
                    // Skip ESC [ ... <letter>
                    i += 2;
                    while (i < input.Length && !(input[i] >= 'A' && input[i] <= 'Z') && !(input[i] >= 'a' && input[i] <= 'z'))
                        i++;
                    i++; // skip terminating letter
                }
                else if (input[i] == '\x1B' && i + 1 < input.Length)
                {
                    // Other escape sequences (e.g. ESC c, ESC =)
                    i += 2;
                }
                else
                {
                    sb.Append(input[i]);
                    i++;
                }
            }
            return sb.ToString();
        }

        public void Disconnect()
        {
            if (_shell != null)
            {
                _shell.DataReceived -= Shell_DataReceived;
                _shell.ErrorOccurred -= Shell_ErrorOccurred;
                _shell.Dispose();
                _shell = null;
            }
            _client?.Disconnect();
            _client?.Dispose();
            _client = null;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Disconnect();
            base.Dispose(disposing);
        }
    }
}
