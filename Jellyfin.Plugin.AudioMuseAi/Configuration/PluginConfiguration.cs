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

        /// <summary>
        /// Optional Bearer token sent in the Authorization header for every API call.
        /// Leave empty to skip authentication.
        /// </summary>
        public string ApiToken { get; set; } = string.Empty;

        /// <summary>
        /// Optional media server name, as configured in a multi-server AudioMuse AI backend.
        /// When set, it is passed as the 'server' query parameter on the API calls that support it.
        /// Leave empty to let the backend use its default server.
        /// </summary>
        public string ServerName { get; set; } = string.Empty;
    }
}
