using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.AudioMuseAi.Services;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AudioMuseAi.Controller
{
    /// <summary>
    /// Controller that overrides the default Jellyfin Instant Mix functionality with advanced logic.
    /// </summary>
    [ApiController]
    public class InstantMixController : ControllerBase
    {
        private readonly ILogger<InstantMixController> _logger;
        private readonly ILibraryManager _libraryManager;
        private readonly IUserManager _userManager;
        private readonly IDtoService _dtoService;
        private readonly IMusicManager _musicManager;
        private readonly IAudioMuseService _audioMuseService = new AudioMuseService();

        /// <summary>
        /// Initializes a new instance of the <see cref="InstantMixController"/> class.
        /// </summary>
        public InstantMixController(
            ILogger<InstantMixController> logger,
            ILibraryManager libraryManager,
            IUserManager userManager,
            IDtoService dtoService,
            IMusicManager musicManager)
        {
            _logger = logger;
            _libraryManager = libraryManager;
            _userManager = userManager;
            _dtoService = dtoService;
            _musicManager = musicManager;
        }

        /// <summary>
        /// Gets a sonic-similarity-based instant mix.
        /// The Order = -1 gives this endpoint priority over the default one.
        /// </summary>
        [HttpGet("Items/{itemId}/InstantMix", Order = -1)]
        [ProducesResponseType(typeof(QueryResult<BaseItemDto>), 200)]
        public async Task<ActionResult<QueryResult<BaseItemDto>>> GetInstantMix(
            [FromRoute] Guid itemId,
            [FromQuery] Guid? userId,
            [FromQuery] int? limit,
            [FromQuery] ItemFields[] fields,
            [FromQuery] bool? enableImages,
            [FromQuery] int? imageTypeLimit,
            [FromQuery] ImageType[] enableImageTypes,
            [FromQuery] bool? enableUserData)
        {
            var user = userId.HasValue ? _userManager.GetUserById(userId.Value) : null;
            var originalItem = _libraryManager.GetItemById(itemId);
            if (originalItem is null)
            {
                _logger.LogError("Original item with ID {ItemId} not found.", itemId);
                return new QueryResult<BaseItemDto>();
            }

            var resultLimit = limit ?? 200;
            var dtoOptions = new DtoOptions
            {
                Fields = fields,
                EnableImages = enableImages ?? false,
                EnableUserData = enableUserData ?? false,
                ImageTypeLimit = imageTypeLimit ?? 1,
                ImageTypes = enableImageTypes
            };

            _logger.LogInformation("AudioMuseAI: Creating Instant Mix for item '{ItemName}' ({ItemId}).", originalItem.Name, itemId);

            var finalItems = new List<BaseItem>();
            var finalItemIds = new HashSet<Guid>();

            // Restore the original logic to handle different item types
            if (originalItem is Audio song)
            {
                await HandleSongMix(song, user, resultLimit, finalItems, finalItemIds);
            }
            else if (originalItem is MusicAlbum album)
            {
                await HandleAlbumMix(album, user, resultLimit, finalItems, finalItemIds);
            }
            else if (originalItem.GetType().Name == "Playlist")
            {
                await HandlePlaylistMix(originalItem, user, resultLimit, finalItems, finalItemIds);
            }
            else if (originalItem is MusicArtist artist)
            {
                await HandleArtistMix(artist, user, resultLimit, finalItems, finalItemIds);
            }
            else
            {
                _logger.LogWarning("AudioMuseAI: Instant Mix requested for an unsupported item type: {ItemType}", originalItem.GetType().Name);
            }

            if (finalItems.Any())
            {
                 _logger.LogInformation("AudioMuseAI: Successfully generated a partial mix of {Count} items from AudioMuse backend.", finalItems.Count);
            }

            // If after all AudioMuse logic, the list is still not full, fall back to native Jellyfin mix.
            if (finalItems.Count < resultLimit)
            {
                var needed = resultLimit - finalItems.Count;
                _logger.LogInformation("AudioMuseAI: Mix is not full. Falling back to native Jellyfin Instant Mix to get {Needed} more items.", needed);

                var fallbackItems = _musicManager.GetInstantMixFromItem(originalItem, user, dtoOptions);
                var existingItemIds = new HashSet<Guid>(finalItems.Select(i => i.Id));
                var itemsToAppend = fallbackItems.Where(i => !existingItemIds.Contains(i.Id)).Take(needed);
                finalItems.AddRange(itemsToAppend);
            }

            var finalDtoList = _dtoService.GetBaseItemDtos(finalItems.Take(resultLimit).ToList(), dtoOptions, user);
            _logger.LogInformation("AudioMuseAI: Sending Instant Mix with {Count} total items.", finalDtoList.Count);

            return new QueryResult<BaseItemDto>
            {
                Items = finalDtoList.ToArray(),
                TotalRecordCount = finalDtoList.Count
            };
        }

        #region Type-Specific Handlers (Restored Logic)

        private async Task HandleSongMix(Audio song, User user, int limit, List<BaseItem> finalItems, HashSet<Guid> finalItemIds)
        {
            _logger.LogInformation("AudioMuseAI: Handling SONG mix for '{SongName}'.", song.Name);
            finalItems.Add(song);
            finalItemIds.Add(song.Id);

            await AddSimilarTracksFromSeeds(new List<Audio> { song }, user, limit, finalItems, finalItemIds);
        }

        private async Task HandleAlbumMix(MusicAlbum album, User user, int limit, List<BaseItem> finalItems, HashSet<Guid> finalItemIds)
        {
            _logger.LogInformation("AudioMuseAI: Handling ALBUM mix for '{AlbumName}'.", album.Name);
            var seedSongs = _libraryManager.GetItemList(new InternalItemsQuery(user) { ParentId = album.Id, IncludeItemTypes = new[] { BaseItemKind.Audio } }).Cast<Audio>().OrderBy(x => Guid.NewGuid()).ToList();
            if (!seedSongs.Any()) return;

            var randomSong = seedSongs.First();
            finalItems.Add(randomSong);
            finalItemIds.Add(randomSong.Id);
            _logger.LogInformation("AudioMuseAI: Added seed song '{SongName}' from album '{AlbumName}'.", randomSong.Name, album.Name);

            await AddSimilarTracksFromSeeds(seedSongs, user, limit, finalItems, finalItemIds);
        }

        private async Task HandlePlaylistMix(BaseItem playlist, User user, int limit, List<BaseItem> finalItems, HashSet<Guid> finalItemIds)
        {
            _logger.LogInformation("AudioMuseAI: Handling PLAYLIST mix for '{PlaylistName}'.", playlist.Name);
            var allPlaylistSongs = ((Folder)playlist).GetChildren(user, true).OfType<Audio>().ToList();
            if (!allPlaylistSongs.Any())
            {
                _logger.LogWarning("AudioMuseAI: Playlist '{PlaylistName}' contains no playable songs.", playlist.Name);
                return;
            }

            var randomSeedSong = allPlaylistSongs[new Random().Next(allPlaylistSongs.Count)];
            finalItems.Add(randomSeedSong);
            finalItemIds.Add(randomSeedSong.Id);
            _logger.LogInformation("AudioMuseAI: Added seed song '{SongName}' from playlist '{PlaylistName}'.", randomSeedSong.Name, playlist.Name);

            const int maxSeedSongs = 20;
            var seedSongs = allPlaylistSongs.OrderBy(x => Guid.NewGuid()).Take(maxSeedSongs).ToList();
            _logger.LogInformation("AudioMuseAI: Selected {Count} songs from the playlist to find similar tracks.", seedSongs.Count);

            await AddSimilarTracksFromSeeds(seedSongs, user, limit, finalItems, finalItemIds);
        }

        private async Task HandleArtistMix(MusicArtist artist, User user, int limit, List<BaseItem> finalItems, HashSet<Guid> finalItemIds)
        {
            _logger.LogInformation("AudioMuseAI: Handling ARTIST mix for '{ArtistName}'.", artist.Name);
            var allArtistSongs = _libraryManager.GetItemList(new InternalItemsQuery(user) { ArtistIds = new[] { artist.Id }, IncludeItemTypes = new[] { BaseItemKind.Audio } }).Cast<Audio>().ToList();
            if (!allArtistSongs.Any())
            {
                _logger.LogWarning("AudioMuseAI: Artist '{ArtistName}' has no playable songs.", artist.Name);
                return;
            }

            var randomInitialSong = allArtistSongs[new Random().Next(allArtistSongs.Count)];
            finalItems.Add(randomInitialSong);
            finalItemIds.Add(randomInitialSong.Id);
            _logger.LogInformation("AudioMuseAI: Added seed song '{SongName}' from artist '{ArtistName}'.", randomInitialSong.Name, artist.Name);

            const int maxSeedSongs = 20;
            var seedSongs = allArtistSongs.OrderBy(x => Guid.NewGuid()).Take(maxSeedSongs).ToList();
            _logger.LogInformation("AudioMuseAI: Selected {Count} songs from the artist to find similar tracks.", seedSongs.Count);

            await AddSimilarTracksFromSeeds(seedSongs, user, limit, finalItems, finalItemIds);
        }

        #endregion

        #region AudioMuse Backend Call

        private async Task AddSimilarTracksFromSeeds(List<Audio> seedSongs, User user, int limit, List<BaseItem> finalItems, HashSet<Guid> finalItemIds)
        {
            if (finalItems.Count >= limit || !seedSongs.Any())
            {
                return;
            }

            var remainingNeeded = limit - finalItems.Count;
            var songsToFetchPerSeed = (int)Math.Ceiling((decimal)remainingNeeded / seedSongs.Count);
            if (seedSongs.Count > 1) songsToFetchPerSeed *= 2;
            if (songsToFetchPerSeed <= 0) return;

            _logger.LogInformation("AudioMuseAI: Requesting up to {SongsToFetchPerSeed} similar tracks for each of the {SeedSongsCount} seed songs.", songsToFetchPerSeed, seedSongs.Count);

            foreach (var song in seedSongs)
            {
                if (finalItems.Count >= limit) break;

                try
                {
                    var response = await _audioMuseService.GetSimilarTracksAsync(song.Id.ToString("N"), null, null, songsToFetchPerSeed, null, HttpContext.RequestAborted).ConfigureAwait(false);
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

                            var itemsToAdd = newItems.OrderBy(item => similarTrackIds.IndexOf(item.Id)).ToList();
                            _logger.LogInformation("AudioMuseAI: Got {Count} new songs from AudioMuse service for seed {SeedItemId}.", itemsToAdd.Count, song.Id);
                            foreach (var item in itemsToAdd)
                            {
                                if (finalItems.Count < limit && finalItemIds.Add(item.Id))
                                {
                                    finalItems.Add(item);
                                }
                            }
                        }
                    }
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, "AudioMuseAI backend call failed for seed {SeedItemId}. Aborting all AudioMuse similarity searches.", song.Id);
                    // CORRECTED: Exit the method immediately on failure.
                    return;
                }
            }
        }
        #endregion
    }
}
