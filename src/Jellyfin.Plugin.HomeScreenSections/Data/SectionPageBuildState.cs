using System.Collections.Concurrent;

namespace Jellyfin.Plugin.HomeScreenSections.Data;

public class SectionPageBuildState
{
    public required UserSectionsData Data { get; init; }

    public ConcurrentDictionary<int, TaskCompletionSource> OrderIndexCompletions { get; init; } =
        new ConcurrentDictionary<int, TaskCompletionSource>();

    public Task? BuildTask { get; set; }
}
