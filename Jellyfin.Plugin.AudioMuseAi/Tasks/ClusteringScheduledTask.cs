using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.AudioMuseAi.Services;
using MediaBrowser.Model.Tasks;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AudioMuseAi.Tasks
{
    /// <summary>
    /// Implements the Jellyfin scheduled task for running the AudioMuse AI clustering.
    /// </summary>
    public class ClusteringScheduledTask : IScheduledTask
    {
        private readonly ILogger<ClusteringScheduledTask> _logger;
        private readonly IAudioMuseService _audioMuseService;

        /// <summary>
        /// Initializes a new instance of the <see cref="ClusteringScheduledTask"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public ClusteringScheduledTask(ILogger<ClusteringScheduledTask> logger)
        {
            _logger = logger;
            _audioMuseService = new AudioMuseService();
        }

        /// <inheritdoc />
        public string Name => "AudioMuse AI Playlist Clustering";

        /// <inheritdoc />
        public string Key => "AudioMuseClustering";

        /// <inheritdoc />
        public string Description => "Periodically runs clustering on the library to generate and update playlists.";

        /// <inheritdoc />
        public string Category => "Library";

        /// <inheritdoc />
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            // This will run the task weekly on Sunday at 2 AM.
            return new[]
            {
                new TaskTriggerInfo
                {
                    Type = TaskTriggerInfo.TriggerWeekly,
                    TimeOfDayTicks = TimeSpan.FromHours(2).Ticks,
                    DayOfWeek = DayOfWeek.Sunday
                }
            };
        }

        /// <inheritdoc />
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting AudioMuse AI clustering scheduled task.");

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // For a scheduled task, we typically want a full clustering run.
                // We'll send an empty payload, assuming the backend interprets this
                // as a request for a standard clustering run.
                var payload = new { };
                var jsonPayload = JsonSerializer.Serialize(payload);

                _logger.LogDebug("Calling backend to start clustering with payload: {Payload}", jsonPayload);

                // Use the service to start the clustering
                var response = await _audioMuseService.StartClusteringAsync(jsonPayload, cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("Successfully initiated AudioMuse AI clustering task. Backend response: {Response}", responseBody);
                }
                else
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogError("Failed to start AudioMuse AI clustering task. Status Code: {StatusCode}. Response: {Response}", response.StatusCode, errorBody);
                }

                progress.Report(100.0);
                _logger.LogInformation("AudioMuse AI clustering scheduled task finished.");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("AudioMuse AI clustering scheduled task was cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while executing the AudioMuse AI clustering scheduled task.");
                // Rethrowing the exception will mark the task as failed in Jellyfin's dashboard.
                throw;
            }
        }
    }
}
