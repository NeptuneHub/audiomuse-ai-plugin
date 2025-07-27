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
            _logger.LogInformation("AudioMuseAI is creating an Instant Mix for item {ItemId}", itemId);

            var similarTrackIds = new List<Guid>();
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
                    similarTrackIds = jsonDoc.RootElement.EnumerateArray()
                        .Select(track => track.TryGetProperty("item_id", out var idElement) ? idElement.GetString() : null)
                        .Where(id => !string.IsNullOrEmpty(id) && Guid.TryParse(id, out _))
                        .Select(id => Guid.Parse(id!))
                        .ToList();

                    if (similarTrackIds.Any())
                    {
                        similarTrackIds.Insert(0, itemId);
                        similarTrackIds = similarTrackIds.Distinct().ToList();
                    }
                }
                else
                {
                     _logger.LogError("AudioMuseAI backend response was not a JSON array. Response: {Json}", json);
                }
            }
            else if (response != null)
            {
                _logger.LogInformation("AudioMuseAI backend returned a non-success status code ({StatusCode}). Proceeding to fallback.", response.StatusCode);
            }

            // Fallback 1: If AudioMuse returns no tracks or call failed, use Jellyfin's logic.
            if (similarTrackIds.Count == 0)
            {
                _logger.LogWarning("AudioMuseAI: No similar tracks found. Falling back to Jellyfin's logic.");

                var item = _libraryManager.GetItemById(itemId);
                if (item is null)
                {
                    _logger.LogError("Original item with ID {ItemId} not found for fallback.", itemId);
                    return new QueryResult<BaseItemDto>();
                }

                var fallbackItems = new List<BaseItem>();

                if (item is MusicAlbum album)
                {
                    _logger.LogInformation("Fallback is for a MusicAlbum. Querying for similar albums, then their songs.");
                    var similarAlbumsQuery = new InternalItemsQuery(user)
                    {
                        SimilarTo = album,
                        IncludeItemTypes = new[] { BaseItemKind.MusicAlbum },
                        Limit = 20 // Sensible limit to avoid overly long mixes.
                    };
                    var similarAlbums = _libraryManager.GetItemList(similarAlbumsQuery);

                    if (similarAlbums.Any())
                    {
                        _logger.LogInformation("Found {Count} similar albums. Getting songs from them.", similarAlbums.Count());
                        var similarAlbumIds = similarAlbums.Select(a => a.Id).ToArray();
                        var songsQuery = new InternalItemsQuery(user)
                        {
                            IncludeItemTypes = new[] { BaseItemKind.Audio },
                            AncestorIds = similarAlbumIds,
                            Recursive = true,
                            IsVirtualItem = false,
                            OrderBy = new[] { (ItemSortBy.Random, SortOrder.Ascending) }
                        };
                        fallbackItems.AddRange(_libraryManager.GetItemList(songsQuery));
                    }
                }
                else // This handles Songs, Artists, and Genres
                {
                    var fallbackQuery = new InternalItemsQuery(user)
                    {
                        IncludeItemTypes = new[] { BaseItemKind.Audio },
                        Limit = resultLimit,
                        Recursive = true,
                        IsVirtualItem = false
                    };

                    if (item is MusicGenre musicGenre)
                    {
                        _logger.LogInformation("Fallback is for a MusicGenre. Querying by GenreId and sorting randomly.");
                        fallbackQuery.GenreIds = new[] { musicGenre.Id };
                        fallbackQuery.OrderBy = new[] { (ItemSortBy.Random, SortOrder.Ascending) };
                    }
                    else // This will be for Audio and MusicArtist
                    {
                        _logger.LogInformation("Fallback is for a standard item ({ItemType}). Querying by SimilarTo.", item.GetType().Name);
                        fallbackQuery.SimilarTo = item;
                    }
                    fallbackItems.AddRange(_libraryManager.GetItemList(fallbackQuery));
                }

                // Fallback 2: If primary fallback fails, try a genre mix.
                if (!fallbackItems.Any() && item is not MusicGenre)
                {
                    _logger.LogWarning("AudioMuseAI: 'SimilarTo' fallback failed. Falling back to a genre mix.");
                    string[]? genreNames = null;
                    BaseItem? songToGetGenreFrom = null;

                    if (item is Audio song) { songToGetGenreFrom = song; }
                    else if (item is MusicAlbum albumForGenre) { songToGetGenreFrom = _libraryManager.GetItemList(new InternalItemsQuery(user) { ParentId = albumForGenre.Id, IncludeItemTypes = new[] { BaseItemKind.Audio } }).FirstOrDefault(); }
                    else if (item is MusicArtist artistForGenre) { songToGetGenreFrom = _libraryManager.GetItemList(new InternalItemsQuery(user) { ArtistIds = new[] { artistForGenre.Id }, IncludeItemTypes = new[] { BaseItemKind.Audio } }).FirstOrDefault(); }

                    if (songToGetGenreFrom != null) { genreNames = songToGetGenreFrom.Genres; }

                    if (genreNames != null && genreNames.Any())
                    {
                        _logger.LogInformation("AudioMuseAI: Performing genre-based fallback for genres: {Genres}", string.Join(", ", genreNames));
                        var genreFallbackQuery = new InternalItemsQuery(user)
                        {
                            IncludeItemTypes = new[] { BaseItemKind.Audio },
                            Limit = resultLimit,
                            Recursive = true,
                            IsVirtualItem = false,
                            Genres = genreNames,
                            OrderBy = new[] { (ItemSortBy.Random, SortOrder.Ascending) }
                        };
                        fallbackItems = _libraryManager.GetItemList(genreFallbackQuery).ToList();
                    }
                }

                // Fallback 3: If genre mix fails, try a direct mix of the artist's songs.
                if (!fallbackItems.Any() && item is MusicArtist directMixArtist)
                {
                    _logger.LogWarning("AudioMuseAI: Genre fallback failed. Falling back to a direct mix of the artist's songs.");
                    var directQuery = new InternalItemsQuery(user)
                    {
                        IncludeItemTypes = new[] { BaseItemKind.Audio },
                        Limit = resultLimit,
                        Recursive = true,
                        IsVirtualItem = false,
                        OrderBy = new[] { (ItemSortBy.Random, SortOrder.Ascending) },
                        ArtistIds = new[] { directMixArtist.Id }
                    };
                    fallbackItems = _libraryManager.GetItemList(directQuery).ToList();
                }

                // Fallback 4: If all else fails, get random songs from the entire library.
                if (!fallbackItems.Any())
                {
                    _logger.LogWarning("AudioMuseAI: All other fallbacks failed. Getting random songs from the entire library.");
                    var randomQuery = new InternalItemsQuery(user)
                    {
                        IncludeItemTypes = new[] { BaseItemKind.Audio },
                        Limit = resultLimit,
                        Recursive = true,
                        IsVirtualItem = false,
                        OrderBy = new[] { (ItemSortBy.Random, SortOrder.Ascending) }
                    };
                    fallbackItems = _libraryManager.GetItemList(randomQuery).ToList();
                }

                // *** CHANGE: Prepend one song from the original item to the start of the mix.
                BaseItem? songToPrepend = null;
                if (item is MusicAlbum originalAlbum)
                {
                    _logger.LogInformation("Prepending one random song from the original album '{AlbumName}'.", originalAlbum.Name);
                    songToPrepend = _libraryManager.GetItemList(new InternalItemsQuery(user)
                    {
                        ParentId = originalAlbum.Id,
                        IncludeItemTypes = new[] { BaseItemKind.Audio },
                        Limit = 1,
                        OrderBy = new[] { (ItemSortBy.Random, SortOrder.Ascending) }
                    }).FirstOrDefault();
                }
                else if (item is MusicArtist originalArtist)
                {
                    _logger.LogInformation("Prepending one random song from the original artist '{ArtistName}'.", originalArtist.Name);
                    songToPrepend = _libraryManager.GetItemList(new InternalItemsQuery(user)
                    {
                        ArtistIds = new[] { originalArtist.Id },
                        IncludeItemTypes = new[] { BaseItemKind.Audio },
                        Limit = 1,
                        OrderBy = new[] { (ItemSortBy.Random, SortOrder.Ascending) }
                    }).FirstOrDefault();
                }
                else // It's a song
                {
                    songToPrepend = item;
                }

                if (songToPrepend != null)
                {
                    fallbackItems.RemoveAll(i => i.Id == songToPrepend.Id);
                    fallbackItems.Insert(0, songToPrepend);
                }

                var finalItems = fallbackItems.Take(resultLimit).ToList();

                var fallbackDtoOptions = new DtoOptions()
                {
                    EnableImages = enableImages ?? false,
                    ImageTypeLimit = imageTypeLimit ?? 1,
                    EnableUserData = enableUserData ?? false
                };

                var fallbackDtoList = finalItems.Select(i => _dtoService.GetBaseItemDto(i, fallbackDtoOptions, user)).ToList();

                _logger.LogInformation("AudioMuseAI: Successfully created a fallback Instant Mix with {Count} items.", fallbackDtoList.Count);

                return new QueryResult<BaseItemDto>
                {
                    Items = fallbackDtoList.ToArray(),
                    TotalRecordCount = fallbackDtoList.Count
                };
            }

            // This is the primary path (AudioMuse success)
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
