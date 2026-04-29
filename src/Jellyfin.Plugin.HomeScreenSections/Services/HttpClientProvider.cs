using Jellyfin.Plugin.HomeScreenSections.Configuration;

namespace Jellyfin.Plugin.HomeScreenSections.Services
{
    /// <summary>
    /// Provides HttpClient instances, with optional reuse when experimental flag is enabled.
    /// </summary>
    public static class HttpClientProvider
    {
        private static readonly HttpClient s_sharedClient = new()
        {
            Timeout = TimeSpan.FromSeconds(30)
        };

        /// <summary>
        /// Gets an HttpClient instance. Returns shared instance when UseHttpClientReuse flag is enabled,
        /// otherwise creates a new instance for backward compatibility.
        /// </summary>
        public static HttpClient GetClient()
        {
            PluginConfiguration? config = HomeScreenSectionsPlugin.Instance?.Configuration;
            if (config?.Experimental?.IsFeatureEnabled(config.Experimental.UseHttpClientReuse) == true)
            {
                return s_sharedClient;
            }

            // Fall back to creating new instance when flag is disabled
            return new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        }

        /// <summary>
        /// Gets an HttpClient instance with custom base address.
        /// </summary>
        public static HttpClient GetClient(string baseAddress)
        {
            HttpClient client = GetClient();
            
            // Only set BaseAddress on new instances to avoid conflicts with shared client
            PluginConfiguration? config = HomeScreenSectionsPlugin.Instance?.Configuration;
            if (config?.Experimental?.IsFeatureEnabled(config.Experimental.UseHttpClientReuse) != true)
            {
                client.BaseAddress = new Uri(baseAddress);
            }
            
            return client;
        }
    }
}
