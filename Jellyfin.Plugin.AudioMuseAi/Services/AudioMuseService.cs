using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.AudioMuseAi.Services
{
    public class AudioMuseService : IAudioMuseService
    {
        private readonly HttpClient _http;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioMuseService"/> class.
        /// Ensures BaseAddress is always set using either configured URL or default.
        /// </summary>
        public AudioMuseService()
        {
            // Retrieve configured backend URL, fallback to default if missing
            var config = Plugin.Instance?.Configuration;
            var backendUrl = !string.IsNullOrWhiteSpace(config?.BackendUrl)
                ? config.BackendUrl.TrimEnd('/')
                : new Configuration.PluginConfiguration().BackendUrl.TrimEnd('/');

            if (!Uri.IsWellFormedUriString(backendUrl, UriKind.Absolute))
            {
                throw new InvalidOperationException(
                    $"AudioMuseAI: BackendUrl is invalid ('{backendUrl}'). " +
                    "Please check the plugin settings (Administration → Plugins → AudioMuse AI).");
            }

            // Instantiate HttpClient with BaseAddress set
            _http = new HttpClient { BaseAddress = new Uri(backendUrl) };
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> HealthCheckAsync()
        {
            return _http.GetAsync("/");
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetPlaylistsAsync()
        {
            return _http.GetAsync("/api/playlists");
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> StartAnalysisAsync(string jsonPayload)
        {
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            return _http.PostAsync("/api/analysis/start", content);
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> StartClusteringAsync(string jsonPayload)
        {
            var content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");
            return _http.PostAsync("/api/clustering/start", content);
        }
    }
}
