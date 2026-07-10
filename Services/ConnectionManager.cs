using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using SSHClient.Models;

namespace SSHClient.Services
{
    public static class ConnectionManager
    {
        private static readonly string ProfilesPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SSHClient", "connections.json");

        public static List<ConnectionProfile> Load()
        {
            try
            {
                if (!File.Exists(ProfilesPath)) return new List<ConnectionProfile>();
                var json = File.ReadAllText(ProfilesPath);
                return JsonConvert.DeserializeObject<List<ConnectionProfile>>(json) ?? new List<ConnectionProfile>();
            }
            catch
            {
                return new List<ConnectionProfile>();
            }
        }

        public static void Save(List<ConnectionProfile> profiles)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ProfilesPath)!);
            File.WriteAllText(ProfilesPath, JsonConvert.SerializeObject(profiles, Formatting.Indented));
        }
    }
}
