using System;
using System.IO;
using System.Text.Json;

namespace RemoteControl.Server
{
    internal class ServerSettings
    {
        public int Port { get; set; } = 8888;
        public string Password { get; set; } = "123456";
        public bool AutoStart { get; set; }

        private static string SettingsFilePath =>
            Path.Combine(AppContext.BaseDirectory, "server_settings.json");

        public static ServerSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                {
                    return new ServerSettings();
                }

                string json = File.ReadAllText(SettingsFilePath);
                return JsonSerializer.Deserialize<ServerSettings>(json) ?? new ServerSettings();
            }
            catch
            {
                return new ServerSettings();
            }
        }

        public void Save()
        {
            string json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(SettingsFilePath, json);
        }
    }
}
