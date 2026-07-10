using System;

namespace SSHClient.Models
{
    public class PeerProfile
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public string Host { get; set; } = "";
        public int Port { get; set; } = 9000;

        public override string ToString() => Name.Length > 0 ? Name : $"{Host}:{Port}";
    }
}
