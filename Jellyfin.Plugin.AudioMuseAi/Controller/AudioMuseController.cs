using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.AudioMuseAi.Models;
using Jellyfin.Plugin.AudioMuseAi.Services;
using System.Net.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Jellyfin.Plugin.AudioMuseAi.Controller
{
    /// <summary>
    /// The AudioMuse AI API controller.
    /// </summary>
    [ApiController]
    [Route("AudioMuseAI")]
    public class AudioMuseController : ControllerBase
    {
        private readonly IAudioMuseService _svc;
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioMuseController"/> class.
        /// </summary>
        public AudioMuseController()
        {
            _svc = new AudioMuseService();
        }

        /// <summary>
        /// DI-enabled constructor for injecting IHttpClientFactory.
        /// </summary>
        public AudioMuseController(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
            _svc = new AudioMuseService();
        }

        /// <summary>
        /// Provides information about the plugin version and available endpoints.
        /// </summary>
        /// <returns>An <see cref="IActionResult"/> with plugin info.</returns>
        [HttpGet("info")]
        public IActionResult GetInfo()
        {
            var version = Plugin.Instance.Version.ToString();

            var controllerType = typeof(AudioMuseController);
            var baseRoute = controllerType.GetCustomAttribute<RouteAttribute>()?.Template ?? string.Empty;

            var availableEndpoints = controllerType.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                .SelectMany(method => method.GetCustomAttributes<HttpMethodAttribute>()
                    .Select(attr => new
                    {
                        HttpMethod = attr.HttpMethods.FirstOrDefault(),
                        RouteTemplate = attr.Template
                    }))
                .Where(attr => attr.HttpMethod != null && attr.RouteTemplate != "info") // Exclude the info endpoint itself
                .Select(attr =>
                {
                    // Combine base route and method-specific route
                    var fullPath = $"/{baseRoute}/{attr.RouteTemplate}".Replace("//", "/");
                    return $"{attr.HttpMethod} {fullPath}";
                })
                .Distinct()
                .OrderBy(e => e)
                .ToList();

            var infoPayload = new
            {
                Version = version,
                AvailableEndpoints = availableEndpoints
            };

            return new OkObjectResult(infoPayload);
        }

        /// <summary>
        /// Health check endpoint.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>An <see cref="IActionResult"/> indicating the health status.</returns>
        [HttpGet("health")]
        public async Task<IActionResult> Health(CancellationToken cancellationToken)
        {
            var resp = await _svc.HealthCheckAsync(cancellationToken).ConfigureAwait(false);
            return resp.IsSuccessStatusCode
                ? Ok()
                : StatusCode((int)resp.StatusCode);
        }

        /// <summary>
        /// Tests connection to a specific backend URL.
        /// </summary>
        /// <param name="backendUrl">The backend URL to test.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>An <see cref="IActionResult"/> with the test result.</returns>
        [HttpPost("test_connection")]
        public async Task<IActionResult> TestConnection([FromBody] string backendUrl, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(backendUrl))
            {
                return BadRequest(new { success = false, message = "Backend URL is required." });
            }

            try
            {
                // Use the host-provided/factory-created HttpClient with proper TLS/proxy configuration
                var client = _httpClientFactory.CreateClient();
                var trimmed = backendUrl.TrimEnd('/');
                client.BaseAddress = new System.Uri(trimmed);
                var resp = await client.GetAsync("/api/active_tasks", cancellationToken).ConfigureAwait(false);

                return Ok(new
                {
                    success = resp.IsSuccessStatusCode,
                    message = resp.IsSuccessStatusCode ? "✓ Connection successful!" : $"✗ Failed: {resp.StatusCode}"
                });
            }
            catch (System.ArgumentException ex)
            {
                return Ok(new { success = false, message = $"Invalid URL: {ex.Message}" });
            }
            catch (System.Net.Http.HttpRequestException ex)
            {
                return Ok(new { success = false, message = $"Connection failed: {ex.Message}" });
            }
            catch (System.Exception ex)
            {
                return Ok(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        /// <summary>
        /// Retrieves playlists from the backend.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="ContentResult"/> containing the playlists JSON.</returns>
        [HttpGet("playlists")]
        public async Task<IActionResult> GetPlaylists(CancellationToken cancellationToken)
        {
            var resp = await _svc.GetPlaylistsAsync(cancellationToken).ConfigureAwait(false);
            var json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
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
        /// <param name="payload">The request payload.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="ContentResult"/> containing the response from the backend.</returns>
        [HttpPost("analysis")]
        public async Task<IActionResult> StartAnalysis([FromBody] object payload, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(payload);
            var resp = await _svc.StartAnalysisAsync(json, cancellationToken).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
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
        /// <param name="payload">The request payload.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="ContentResult"/> containing the response from the backend.</returns>
        [HttpPost("clustering")]
        public async Task<IActionResult> StartClustering([FromBody] object payload, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(payload);
            var resp = await _svc.StartClusteringAsync(json, cancellationToken).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new ContentResult
            {
                Content = body,
                ContentType = "application/json",
                StatusCode = (int)resp.StatusCode
            };
        }

        /// <summary>
        /// Searches for tracks using CLAP query.
        /// </summary>
        /// <param name="payload">The request payload.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="ContentResult"/> containing the response from the backend.</returns>
        [HttpPost("clap/search")]
        public async Task<IActionResult> ClapSearch([FromBody] object payload, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(payload);
            var resp = await _svc.ClapSearchAsync(json, cancellationToken).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
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
        /// <param name="title">The track title.</param>
        /// <param name="artist">The track artist.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="ContentResult"/> containing the search results.</returns>
        [HttpGet("search_tracks")]
        public async Task<IActionResult> SearchTracks(
            [FromQuery] string? title,
            [FromQuery] string? artist,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(artist))
            {
                return BadRequest("Either 'title' or 'artist' query parameter must be provided.");
            }

            var resp = await _svc.SearchTracksAsync(title, artist, cancellationToken).ConfigureAwait(false);
            var json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
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
        /// <param name="item_id">The item id.</param>
        /// <param name="title">The track title.</param>
        /// <param name="artist">The track artist.</param>
        /// <param name="n">The number of results to return.</param>
        /// <param name="eliminate_duplicates">Optional flag to limit songs per artist.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="ContentResult"/> containing the similar tracks.</returns>
        [HttpGet("similar_tracks")]
        public async Task<IActionResult> GetSimilarTracks(
            [FromQuery] string? item_id,
            [FromQuery] string? title,
            [FromQuery] string? artist,
            [FromQuery] int n,
            [FromQuery] string? eliminate_duplicates,
            CancellationToken cancellationToken)
        {
            var resp = await _svc.GetSimilarTracksAsync(item_id, title, artist, n, eliminate_duplicates, cancellationToken).ConfigureAwait(false);
            var json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new ContentResult
            {
                Content = json,
                ContentType = "application/json",
                StatusCode = (int)resp.StatusCode
            };
        }

        /// <summary>
        /// Retrieves similar artists.
        /// </summary>
        /// <param name="artist">The artist name.</param>
        /// <param name="artist_id">The artist ID.</param>
        /// <param name="n">The number of results to return.</param>
        /// <param name="ef_search">Optional HNSW search parameter.</param>
        /// <param name="include_component_matches">Optional flag to include component-level matches.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="ContentResult"/> containing the similar artists.</returns>
        [HttpGet("similar_artists")]
        public async Task<IActionResult> GetSimilarArtists(
            [FromQuery] string? artist,
            [FromQuery] string? artist_id,
            [FromQuery] int n,
            [FromQuery] int? ef_search,
            [FromQuery] bool? include_component_matches,
            CancellationToken cancellationToken)
        {
            var resp = await _svc.GetSimilarArtistsAsync(artist, artist_id, n, ef_search, include_component_matches, cancellationToken).ConfigureAwait(false);
            var json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new ContentResult
            {
                Content = json,
                ContentType = "application/json",
                StatusCode = (int)resp.StatusCode
            };
        }

        /// <summary>
        /// Gets the maximum distance information for a given item.
        /// Forwards the backend response 1:1.
        /// </summary>
        /// <param name="item_id">The item id.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="ContentResult"/> containing the backend response.</returns>
        [HttpGet("max_distance")]
        public async Task<IActionResult> GetMaxDistance(
            [FromQuery] string? item_id,
            CancellationToken cancellationToken)
        {
            var resp = await _svc.GetMaxDistanceAsync(item_id, cancellationToken).ConfigureAwait(false);
            var json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new ContentResult
            {
                Content = json,
                ContentType = "application/json",
                StatusCode = (int)resp.StatusCode
            };
        }

        /// <summary>
        /// Finds a path of similar songs between a start and end track.
        /// </summary>
        /// <param name="start_song_id">The starting song ID.</param>
        /// <param name="end_song_id">The ending song ID.</param>
        /// <param name="max_steps">Optional maximum number of steps in the path.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="ContentResult"/> containing the path.</returns>
        [HttpGet("find_path")]
        public async Task<IActionResult> FindPath(
            [FromQuery] string start_song_id,
            [FromQuery] string end_song_id,
            [FromQuery] int? max_steps,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(start_song_id) || string.IsNullOrWhiteSpace(end_song_id))
            {
                return BadRequest("start_song_id and end_song_id are required.");
            }

            var resp = await _svc.FindPathAsync(start_song_id, end_song_id, max_steps, cancellationToken).ConfigureAwait(false);
            var json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new ContentResult
            {
                Content = json,
                ContentType = "application/json",
                StatusCode = (int)resp.StatusCode
            };
        }

        /// <summary>
        /// Creates a new playlist.
        /// </summary>
        /// <param name="model">The playlist creation model.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="ContentResult"/> containing the response from the backend.</returns>
        [HttpPost("create_playlist")]
        public async Task<IActionResult> CreatePlaylist([FromBody] CreatePlaylistModel model, CancellationToken cancellationToken)
        {
            var resp = await _svc.CreatePlaylistAsync(model.playlist_name, model.track_ids, cancellationToken).ConfigureAwait(false);
            var json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
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
        /// <param name="task_id">The task ID.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="ContentResult"/> containing the task status.</returns>
        [HttpGet("status/{task_id}")]
        public async Task<IActionResult> GetTaskStatus(string task_id, CancellationToken cancellationToken)
        {
            var resp = await _svc.GetTaskStatusAsync(task_id, cancellationToken).ConfigureAwait(false);
            var json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
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
        /// <param name="task_id">The task ID.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="ContentResult"/> containing the response from the backend.</returns>
        [HttpPost("cancel/{task_id}")]
        public async Task<IActionResult> CancelTask(string task_id, CancellationToken cancellationToken)
        {
            var resp = await _svc.CancelTaskAsync(task_id, cancellationToken).ConfigureAwait(false);
            var json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
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
        /// <param name="task_type_prefix">The task type prefix.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="ContentResult"/> containing the response from the backend.</returns>
        [HttpPost("cancel_all/{task_type_prefix}")]
        public async Task<IActionResult> CancelAllTasksByType(string task_type_prefix, CancellationToken cancellationToken)
        {
            var resp = await _svc.CancelAllTasksByTypeAsync(task_type_prefix, cancellationToken).ConfigureAwait(false);
            var json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
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
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="ContentResult"/> containing the task status.</returns>
        [HttpGet("last_task")]
        public async Task<IActionResult> GetLastTask(CancellationToken cancellationToken)
        {
            var resp = await _svc.GetLastTaskAsync(cancellationToken).ConfigureAwait(false);
            var json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
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
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="ContentResult"/> containing the task status.</returns>
        [HttpGet("active_tasks")]
        public async Task<IActionResult> GetActiveTasks(CancellationToken cancellationToken)
        {
            var resp = await _svc.GetActiveTasksAsync(cancellationToken).ConfigureAwait(false);
            var json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new ContentResult
            {
                Content = json,
                ContentType = "application/json",
                StatusCode = (int)resp.StatusCode
            };
        }

        /*
        /// <summary>
        /// Gets the current server configuration.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="ContentResult"/> containing the configuration.</returns>
        [HttpGet("config")]
        public async Task<IActionResult> GetConfig(CancellationToken cancellationToken)
        {
            var resp = await _svc.GetConfigAsync(cancellationToken).ConfigureAwait(false);
            var json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new ContentResult
            {
                Content = json,
                ContentType = "application/json",
                StatusCode = (int)resp.StatusCode
            };
        }
        */

        /*
        /// <summary>
        /// Gets the default AI configuration for the chat interface.
        /// </summary>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="ContentResult"/> containing the configuration.</returns>
        [HttpGet("chat/config_defaults")]
        public async Task<IActionResult> GetChatConfigDefaults(CancellationToken cancellationToken)
        {
        var resp = await _svc.GetChatConfigDefaultsAsync(cancellationToken).ConfigureAwait(false);
            var json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new ContentResult
            {
                Content = json,
                ContentType = "application/json",
                StatusCode = (int)resp.StatusCode
            };
        }
        */

        /// <summary>
        /// Processes a user's chat input to generate a playlist.
        /// </summary>
        /// <param name="payload">The request payload.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="ContentResult"/> containing the response from the backend.</returns>
        [HttpPost("chat/playlist")]
        public async Task<IActionResult> PostChatPlaylist([FromBody] object payload, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(payload);
            var resp = await _svc.PostChatPlaylistAsync(json, cancellationToken).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new ContentResult
            {
                Content = body,
                ContentType = "application/json",
                StatusCode = (int)resp.StatusCode
            };
        }

        /// <summary>
        /// Creates a new playlist from the chat interface.
        /// </summary>
        /// <param name="payload">The request payload.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="ContentResult"/> containing the response from the backend.</returns>
        [HttpPost("chat/create_playlist")]
        public async Task<IActionResult> CreateChatPlaylist([FromBody] object payload, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(payload);
            var resp = await _svc.CreateChatPlaylistAsync(json, cancellationToken).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new ContentResult
            {
                Content = body,
                ContentType = "application/json",
                StatusCode = (int)resp.StatusCode
            };
        }

        /// <summary>
        /// Forwards an alchemy request payload to the backend AudioMuse service.
        /// This endpoint preserves parameter names and forwards the JSON body 1:1.
        /// </summary>
        /// <param name="payload">The raw request payload (kept as object to preserve keys).</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="ContentResult"/> containing the backend response.</returns>
        [HttpPost("alchemy")]
        public async Task<IActionResult> Alchemy([FromBody] object payload, CancellationToken cancellationToken)
        {
            var json = JsonSerializer.Serialize(payload);
            var resp = await _svc.AlchemyAsync(json, cancellationToken).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new ContentResult
            {
                Content = body,
                ContentType = "application/json",
                StatusCode = (int)resp.StatusCode
            };
        }

        /// <summary>
        /// Generates a sonic fingerprint for a user.
        /// </summary>
        /// <param name="jellyfin_user_identifier">The Jellyfin username or user ID.</param>
        /// <param name="jellyfin_token">The Jellyfin API token.</param>
        /// <param name="n">Optional number of results to return.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>A <see cref="ContentResult"/> containing the sonic fingerprint tracks.</returns>
        [HttpGet("sonic_fingerprint/generate")]
        public async Task<IActionResult> GenerateSonicFingerprint(
            [FromQuery] string jellyfin_user_identifier,
            [FromQuery] string jellyfin_token,
            [FromQuery] int? n,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(jellyfin_user_identifier) || string.IsNullOrWhiteSpace(jellyfin_token))
            {
                return BadRequest("jellyfin_user_identifier and jellyfin_token are required.");
            }

            var resp = await _svc.GenerateSonicFingerprintAsync(jellyfin_user_identifier, jellyfin_token, n, cancellationToken).ConfigureAwait(false);
            var json = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return new ContentResult
            {
                Content = json,
                ContentType = "application/json",
                StatusCode = (int)resp.StatusCode
            };
        }
    }
}
