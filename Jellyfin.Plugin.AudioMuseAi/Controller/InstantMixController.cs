using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
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
            _logger.LogInformation("AudioMuseAI is creating an Instant Mix for item {ItemId} with a target of {Limit} songs.", itemId, resultLimit);

            var finalItems = new List<BaseItem>();
            var finalItemIds = new HashSet<Guid>();

            // Step 1: Always add the seed item first.
            var originalItem = _libraryManager.GetItemById(itemId);
            if (originalItem is null)
            {
                _logger.LogError("Original item with ID {ItemId} not found.", itemId);
                return new QueryResult<BaseItemDto>();
            }

            BaseItem? seedSong = null;
            if (originalItem is MusicAlbum originalAlbum)
            {
                seedSong = _libraryManager.GetItemList(new InternalItemsQuery(user) { ParentId = originalAlbum.Id, IncludeItemTypes = new[] { BaseItemKind.Audio }, Limit = 1, OrderBy = new[] { (ItemSortBy.Random, SortOrder.Ascending) } }).FirstOrDefault();
            }
            else if (originalItem is MusicArtist originalArtist)
            {
                seedSong = _libraryManager.GetItemList(new InternalItemsQuery(user) { ArtistIds = new[] { originalArtist.Id }, IncludeItemTypes = new[] { BaseItemKind.Audio }, Limit = 1, OrderBy = new[] { (ItemSortBy.Random, SortOrder.Ascending) } }).FirstOrDefault();
            }
            else // It's a song or genre
            {
                seedSong = originalItem;
            }

            if (seedSong != null && seedSong is Audio)
            {
                finalItems.Add(seedSong);
                finalItemIds.Add(seedSong.Id);
                _logger.LogInformation("AudioMuseAI: Added seed song '{SongName}' to the mix.", seedSong.Name);
            }

            // Step 2: Try to get items from AudioMuse service.
            HttpResponseMessage? response = null;
            try
            {
                response = await _audioMuseService.GetSimilarTracksAsync(itemId.ToString("N"), null, null, resultLimit, null, HttpContext.RequestAborted).ConfigureAwait(false);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "AudioMuseAI backend call failed with an exception. Proceeding to fallback.");
            }

            if (response != null && response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync(HttpContext.RequestAborted).ConfigureAwait(false);
                using var jsonDoc = JsonDocument.Parse(json);
                if (jsonDoc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    var similarTrackIds = jsonDoc.RootElement.EnumerateArray()
                        .Select(track => track.TryGetProperty("item_id", out var idElement) ? idElement.GetString() : null)
                        .Where(id => !string.IsNullOrEmpty(id) && Guid.TryParse(id, out _))
                        .Select(id => Guid.Parse(id!))
                        .ToList();

                    var newItems = _libraryManager.GetItemList(new InternalItemsQuery(user) { ItemIds = similarTrackIds.ToArray() })
                        .Where(i => !finalItemIds.Contains(i.Id))
                        .ToList();
                    
                    // *** CHANGE: Re-sort the items to match the order from the AudioMuse response.
                    newItems = newItems.OrderBy(item => similarTrackIds.IndexOf(item.Id)).ToList();

                    _logger.LogInformation("AudioMuseAI: Got {Count} new songs from AudioMuse service.", newItems.Count);
                    if (newItems.Any())
                    {
                        finalItems.AddRange(newItems);
                        foreach (var item in newItems) { finalItemIds.Add(item.Id); }
                    }
                }
            }

            // Step 3: Fallback - SimilarTo logic.
            if (finalItems.Count < resultLimit)
            {
                var needed = resultLimit - finalItems.Count;
                var newItems = new List<BaseItem>();

                if (originalItem is MusicArtist artist)
                {
                    var similarArtistsQuery = new InternalItemsQuery(user) { IncludeItemTypes = new[] { BaseItemKind.MusicArtist }, Limit = 20, SimilarTo = artist };
                    var similarArtistIds = _libraryManager.GetItemList(similarArtistsQuery).Select(a => a.Id).ToArray();
                    if (similarArtistIds.Any())
                    {
                        var songsQuery = new InternalItemsQuery(user) { IncludeItemTypes = new[] { BaseItemKind.Audio }, Limit = needed, Recursive = true, ArtistIds = similarArtistIds };
                        newItems = _libraryManager.GetItemList(songsQuery).ToList();
                    }
                }
                else if (originalItem is MusicAlbum album)
                {
                    var similarAlbumsQuery = new InternalItemsQuery(user) { IncludeItemTypes = new[] { BaseItemKind.MusicAlbum }, Limit = 20, SimilarTo = album };
                    var similarAlbumIds = _libraryManager.GetItemList(similarAlbumsQuery).Select(a => a.Id).ToArray();
                    if (similarAlbumIds.Any())
                    {
                        var songsQuery = new InternalItemsQuery(user) { IncludeItemTypes = new[] { BaseItemKind.Audio }, Limit = needed, Recursive = true, AncestorIds = similarAlbumIds };
                        newItems = _libraryManager.GetItemList(songsQuery).ToList();
                    }
                }
                else
                {
                    var similarQuery = new InternalItemsQuery(user) { IncludeItemTypes = new[] { BaseItemKind.Audio }, Limit = needed, Recursive = true, IsVirtualItem = false, SimilarTo = originalItem };
                    newItems = _libraryManager.GetItemList(similarQuery).ToList();
                }

                var itemsToAdd = newItems.Where(i => !finalItemIds.Contains(i.Id)).ToList();
                _logger.LogInformation("AudioMuseAI Fallback: Got {Count} new songs from 'SimilarTo' stage.", itemsToAdd.Count);
                if (itemsToAdd.Any())
                {
                    finalItems.AddRange(itemsToAdd);
                    foreach (var item in itemsToAdd) { finalItemIds.Add(item.Id); }
                }
            }

            // Step 4: Fallback - Genre logic.
            if (finalItems.Count < resultLimit)
            {
                var needed = resultLimit - finalItems.Count;
                string[]? genreNames = null;

                if (originalItem is Audio audio) { genreNames = audio.Genres; }
                else if (originalItem is MusicAlbum album)
                {
                    var firstSongInAlbum = _libraryManager.GetItemList(new InternalItemsQuery(user) { ParentId = album.Id, IncludeItemTypes = new[] { BaseItemKind.Audio }, Limit = 1 }).FirstOrDefault() as Audio;
                    if (firstSongInAlbum != null) { genreNames = firstSongInAlbum.Genres; }
                }
                else if (originalItem is MusicArtist artist)
                {
                    var firstSongByArtist = _libraryManager.GetItemList(new InternalItemsQuery(user) { ArtistIds = new[] { artist.Id }, IncludeItemTypes = new[] { BaseItemKind.Audio }, Limit = 1 }).FirstOrDefault() as Audio;
                    if (firstSongByArtist != null) { genreNames = firstSongByArtist.Genres; }
                }
                else if (originalItem is MusicGenre genre) { genreNames = new[] { genre.Name }; }

                if (genreNames != null && genreNames.Any())
                {
                    var genreQuery = new InternalItemsQuery(user) { IncludeItemTypes = new[] { BaseItemKind.Audio }, Limit = needed, Recursive = true, IsVirtualItem = false, Genres = genreNames, OrderBy = new[] { (ItemSortBy.Random, SortOrder.Ascending) } };
                    var newItems = _libraryManager.GetItemList(genreQuery).Where(i => !finalItemIds.Contains(i.Id)).ToList();
                    _logger.LogInformation("AudioMuseAI Fallback: Got {Count} new songs from 'Genre' logic.", newItems.Count);
                    if (newItems.Any())
                    {
                        finalItems.AddRange(newItems);
                        foreach (var item in newItems) { finalItemIds.Add(item.Id); }
                    }
                }
                else
                {
                    _logger.LogInformation("AudioMuseAI Fallback: Got 0 new songs from 'Genre' logic (no genres found).");
                }
            }

            // Step 5: Final Fallback - Random from library.
            if (finalItems.Count < resultLimit)
            {
                var needed = resultLimit - finalItems.Count;
                var randomQuery = new InternalItemsQuery(user) { IncludeItemTypes = new[] { BaseItemKind.Audio }, Limit = needed, Recursive = true, IsVirtualItem = false, OrderBy = new[] { (ItemSortBy.Random, SortOrder.Ascending) } };
                var newItems = _libraryManager.GetItemList(randomQuery).Where(i => !finalItemIds.Contains(i.Id)).ToList();
                _logger.LogInformation("AudioMuseAI Fallback: Got {Count} new songs from 'Random' library logic.", newItems.Count);
                if (newItems.Any())
                {
                    finalItems.AddRange(newItems);
                }
            }

            // Final step: Create the DTOs and return the result.
            var dtoOptions = new DtoOptions() { EnableImages = enableImages ?? false, ImageTypeLimit = imageTypeLimit ?? 1, EnableUserData = enableUserData ?? false };
            var finalDtoList = finalItems.Take(resultLimit).Select(i => _dtoService.GetBaseItemDto(i, dtoOptions, user)).ToList();
            _logger.LogInformation("AudioMuseAI: Successfully created an Instant Mix with {Count} total items.", finalDtoList.Count);

            return new QueryResult<BaseItemDto>
            {
                Items = finalDtoList.ToArray(),
                TotalRecordCount = finalDtoList.Count
            };
        }
    }
}
