using System;
using System.Collections.Generic;
using Jellyfin.Plugin.AudioMuseAi.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.AudioMuseAi
{
    /// <summary>
    /// Main entry point for the AudioMuse AI plugin.
    /// </summary>
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Plugin"/> class.
        /// </summary>
        /// <param name="applicationPaths">Application paths for embedded resources.</param>
        /// <param name="xmlSerializer">XML serializer for configuration.</param>
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        /// <summary>
        /// Gets the singleton instance of the plugin.
        /// </summary>
        public static Plugin Instance { get; private set; }

        /// <inheritdoc />
        public override string Name => "AudioMuse AI";

        /// <inheritdoc />
        public override Guid Id => Guid.Parse("e3831be1-c025-4ebc-bc79-121ad0dfc4e1");

        /// <inheritdoc />
        public override string Description => "Provides integration with the AudioMuse AI backend service.";

        /// <inheritdoc />
        public IEnumerable<PluginPageInfo> GetPages()
        {
            yield return new PluginPageInfo
            {
                Name = "AudioMuse AI",
                // Path to the embedded HTML configuration page
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html"
            };
        }
    }
}
