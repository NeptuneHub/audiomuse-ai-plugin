using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.AudioMuseAi.Services;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.AudioMuseAi.Controller
{
    [ApiController]
    [Route("AudioMuseAI")]
    public class AudioMuseController : ControllerBase
    {
        private readonly IAudioMuseService _svc;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioMuseController"/> class.
        /// </summary>
        public AudioMuseController()
        {
            // Instantiate service directly to avoid DI issues and ensure config is loaded.
            _svc = new AudioMuseService();
        }

        /// <summary>
        /// Health check endpoint.
        /// </summary>
        [HttpGet("health")]
        public async Task<IActionResult> Health()
        {
            var resp = await _svc.HealthCheckAsync();
            return resp.IsSuccessStatusCode
                ? Ok()
                : StatusCode((int)resp.StatusCode);
        }

        /// <summary>
        /// Retrieves playlists from the backend.
        /// </summary>
        [HttpGet("playlists")]
        public async Task<IActionResult> GetPlaylists()
        {
            var resp = await _svc.GetPlaylistsAsync();
            var json = await resp.Content.ReadAsStringAsync();
            return new ContentResult
            {
                Content = json,
                ContentType = "application/json",
                StatusCode = (int)resp.StatusCode
            };
        }

        /// <summary>
        /// Starts an analysis job.
        /// </summary>
        [HttpPost("analysis")]
        public async Task<IActionResult> StartAnalysis([FromBody] object payload)
        {
            var json = JsonSerializer.Serialize(payload);
            var resp = await _svc.StartAnalysisAsync(json);
            var body = await resp.Content.ReadAsStringAsync();
            return new ContentResult
            {
                Content = body,
                ContentType = "application/json",
                StatusCode = (int)resp.StatusCode
            };
        }

        /// <summary>
        /// Starts a clustering job.
        /// </summary>
        [HttpPost("clustering")]
        public async Task<IActionResult> StartClustering([FromBody] object payload)
        {
            var json = JsonSerializer.Serialize(payload);
            var resp = await _svc.StartClusteringAsync(json);
            var body = await resp.Content.ReadAsStringAsync();
            return new ContentResult
            {
                Content = body,
                ContentType = "application/json",
                StatusCode = (int)resp.StatusCode
            };
        }

        /// <summary>
        /// Searches for tracks by title or artist (at least one required).
        /// </summary>
        [HttpGet("search_tracks")]
        public async Task<IActionResult> SearchTracks(
            [FromQuery] string? title = null,
            [FromQuery] string? artist = null)
        {
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(artist))
            {
                return BadRequest("Either 'title' or 'artist' query parameter must be provided.");
            }

            var resp = await _svc.SearchTracksAsync(title, artist);
            var json = await resp.Content.ReadAsStringAsync();
            return new ContentResult
            {
                Content = json,
                ContentType = "application/json",
                StatusCode = (int)resp.StatusCode
            };
        }

        /// <summary>
        /// Retrieves similar tracks.
        /// </summary>
        [HttpGet("similar_tracks")]
        public async Task<IActionResult> GetSimilarTracks(
            [FromQuery] string? item_id = null,
            [FromQuery] string? title = null,
            [FromQuery] string? artist = null,
            [FromQuery] int n = 10)
        {
            var resp = await _svc.GetSimilarTracksAsync(item_id, title, artist, n);
            var json = await resp.Content.ReadAsStringAsync();
            return new ContentResult
            {
                Content = json,
                ContentType = "application/json",
                StatusCode = (int)resp.StatusCode
            };
        }

        /// <summary>
        /// Model for create playlist request.
        /// </summary>
        public class CreatePlaylistModel
        {
            public string playlist_name { get; set; }
            public IEnumerable<string> track_ids { get; set; }
        }

        /// <summary>
        /// Creates a new playlist.
        /// </summary>
        [HttpPost("create_playlist")]
        public async Task<IActionResult> CreatePlaylist([FromBody] CreatePlaylistModel model)
        {
            var resp = await _svc.CreatePlaylistAsync(model.playlist_name, model.track_ids);
            var json = await resp.Content.ReadAsStringAsync();
            return new ContentResult
            {
                Content = json,
                ContentType = "application/json",
                StatusCode = (int)resp.StatusCode
            };
        }

        /// <summary>
        /// Gets the status of a specific task.
        /// </summary>
        [HttpGet("status/{taskId}")]
        public async Task<IActionResult> GetTaskStatus(string taskId)
        {
            var resp = await _svc.GetTaskStatusAsync(taskId);
            var json = await resp.Content.ReadAsStringAsync();
            return new ContentResult
            {
                Content = json,
                ContentType = "application/json",
                StatusCode = (int)resp.StatusCode
            };
        }

        /// <summary>
        /// Cancels a specific task.
        /// </summary>
        [HttpPost("cancel/{taskId}")]
        public async Task<IActionResult> CancelTask(string taskId)
        {
            var resp = await _svc.CancelTaskAsync(taskId);
            var json = await resp.Content.ReadAsStringAsync();
            return new ContentResult
            {
                Content = json,
                ContentType = "application/json",
                StatusCode = (int)resp.StatusCode
            };
        }

        /// <summary>
        /// Cancels all tasks of a specific type.
        /// </summary>
        [HttpPost("cancel_all/{taskTypePrefix}")]
        public async Task<IActionResult> CancelAllTasksByType(string taskTypePrefix)
        {
            var resp = await _svc.CancelAllTasksByTypeAsync(taskTypePrefix);
            var json = await resp.Content.ReadAsStringAsync();
            return new ContentResult
            {
                Content = json,
                ContentType = "application/json",
                StatusCode = (int)resp.StatusCode
            };
        }

        /// <summary>
        /// Gets the status of the most recent overall main task.
        /// </summary>
        [HttpGet("last_task")]
        public async Task<IActionResult> GetLastTask()
        {
            var resp = await _svc.GetLastTaskAsync();
            var json = await resp.Content.ReadAsStringAsync();
            return new ContentResult
            {
                Content = json,
                ContentType = "application/json",
                StatusCode = (int)resp.StatusCode
            };
        }

        /// <summary>
        /// Gets the status of the currently active main task.
        /// </summary>
        [HttpGet("active_tasks")]
        public async Task<IActionResult> GetActiveTasks()
        {
            var resp = await _svc.GetActiveTasksAsync();
            var json = await resp.Content.ReadAsStringAsync();
            return new ContentResult
            {
                Content = json,
                ContentType = "application/json",
                StatusCode = (int)resp.StatusCode
            };
        }

        /// <summary>
        /// Gets the current server configuration.
        /// </summary>
        [HttpGet("config")]
        public async Task<IActionResult> GetConfig()
        {
            var resp = await _svc.GetConfigAsync();
            var json = await resp.Content.ReadAsStringAsync();
            return new ContentResult
            {
                Content = json,
                ContentType = "application/json",
                StatusCode = (int)resp.StatusCode
            };
        }
    }
}
