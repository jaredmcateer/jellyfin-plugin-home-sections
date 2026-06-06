using Jellyfin.Plugin.HomeScreenSections.Model;

namespace Jellyfin.Plugin.HomeScreenSections.Library;

public interface ISectionPageResolver
{
    Task<SectionPageResolveResult> ResolvePageAsync(
        Guid userId,
        string? language,
        int page,
        int? pageSize,
        Guid? pageHash,
        CancellationToken cancellationToken = default);
}
