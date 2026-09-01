using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.AudioMuseAi.Configuration;
using Jellyfin.Plugin.AudioMuseAi.Services;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Audio;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Configuration;

namespace Jellyfin.Plugin.AudioMuseAi.Providers
{
    /// <summary>
    /// Feeds the Jellyfin "More Like This" row from the AudioMuse AI backend.
    /// Registered automatically by Jellyfin through <see cref="ILocalSimilarItemsProvider"/> discovery,
    /// and surfaced in Dashboard, Libraries, Advanced, "Similar item providers".
    /// Every song-seeded lookup goes through <see cref="SimilarTrackSearch"/>, so it follows the
    /// engine selected in <see cref="Configuration.PluginConfiguration.SimilarityProvider"/> and
    /// returns identically shaped results whichever engine is active.
    /// </summary>
    public sealed class AudioMuseSimilarItemsProvider :
        ILocalSimilarItemsProvider<Audio>,
        ILocalSimilarItemsProvider<MusicAlbum>,
        ILocalSimilarItemsProvider<MusicArtist>,
        ILocalSimilarItemsProvider<Playlist>
    {
        private readonly ILibraryManager _libraryManager;
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioMuseSimilarItemsProvider"/> class.
        /// </summary>
        /// <param name="libraryManager">The library manager.</param>
        /// <param name="httpClientFactory">The HTTP client factory.</param>
        public AudioMuseSimilarItemsProvider(ILibraryManager libraryManager, IHttpClientFactory httpClientFactory)
        {
            _libraryManager = libraryManager;
            _httpClientFactory = httpClientFactory;
        }

        /// <inheritdoc />
        public string Name => "AudioMuse AI";

        /// <inheritdoc />
        public MetadataPluginType Type => MetadataPluginType.LocalSimilarityProvider;

        /// <summary>
        /// Gets songs sonically similar to the given song.
        /// </summary>
        /// <param name="item">The source song.</param>
        /// <param name="query">The query options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The similar songs, in backend rank order.</returns>
        public async Task<IReadOnlyList<BaseItem>> GetSimilarItemsAsync(Audio item, SimilarItemsQuery query, CancellationToken cancellationToken)
        {
            var ids = await SimilarTrackIdsAsync(item, Limit(query), cancellationToken).ConfigureAwait(false);
            return Resolve(ids, BaseItemKind.Audio, query);
        }

        /// <summary>
        /// Gets artists sonically similar to the given artist.
        /// </summary>
        /// <param name="item">The source artist.</param>
        /// <param name="query">The query options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The similar artists, in backend rank order.</returns>
        public async Task<IReadOnlyList<BaseItem>> GetSimilarItemsAsync(MusicArtist item, SimilarItemsQuery query, CancellationToken cancellationToken)
        {
            using var service = new AudioMuseService(_httpClientFactory);
            using var response = await service.GetSimilarArtistsAsync(null, Format(item.Id), Limit(query), null, null, cancellationToken).ConfigureAwait(false);
            var ids = await SimilarTrackSearch.ReadIdsAsync(response, "artist_id", cancellationToken).ConfigureAwait(false);
            return Resolve(ids, BaseItemKind.MusicArtist, query);
        }

        /// <summary>
        /// Gets albums similar to the given album, taken from the albums that own the songs
        /// the backend returns for one seed track of the source album.
        /// </summary>
        /// <param name="item">The source album.</param>
        /// <param name="query">The query options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The similar albums, in backend rank order.</returns>
        public async Task<IReadOnlyList<BaseItem>> GetSimilarItemsAsync(MusicAlbum item, SimilarItemsQuery query, CancellationToken cancellationToken)
        {
            var seed = SeedTrack(item, query.User);
            if (seed is null)
            {
                return Array.Empty<BaseItem>();
            }

            // Overfetch tracks because many of them collapse onto the same album.
            var trackIds = await SimilarTrackIdsAsync(seed, Limit(query) * 4, cancellationToken).ConfigureAwait(false);
            if (trackIds.Count == 0)
            {
                return Array.Empty<BaseItem>();
            }

            var tracks = _libraryManager.GetItemList(new InternalItemsQuery(query.User)
            {
                ItemIds = trackIds.ToArray(),
                IncludeItemTypes = new[] { BaseItemKind.Audio },
                DtoOptions = query.DtoOptions ?? new DtoOptions()
            });

            var albumIds = Order(tracks, trackIds)
                .OfType<Audio>()
                .Select(track => track.AlbumEntity)
                .Where(album => album is not null)
                .Select(album => album!.Id)
                .Distinct()
                .ToList();

            return Resolve(albumIds, BaseItemKind.MusicAlbum, query);
        }

        /// <summary>
        /// Gets songs sonically similar to one seed track of the given playlist.
        /// </summary>
        /// <param name="item">The source playlist.</param>
        /// <param name="query">The query options.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The similar songs, in backend rank order.</returns>
        public async Task<IReadOnlyList<BaseItem>> GetSimilarItemsAsync(Playlist item, SimilarItemsQuery query, CancellationToken cancellationToken)
        {
            var seed = SeedTrack(item, query.User);
            if (seed is null)
            {
                return Array.Empty<BaseItem>();
            }

            var ids = await SimilarTrackIdsAsync(seed, Limit(query), cancellationToken).ConfigureAwait(false);
            return Resolve(ids, BaseItemKind.Audio, query);
        }

        bool ILocalSimilarItemsProvider.Supports(Type itemType)
            => typeof(Audio).IsAssignableFrom(itemType)
            || typeof(MusicAlbum).IsAssignableFrom(itemType)
            || typeof(MusicArtist).IsAssignableFrom(itemType)
            || typeof(Playlist).IsAssignableFrom(itemType);

        Task<IReadOnlyList<BaseItem>> ILocalSimilarItemsProvider.GetSimilarItemsAsync(BaseItem item, SimilarItemsQuery query, CancellationToken cancellationToken)
            => item switch
            {
                Audio song => GetSimilarItemsAsync(song, query, cancellationToken),
                MusicAlbum album => GetSimilarItemsAsync(album, query, cancellationToken),
                MusicArtist artist => GetSimilarItemsAsync(artist, query, cancellationToken),
                Playlist playlist => GetSimilarItemsAsync(playlist, query, cancellationToken),
                _ => Task.FromResult<IReadOnlyList<BaseItem>>(Array.Empty<BaseItem>())
            };

        private static int Limit(SimilarItemsQuery query) => Math.Max(1, query.Limit ?? 50);

        private static string Format(Guid id) => id.ToString("N", CultureInfo.InvariantCulture);

        private static Audio? SeedTrack(Folder folder, User? user)
            => user is null ? null : folder.GetChildren(user, true).OfType<Audio>().FirstOrDefault();

        private static IReadOnlyList<BaseItem> Order(IReadOnlyList<BaseItem> items, List<Guid> ids)
            => items.OrderBy(item => ids.IndexOf(item.Id)).ToList();

        /// <summary>
        /// Gets the tracks the configured engine ranks closest to the seed song.
        /// The engine only changes where the songs come from on the AudioMuse AI side;
        /// the IDs come back in backend rank order either way.
        /// </summary>
        /// <param name="seed">The seed song.</param>
        /// <param name="limit">The number of tracks to request.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The item IDs in backend rank order.</returns>
        private async Task<List<Guid>> SimilarTrackIdsAsync(Audio seed, int limit, CancellationToken cancellationToken)
        {
            var engine = Plugin.Instance?.Configuration?.SimilarityProvider ?? SimilarityEngine.SimilarSong;
            using var service = new AudioMuseService(_httpClientFactory);
            return await SimilarTrackSearch
                .GetSimilarTrackIdsAsync(service, engine, seed.Id, limit, cancellationToken)
                .ConfigureAwait(false);
        }

        private IReadOnlyList<BaseItem> Resolve(List<Guid> ids, BaseItemKind kind, SimilarItemsQuery query)
        {
            if (ids.Count == 0)
            {
                return Array.Empty<BaseItem>();
            }

            var items = _libraryManager.GetItemList(new InternalItemsQuery(query.User)
            {
                ItemIds = ids.ToArray(),
                IncludeItemTypes = new[] { kind },
                ExcludeItemIds = query.ExcludeItemIds.ToArray(),
                ExcludeArtistIds = query.ExcludeArtistIds.ToArray(),
                DtoOptions = query.DtoOptions ?? new DtoOptions()
            });

            return Order(items, ids);
        }
    }
}
