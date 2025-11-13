using System;
using System.Linq;

namespace SpinMonitor.Services
{
    /// <summary>
    /// Detects stream type and extracts stream number/identifier from URL
    /// </summary>
    public static class StreamTypeDetector
    {
        public enum StreamType
        {
            Unknown,
            HLS,
            DASH,
            HTTP,
            RTMP,
            RTSP,
            Icecast,
            MMS
        }

        public class StreamInfo
        {
            public StreamType Type { get; set; }
            public string TypeString { get; set; } = "Unknown";
            public string? StreamNumber { get; set; }
            public string Protocol { get; set; } = "";
            public string? Host { get; set; }
        }

        public static StreamInfo Analyze(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return new StreamInfo { Type = StreamType.Unknown, TypeString = "Unknown" };

            var info = new StreamInfo();

            try
            {
                var uri = new Uri(url);
                info.Protocol = uri.Scheme.ToUpperInvariant();
                info.Host = uri.Host;

                var path = uri.AbsolutePath.ToLowerInvariant();
                
                if (path.Contains(".m3u8") || path.Contains(".m3u"))
                {
                    info.Type = StreamType.HLS;
                    info.TypeString = "HLS";
                    info.StreamNumber = ExtractStreamNumber(uri.AbsolutePath, new[] { "master", "playlist", "stream" });
                }
                else if (path.Contains(".mpd"))
                {
                    info.Type = StreamType.DASH;
                    info.TypeString = "DASH";
                    info.StreamNumber = ExtractStreamNumber(uri.AbsolutePath, new[] { "manifest", "stream" });
                }
                else if (uri.Scheme.Equals("rtmp", StringComparison.OrdinalIgnoreCase))
                {
                    info.Type = StreamType.RTMP;
                    info.TypeString = "RTMP";
                    info.StreamNumber = ExtractStreamNumber(uri.AbsolutePath, new[] { "live", "stream" });
                }
                else if (uri.Scheme.Equals("rtsp", StringComparison.OrdinalIgnoreCase))
                {
                    info.Type = StreamType.RTSP;
                    info.TypeString = "RTSP";
                    info.StreamNumber = ExtractStreamNumber(uri.AbsolutePath, new[] { "stream", "channel" });
                }
                else if (uri.Scheme.Equals("mms", StringComparison.OrdinalIgnoreCase))
                {
                    info.Type = StreamType.MMS;
                    info.TypeString = "MMS";
                }
                else if (path.Contains("/stream") || uri.Host.Contains("icecast") || uri.Host.Contains("shoutcast"))
                {
                    info.Type = StreamType.Icecast;
                    info.TypeString = "Icecast";
                    info.StreamNumber = ExtractStreamNumber(uri.AbsolutePath, new[] { "stream", "radio" });
                }
                else if (uri.Scheme.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    info.Type = StreamType.HTTP;
                    info.TypeString = "HTTP";
                    
                    if (path.Contains(".mp3"))
                        info.TypeString = "HTTP-MP3";
                    else if (path.Contains(".aac"))
                        info.TypeString = "HTTP-AAC";
                    else if (path.Contains(".ogg"))
                        info.TypeString = "HTTP-OGG";
                    
                    info.StreamNumber = ExtractStreamNumber(uri.AbsolutePath, new[] { "stream", "radio", "channel" });
                }
                else
                {
                    info.Type = StreamType.Unknown;
                    info.TypeString = $"Unknown-{uri.Scheme}";
                }

                if (string.IsNullOrEmpty(info.StreamNumber) && !string.IsNullOrEmpty(uri.Query))
                {
                    info.StreamNumber = ExtractStreamNumberFromQuery(uri.Query);
                }
            }
            catch (Exception)
            {
                info.Type = StreamType.Unknown;
                info.TypeString = "Invalid-URL";
            }

            return info;
        }

        private static string? ExtractStreamNumber(string path, string[] keywords)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            var parts = path.Split(new[] { '/', '_', '-' }, StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var part in parts)
            {
                var lower = part.ToLowerInvariant();
                
                foreach (var keyword in keywords)
                {
                    if (lower.StartsWith(keyword))
                    {
                        var numPart = lower.Substring(keyword.Length);
                        var digits = new string(numPart.Where(char.IsDigit).ToArray());
                        if (!string.IsNullOrEmpty(digits))
                            return digits;
                    }
                }

                if (part.All(char.IsDigit) && part.Length <= 3)
                    return part;
            }

            return null;
        }

        private static string? ExtractStreamNumberFromQuery(string query)
        {
            if (string.IsNullOrEmpty(query))
                return null;

            var queryParams = new[] { "stream", "channel", "id", "radio", "station" };
            
            foreach (var param in queryParams)
            {
                var pattern = $"{param}=";
                var index = query.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
                if (index >= 0)
                {
                    var start = index + pattern.Length;
                    var end = query.IndexOf('&', start);
                    var value = end > 0 ? query.Substring(start, end - start) : query.Substring(start);
                    
                    var digits = new string(value.Where(char.IsDigit).ToArray());
                    if (!string.IsNullOrEmpty(digits))
                        return digits;
                }
            }

            return null;
        }

        public static string GetDisplayName(string streamName, StreamInfo info)
        {
            if (!string.IsNullOrEmpty(info.StreamNumber))
                return $"{streamName} [{info.TypeString}-{info.StreamNumber}]";
            else
                return $"{streamName} [{info.TypeString}]";
        }
    }
}