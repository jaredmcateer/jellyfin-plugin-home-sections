using Jellyfin.Plugin.HomeScreenSections.Library;
using Jellyfin.Plugin.HomeScreenSections.Services;

namespace Jellyfin.Plugin.HomeScreenSections.HomeScreen.Sections;

public class DiscoverTvSection(
    IJellyseerrClient jellyseerrClient,
    ImageCacheService imageCacheService)
    : DiscoverSection(jellyseerrClient, imageCacheService)
{
    public override string? Section => "DiscoverTV";

    public override string? DisplayText { get; set; } = "Discover TV Shows";

    protected override string JellyseerEndpoint => "/api/v1/discover/tv";
}
