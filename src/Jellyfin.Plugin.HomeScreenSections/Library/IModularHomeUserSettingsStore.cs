using Jellyfin.Plugin.HomeScreenSections.Library;

namespace Jellyfin.Plugin.HomeScreenSections.Library;

public interface IModularHomeUserSettingsStore
{
    ModularHomeUserSettings GetUserSettings(Guid userId);

    bool UpdateUserSettings(Guid userId, ModularHomeUserSettings userSettings);

    bool GetUserFeatureEnabled(Guid userId);

    void SetUserFeatureEnabled(Guid userId, bool enabled);
}
