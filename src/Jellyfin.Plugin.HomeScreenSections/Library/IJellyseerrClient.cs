using Jellyfin.Plugin.HomeScreenSections.Model;
using Jellyfin.Plugin.HomeScreenSections.Model.Jellyseerr;

namespace Jellyfin.Plugin.HomeScreenSections.Library;

public interface IJellyseerrClient
{
    Task<IReadOnlyList<JellyseerrDiscoverItem>> GetDiscoverPageAsync(
        Guid jellyfinUserId,
        string discoverEndpoint,
        int page,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JellyseerrRequestMedia>> GetUserRequestsAsync(
        Guid jellyfinUserId,
        int take,
        CancellationToken cancellationToken = default);

    Task<JellyseerrSubmitResult?> SubmitRequestAsync(
        Guid jellyfinUserId,
        DiscoverRequestPayload payload,
        CancellationToken cancellationToken = default);
}
