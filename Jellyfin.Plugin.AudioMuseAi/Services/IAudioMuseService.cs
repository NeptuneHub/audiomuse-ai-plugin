using System.Net.Http;
using System.Threading.Tasks;

namespace Jellyfin.Plugin.AudioMuseAi.Services
{
    public interface IAudioMuseService
    {
        Task<HttpResponseMessage> HealthCheckAsync();
        
        Task<HttpResponseMessage> GetPlaylistsAsync();
        
        Task<HttpResponseMessage> StartAnalysisAsync(string jsonPayload);
        
        Task<HttpResponseMessage> StartClusteringAsync(string jsonPayload);
    }
}
