using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.AudioMuseAi.Services
{
    /// <summary>
    /// Concrete implementation of <see cref="IAudioMuseService"/>,
    /// handling all HTTP interactions with the AudioMuse backend.
    /// </summary>
    public class AudioMuseService : IAudioMuseService, IDisposable
    {
        private readonly HttpClient _http;
        private bool _disposed = false;

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

        /// <inheritdoc />
        public Task<HttpResponseMessage> HealthCheckAsync(CancellationToken cancellationToken)
        {
            return _http.GetAsync("/", cancellationToken);
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetPlaylistsAsync(CancellationToken cancellationToken)
        {
            return _http.GetAsync("/api/playlists", cancellationToken);
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> StartAnalysisAsync(string jsonPayload, CancellationToken cancellationToken)
        {
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            return _http.PostAsync("/api/analysis/start", content, cancellationToken);
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> StartClusteringAsync(string jsonPayload, CancellationToken cancellationToken)
        {
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            return _http.PostAsync("/api/clustering/start", content, cancellationToken);
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> SearchTracksAsync(string? title, string? artist, CancellationToken cancellationToken)
        {
            var query = new List<string>();
            if (!string.IsNullOrWhiteSpace(title))
            {
                query.Add($"title={Uri.EscapeDataString(title)}");
            }

            if (!string.IsNullOrWhiteSpace(artist))
            {
                query.Add($"artist={Uri.EscapeDataString(artist)}");
            }

            var url = "/api/search_tracks";
            if (query.Count > 0)
            {
                url += "?" + string.Join("&", query);
            }

            return _http.GetAsync(url, cancellationToken);
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetSimilarTracksAsync(string? item_id, string? title, string? artist, int n, string? eliminate_duplicates, CancellationToken cancellationToken)
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

            if (!string.IsNullOrWhiteSpace(eliminate_duplicates))
            {
                query.Add($"eliminate_duplicates={eliminate_duplicates.ToLowerInvariant()}");
            }

            var url = "/api/similar_tracks";
            if (query.Count > 1) // n is always present
            {
                url += "?" + string.Join("&", query);
            }

            return _http.GetAsync(url, cancellationToken);
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> FindPathAsync(string start_song_id, string end_song_id, int? max_steps, CancellationToken cancellationToken)
        {
            var query = new List<string>
            {
                $"start_song_id={Uri.EscapeDataString(start_song_id)}",
                $"end_song_id={Uri.EscapeDataString(end_song_id)}"
            };

            if (max_steps.HasValue)
            {
                query.Add($"max_steps={max_steps.Value}");
            }

            var url = "/api/find_path?" + string.Join("&", query);
            return _http.GetAsync(url, cancellationToken);
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> CreatePlaylistAsync(string playlist_name, IEnumerable<string> track_ids, CancellationToken cancellationToken)
        {
            var payload = new
            {
                playlist_name,
                track_ids
            };
            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            return _http.PostAsync("/api/create_playlist", content, cancellationToken);
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetTaskStatusAsync(string task_id, CancellationToken cancellationToken)
        {
            return _http.GetAsync($"/api/status/{task_id}", cancellationToken);
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> CancelTaskAsync(string task_id, CancellationToken cancellationToken)
        {
            return _http.PostAsync($"/api/cancel/{task_id}", null, cancellationToken);
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> CancelAllTasksByTypeAsync(string task_type_prefix, CancellationToken cancellationToken)
        {
            return _http.PostAsync($"/api/cancel_all/{task_type_prefix}", null, cancellationToken);
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetLastTaskAsync(CancellationToken cancellationToken)
        {
            return _http.GetAsync("/api/last_task", cancellationToken);
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetActiveTasksAsync(CancellationToken cancellationToken)
        {
            return _http.GetAsync("/api/active_tasks", cancellationToken);
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetConfigAsync(CancellationToken cancellationToken)
        {
            return _http.GetAsync("/api/config", cancellationToken);
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetChatConfigDefaultsAsync(CancellationToken cancellationToken)
        {
            return _http.GetAsync("/chat/api/config_defaults", cancellationToken);
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> PostChatPlaylistAsync(string jsonPayload, CancellationToken cancellationToken)
        {
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            return _http.PostAsync("/chat/api/chatPlaylist", content, cancellationToken);
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> CreateChatPlaylistAsync(string jsonPayload, CancellationToken cancellationToken)
        {
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            return _http.PostAsync("/chat/api/create_playlist", content, cancellationToken);
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GenerateSonicFingerprintAsync(string jellyfin_user_identifier, string? jellyfin_token, int? n, CancellationToken cancellationToken)
        {
            var query = new List<string>
            {
                $"jellyfin_user_identifier={Uri.EscapeDataString(jellyfin_user_identifier)}"
            };

            if (!string.IsNullOrWhiteSpace(jellyfin_token))
            {
                 query.Add($"jellyfin_token={Uri.EscapeDataString(jellyfin_token)}");
            }

            if (n.HasValue)
            {
                query.Add($"n={n.Value}");
            }

            var url = "/api/sonic_fingerprint/generate?" + string.Join("&", query);
            return _http.GetAsync(url, cancellationToken);
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="AudioMuseService"/> and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">True to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _http.Dispose();
            }

            _disposed = true;
        }
    }
}
