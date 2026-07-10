using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using SSHClient.Models;

namespace SSHClient.Services
{
    public static class PeerManager
    {
        private static readonly string PeersPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SSHClient", "peers.json");

        public static List<PeerProfile> Load()
        {
            try
            {
                if (!File.Exists(PeersPath)) return new List<PeerProfile>();
                var json = File.ReadAllText(PeersPath);
                return JsonConvert.DeserializeObject<List<PeerProfile>>(json) ?? new List<PeerProfile>();
            }
            catch
            {
                return new List<PeerProfile>();
            }
        }

        public static void Save(List<PeerProfile> peers)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PeersPath)!);
            File.WriteAllText(PeersPath, JsonConvert.SerializeObject(peers, Formatting.Indented));
        }
    }
}
