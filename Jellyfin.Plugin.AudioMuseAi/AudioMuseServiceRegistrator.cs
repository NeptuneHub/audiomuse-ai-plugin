using Jellyfin.Plugin.AudioMuseAi.Controller;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
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
            // Register our convention to disable the default Instant Mix controller.
            serviceCollection.AddSingleton<IControllerModelConvention, AudioMuseControllerConvention>();

            // Register your controllers.
            serviceCollection.AddTransient<AudioMuseController>();
            serviceCollection.AddTransient<InstantMixController>();
        }
    }
}
