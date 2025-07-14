using Jellyfin.Plugin.AudioMuseAi.Controller;
using Jellyfin.Plugin.AudioMuseAi.Services;
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
            // Register the controller to be created when an API request comes in.
            serviceCollection.AddTransient<AudioMuseController>();

            // Register our service as a Singleton. The DI container will create one
            // instance and reuse it. The configuration happens inside the service's constructor.
            serviceCollection.AddSingleton<IAudioMuseService, AudioMuseService>();
        }
    }
}
