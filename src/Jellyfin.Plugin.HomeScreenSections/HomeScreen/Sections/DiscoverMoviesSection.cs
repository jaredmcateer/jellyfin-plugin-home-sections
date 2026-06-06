using Jellyfin.Plugin.HomeScreenSections.Library;
using Jellyfin.Plugin.HomeScreenSections.Services;

namespace Jellyfin.Plugin.HomeScreenSections.HomeScreen.Sections;

public class DiscoverMoviesSection(
    IJellyseerrClient jellyseerrClient,
    ImageCacheService imageCacheService)
    : DiscoverSection(jellyseerrClient, imageCacheService)
{
    public override string? Section => "DiscoverMovies";

    public override string? DisplayText { get; set; } = "Discover Movies";

    protected override string JellyseerEndpoint => "/api/v1/discover/movies";
}
