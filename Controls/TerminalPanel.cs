using System;
using System.Drawing;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using Renci.SshNet;
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
        private Thread? _readThread;
        private volatile bool _running;

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

            _btnSend = new Button
            {
                Text = "Send",
                Width = 60,
                Dock = DockStyle.Right
            };
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

                _shell = _client.CreateShellStream("xterm", 200, 50, 0, 0, 4096);
                _running = true;
                _readThread = new Thread(ReadLoop) { IsBackground = true };
                _readThread.Start();

                AppendOutput($"[Connected to {Profile.Host}:{Profile.Port}]\r\n", Color.Cyan);
            }
            catch (Exception ex)
            {
                AppendOutput($"[Connection failed: {ex.Message}]\r\n", Color.Red);
            }
        }

        private void ReadLoop()
        {
            var buffer = new byte[4096];
            while (_running && _shell != null)
            {
                try
                {
                    int read = _shell.Read(buffer, 0, buffer.Length);
                    if (read > 0)
                    {
                        var text = Encoding.UTF8.GetString(buffer, 0, read);
                        AppendOutput(text, Color.LightGreen);
                    }
                    else
                    {
                        Thread.Sleep(10);
                    }
                }
                catch
                {
                    break;
                }
            }
            AppendOutput("\r\n[Session ended]\r\n", Color.Cyan);
        }

        private void SendCommand()
        {
            if (_shell == null || _input.Text.Length == 0) return;
            var text = _input.Text;
            _input.Clear();
            _shell.Write(text + "\n");
        }

        private void Input_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SendCommand();
            }
        }

        private void AppendOutput(string text, Color color)
        {
            if (InvokeRequired)
            {
                BeginInvoke(() => AppendOutput(text, color));
                return;
            }
            _output.SelectionStart = _output.TextLength;
            _output.SelectionLength = 0;
            _output.SelectionColor = color;
            _output.AppendText(text);
            _output.ScrollToCaret();
        }

        public void Disconnect()
        {
            _running = false;
            _shell?.Dispose();
            _client?.Disconnect();
            _client?.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) Disconnect();
            base.Dispose(disposing);
        }
    }
}
