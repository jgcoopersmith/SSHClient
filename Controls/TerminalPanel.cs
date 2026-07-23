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

        // Minimal terminal line discipline: the current (not-yet-newlined) line is modeled
        // here so C0 control characters — backspace, carriage return, tab — actually move
        // the cursor instead of leaking into the RichTextBox as unresolved glyphs.
        private readonly StringBuilder _curLine = new StringBuilder();
        private int _curCol;    // cursor column within _curLine
        private int _lineStart; // index in _output where the current line begins

        // Command history for Up/Down recall on the entry line. _historyIndex points at the
        // currently recalled entry, or _history.Count when composing a fresh line; _draft
        // holds that in-progress line so it comes back when arrowing past the newest entry.
        private readonly List<string> _history = new List<string>();
        private int _historyIndex;
        private string _draft = "";

        private static readonly System.Text.RegularExpressions.Regex _passwordPrompt =
            new System.Text.RegularExpressions.Regex(
                @"(?:password(?: for [^:]*)?|passphrase(?: for [^:]*)?|verification code)\s*:\s*$",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase |
                System.Text.RegularExpressions.RegexOptions.Compiled);

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

                AppendSystemLine($"[Connected to {Profile.Host}:{Profile.Port}]\r\n", Color.Cyan);
            }
            catch (Exception ex)
            {
                AppendSystemLine($"[Connection failed: {ex.Message}]\r\n", Color.Red);
            }
        }

        private void Shell_DataReceived(object? sender, ShellDataEventArgs e)
        {
            // Fires on a background SSH.NET thread — strip ANSI escapes before appending.
            // A single escape sequence can be split across two DataReceived events, so any
            // incomplete trailing sequence is carried over in _pendingEscape and re-prepended.
            var text = StripAnsi(_pendingEscape + Encoding.UTF8.GetString(e.Data), out _pendingEscape);
            if (text.Length > 0)
                AppendShellText(text);
        }

        private void Shell_ErrorOccurred(object? sender, ExceptionEventArgs e)
        {
            AppendSystemLine($"\r\n[Error: {e.Exception.Message}]\r\n", Color.Red);
        }

        private void SendCommand()
        {
            if (_shell == null) return;
            // An empty line still sends a bare newline so the shell echoes a fresh prompt,
            // just like pressing Enter in a real terminal.
            var text = _input.Text;
            _input.Clear();

            // Record non-empty commands for Up/Down recall, skipping consecutive duplicates.
            if (text.Length > 0 && (_history.Count == 0 || _history[^1] != text))
                _history.Add(text);
            _historyIndex = _history.Count; // back to "composing a fresh line"
            _draft = "";

            var bytes = Encoding.UTF8.GetBytes(text + "\n");
            _shell.Write(bytes, 0, bytes.Length);
            _shell.Flush();
            _input.Focus(); // keep focus on the entry bar (esp. after clicking Send)
        }

        // Replace the entry line with a recalled command and drop the caret at the end.
        private void SetInput(string value)
        {
            _input.Text = value;
            _input.SelectionStart = _input.Text.Length;
            _input.SelectionLength = 0;
        }

        private void RecallPrevious()
        {
            if (_historyIndex == 0 || _history.Count == 0) return;
            if (_historyIndex == _history.Count) _draft = _input.Text; // stash the fresh line
            _historyIndex--;
            SetInput(_history[_historyIndex]);
        }

        private void RecallNext()
        {
            if (_historyIndex >= _history.Count) return;
            _historyIndex++;
            SetInput(_historyIndex == _history.Count ? _draft : _history[_historyIndex]);
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
            switch (e.KeyCode)
            {
                case Keys.Enter:
                    e.SuppressKeyPress = true;
                    SendCommand();
                    break;
                case Keys.Up:
                    e.SuppressKeyPress = true; // also stops the caret from jumping
                    RecallPrevious();
                    break;
                case Keys.Down:
                    e.SuppressKeyPress = true;
                    RecallNext();
                    break;
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

        // Feed shell output through a minimal terminal line discipline: printable text is
        // written at the cursor column of the current line; backspace/CR/tab move the cursor;
        // newline commits the line. This keeps raw control bytes (and things like sudo's
        // password-feedback erase sequences) from showing up as unresolved glyphs.
        private void AppendShellText(string text)
        {
            if (InvokeRequired) { BeginInvoke(() => AppendShellText(text)); return; }

            var committed = new StringBuilder();
            foreach (char ch in text)
            {
                switch (ch)
                {
                    case '\r': _curCol = 0; break;
                    case '\b': if (_curCol > 0) _curCol--; break;
                    case '\n':
                        committed.Append(_curLine).Append('\n');
                        _curLine.Clear();
                        _curCol = 0;
                        break;
                    case '\t':
                        int stop = ((_curCol / 8) + 1) * 8;
                        while (_curCol < stop) WriteChar(' ');
                        break;
                    default:
                        // Printable only; silently drop any remaining C0/DEL control bytes.
                        if (ch >= ' ' && ch != '\x7F') WriteChar(ch);
                        break;
                }
            }

            RenderCurrentLine(committed.ToString());

            // Detect password prompts on the resolved current line (backspaces already
            // applied), so masking is accurate even with sudo's pwfeedback asterisks.
            bool mask = _passwordPrompt.IsMatch(_curLine.ToString());
            if (_input.UseSystemPasswordChar != mask)
                _input.UseSystemPasswordChar = mask;
        }

        // Write one printable char at the cursor: overwrite in place if the cursor is inside
        // the existing line (as with a CR-then-retype redraw), otherwise extend the line.
        private void WriteChar(char ch)
        {
            if (_curCol < _curLine.Length) _curLine[_curCol] = ch;
            else _curLine.Append(ch);
            _curCol++;
        }

        // Replace the on-screen tail (everything from _lineStart) with the freshly committed
        // lines plus the live current line, preserving any active selection / auto-scroll.
        private void RenderCurrentLine(string committed)
        {
            int selStart = _output.SelectionStart;
            int selLength = _output.SelectionLength;
            bool userHasSelection = selLength > 0 && selStart + selLength <= _lineStart;

            _output.Select(_lineStart, _output.TextLength - _lineStart);
            _output.SelectionColor = Color.LightGreen;
            _output.SelectedText = committed + _curLine.ToString();
            _lineStart += committed.Length;

            if (userHasSelection)
            {
                _output.Select(selStart, selLength);
            }
            else if (_autoScroll)
            {
                _output.SelectionStart = _output.TextLength;
                _output.SelectionLength = 0;
                _output.ScrollToCaret();
            }
        }

        // Append client-generated status text (connect/error notices) as a clean line,
        // freezing whatever partial shell line preceded it.
        private void AppendSystemLine(string text, Color color)
        {
            if (InvokeRequired) { BeginInvoke(() => AppendSystemLine(text, color)); return; }

            _output.Select(_output.TextLength, 0);
            _output.SelectionColor = color;
            _output.AppendText(text);

            _curLine.Clear();
            _curCol = 0;
            _lineStart = _output.TextLength;

            if (_autoScroll) _output.ScrollToCaret();
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
