using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using ProtoBuf;
using Serilog;
using SoundFingerprinting;
using SoundFingerprinting.Data;
using SoundFingerprinting.InMemory;

namespace SpinMonitor.Services
{
    public class SqliteFingerprintStore
    {
        private readonly string _dbPath;
        private IModelService _inMemoryModel = new InMemoryModelService();  // ✅ Mutable for reload

        public IModelService InMemoryModel => _inMemoryModel;

        public SqliteFingerprintStore(string dbPath)
        {
            _dbPath = Path.GetFullPath(dbPath);
            Directory.CreateDirectory(Path.GetDirectoryName(_dbPath)!);
            Initialize();
        }

        private void Initialize()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
PRAGMA journal_mode=WAL;
CREATE TABLE IF NOT EXISTS meta (key TEXT PRIMARY KEY, value TEXT);
CREATE TABLE IF NOT EXISTS tracks(
    track_id TEXT PRIMARY KEY,
    title TEXT, artist TEXT, album TEXT,
    duration REAL,
    file_path TEXT,
    last_write_utc TEXT
);
CREATE TABLE IF NOT EXISTS avhashes(
    track_id TEXT PRIMARY KEY,
    payload BLOB NOT NULL,
    hash_count INTEGER NOT NULL,
    created_utc TEXT NOT NULL
);
";
            cmd.ExecuteNonQuery();

            using var up = conn.CreateCommand();
            up.CommandText = "INSERT OR REPLACE INTO meta(key,value) VALUES('sf_version', $v)";
            up.Parameters.AddWithValue("$v", typeof(InMemoryModelService).Assembly.GetName().Version?.ToString() ?? "unknown");
            up.ExecuteNonQuery();
        }

        // ✅ NEW: Reload entire in-memory model (only after cleanup)
        public int ReloadInMemoryModel()
        {
            _inMemoryModel = new InMemoryModelService();
            return LoadAllIntoInMemoryModel();
        }

        public int LoadAllIntoInMemoryModel()
        {
            int loaded = 0;
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT t.track_id, t.title, t.artist, t.album, ifnull(t.duration,0), a.payload
                                FROM tracks t JOIN avhashes a ON t.track_id=a.track_id";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var trackId = reader.GetString(0);
                var title = reader.IsDBNull(1) ? null : reader.GetString(1);
                var artist = reader.IsDBNull(2) ? null : reader.GetString(2);
                var blob = (byte[])reader[5];
                try
                {
                    using var ms = new MemoryStream(blob);
                    var av = Serializer.Deserialize<AVHashes>(ms);
                    var track = new TrackInfo(trackId, title ?? trackId, artist ?? "");
                    _inMemoryModel.Insert(track, av);
                    loaded++;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Failed to deserialize AVHashes for {TrackId}", trackId);
                }
            }
            return loaded;
        }

        public bool TrackExists(string trackId)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1 FROM tracks WHERE track_id=$id";
            cmd.Parameters.AddWithValue("$id", trackId);
            return cmd.ExecuteScalar() != null;
        }

        public bool IsTrackUpToDate(string trackId, DateTime lastWriteUtc)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT last_write_utc FROM tracks WHERE track_id=$id";
            cmd.Parameters.AddWithValue("$id", trackId);
            var v = cmd.ExecuteScalar() as string;
            if (string.IsNullOrEmpty(v)) return false;
            if (!DateTime.TryParse(v, null, System.Globalization.DateTimeStyles.RoundtripKind, out var stored)) return false;
            return stored >= lastWriteUtc;
        }

        public void UpsertTrackAndHashes(TrackInfo track, AVHashes av, string? filePath = null, DateTime? lastWriteUtc = null)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var tran = conn.BeginTransaction();
            using var cmd1 = conn.CreateCommand();
            cmd1.CommandText = @"INSERT INTO tracks(track_id,title,artist,album,duration,file_path,last_write_utc)
                                 VALUES($id,$t,$ar,$al,$du,$fp,$lw)
                                 ON CONFLICT(track_id) DO UPDATE SET
                                   title=$t, artist=$ar, album=$al, duration=$du, file_path=$fp, last_write_utc=$lw";
            cmd1.Parameters.AddWithValue("$id", track.Id);
            cmd1.Parameters.AddWithValue("$t", track.Title ?? "");
            cmd1.Parameters.AddWithValue("$ar", track.Artist ?? "");
            cmd1.Parameters.AddWithValue("$al", "");
            cmd1.Parameters.AddWithValue("$du", 0.0);
            cmd1.Parameters.AddWithValue("$fp", filePath ?? "");
            cmd1.Parameters.AddWithValue("$lw", (lastWriteUtc ?? DateTime.UtcNow).ToString("o"));
            cmd1.ExecuteNonQuery();

            byte[] payload;
            using (var ms = new MemoryStream())
            {
                Serializer.Serialize(ms, av);
                payload = ms.ToArray();
            }

            using var cmd2 = conn.CreateCommand();
            cmd2.CommandText = @"INSERT INTO avhashes(track_id,payload,hash_count,created_utc)
                                 VALUES($id,$p,$hc,$ts)
                                 ON CONFLICT(track_id) DO UPDATE SET payload=$p, hash_count=$hc, created_utc=$ts";
            cmd2.Parameters.AddWithValue("$id", track.Id);
            cmd2.Parameters.AddWithValue("$p", payload);
            cmd2.Parameters.AddWithValue("$hc", av.Audio?.Count ?? 0);
            cmd2.Parameters.AddWithValue("$ts", DateTime.UtcNow.ToString("o"));
            cmd2.ExecuteNonQuery();

            tran.Commit();
            _inMemoryModel.Insert(track, av);
        }

        public int EstimatedTrackCount()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(1) FROM tracks";
            var obj = cmd.ExecuteScalar();
            return Convert.ToInt32(obj ?? 0);
        }

        public IEnumerable<(string TrackId, string FilePath)> EnumerateTracks()
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT track_id, file_path FROM tracks";
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                yield return (r.GetString(0), r.IsDBNull(1) ? string.Empty : r.GetString(1));
            }
        }

        public void DeleteTrack(string trackId)
        {
            using var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            using var tx = conn.BeginTransaction();
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM avhashes WHERE track_id=$id";
                cmd.Parameters.AddWithValue("$id", trackId);
                cmd.ExecuteNonQuery();
            }
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText = "DELETE FROM tracks WHERE track_id=$id";
                cmd.Parameters.AddWithValue("$id", trackId);
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
            
            // Note: Will reload after batch delete completes
        }
    }
}