using Jellyfin.Plugin.HomeScreenSections.Configuration;
using Jellyfin.Plugin.HomeScreenSections.Library;

namespace Jellyfin.Plugin.HomeScreenSections.Services;

public static class ModularHomeSettingsMergeRules
{
    public static ModularHomeUserSettings CreateDefaults(Guid userId, IReadOnlyList<SectionSettings> adminSections)
    {
        return new ModularHomeUserSettings
        {
            UserId = userId,
            LockedSections = GetLockedSectionIds(adminSections),
            DefaultEnabledSections = GetDefaultEnabledSectionIds(adminSections),
            EnabledSections = []
        };
    }

    public static ModularHomeUserSettings ApplyEffectiveSettings(
        ModularHomeUserSettings settings,
        IReadOnlyList<SectionSettings> adminSections)
    {
        ModularHomeUserSettings effective = Clone(settings);

        if (effective.EnabledSections.Count == 0)
        {
            effective.EnabledSections.AddRange(GetDefaultEnabledSectionIds(adminSections));
        }

        foreach (SectionSettings sectionSettings in adminSections.Where(x => !x.AllowUserOverride))
        {
            if (sectionSettings.Enabled && !effective.EnabledSections.Contains(sectionSettings.SectionId))
            {
                effective.EnabledSections.Add(sectionSettings.SectionId);
            }
            else if (!sectionSettings.Enabled && effective.EnabledSections.Contains(sectionSettings.SectionId))
            {
                effective.EnabledSections.Remove(sectionSettings.SectionId);
            }
        }

        return effective;
    }

    private static List<string> GetLockedSectionIds(IReadOnlyList<SectionSettings> adminSections) =>
        [.. adminSections.Where(x => !x.AllowUserOverride).Select(x => x.SectionId)];

    private static List<string> GetDefaultEnabledSectionIds(IReadOnlyList<SectionSettings> adminSections) =>
        [.. adminSections.Where(x => x.Enabled).Select(x => x.SectionId)];

    private static ModularHomeUserSettings Clone(ModularHomeUserSettings settings) =>
        new()
        {
            UserId = settings.UserId,
            EnabledSections = [.. settings.EnabledSections],
            LockedSections = [.. settings.LockedSections],
            DefaultEnabledSections = [.. settings.DefaultEnabledSections]
        };
}
