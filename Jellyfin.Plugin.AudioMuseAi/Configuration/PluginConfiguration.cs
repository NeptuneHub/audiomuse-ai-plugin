using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.AudioMuseAi.Configuration
{
    /// <summary>
    /// Plugin configuration class persisted by Jellyfin.
    /// </summary>
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// The base URL of the AudioMuse AI backend (include http:// or https://).
        /// </summary>
        public string BackendUrl { get; set; } = "http://192.168.3.14:8000";
    }
}
