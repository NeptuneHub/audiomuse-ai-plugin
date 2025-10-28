using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
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
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> containing the <see cref="HttpResponseMessage"/>.</returns>
        Task<HttpResponseMessage> HealthCheckAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Gets playlists from the backend service.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> containing the <see cref="HttpResponseMessage"/>.</returns>
        Task<HttpResponseMessage> GetPlaylistsAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Starts an analysis task on the backend service.
        /// </summary>
        /// <param name="jsonPayload">The JSON payload for the request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> containing the <see cref="HttpResponseMessage"/>.</returns>
        Task<HttpResponseMessage> StartAnalysisAsync(string jsonPayload, CancellationToken cancellationToken);

        /// <summary>
        /// Starts a clustering task on the backend service.
        /// </summary>
        /// <param name="jsonPayload">The JSON payload for the request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> containing the <see cref="HttpResponseMessage"/>.</returns>
        Task<HttpResponseMessage> StartClusteringAsync(string jsonPayload, CancellationToken cancellationToken);

        /// <summary>
        /// Searches for tracks by title and artist.
        /// </summary>
        /// <param name="title">The track title to search for.</param>
        /// <param name="artist">The artist name to search for.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> containing the <see cref="HttpResponseMessage"/>.</returns>
        Task<HttpResponseMessage> SearchTracksAsync(string? title, string? artist, CancellationToken cancellationToken);

        /// <summary>
        /// Retrieves similar tracks by item ID or by title and artist.
        /// </summary>
        /// <param name="item_id">Optional track item ID.</param>
        /// <param name="title">Optional track title.</param>
        /// <param name="artist">Optional artist name.</param>
        /// <param name="n">Number of similar tracks to return.</param>
        /// <param name="eliminate_duplicates">Optional flag to limit songs per artist.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> containing the <see cref="HttpResponseMessage"/>.</returns>
        Task<HttpResponseMessage> GetSimilarTracksAsync(string? item_id, string? title, string? artist, int n, string? eliminate_duplicates, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the maximum distance information for a given item.
    /// </summary>
    /// <param name="item_id">The item id to query.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A <see cref="Task"/> containing the <see cref="HttpResponseMessage"/>.</returns>
    Task<HttpResponseMessage> GetMaxDistanceAsync(string? item_id, CancellationToken cancellationToken);

        /// <summary>
        /// Finds a path of similar songs between a start and end track.
        /// </summary>
        /// <param name="start_song_id">The starting song ID.</param>
        /// <param name="end_song_id">The ending song ID.</param>
        /// <param name="max_steps">Optional maximum number of steps in the path.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> containing the <see cref="HttpResponseMessage"/>.</returns>
        Task<HttpResponseMessage> FindPathAsync(string start_song_id, string end_song_id, int? max_steps, CancellationToken cancellationToken);

        /// <summary>
        /// Creates a new playlist on the media server with specified tracks.
        /// </summary>
        /// <param name="playlist_name">Name of the playlist to create.</param>
        /// <param name="track_ids">Collection of track IDs to include.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> containing the <see cref="HttpResponseMessage"/>.</returns>
        Task<HttpResponseMessage> CreatePlaylistAsync(string playlist_name, IEnumerable<string> track_ids, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the status of a specific task.
        /// </summary>
        /// <param name="task_id">The ID of the task.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> containing the <see cref="HttpResponseMessage"/>.</returns>
        Task<HttpResponseMessage> GetTaskStatusAsync(string task_id, CancellationToken cancellationToken);

        /// <summary>
        /// Cancels a specific task.
        /// </summary>
        /// <param name="task_id">The ID of the task to cancel.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> containing the <see cref="HttpResponseMessage"/>.</returns>
        Task<HttpResponseMessage> CancelTaskAsync(string task_id, CancellationToken cancellationToken);

        /// <summary>
        /// Cancels all tasks of a specific type.
        /// </summary>
        /// <param name="task_type_prefix">The prefix of the task type to cancel (e.g., "main_analysis").</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> containing the <see cref="HttpResponseMessage"/>.</returns>
        Task<HttpResponseMessage> CancelAllTasksByTypeAsync(string task_type_prefix, CancellationToken cancellationToken);

        /// <summary>
        /// Gets the status of the most recent overall main task.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> containing the <see cref="HttpResponseMessage"/>.</returns>
        Task<HttpResponseMessage> GetLastTaskAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Gets the status of the currently active main task, if any.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> containing the <see cref="HttpResponseMessage"/>.</returns>
        Task<HttpResponseMessage> GetActiveTasksAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Gets the current server configuration.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> containing the <see cref="HttpResponseMessage"/>.</returns>
        Task<HttpResponseMessage> GetConfigAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Gets the default AI configuration for the chat interface.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> containing the <see cref="HttpResponseMessage"/>.</returns>
        Task<HttpResponseMessage> GetChatConfigDefaultsAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Processes a user's chat input to generate a playlist.
        /// </summary>
        /// <param name="jsonPayload">The JSON payload for the request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> containing the <see cref="HttpResponseMessage"/>.</returns>
        Task<HttpResponseMessage> PostChatPlaylistAsync(string jsonPayload, CancellationToken cancellationToken);

        /// <summary>
        /// Creates a new playlist from the chat interface.
        /// </summary>
        /// <param name="jsonPayload">The JSON payload containing playlist name and track IDs.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> containing the <see cref="HttpResponseMessage"/>.</returns>
        Task<HttpResponseMessage> CreateChatPlaylistAsync(string jsonPayload, CancellationToken cancellationToken);

        /// <summary>
        /// Generates a sonic fingerprint for a user.
        /// </summary>
        /// <param name="jellyfin_user_identifier">The Jellyfin username or user ID.</param>
        /// <param name="jellyfin_token">The Jellyfin API token (optional).</param>
        /// <param name="n">Optional number of results to return.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> containing the <see cref="HttpResponseMessage"/>.</returns>
        Task<HttpResponseMessage> GenerateSonicFingerprintAsync(string jellyfin_user_identifier, string? jellyfin_token, int? n, CancellationToken cancellationToken);

        /// <summary>
        /// Forwards an alchemy request payload to the backend AudioMuse service.
        /// </summary>
        /// <param name="jsonPayload">The raw JSON payload to forward.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="Task"/> containing the <see cref="HttpResponseMessage"/>.</returns>
        Task<HttpResponseMessage> AlchemyAsync(string jsonPayload, CancellationToken cancellationToken);
    }
}
