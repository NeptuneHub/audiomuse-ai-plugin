using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.AudioMuseAi.Services;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AudioMuseAi.Controller
{
    /// <summary>
    /// Controller that overrides the default Jellyfin Instant Mix functionality.
    /// </summary>
    [ApiController]
    public class InstantMixController : ControllerBase
    {
        private readonly ILogger<InstantMixController> _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly IUserManager _userManager;
        private readonly IDtoService _dtoService;
        private readonly IAudioMuseService _audioMuseService = new AudioMuseService();

        /// <summary>
        /// Initializes a new instance of the <see cref="InstantMixController"/> class.
        /// </summary>
        public InstantMixController(
            ILogger<InstantMixController> logger,
            ILibraryManager libraryManager,
            IUserManager userManager,
            IDtoService dtoService)
        {
            _logger = logger;
            _libraryManager = libraryManager;
            _userManager = userManager;
            _dtoService = dtoService;
        }

        /// <summary>
        /// Gets a sonic-similarity-based instant mix.
        /// The Order = -1 gives this endpoint priority over the default one, resolving the ambiguity.
        /// </summary>
        /// <param name="itemId">The id of the item to get an instant mix from.</param>
        /// <param name="userId">The user id.</param>
        /// <param name="limit">The item limit.</param>
        /// <param name="fields">Optional. Specify additional fields of information to return in the output.</param>
        /// <param name="enableImages">Optional. Include image information in output.</param>
        /// <param name="imageTypeLimit">Optional. The max number of images to return, per image type.</param>
        /// <param name="enableImageTypes">Optional. The image types to include in the output.</param>
        /// <param name="enableUserData">Optional. Include user data.</param>
        /// <returns>A query result containing the instant mix.</returns>
        [HttpGet("Items/{itemId}/InstantMix", Order = -1)]
        [ProducesResponseType(typeof(QueryResult<BaseItemDto>), 200)]
        public async Task<ActionResult<QueryResult<BaseItemDto>>> GetInstantMix(
            [FromRoute] Guid itemId,
            [FromQuery] Guid? userId,
            [FromQuery] int? limit,
            [FromQuery] string? fields,
            [FromQuery] bool? enableImages,
            [FromQuery] int? imageTypeLimit,
            [FromQuery] string? enableImageTypes,
            [FromQuery] bool? enableUserData)
        {
            var user = userId.HasValue ? _userManager.GetUserById(userId.Value) : null;
            if (user == null)
            {
                return Unauthorized("Invalid UserId.");
            }

            var resultLimit = limit ?? 200;
            _logger.LogInformation("AudioMuseAI is creating an Instant Mix for item {ItemId}", itemId);

            var response = await _audioMuseService.GetSimilarTracksAsync(itemId.ToString("N"), null, null, resultLimit, "true", HttpContext.RequestAborted).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return StatusCode((int)response.StatusCode, "Failed to get similar tracks from backend.");
            }

            var json = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted).ConfigureAwait(false);
            
            using var jsonDoc = JsonDocument.Parse(json);
            if (jsonDoc.RootElement.ValueKind != JsonValueKind.Array)
            {
                _logger.LogError("AudioMuseAI backend response was not a JSON array. Response: {Json}", json);
                return StatusCode(500, "Invalid response format from backend. Expected a JSON array.");
            }

            var similarTrackIds = jsonDoc.RootElement.EnumerateArray()
                .Select(track => track.TryGetProperty("item_id", out var idElement) ? idElement.GetString() : null)
                .Where(id => !string.IsNullOrEmpty(id) && Guid.TryParse(id, out _))
                .Select(id => Guid.Parse(id!))
                .ToList();

            if (similarTrackIds.Count == 0)
            {
                _logger.LogWarning("AudioMuseAI: Parsed 0 valid track IDs from the backend response.");
                return new QueryResult<BaseItemDto>();
            }

            var query = new InternalItemsQuery(user)
            {
                // CORRECTED: The property name is "ItemIds", not "Ids".
                ItemIds = similarTrackIds.ToArray()
            };

            var similarItems = _libraryManager.GetItemList(query);

            var dtoOptions = new DtoOptions()
            {
                EnableImages = enableImages ?? false,
                ImageTypeLimit = imageTypeLimit ?? 1,
                EnableUserData = enableUserData ?? false
            };

            var dtoList = similarItems.Select(item => _dtoService.GetBaseItemDto(item, dtoOptions, user)).ToList();

            return new QueryResult<BaseItemDto>
            {
                Items = dtoList.ToArray(),
                TotalRecordCount = dtoList.Count
            };
        }
    }
}
