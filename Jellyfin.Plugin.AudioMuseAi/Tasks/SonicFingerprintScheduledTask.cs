using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Data.Enums;
using Jellyfin.Plugin.AudioMuseAi.Services;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Playlists;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Playlists;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AudioMuseAi.Tasks
{
    /// <summary>
    /// A helper class to deserialize the response from the sonic fingerprint endpoint.
    /// </summary>
    public class SonicFingerprintTrack
    {
        [JsonPropertyName("item_id")]
        public string item_id { get; set; } = string.Empty;

        [JsonPropertyName("title")]
        public string title { get; set; } = string.Empty;

        [JsonPropertyName("author")]
        public string author { get; set; } = string.Empty;

        [JsonPropertyName("distance")]
        public double distance { get; set; }
    }

    /// <summary>
    /// Implements the Jellyfin scheduled task for generating sonic fingerprint playlists.
    /// </summary>
    public class SonicFingerprintScheduledTask : IScheduledTask
    {
        private readonly ILogger<SonicFingerprintScheduledTask> _logger;
        private readonly IAudioMuseService _audioMuseService;
        private readonly IUserManager _userManager;
        private readonly IPlaylistManager _playlistManager;
        private readonly ILibraryManager _libraryManager;
        private readonly Random _random = new Random();

        // This is the only addition to the original logic, to prevent the race condition.
        private static readonly SemaphoreSlim _taskLock = new SemaphoreSlim(1, 1);
        
        // Per-user locks to prevent concurrent playlist updates for the same user
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, SemaphoreSlim> _userLocks = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="SonicFingerprintScheduledTask"/> class.
        /// </summary>
        public SonicFingerprintScheduledTask(
            ILogger<SonicFingerprintScheduledTask> logger,
            IUserManager userManager,
            IPlaylistManager playlistManager,
            ILibraryManager libraryManager)
        {
            _logger = logger;
            _userManager = userManager;
            _playlistManager = playlistManager;
            _libraryManager = libraryManager;
            _audioMuseService = new AudioMuseService();
        }

        /// <inheritdoc />
        public string Name => "AudioMuse AI Sonic Fingerprint";

        /// <inheritdoc />
        public string Key => "AudioMuseSonicFingerprint";

        /// <inheritdoc />
        public string Description => "Generates a sonic fingerprint playlist for each user by delegating to the AudioMuse service.";

        /// <inheritdoc />
        public string Category => "Library";

        /// <inheritdoc />
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfoType.WeeklyTrigger,
                    DayOfWeek = DayOfWeek.Sunday,
                    TimeOfDayTicks = TimeSpan.FromHours(1).Ticks
                }
            };
        }

        /// <inheritdoc />
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            // Lock to ensure only one instance of the task can run at a time.
            if (!await _taskLock.WaitAsync(0, cancellationToken))
            {
                _logger.LogInformation("Sonic Fingerprint task is already running. Skipping this execution.");
                return;
            }

            try
            {
                _logger.LogInformation("Starting AudioMuse AI Sonic Fingerprint scheduled task.");

                var users = _userManager.Users.ToList();
                if (!users.Any())
                {
                    _logger.LogInformation("No users found to process.");
                    progress.Report(100.0);
                    return;
                }

                var progressIncrement = 100.0 / users.Count;
                var currentProgress = 0.0;

                foreach (var user in users)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        _logger.LogWarning("Sonic Fingerprint task was cancelled.");
                        break;
                    }

                    try
                    {
                        _logger.LogInformation("Processing user: {Username}", user.Username);

                        var fingerprintResponse = await _audioMuseService.GenerateSonicFingerprintAsync(user.Username, null, null, cancellationToken).ConfigureAwait(false);

                        if (!fingerprintResponse.IsSuccessStatusCode)
                        {
                            var errorBody = await fingerprintResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                            _logger.LogError("Failed to generate sonic fingerprint for {Username}. Status: {StatusCode}, Response: {Response}", user.Username, fingerprintResponse.StatusCode, errorBody);
                            continue;
                        }

                        var responseBody = await fingerprintResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                        var tracks = JsonSerializer.Deserialize<List<SonicFingerprintTrack>>(responseBody, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                        if (tracks == null || !tracks.Any())
                        {
                            _logger.LogInformation("Sonic fingerprint for {Username} returned no tracks.", user.Username);
                            continue;
                        }

                        var trackIds = tracks.Where(t => !string.IsNullOrEmpty(t.item_id))
                                             .Select(t => Guid.Parse(t.item_id!))
                                             .OrderBy(id => _random.Next())
                                             .ToArray();

                        if (trackIds.Length == 0)
                        {
                            _logger.LogInformation("No valid track IDs found in sonic fingerprint for {Username}.", user.Username);
                            continue;
                        }

                        var playlistName = $"{user.Username}-fingerprint";

                        // Step 2: Find existing playlist or create new one
                        var existingPlaylists = _playlistManager.GetPlaylists(user.Id);
                        var existingPlaylist = existingPlaylists.FirstOrDefault(p => p.Name.Equals(playlistName, StringComparison.OrdinalIgnoreCase));

                        if (existingPlaylist != null)
                        {
                            // Acquire per-user lock to prevent concurrent updates to the same playlist
                            var userLock = _userLocks.GetOrAdd(user.Id, _ => new SemaphoreSlim(1, 1));
                            
                            if (!await userLock.WaitAsync(0, cancellationToken))
                            {
                                _logger.LogWarning("Playlist update already in progress for user {Username}. Skipping.", user.Username);
                                continue;
                            }

                            try
                            {
                                _logger.LogInformation("Updating playlist '{PlaylistName}' for user {Username}", playlistName, user.Username);
                                
                                // Check for cancellation before starting
                                cancellationToken.ThrowIfCancellationRequested();
                                
                                // Remove all items with verification
                                for (int attempt = 0; attempt < 3; attempt++)
                                {
                                    var currentItems = existingPlaylist.GetManageableItems();
                                    
                                    if (!currentItems.Any())
                                    {
                                        _logger.LogInformation("Playlist '{PlaylistName}' is empty", playlistName);
                                        break;
                                    }
                                    
                                    // Use LinkedChild.ItemId as per Jellyfin's RemoveItemFromPlaylistAsync implementation
                                    var entryIds = currentItems
                                        .Select(item => item.Item1.ItemId?.ToString("N", System.Globalization.CultureInfo.InvariantCulture))
                                        .Where(id => id != null)
                                        .ToList();
                                    
                                    _logger.LogInformation("Removing {Count} items from playlist '{PlaylistName}' (attempt {Attempt})", entryIds.Count, playlistName, attempt + 1);
                                    
                                    await _playlistManager.RemoveItemFromPlaylistAsync(existingPlaylist.Id.ToString(), entryIds).ConfigureAwait(false);
                                    
                                    // Verify removal
                                    existingPlaylist = _playlistManager.GetPlaylists(user.Id).FirstOrDefault(p => p.Id == existingPlaylist.Id);
                                    if (existingPlaylist == null)
                                    {
                                        _logger.LogError("Playlist '{PlaylistName}' disappeared during update", playlistName);
                                        throw new InvalidOperationException($"Playlist {playlistName} no longer exists");
                                    }
                                    
                                    var remaining = existingPlaylist.GetManageableItems();
                                    var remainingCount = remaining.Count();
                                    
                                    if (remainingCount == 0)
                                    {
                                        _logger.LogInformation("All items removed from playlist '{PlaylistName}'", playlistName);
                                        break;
                                    }
                                    
                                    if (attempt == 2)
                                    {
                                        _logger.LogError("Failed to clear playlist after 3 attempts, {Count} items remain in {PlaylistName}", remainingCount, playlistName);
                                        throw new InvalidOperationException($"Cannot clear playlist {playlistName}");
                                    }
                                    
                                    _logger.LogWarning("Playlist {PlaylistName} still has {Count} items, retrying", playlistName, remainingCount);
                                    await Task.Delay(200, cancellationToken).ConfigureAwait(false);
                                }
                                
                                // Add new items
                                cancellationToken.ThrowIfCancellationRequested();
                                _logger.LogInformation("Adding {Count} items to playlist '{PlaylistName}'", trackIds.Length, playlistName);
                                await _playlistManager.AddItemToPlaylistAsync(existingPlaylist.Id, trackIds, user.Id).ConfigureAwait(false);
                                
                                _logger.LogInformation("Successfully updated playlist '{PlaylistName}' with {Count} tracks", playlistName, trackIds.Length);
                            }
                            catch (OperationCanceledException)
                            {
                                _logger.LogWarning("Playlist update cancelled for user {Username}", user.Username);
                                throw;
                            }
                            finally
                            {
                                userLock.Release();
                            }
                        }
                        else
                        {
                            // Create new playlist if it doesn't exist
                            _logger.LogInformation("Creating new playlist '{PlaylistName}' for user {Username} with {TrackCount} tracks", playlistName, user.Username, trackIds.Length);

                            var request = new PlaylistCreationRequest
                            {
                                Name = playlistName,
                                UserId = user.Id,
                                ItemIdList = trackIds,
                                MediaType = MediaType.Audio
                            };

                            await _playlistManager.CreatePlaylist(request).ConfigureAwait(false);
                        }

                        _logger.LogInformation("Completed processing for user {Username}", user.Username);
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("Sonic Fingerprint task was cancelled during processing of user {Username}.", user.Username);
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "An error occurred while processing sonic fingerprint for user {Username}.", user.Username);
                    }
                    finally
                    {
                        currentProgress += progressIncrement;
                        progress.Report(currentProgress);
                    }
                }

                progress.Report(100.0);
                _logger.LogInformation("AudioMuse AI Sonic Fingerprint scheduled task finished.");
            }
            finally
            {
                // Release the lock for the next run.
                _taskLock.Release();
            }
        }
    }
}
