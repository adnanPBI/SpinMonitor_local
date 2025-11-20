using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;

namespace SpinMonitor.Services
{
    /// <summary>
    /// HTTP client for communicating with the SpinMonitor Backend API.
    /// Sends detection events to the Express.js backend instead of directly to MySQL.
    /// </summary>
    public class DetectionApiClient : IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl;
        private readonly string? _apiKey;
        private bool _disposed;

        public DetectionApiClient(string baseUrl, string? apiKey = null)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _apiKey = apiKey;

            _httpClient = new HttpClient
            {
                BaseAddress = new Uri(_baseUrl),
                Timeout = TimeSpan.FromSeconds(10)
            };

            // Set headers
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "SpinMonitor-Desktop/1.0");
            if (!string.IsNullOrEmpty(_apiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("X-API-Key", _apiKey);
            }

            Log.Information("DetectionApiClient initialized: {BaseUrl}", _baseUrl);
        }

        /// <summary>
        /// Send a detection event to the backend API.
        /// </summary>
        public async Task<bool> SendDetectionAsync(DetectionRecord detection)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync("/api/detections", detection);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<ApiResponse>();
                    Log.Debug("Detection sent successfully: {Track} on {Stream}",
                        detection.Track, detection.Stream);
                    return true;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Log.Warning("Failed to send detection: {StatusCode} - {Error}",
                        response.StatusCode, error);
                    return false;
                }
            }
            catch (HttpRequestException ex)
            {
                Log.Error(ex, "Network error sending detection to API: {Message}", ex.Message);
                return false;
            }
            catch (TaskCanceledException ex)
            {
                Log.Warning(ex, "Timeout sending detection to API");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Unexpected error sending detection to API");
                return false;
            }
        }

        /// <summary>
        /// Send multiple detection events in a batch.
        /// </summary>
        public async Task<bool> SendDetectionBatchAsync(DetectionRecord[] detections)
        {
            try
            {
                var payload = new { detections };
                var response = await _httpClient.PostAsJsonAsync("/api/detections/batch", payload);

                if (response.IsSuccessStatusCode)
                {
                    Log.Debug("Batch of {Count} detections sent successfully", detections.Length);
                    return true;
                }
                else
                {
                    var error = await response.Content.ReadAsStringAsync();
                    Log.Warning("Failed to send detection batch: {StatusCode} - {Error}",
                        response.StatusCode, error);
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error sending detection batch to API");
                return false;
            }
        }

        /// <summary>
        /// Check if the backend API is healthy and reachable.
        /// </summary>
        public async Task<bool> CheckHealthAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync("/api/health");

                if (response.IsSuccessStatusCode)
                {
                    var health = await response.Content.ReadFromJsonAsync<HealthResponse>();
                    Log.Information("Backend API health: {Status}, Database: {DbStatus}",
                        health?.Status, health?.Database);
                    return health?.Status == "healthy";
                }

                return false;
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Backend API health check failed");
                return false;
            }
        }

        /// <summary>
        /// Get recent "live" detections from the backend.
        /// </summary>
        public async Task<DetectionRecord[]?> GetLiveDetectionsAsync(int seconds = 120)
        {
            try
            {
                var response = await _httpClient.GetAsync($"/api/detections/live?seconds={seconds}");

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<LiveDetectionsResponse>();
                    return result?.Data;
                }

                return null;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error fetching live detections");
                return null;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _httpClient?.Dispose();
                _disposed = true;
            }
        }

        #region DTOs

        public class DetectionRecord
        {
            public string Timestamp { get; set; } = "";
            public string Stream { get; set; } = "";
            public string? Stream_Type { get; set; }
            public string? Stream_Number { get; set; }
            public string Track { get; set; } = "";
            public int Duration_Seconds { get; set; } = 10;
            public double? Confidence { get; set; }

            /// <summary>
            /// Create a DetectionRecord from detection parameters.
            /// </summary>
            public static DetectionRecord Create(
                string stream,
                string streamType,
                string? streamNumber,
                string track,
                DateTime timestamp,
                int durationSeconds,
                double confidence)
            {
                return new DetectionRecord
                {
                    Timestamp = timestamp.ToString("yyyy-MM-ddTHH:mm:ss"),
                    Stream = stream,
                    Stream_Type = streamType,
                    Stream_Number = streamNumber,
                    Track = track,
                    Duration_Seconds = durationSeconds,
                    Confidence = confidence
                };
            }
        }

        private class ApiResponse
        {
            public bool Success { get; set; }
            public string? Message { get; set; }
            public object? Data { get; set; }
        }

        private class HealthResponse
        {
            public bool Success { get; set; }
            public string? Status { get; set; }
            public string? Database { get; set; }
        }

        private class LiveDetectionsResponse
        {
            public bool Success { get; set; }
            public DetectionRecord[]? Data { get; set; }
        }

        #endregion
    }
}
