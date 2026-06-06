namespace Jellyfin.Plugin.HomeScreenSections.Model.Jellyseerr;

public sealed class JellyseerrDiscoverItem
{
    public int Id { get; init; }

    public string? MediaType { get; init; }

    public string? Name { get; init; }

    public string? OriginalName { get; init; }

    public string? OriginalLanguage { get; init; }

    public string? PosterPath { get; init; }

    public string? ReleaseDate { get; init; }

    public float? CommunityRating { get; init; }
}
