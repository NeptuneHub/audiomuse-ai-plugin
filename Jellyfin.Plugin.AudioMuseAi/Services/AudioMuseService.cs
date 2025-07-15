using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.AudioMuseAi.Services
{
    /// <summary>
    /// Concrete implementation of <see cref="IAudioMuseService"/>,
    /// handling all HTTP interactions with the AudioMuse backend.
    /// </summary>
    public class AudioMuseService : IAudioMuseService
    {
        private readonly HttpClient _http;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioMuseService"/> class.
        /// Ensures <see cref="HttpClient.BaseAddress"/> is set via plugin settings.
        /// </summary>
        public AudioMuseService()
        {
            var config = Plugin.Instance?.Configuration;
            var backendUrl = !string.IsNullOrWhiteSpace(config?.BackendUrl)
                ? config.BackendUrl.TrimEnd('/')
                : new Configuration.PluginConfiguration().BackendUrl.TrimEnd('/');

            if (!Uri.IsWellFormedUriString(backendUrl, UriKind.Absolute))
            {
                throw new InvalidOperationException(
                    $"AudioMuseAI: BackendUrl is invalid ('{backendUrl}'). " +
                    "Please configure a valid absolute URL in Administration → Plugins → AudioMuse AI.");
            }

            _http = new HttpClient { BaseAddress = new Uri(backendUrl) };
        }

        public Task<HttpResponseMessage> HealthCheckAsync() =>
            _http.GetAsync("/");

        public Task<HttpResponseMessage> GetPlaylistsAsync() =>
            _http.GetAsync("/api/playlists");

        public Task<HttpResponseMessage> StartAnalysisAsync(string jsonPayload)
        {
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            return _http.PostAsync("/api/analysis/start", content);
        }

        public Task<HttpResponseMessage> StartClusteringAsync(string jsonPayload)
        {
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            return _http.PostAsync("/api/clustering/start", content);
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> SearchTracksAsync(string? title, string? artist)
        {
            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(title))
                query.Add($"title={Uri.EscapeDataString(title)}");
            if (!string.IsNullOrWhiteSpace(artist))
                query.Add($"artist={Uri.EscapeDataString(artist)}");

            var url = "/api/search_tracks";
            if (query.Count > 0)
                url += "?" + string.Join("&", query);

            return _http.GetAsync(url);
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetSimilarTracksAsync(
            string? item_id = null,
            string? title = null,
            string? artist = null,
            int n = 10)
        {
            var query = new List<string> { $"n={n}" };
            if (!string.IsNullOrWhiteSpace(item_id))
            {
                query.Add($"item_id={Uri.EscapeDataString(item_id)}");
            }
            else if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(artist))
            {
                query.Add($"title={Uri.EscapeDataString(title)}");
                query.Add($"artist={Uri.EscapeDataString(artist)}");
            }

            var url = "/api/similar_tracks";
            if (query.Count > 0)
                url += "?" + string.Join("&", query);

            return _http.GetAsync(url);
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> CreatePlaylistAsync(
            string playlist_name,
            IEnumerable<string> track_ids)
        {
            var payload = new
            {
                playlist_name,
                track_ids
            };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            return _http.PostAsync("/api/create_playlist", content);
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetTaskStatusAsync(string taskId) =>
            _http.GetAsync($"/api/status/{taskId}");

        /// <inheritdoc />
        public Task<HttpResponseMessage> CancelTaskAsync(string taskId) =>
            _http.PostAsync($"/api/cancel/{taskId}", null);

        /// <inheritdoc />
        public Task<HttpResponseMessage> CancelAllTasksByTypeAsync(string taskTypePrefix) =>
            _http.PostAsync($"/api/cancel_all/{taskTypePrefix}", null);

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetLastTaskAsync() =>
            _http.GetAsync("/api/last_task");

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetActiveTasksAsync() =>
            _http.GetAsync("/api/active_tasks");

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetConfigAsync() =>
            _http.GetAsync("/api/config");

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetChatConfigDefaultsAsync() =>
            _http.GetAsync("/chat/api/config_defaults");

        /// <inheritdoc />
        public Task<HttpResponseMessage> PostChatPlaylistAsync(string jsonPayload)
        {
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            return _http.PostAsync("/chat/api/chatPlaylist", content);
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> CreateChatPlaylistAsync(string jsonPayload)
        {
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            return _http.PostAsync("/chat/api/create_playlist", content);
        }
    }
}
