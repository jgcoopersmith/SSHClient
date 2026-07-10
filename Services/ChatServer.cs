using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SSHClient.Services
{
    public class ChatMessageEventArgs : EventArgs
    {
        public string PeerId { get; }
        public string Sender { get; }
        public string Message { get; }
        public ChatMessageEventArgs(string peerId, string sender, string message)
        {
            PeerId = peerId; Sender = sender; Message = message;
        }
    }

    public class ChatPeerEventArgs : EventArgs
    {
        public string PeerId { get; }
        public string DisplayName { get; }
        public ChatPeerEventArgs(string peerId, string displayName) { PeerId = peerId; DisplayName = displayName; }
    }

    public class ChatServer : IDisposable
    {
        private TcpListener? _listener;
        private Thread? _acceptThread;
        private volatile bool _running;
        private readonly ConcurrentDictionary<string, PeerConnection> _peers = new();

        public string LocalUsername { get; }
        public int ListenPort { get; private set; }

        public event EventHandler<ChatMessageEventArgs>? MessageReceived;
        public event EventHandler<ChatPeerEventArgs>? PeerConnected;
        public event EventHandler<ChatPeerEventArgs>? PeerDisconnected;

        public ChatServer(string localUsername) => LocalUsername = localUsername;

        public void Start(int port = 0)
        {
            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            ListenPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
            _running = true;
            _acceptThread = new Thread(AcceptLoop) { IsBackground = true };
            _acceptThread.Start();
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    var client = _listener!.AcceptTcpClient();
                    var peer = new PeerConnection(client, this);
                    peer.Start();
                }
                catch { break; }
            }
        }

        public PeerConnection ConnectTo(string host, int port)
        {
            var client = new TcpClient();
            client.Connect(host, port);
            var peer = new PeerConnection(client, this);
            peer.Start();
            return peer;
        }

        internal void RegisterPeer(PeerConnection peer)
        {
            _peers[peer.PeerId] = peer;
            PeerConnected?.Invoke(this, new ChatPeerEventArgs(peer.PeerId, peer.RemoteUsername));
        }

        internal void UnregisterPeer(PeerConnection peer)
        {
            _peers.TryRemove(peer.PeerId, out _);
            PeerDisconnected?.Invoke(this, new ChatPeerEventArgs(peer.PeerId, peer.RemoteUsername));
        }

        internal void RaiseMessage(PeerConnection peer, string message)
        {
            MessageReceived?.Invoke(this, new ChatMessageEventArgs(peer.PeerId, peer.RemoteUsername, message));
        }

        public void Broadcast(string message)
        {
            foreach (var peer in _peers.Values)
                peer.Send(message);
        }

        public void SendTo(string peerId, string message)
        {
            if (_peers.TryGetValue(peerId, out var peer))
                peer.Send(message);
        }

        public void Dispose()
        {
            _running = false;
            _listener?.Stop();
            foreach (var peer in _peers.Values) peer.Dispose();
        }
    }

    public class PeerConnection : IDisposable
    {
        private readonly TcpClient _client;
        private readonly ChatServer _server;
        private StreamWriter? _writer;
        private Thread? _readThread;

        public string PeerId { get; } = Guid.NewGuid().ToString();
        public string RemoteUsername { get; private set; } = "Unknown";

        public PeerConnection(TcpClient client, ChatServer server)
        {
            _client = client;
            _server = server;
        }

        public void Start()
        {
            var stream = _client.GetStream();
            _writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };
            // Send handshake
            _writer.WriteLine($"HELLO:{_server.LocalUsername}");

            _readThread = new Thread(() => ReadLoop(new StreamReader(stream, Encoding.UTF8))) { IsBackground = true };
            _readThread.Start();
        }

        private void ReadLoop(StreamReader reader)
        {
            try
            {
                bool handshook = false;
                while (true)
                {
                    var line = reader.ReadLine();
                    if (line == null) break;

                    if (!handshook && line.StartsWith("HELLO:"))
                    {
                        RemoteUsername = line[6..];
                        handshook = true;
                        _server.RegisterPeer(this);
                        continue;
                    }

                    if (line.StartsWith("MSG:"))
                        _server.RaiseMessage(this, line[4..]);
                }
            }
            catch { }
            finally
            {
                _server.UnregisterPeer(this);
                _client.Close();
            }
        }

        public void Send(string message)
        {
            try { _writer?.WriteLine($"MSG:{message}"); }
            catch { }
        }

        public void Dispose()
        {
            _writer?.Dispose();
            _client.Dispose();
        }
    }
}
