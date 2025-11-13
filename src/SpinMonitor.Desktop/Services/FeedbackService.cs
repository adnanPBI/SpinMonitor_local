using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;

namespace SpinMonitor.Services
{
    public class FeedbackService
    {
        private readonly string _dbPath;

        public FeedbackService()
        {
            var cfgPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
            var settings = AppSettings.Load(cfgPath);
            _dbPath = Path.Combine(AppContext.BaseDirectory, settings.Persistence.SqlitePath);
        }

        public IEnumerable<(string File, string Stream, DateTime? CreationDate)> Snapshot()
        {
            var results = new List<(string File, string Stream, DateTime? CreationDate)>();
            if (!File.Exists(_dbPath)) return results;

            try
            {
                var cs = new SqliteConnectionStringBuilder
                {
                    DataSource = _dbPath,
                    Mode = SqliteOpenMode.ReadOnly,
                    Cache = SqliteCacheMode.Shared
                }.ToString();

                using var conn = new SqliteConnection(cs);
                conn.Open();

                var tables = new List<string>();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";
                    using var rdr = cmd.ExecuteReader();
                    while (rdr.Read()) tables.Add(rdr.GetString(0));
                }

                foreach (var t in new[] { "Detections", "detections", "Matches", "matches" })
                {
                    var table = tables.FirstOrDefault(n => string.Equals(n, t, StringComparison.OrdinalIgnoreCase));
                    if (table == null) continue;

                    var cols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = $"PRAGMA table_info([{table}]);";
                        using var rdr = cmd.ExecuteReader();
                        while (rdr.Read()) cols.Add(rdr.GetString(1));
                    }

                    string? fileCol = cols.Contains("TrackTitle") ? "TrackTitle"
                                     : cols.Contains("Title") ? "Title"
                                     : cols.Contains("Track") ? "Track"
                                     : null;

                    string? streamCol = cols.Contains("StreamName") ? "StreamName"
                                       : cols.Contains("Station") ? "Station"
                                       : cols.Contains("Stream") ? "Stream"
                                       : null;

                    if (fileCol != null && streamCol != null)
                    {
                        using var cmd = conn.CreateCommand();
                        cmd.CommandText = $"SELECT [{fileCol}], [{streamCol}] FROM [{table}] ORDER BY [{fileCol}] COLLATE NOCASE;";
                        using var rdr = cmd.ExecuteReader();
                        while (rdr.Read())
                        {
                            results.Add((rdr.IsDBNull(0) ? "" : rdr.GetString(0),
                                         rdr.IsDBNull(1) ? "" : rdr.GetString(1)));
                        }
                        return results;
                    }
                }

                // ✅ Query tracks with creation date from avhashes table
                var tracks = tables.FirstOrDefault(n => string.Equals(n, "Tracks", StringComparison.OrdinalIgnoreCase));
                var avhashes = tables.FirstOrDefault(n => string.Equals(n, "avhashes", StringComparison.OrdinalIgnoreCase));

                if (tracks != null)
                {
                    var cols2 = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = $"PRAGMA table_info([{tracks}]);";
                        using var rdr = cmd.ExecuteReader();
                        while (rdr.Read()) cols2.Add(rdr.GetString(1));
                    }

                    string? titleCol = cols2.Contains("Title") ? "Title"
                                      : cols2.Contains("Name") ? "Name"
                                      : cols2.Contains("FileName") ? "FileName"
                                      : null;

                    if (titleCol != null)
                    {
                        using var cmd = conn.CreateCommand();

                        // ✅ Join with avhashes to get creation date
                        if (avhashes != null)
                        {
                            cmd.CommandText = $@"
                                SELECT t.[{titleCol}], a.created_utc
                                FROM [{tracks}] t
                                LEFT JOIN [{avhashes}] a ON t.track_id = a.track_id
                                ORDER BY t.[{titleCol}] COLLATE NOCASE;";
                        }
                        else
                        {
                            cmd.CommandText = $"SELECT [{titleCol}], NULL FROM [{tracks}] ORDER BY [{titleCol}] COLLATE NOCASE;";
                        }

                        using var rdr = cmd.ExecuteReader();
                        while (rdr.Read())
                        {
                            var file = rdr.IsDBNull(0) ? "" : rdr.GetString(0);
                            DateTime? creationDate = null;
                            if (!rdr.IsDBNull(1))
                            {
                                var dateStr = rdr.GetString(1);
                                if (DateTime.TryParse(dateStr, null, System.Globalization.DateTimeStyles.RoundtripKind, out var parsed))
                                {
                                    creationDate = parsed;
                                }
                            }
                            results.Add((file, "", creationDate));
                        }
                    }
                }
            }
            catch
            {
                // ignore
            }

            return results;
        }
    }
}