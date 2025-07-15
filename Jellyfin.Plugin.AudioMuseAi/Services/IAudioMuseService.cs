using System.Collections.Generic;
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
        Task<HttpResponseMessage> HealthCheckAsync();

        /// <summary>
        /// Gets playlists from the backend service.
        /// </summary>
        Task<HttpResponseMessage> GetPlaylistsAsync();

        /// <summary>
        /// Starts an analysis task on the backend service.
        /// </summary>
        /// <param name="jsonPayload">The JSON payload for the request.</param>
        Task<HttpResponseMessage> StartAnalysisAsync(string jsonPayload);

        /// <summary>
        /// Starts a clustering task on the backend service.
        /// </summary>
        /// <param name="jsonPayload">The JSON payload for the request.</param>
        Task<HttpResponseMessage> StartClusteringAsync(string jsonPayload);

        /// <summary>
        /// Searches for tracks by title and artist.
        /// </summary>
        /// <param name="title">The track title to search for.</param>
        /// <param name="artist">The artist name to search for.</param>
        Task<HttpResponseMessage> SearchTracksAsync(string? title, string? artist);

        /// <summary>
        /// Retrieves similar tracks by item ID or by title and artist.
        /// </summary>
        /// <param name="item_id">Optional track item ID.</param>
        /// <param name="title">Optional track title.</param>
        /// <param name="artist">Optional artist name.</param>
        /// <param name="n">Number of similar tracks to return.</param>
        Task<HttpResponseMessage> GetSimilarTracksAsync(string? item_id = null, string? title = null, string? artist = null, int n = 10);

        /// <summary>
        /// Creates a new playlist on the media server with specified tracks.
        /// </summary>
        /// <param name="playlist_name">Name of the playlist to create.</param>
        /// <param name="track_ids">Collection of track IDs to include.</param>
        Task<HttpResponseMessage> CreatePlaylistAsync(string playlist_name, IEnumerable<string> track_ids);

        /// <summary>
        /// Gets the status of a specific task.
        /// </summary>
        /// <param name="taskId">The ID of the task.</param>
        Task<HttpResponseMessage> GetTaskStatusAsync(string taskId);

        /// <summary>
        /// Cancels a specific task.
        /// </summary>
        /// <param name="taskId">The ID of the task to cancel.</param>
        Task<HttpResponseMessage> CancelTaskAsync(string taskId);

        /// <summary>
        /// Cancels all tasks of a specific type.
        /// </summary>
        /// <param name="taskTypePrefix">The prefix of the task type to cancel (e.g., "main_analysis").</param>
        Task<HttpResponseMessage> CancelAllTasksByTypeAsync(string taskTypePrefix);

        /// <summary>
        /// Gets the status of the most recent overall main task.
        /// </summary>
        Task<HttpResponseMessage> GetLastTaskAsync();

        /// <summary>
        /// Gets the status of the currently active main task, if any.
        /// </summary>
        Task<HttpResponseMessage> GetActiveTasksAsync();

        /// <summary>
        /// Gets the current server configuration.
        /// </summary>
        Task<HttpResponseMessage> GetConfigAsync();
    }
}
