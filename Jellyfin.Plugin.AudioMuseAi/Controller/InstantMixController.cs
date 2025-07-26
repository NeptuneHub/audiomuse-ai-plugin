using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.AudioMuseAi.Services;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
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

            // If the AudioMuse service returns no tracks, fallback to the default Jellyfin Instant Mix logic.
            if (similarTrackIds.Count == 0)
            {
                _logger.LogWarning("AudioMuseAI: No similar tracks found. Falling back to default Jellyfin Instant Mix logic.");

                // Get the original item to find similar items based on it.
                var item = _libraryManager.GetItemById(itemId);
                if (item is null)
                {
                    // If the original item is not found, we can't find similar items.
                    _logger.LogError("Original item with ID {ItemId} not found for fallback.", itemId);
                    return new QueryResult<BaseItemDto>();
                }

                var fallbackQuery = new InternalItemsQuery(user)
                {
                    IncludeItemTypes = new[] { BaseItemKind.Audio },
                    Limit = resultLimit,
                    Recursive = true,
                    IsVirtualItem = false
                };

                // *** MINIMAL CHANGE: Handle MusicGenre as a special case. ***
                if (item is MusicGenre musicGenre)
                {
                    _logger.LogInformation("Fallback is for a MusicGenre. Querying by GenreId and sorting randomly.");
                    fallbackQuery.GenreIds = new[] { musicGenre.Id };
                    fallbackQuery.OrderBy = new[] { (ItemSortBy.Random, SortOrder.Ascending) };
                }
                else
                {
                    // For all other types (Album, Artist, Song fallback), use the standard SimilarTo logic.
                    _logger.LogInformation("Fallback is for a standard item ({ItemType}). Querying by SimilarTo.", item.GetType().Name);
                    fallbackQuery.SimilarTo = item;
                }

                var fallbackItems = _libraryManager.GetItemList(fallbackQuery);

                var fallbackDtoOptions = new DtoOptions()
                {
                    EnableImages = enableImages ?? false,
                    ImageTypeLimit = imageTypeLimit ?? 1,
                    EnableUserData = enableUserData ?? false
                };

                var fallbackDtoList = fallbackItems.Select(i => _dtoService.GetBaseItemDto(i, fallbackDtoOptions, user)).ToList();

                _logger.LogInformation("AudioMuseAI: Successfully created a fallback Instant Mix with {Count} items using default Jellyfin logic.", fallbackDtoList.Count);

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
