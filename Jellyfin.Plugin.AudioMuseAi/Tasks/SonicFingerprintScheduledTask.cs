using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.AudioMuseAi.Services;
using MediaBrowser.Controller.Library;
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

        /// <summary>
        /// Initializes a new instance of the <see cref="SonicFingerprintScheduledTask"/> class.
        /// </summary>
        public SonicFingerprintScheduledTask(
            ILogger<SonicFingerprintScheduledTask> logger,
            IUserManager userManager)
        {
            _logger = logger;
            _userManager = userManager;
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
                    Type = TaskTriggerInfo.TriggerWeekly,
                    DayOfWeek = DayOfWeek.Sunday,
                    TimeOfDayTicks = TimeSpan.FromHours(1).Ticks
                }
            };
        }

        /// <inheritdoc />
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
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

                    // Step 1: Get 200 sonic fingerprint tracks from the AudioMuse service.
                    var fingerprintResponse = await _audioMuseService.GenerateSonicFingerprintAsync(user.Username, null, 200, cancellationToken).ConfigureAwait(false);

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

                    var trackIds = tracks.Where(t => !string.IsNullOrEmpty(t.item_id)).Select(t => t.item_id).ToList();

                    if (trackIds.Count == 0)
                    {
                        _logger.LogInformation("No valid track IDs found in sonic fingerprint for {Username}.", user.Username);
                        continue;
                    }

                    // Step 2: Call the correct service method to create the playlist.
                    // The AudioMuse service is responsible for removing the old playlist if it exists.
                    var playlistName = $"{user.Username}-fingerprint";
                    _logger.LogInformation("Requesting AudioMuse service to create playlist '{PlaylistName}' for user {Username}.", playlistName, user.Username);

                    var createPlaylistResponse = await _audioMuseService.CreatePlaylistAsync(playlistName, trackIds, cancellationToken).ConfigureAwait(false);

                    if (createPlaylistResponse.IsSuccessStatusCode)
                    {
                        _logger.LogInformation("Successfully requested playlist creation for {Username}.", user.Username);
                    }
                    else
                    {
                        var createErrorBody = await createPlaylistResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                        _logger.LogError("Failed to request playlist creation for {Username}. Status: {StatusCode}, Response: {Response}", user.Username, createPlaylistResponse.StatusCode, createErrorBody);
                    }
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
    }
}
