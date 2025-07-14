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
            // By creating the service here, we bypass the dependency injection issue
            // and ensure the service is created only when the controller is requested,
            // by which time the plugin's configuration is guaranteed to be loaded.
            _svc = new AudioMuseService();
        }

        // Health check
        [HttpGet("health")]
        public async Task<IActionResult> Health()
        {
            var resp = await _svc.HealthCheckAsync();
            if (resp.IsSuccessStatusCode)
            {
                return Ok();
            }
            
            return StatusCode((int)resp.StatusCode);
        }

        // Proxy to GET /api/playlists
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

        // Start analysis
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

        // Start clustering
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
    }
}
