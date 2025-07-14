using Jellyfin.Plugin.AudioMuseAi.Controller;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.AudioMuseAi
{
    /// <summary>
    /// Registers the plugin's services with the DI container.
    /// </summary>
    public class AudioMuseServiceRegistrator : IPluginServiceRegistrator
    {
        /// <inheritdoc />
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            // We only need to register the controller.
            // The service will be created manually inside the controller.
            serviceCollection.AddTransient<AudioMuseController>();
        }
    }
}
