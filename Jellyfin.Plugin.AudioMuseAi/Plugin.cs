using System;
using System.Collections.Generic;
using System.IO;
using Jellyfin.Plugin.AudioMuseAi.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Drawing;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

namespace Jellyfin.Plugin.AudioMuseAi
{
    /// <summary>
    /// The main plugin entry point.
    /// </summary>
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages, IHasThumbImage
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Plugin"/> class.
        /// </summary>
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        /// <summary>
        /// Gets the plugin instance.
        /// </summary>
        public static Plugin Instance { get; private set; }

        /// <inheritdoc />
        public override string Name => "AudioMuse AI";

        /// <inheritdoc />
        public override Guid Id => Guid.Parse("e3831be1-c025-4ebc-bc79-121ad0dfc4e1");

        /// <inheritdoc />
        public override string Description => "Integrates Jellyfin with an AudioMuse AI backend.";

        /// <inheritdoc />
        public IEnumerable<PluginPageInfo> GetPages()
        {
            yield return new PluginPageInfo
            {
                Name = "AudioMuse AI",
                EmbeddedResourcePath =
                    $"{GetType().Namespace}.Configuration.configPage.html"
            };
        }

        /// <inheritdoc />
        public Stream GetThumbImage()
        {
            var type = GetType();
            // The resource name is determined by the project's namespace and the <Link> value in the .csproj file.
            return type.Assembly.GetManifestResourceStream($"{type.Namespace}.audiomuseai.png");
        }

        /// <inheritdoc />
        public ImageFormat ThumbImageFormat => ImageFormat.Png;
    }
}
