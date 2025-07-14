using System.Net.Http;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.AudioMuseAi.Services
{
    /// <summary>
    /// Defines the interface for the AudioMuse service client.
    /// </summary>
    public interface IAudioMuseService
    {
        /// <summary>
        /// Performs a health check on the backend service.
        /// </summary>
        /// <returns>A task containing the HTTP response.</returns>
        Task<HttpResponseMessage> HealthCheckAsync();

        /// <summary>
        /// Gets playlists from the backend service.
        /// </summary>
        /// <returns>A task containing the HTTP response.</returns>
        Task<HttpResponseMessage> GetPlaylistsAsync();

        /// <summary>
        /// Starts an analysis task on the backend service.
        /// </summary>
        /// <param name="jsonPayload">The JSON payload for the request.</param>
        /// <returns>A task containing the HTTP response.</returns>
        Task<HttpResponseMessage> StartAnalysisAsync(string jsonPayload);

        /// <summary>
        /// Starts a clustering task on the backend service.
        /// </summary>
        /// <param name="jsonPayload">The JSON payload for the request.</param>
        /// <returns>A task containing the HTTP response.</returns>
        Task<HttpResponseMessage> StartClusteringAsync(string jsonPayload);
    }
}
