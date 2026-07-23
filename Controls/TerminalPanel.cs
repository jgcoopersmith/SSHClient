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
        private string _pendingEscape = "";

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

            EditContextMenu.Attach(_output); // copy / select-all on the read-only terminal
            EditContextMenu.Attach(_input);  // cut / copy / paste on the command line

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
            // Fires on a background SSH.NET thread — strip ANSI escapes before appending.
            // A single escape sequence can be split across two DataReceived events, so any
            // incomplete trailing sequence is carried over in _pendingEscape and re-prepended.
            var text = StripAnsi(_pendingEscape + Encoding.UTF8.GetString(e.Data), out _pendingEscape);
            if (text.Length > 0)
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
            _input.Focus(); // keep focus on the entry bar (esp. after clicking Send)
        }

        /// <summary>Put keyboard focus on the command line so the user can type immediately.</summary>
        public void FocusInput()
        {
            if (!IsHandleCreated) { HandleCreated += (_, _) => BeginInvoke(FocusInput); return; }
            BeginInvoke(() => _input.Focus());
        }

        // When this tab/panel gains focus, redirect it to the command line rather than
        // letting the container or output box take it.
        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            FocusInput();
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

            // Preserve any selection the user is making so incoming shell data doesn't
            // wipe it out before they can Copy. Appending happens at the end of the buffer,
            // which is past the selection, so the saved offsets stay valid afterward.
            int selStart = _output.SelectionStart;
            int selLength = _output.SelectionLength;
            bool userHasSelection = selLength > 0;

            _output.SelectionStart = _output.TextLength;
            _output.SelectionLength = 0;
            _output.SelectionColor = color;
            _output.AppendText(text);

            if (userHasSelection)
            {
                // Restore the user's selection; don't scroll away while they're selecting.
                _output.SelectionStart = selStart;
                _output.SelectionLength = selLength;
            }
            else if (_autoScroll)
            {
                _output.ScrollToCaret();
            }
        }

        // Remove ANSI/VT100 escape sequences so the RichTextBox doesn't get garbled.
        // Any incomplete sequence at the tail (split across SSH data chunks) is returned
        // via 'pending' so it can be prepended to the next chunk instead of leaking.
        private static string StripAnsi(string input, out string pending)
        {
            pending = "";
            var sb = new System.Text.StringBuilder(input.Length);
            int i = 0;
            while (i < input.Length)
            {
                if (input[i] != '\x1B')
                {
                    sb.Append(input[i]);
                    i++;
                    continue;
                }

                // We're at an ESC. If it's the only byte, the sequence is incomplete.
                if (i + 1 >= input.Length)
                {
                    pending = input.Substring(i);
                    break;
                }

                char next = input[i + 1];
                if (next == '[')
                {
                    // CSI: ESC [ ... <final byte 0x40-0x7E>
                    int j = i + 2;
                    while (j < input.Length && !(input[j] >= '\x40' && input[j] <= '\x7E'))
                        j++;
                    if (j >= input.Length) { pending = input.Substring(i); break; } // no final byte yet
                    i = j + 1; // skip through final byte
                }
                else if (next == ']')
                {
                    // OSC: ESC ] ... terminated by BEL (0x07) or ST (ESC \)
                    int j = i + 2;
                    while (j < input.Length && input[j] != '\x07' &&
                           !(input[j] == '\x1B' && j + 1 < input.Length && input[j + 1] == '\\'))
                        j++;
                    if (j >= input.Length) { pending = input.Substring(i); break; } // terminator not seen yet
                    if (input[j] == '\x1B')
                    {
                        if (j + 1 >= input.Length) { pending = input.Substring(i); break; } // ESC of ST split off
                        j++; // consume ESC of the ST pair
                    }
                    i = j + 1; // consume BEL, or the backslash of ST
                }
                else
                {
                    // Other two-byte escape (e.g. ESC c, ESC =)
                    i += 2;
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
