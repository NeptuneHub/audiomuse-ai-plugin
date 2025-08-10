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
    /// Implements the Jellyfin scheduled task for running the AudioMuse AI analysis.
    /// </summary>
    public class AnalysisScheduledTask : IScheduledTask
    {
        private readonly ILogger<AnalysisScheduledTask> _logger;
        private readonly IAudioMuseService _audioMuseService;

        /// <summary>
        /// Initializes a new instance of the <see cref="AnalysisScheduledTask"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public AnalysisScheduledTask(ILogger<AnalysisScheduledTask> logger)
        {
            _logger = logger;
            _audioMuseService = new AudioMuseService();
        }

        /// <inheritdoc />
        public string Name => "AudioMuse AI Library Analysis";

        /// <inheritdoc />
        public string Key => "AudioMuseAnalysis";

        /// <inheritdoc />
        public string Description => "Periodically analyzes the music library to generate and update AudioMuse AI data.";

        /// <inheritdoc />
        public string Category => "Library";

        /// <inheritdoc />
        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers()
        {
            // This will run the task daily at 2 AM.
            return new[]
            {
                new TaskTriggerInfo
                {
                    // *** FIX: Use the static property for the trigger type for API compatibility. ***
                    Type = TaskTriggerInfo.TriggerDaily,
                    TimeOfDayTicks = TimeSpan.FromHours(2).Ticks
                }
            };
        }

        /// <inheritdoc />
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
        {
            _logger.LogInformation("Starting AudioMuse AI analysis scheduled task.");

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // For a scheduled task, we typically want a full analysis.
                // We'll send an empty payload, assuming the backend interprets this
                // as a request for a full, standard analysis.
                var payload = new { };
                var jsonPayload = JsonSerializer.Serialize(payload);

                _logger.LogDebug("Calling backend to start analysis with payload: {Payload}", jsonPayload);

                // Use the service to start the analysis
                var response = await _audioMuseService.StartAnalysisAsync(jsonPayload, cancellationToken).ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogInformation("Successfully initiated AudioMuse AI analysis task. Backend response: {Response}", responseBody);
                }
                else
                {
                    var errorBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    _logger.LogError("Failed to start AudioMuse AI analysis task. Status Code: {StatusCode}. Response: {Response}", response.StatusCode, errorBody);
                }

                progress.Report(100.0);
                _logger.LogInformation("AudioMuse AI analysis scheduled task finished.");
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("AudioMuse AI analysis scheduled task was cancelled.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while executing the AudioMuse AI analysis scheduled task.");
                // Rethrowing the exception will mark the task as failed in Jellyfin's dashboard.
                throw;
            }
        }
    }
}
