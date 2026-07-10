using System;

namespace SSHClient.Models
{
    public class ConnectionProfile
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public string Host { get; set; } = "";
        public int Port { get; set; } = 22;
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public string PrivateKeyPath { get; set; } = "";
        public bool UseKeyAuth { get; set; } = false;

        public override string ToString() => Name.Length > 0 ? Name : $"{Username}@{Host}:{Port}";
    }
}
