using System.IO;
using System.Text.Json;

namespace SpinMonitor.Services
{
    public class AppSettings
    {
        public string LibraryFolder { get; set; } = "library";

        /// <summary>
        /// How often streams.json is polled by AutoReloadStreamsAsync.
        /// </summary>
        public int RefreshMinutes { get; set; } = 5;

        /// <summary>
        /// If true, replace MP3 content with 0 bytes after fingerprinting
        /// (keeps file name/extension for folder watcher compatibility).
        /// </summary>
        public bool ZeroOutAfterFingerprint { get; set; } = true;

        public string FFmpegArgs { get; set; } =
            "-user_agent \"Mozilla/5.0\" -reconnect 1 -reconnect_on_network_error 1 -reconnect_streamed 1 -reconnect_delay_max 5 -nostats -loglevel error";

        public DetectionSettings Detection { get; set; } = new DetectionSettings();
        public PersistenceSettings Persistence { get; set; } = new PersistenceSettings();
        public ReconnectSettings Reconnect { get; set; } = new ReconnectSettings();
        public MySqlSettings MySQL { get; set; } = new MySqlSettings();  // ✅ ADDED MySQL settings
        public BackendApiSettings BackendApi { get; set; } = new BackendApiSettings();  // ✅ Backend API settings

        public class DetectionSettings
        {
            public double MinConfidence { get; set; } = 0.2;
            public int QueryWindowSeconds { get; set; } = 10;
            public int QueryHopSeconds { get; set; } = 5;
        }

        public class PersistenceSettings
        {
            public string SqlitePath { get; set; } = "data/fingerprints.db";
        }

        public class ReconnectSettings
        {
            public int DelaySeconds { get; set; } = 60;
            public int OfflineTimeoutSeconds { get; set; } = 300;
        }

        // ✅ MySQL configuration for detection logging
        public class MySqlSettings
        {
            public bool Enabled { get; set; } = false;
            public string Hostname { get; set; } = "198.251.89.164";
            public string Database { get; set; } = "civicos_monitor";
            public string Username { get; set; } = "civicos_monitor";
            public string Password { get; set; } = "Na4FGYcayR5MpcJdrZVP";
            public int Port { get; set; } = 3306;
        }

        // ✅ Backend API configuration (alternative to direct MySQL)
        public class BackendApiSettings
        {
            /// <summary>
            /// Enable Backend API logging instead of direct MySQL connection.
            /// If both MySQL.Enabled and BackendApi.Enabled are true, both methods will be used.
            /// </summary>
            public bool Enabled { get; set; } = false;

            /// <summary>
            /// Base URL of the Express.js backend API (e.g., http://localhost:3000)
            /// </summary>
            public string BaseUrl { get; set; } = "http://localhost:3000";

            /// <summary>
            /// Optional API key for authentication (leave empty if not required)
            /// </summary>
            public string? ApiKey { get; set; } = null;

            /// <summary>
            /// Check backend health on startup
            /// </summary>
            public bool CheckHealthOnStartup { get; set; } = true;
        }

        public static AppSettings Load(string path)
        {
            if (!File.Exists(path))
            {
                var s = new AppSettings();
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                s.Save(path);
                return s;
            }
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(path)) ?? new AppSettings();
        }

        public void Save(string path)
        {
            File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
    }
}