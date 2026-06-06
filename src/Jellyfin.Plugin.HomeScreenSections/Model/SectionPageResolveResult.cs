using Jellyfin.Plugin.HomeScreenSections.Library;

namespace Jellyfin.Plugin.HomeScreenSections.Model;

public class SectionPageResolveResult
{
    public Guid PageHash { get; set; }

    public List<HomeScreenSectionInfo> Sections { get; set; } = [];

    public bool IsComplete { get; set; }
}
