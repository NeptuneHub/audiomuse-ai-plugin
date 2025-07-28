using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Data.Entities;
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
    /// Controller that overrides the default Jellyfin Instant Mix functionality with advanced logic.
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
        /// The Order = -1 gives this endpoint priority over the default one.
        /// </summary>
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

            var originalItem = _libraryManager.GetItemById(itemId);
            if (originalItem is null)
            {
                _logger.LogError("Original item with ID {ItemId} not found.", itemId);
                return new QueryResult<BaseItemDto>();
            }

            var resultLimit = limit ?? 200;
            _logger.LogInformation("AudioMuseAI: Creating Instant Mix for item '{ItemName}' ({ItemId}) of type {ItemType} with a limit of {Limit}.", originalItem.Name, itemId, originalItem.GetType().Name, resultLimit);

            var finalItems = new List<BaseItem>();
            var finalItemIds = new HashSet<Guid>();

            // Dispatch to the correct handler based on the item type
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
            else if (originalItem is MusicGenre genre)
            {
                await HandleGenreMix(genre, user, resultLimit, finalItems, finalItemIds);
            }
            else
            {
                _logger.LogWarning("AudioMuseAI: Instant Mix requested for an unsupported item type: {ItemType}", originalItem.GetType().Name);
            }

            // Final fallback: if we are still under the limit, add random songs from the library.
            if (finalItems.Count < resultLimit)
            {
                _logger.LogInformation("AudioMuseAI: Mix not full. Adding random songs as a final fallback.");
                AddRandomTracks(finalItems, finalItemIds, resultLimit, user);
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

        #region Type-Specific Handlers

        private async Task HandleSongMix(Audio song, User user, int limit, List<BaseItem> finalItems, HashSet<Guid> finalItemIds)
        {
            _logger.LogInformation("AudioMuseAI: Handling SONG mix for '{SongName}'.", song.Name);
            finalItems.Add(song);
            finalItemIds.Add(song.Id);

            await AddSimilarTracksFromSeeds(new List<Audio> { song }, user, limit, finalItems, finalItemIds);
            if (finalItems.Count >= limit) return;

            AddSimilarToTracks(finalItems, finalItemIds, song, limit, user);
            if (finalItems.Count >= limit) return;

            AddGenreTracks(finalItems, finalItemIds, song.Genres, limit, user);
        }

        private async Task HandleAlbumMix(MusicAlbum album, User user, int limit, List<BaseItem> finalItems, HashSet<Guid> finalItemIds)
        {
            _logger.LogInformation("AudioMuseAI: Handling ALBUM mix for '{AlbumName}'.", album.Name);
            // CORRECTED: Randomize the seed songs from the album.
            var seedSongs = _libraryManager.GetItemList(new InternalItemsQuery(user) { ParentId = album.Id, IncludeItemTypes = new[] { BaseItemKind.Audio } }).Cast<Audio>().OrderBy(x => Guid.NewGuid()).ToList();
            if (!seedSongs.Any()) return;

            // The list is already shuffled, so taking the first is effectively random.
            var randomSong = seedSongs.First();
            finalItems.Add(randomSong);
            finalItemIds.Add(randomSong.Id);
            _logger.LogInformation("AudioMuseAI: Added seed song '{SongName}' from album '{AlbumName}'.", randomSong.Name, album.Name);

            await AddSimilarTracksFromSeeds(seedSongs, user, limit, finalItems, finalItemIds);
            if (finalItems.Count >= limit) return;

            AddSimilarToTracks(finalItems, finalItemIds, album, limit, user);
            if (finalItems.Count >= limit) return;

            var genreNames = seedSongs.SelectMany(s => s.Genres).Distinct().ToArray();
            AddGenreTracks(finalItems, finalItemIds, genreNames, limit, user);
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
            if (finalItems.Count >= limit) return;

            foreach (var song in seedSongs)
            {
                AddSimilarToTracks(finalItems, finalItemIds, song, limit, user);
                if (finalItems.Count >= limit) return;
            }

            var genreNames = allPlaylistSongs.SelectMany(s => s.Genres).Distinct().ToArray();
            AddGenreTracks(finalItems, finalItemIds, genreNames, limit, user);
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

            // CORRECTED: Add a random song from the artist, not always the first.
            var randomInitialSong = allArtistSongs[new Random().Next(allArtistSongs.Count)];
            finalItems.Add(randomInitialSong);
            finalItemIds.Add(randomInitialSong.Id);
            _logger.LogInformation("AudioMuseAI: Added seed song '{SongName}' from artist '{ArtistName}'.", randomInitialSong.Name, artist.Name);

            const int maxSeedSongs = 20;
            var seedSongs = allArtistSongs.OrderBy(x => Guid.NewGuid()).Take(maxSeedSongs).ToList();
            _logger.LogInformation("AudioMuseAI: Selected {Count} songs from the artist to find similar tracks.", seedSongs.Count);

            await AddSimilarTracksFromSeeds(seedSongs, user, limit, finalItems, finalItemIds);
            if (finalItems.Count >= limit) return;

            AddSimilarToTracks(finalItems, finalItemIds, artist, limit, user);
            if (finalItems.Count >= limit) return;

            var genreNames = allArtistSongs.SelectMany(s => s.Genres).Distinct().ToArray();
            AddGenreTracks(finalItems, finalItemIds, genreNames, limit, user);
        }

        private Task HandleGenreMix(MusicGenre genre, User user, int limit, List<BaseItem> finalItems, HashSet<Guid> finalItemIds)
        {
            _logger.LogInformation("AudioMuseAI: Handling GENRE mix for '{GenreName}', filling with random songs from the genre.", genre.Name);
            AddGenreTracks(finalItems, finalItemIds, new[] { genre.Name }, limit, user);
            return Task.CompletedTask;
        }

        #endregion

        #region Helper Methods

        private async Task AddSimilarTracksFromSeeds(List<Audio> seedSongs, User user, int limit, List<BaseItem> finalItems, HashSet<Guid> finalItemIds)
        {
            if (finalItems.Count >= limit || !seedSongs.Any())
            {
                return;
            }

            var remainingNeeded = limit - finalItems.Count;
            var songsToFetchPerSeed = (int)Math.Ceiling((decimal)remainingNeeded / seedSongs.Count);

            // Over-fetch for multi-seed requests (Album, Playlist, Artist) to compensate for duplicates.
            if (seedSongs.Count > 1)
            {
                songsToFetchPerSeed *= 2;
            }

            if (songsToFetchPerSeed <= 0)
            {
                return;
            }

            _logger.LogInformation("AudioMuseAI: Requesting a fixed number of {SongsToFetchPerSeed} similar tracks for each of the {SeedSongsCount} seed songs.", songsToFetchPerSeed, seedSongs.Count);

            foreach (var song in seedSongs)
            {
                if (finalItems.Count >= limit)
                {
                    break;
                }

                _logger.LogInformation("AudioMuseAI Step: Calling AudioMuse service for item {SeedItemId}, requesting {SongsToFetchPerSeed} tracks.", song.Id, songsToFetchPerSeed);

                HttpResponseMessage? response = null;
                try
                {
                    response = await _audioMuseService.GetSimilarTracksAsync(song.Id.ToString("N"), null, null, songsToFetchPerSeed, null, HttpContext.RequestAborted).ConfigureAwait(false);
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, "AudioMuseAI backend call failed for seed {SeedItemId}. Aborting similarity search and proceeding to fallback.", song.Id);
                    return; // Exit the entire method on the first failure.
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

                        var itemsToAdd = newItems.OrderBy(item => similarTrackIds.IndexOf(item.Id)).ToList();

                        _logger.LogInformation("AudioMuseAI Step: Got {Count} new songs from AudioMuse service for seed {SeedItemId}.", itemsToAdd.Count, song.Id);
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
        }

        private void AddSimilarToTracks(List<BaseItem> finalItems, HashSet<Guid> finalItemIds, BaseItem seedItem, int limit, User user)
        {
            if (finalItems.Count >= limit) return;

            var needed = limit - finalItems.Count;
            _logger.LogInformation("AudioMuseAI Fallback: Getting 'SimilarTo' tracks for item '{ItemName}', needing {Needed} tracks.", seedItem.Name, needed);
            
            var newItems = new List<BaseItem>();
            var query = new InternalItemsQuery(user)
            {
                IncludeItemTypes = new[] { BaseItemKind.Audio },
                Limit = needed,
                Recursive = true,
                IsVirtualItem = false,
                SimilarTo = seedItem
            };

            if (seedItem is MusicArtist artist)
            {
                var similarArtistsQuery = new InternalItemsQuery(user) { IncludeItemTypes = new[] { BaseItemKind.MusicArtist }, Limit = 20, SimilarTo = artist };
                var similarArtistIds = _libraryManager.GetItemList(similarArtistsQuery).Select(a => a.Id).ToArray();
                if (similarArtistIds.Any())
                {
                    var songsQuery = new InternalItemsQuery(user) { IncludeItemTypes = new[] { BaseItemKind.Audio }, Limit = needed, Recursive = true, ArtistIds = similarArtistIds, OrderBy = new[] { (ItemSortBy.Random, SortOrder.Ascending) } };
                    newItems = _libraryManager.GetItemList(songsQuery).ToList();
                }
            }
            else if (seedItem is MusicAlbum album)
            {
                var similarAlbumsQuery = new InternalItemsQuery(user) { IncludeItemTypes = new[] { BaseItemKind.MusicAlbum }, Limit = 20, SimilarTo = album };
                var similarAlbumIds = _libraryManager.GetItemList(similarAlbumsQuery).Select(a => a.Id).ToArray();
                if (similarAlbumIds.Any())
                {
                    var songsQuery = new InternalItemsQuery(user) { IncludeItemTypes = new[] { BaseItemKind.Audio }, Limit = needed, Recursive = true, AncestorIds = similarAlbumIds, OrderBy = new[] { (ItemSortBy.Random, SortOrder.Ascending) } };
                    newItems = _libraryManager.GetItemList(songsQuery).ToList();
                }
            }
            else
            {
                newItems = _libraryManager.GetItemList(query).ToList();
            }

            var itemsToAdd = newItems.Where(i => !finalItemIds.Contains(i.Id)).ToList();
            _logger.LogInformation("AudioMuseAI Fallback: Got {Count} new songs from 'SimilarTo' stage.", itemsToAdd.Count);
            foreach (var item in itemsToAdd)
            {
                if (finalItems.Count < limit && finalItemIds.Add(item.Id))
                {
                    finalItems.Add(item);
                }
            }
        }

        private void AddGenreTracks(List<BaseItem> finalItems, HashSet<Guid> finalItemIds, string[] genreNames, int limit, User user)
        {
            if (finalItems.Count >= limit || genreNames == null || !genreNames.Any()) return;

            var needed = limit - finalItems.Count;
            _logger.LogInformation("AudioMuseAI Fallback: Getting 'Genre' tracks for genres '{Genres}', needing {Needed} tracks.", string.Join(", ", genreNames), needed);

            var genreQuery = new InternalItemsQuery(user)
            {
                IncludeItemTypes = new[] { BaseItemKind.Audio },
                Limit = needed,
                Recursive = true,
                IsVirtualItem = false,
                Genres = genreNames,
                OrderBy = new[] { (ItemSortBy.Random, SortOrder.Ascending) }
            };
            var newItems = _libraryManager.GetItemList(genreQuery).Where(i => !finalItemIds.Contains(i.Id)).ToList();
            _logger.LogInformation("AudioMuseAI Fallback: Got {Count} new songs from 'Genre' stage.", newItems.Count);
            foreach (var item in newItems)
            {
                if (finalItems.Count < limit && finalItemIds.Add(item.Id))
                {
                    finalItems.Add(item);
                }
            }
        }

        private void AddRandomTracks(List<BaseItem> finalItems, HashSet<Guid> finalItemIds, int limit, User user)
        {
            if (finalItems.Count >= limit) return;

            var needed = limit - finalItems.Count;
            _logger.LogInformation("AudioMuseAI Fallback: Getting 'Random' tracks from library, needing {Needed} tracks.", needed);

            var randomQuery = new InternalItemsQuery(user)
            {
                IncludeItemTypes = new[] { BaseItemKind.Audio },
                Limit = needed,
                Recursive = true,
                IsVirtualItem = false,
                OrderBy = new[] { (ItemSortBy.Random, SortOrder.Ascending) }
            };
            var newItems = _libraryManager.GetItemList(randomQuery).Where(i => !finalItemIds.Contains(i.Id)).ToList();
            _logger.LogInformation("AudioMuseAI Fallback: Got {Count} new songs from 'Random' library stage.", newItems.Count);
            foreach (var item in newItems)
            {
                if (finalItems.Count < limit && finalItemIds.Add(item.Id))
                {
                    finalItems.Add(item);
                }
            }
        }

        #endregion
    }
}
