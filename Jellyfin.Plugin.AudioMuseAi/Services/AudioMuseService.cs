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
        /// </summary>
        public AudioMuseService()
        {
            _http = new HttpClient();
            
            // Get the configuration from the static Plugin instance.
            // This is safe because this constructor is only called when the service
            // is first requested, by which time the plugin has been fully initialized.
            var config = Plugin.Instance.Configuration;
            if (!string.IsNullOrEmpty(config.BackendUrl) && Uri.IsWellFormedUriString(config.BackendUrl, UriKind.Absolute))
            {
                _http.BaseAddress = new Uri(config.BackendUrl);
            }
        }

        public Task<HttpResponseMessage> HealthCheckAsync()
        {
            // Hits the root of the Flask service
            return _http.GetAsync("/");
        }

        public Task<HttpResponseMessage> GetPlaylistsAsync()
        {
            return _http.GetAsync("/api/playlists");
        }

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
    }
}
