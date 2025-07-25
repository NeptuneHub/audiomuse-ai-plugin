using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AudioMuseAi.Controller
{
    /// <summary>
    /// An <see cref="IControllerModelConvention"/> that disables the default InstantMixController
    /// to prevent ambiguous match exceptions.
    /// </summary>
    public class AudioMuseControllerConvention : IControllerModelConvention
    {
        private readonly ILogger<AudioMuseControllerConvention> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="AudioMuseControllerConvention"/> class.
        /// </summary>
        /// <param name="logger">The logger.</param>
        public AudioMuseControllerConvention(ILogger<AudioMuseControllerConvention> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public void Apply(ControllerModel controller)
        {
            // We identify the controller to disable by its name and namespace as strings,
            // since we can't reference the Jellyfin.Api.Controllers type directly from a plugin.
            if (controller.ControllerType.Name == "InstantMixController"
                && (controller.ControllerType.Namespace?.Contains("Jellyfin.Api.Controllers", StringComparison.Ordinal) ?? false))
            {
                // Find the action method that handles the Instant Mix request on the default controller.
                var originalAction = controller.Actions.FirstOrDefault(a => a.ActionName == "GetInstantMixFromItem");
                if (originalAction != null)
                {
                    // CORRECTED: Instead of hiding the action, we remove it entirely from the controller model.
                    // This prevents it from being added to the routing table.
                    controller.Actions.Remove(originalAction);
                    _logger.LogInformation("AudioMuseAI Plugin: Successfully removed the default Jellyfin InstantMix endpoint to allow override.");
                }
            }
        }
    }
}
