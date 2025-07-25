using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.AudioMuseAi.Services;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Net;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
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

            var similarTrackIds = new List<Guid>();
            var response = await _audioMuseService.GetSimilarTracksAsync(itemId.ToString("N"), null, null, resultLimit, null, HttpContext.RequestAborted).ConfigureAwait(false);
            
            // CORRECTED: Only try to parse the JSON if the API call was successful.
            // A 404 Not Found is not a success, so this block will be skipped.
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted).ConfigureAwait(false);
                using var jsonDoc = JsonDocument.Parse(json);
                if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    similarTrackIds = jsonDoc.RootElement.EnumerateArray()
                        .Select(track => track.TryGetProperty("item_id", out var idElement) ? idElement.GetString() : null)
                        .Where(id => !string.IsNullOrEmpty(id) && Guid.TryParse(id, out _))
                        .Select(id => Guid.Parse(id!))
                        .ToList();
                }
                else
                {
                     _logger.LogError("AudioMuseAI backend response was not a JSON array. Response: {Json}", json);
                }
            }
            else
            {
                _logger.LogInformation("AudioMuseAI backend returned a non-success status code ({StatusCode}). Proceeding to fallback.", response.StatusCode);
            }


            // This check now correctly handles three cases:
            // 1. API returns a 404 or other error (list is empty).
            // 2. API returns a success code with an empty JSON array `[]` (list is empty).
            // 3. API returns a success code with invalid JSON (list is empty).
            if (similarTrackIds.Count == 0)
            {
                _logger.LogWarning("AudioMuseAI: No similar tracks found. Falling back to a standard random mix.");

                var fallbackQuery = new InternalItemsQuery(user)
                {
                    IncludeItemTypes = new[] { BaseItemKind.Audio },
                    OrderBy = new[] { (ItemSortBy.Random, SortOrder.Ascending) },
                    Limit = resultLimit,
                    Recursive = true,
                    IsVirtualItem = false
                };

                var randomItems = _libraryManager.GetItemList(fallbackQuery);

                var fallbackDtoOptions = new DtoOptions()
                {
                    EnableImages = enableImages ?? false,
                    ImageTypeLimit = imageTypeLimit ?? 1,
                    EnableUserData = enableUserData ?? false
                };

                var fallbackDtoList = randomItems.Select(item => _dtoService.GetBaseItemDto(item, fallbackDtoOptions, user)).ToList();

                _logger.LogInformation("AudioMuseAI: Successfully created a fallback random Instant Mix with {Count} items.", fallbackDtoList.Count);

                return new QueryResult<BaseItemDto>
                {
                    Items = fallbackDtoList.ToArray(),
                    TotalRecordCount = fallbackDtoList.Count
                };
            }

            var query = new InternalItemsQuery(user)
            {
                ItemIds = similarTrackIds.ToArray()
            };

            var similarItems = _libraryManager.GetItemList(query).ToList();

            var sortedItems = similarItems.OrderBy(item => similarTrackIds.IndexOf(item.Id)).ToList();

            var dtoOptions = new DtoOptions()
            {
                EnableImages = enableImages ?? false,
                ImageTypeLimit = imageTypeLimit ?? 1,
                EnableUserData = enableUserData ?? false
            };

            var dtoList = sortedItems.Select(item => _dtoService.GetBaseItemDto(item, dtoOptions, user)).ToList();
            
            _logger.LogInformation("AudioMuseAI: Successfully created an Instant Mix with {Count} items (from AudioMuse).", dtoList.Count);

            return new QueryResult<BaseItemDto>
            {
                Items = dtoList.ToArray(),
                TotalRecordCount = dtoList.Count
            };
        }
    }
}
