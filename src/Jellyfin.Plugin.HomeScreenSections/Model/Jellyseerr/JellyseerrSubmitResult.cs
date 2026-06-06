namespace Jellyfin.Plugin.HomeScreenSections.Model.Jellyseerr;

public sealed class JellyseerrSubmitResult
{
    public bool IsConfigured { get; init; }

    public bool UserResolved { get; init; }

    public string Content { get; init; } = string.Empty;

    public string ContentType { get; init; } = "application/json";

    public int StatusCode { get; init; }
}
