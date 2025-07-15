using System;
using System.Collections.Generic;
using Jellyfin.Plugin.AudioMuseAi.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Controller.Chapters;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.AudioMuseAi
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        public static Plugin Instance { get; private set; }

        public override string Name => "AudioMuse AI";
        public override Guid Id => Guid.Parse("e3831be1-c025-4ebc-bc79-121ad0dfc4e1");
        public override string Description => "Integrates Jellyfin with an AudioMuse AI backend.";

        public IEnumerable<PluginPageInfo> GetPages()
        {
            // must exactly match the EmbeddedResource name in the DLL:
            // Jellyfin.Plugin.AudioMuseAi.Configuration.configPage.html
            yield return new PluginPageInfo
            {
                Name = "AudioMuse AI",
                EmbeddedResourcePath = 
                    $"{GetType().Namespace}.Configuration.configPage.html"

            };
        }
    }
}
