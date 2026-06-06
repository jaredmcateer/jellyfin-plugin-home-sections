using Jellyfin.Plugin.HomeScreenSections.Configuration;
using Jellyfin.Plugin.HomeScreenSections.Library;
using MediaBrowser.Common.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Jellyfin.Plugin.HomeScreenSections.Services;

public class ModularHomeUserSettingsStore(
    IApplicationPaths applicationPaths,
    ILogger<ModularHomeUserSettingsStore> logger) : IModularHomeUserSettingsStore
{
    private const string SettingsFileName = "ModularHomeSettings.json";
    private const string FeatureEnabledFileName = "userFeatureEnabled.json";

    private readonly Dictionary<Guid, bool> m_userFeatureEnabledStates = LoadFeatureEnabledStates(applicationPaths);

    public ModularHomeUserSettings GetUserSettings(Guid userId)
    {
        SectionSettings[] adminSections = HomeScreenSectionsPlugin.Instance.Configuration.SectionSettings;
        ModularHomeUserSettings? persisted = LoadPersistedSettings(userId);

        ModularHomeUserSettings settings = persisted ?? ModularHomeSettingsMergeRules.CreateDefaults(userId, adminSections);
        return ModularHomeSettingsMergeRules.ApplyEffectiveSettings(settings, adminSections);
    }

    public bool UpdateUserSettings(Guid userId, ModularHomeUserSettings userSettings)
    {
        logger.LogInformation("Updating user settings for user {UserId}", userId);
        logger.LogInformation("Json of user settings received from browser: {UserSettings}", JsonConvert.SerializeObject(userSettings));

        string pluginSettingsPath = GetSettingsFilePath();
        logger.LogInformation("Plugin settings file: {PluginSettingsPath}", pluginSettingsPath);

        Directory.CreateDirectory(Path.GetDirectoryName(pluginSettingsPath)!);

        List<ModularHomeUserSettings> allSettings = [];

        if (File.Exists(pluginSettingsPath))
        {
            JArray settingsArray = JArray.Parse(File.ReadAllText(pluginSettingsPath));
            allSettings = settingsArray
                .Select(x => JsonConvert.DeserializeObject<ModularHomeUserSettings>(x.ToString()))
                .Where(x => x != null && !x.UserId.Equals(userId))
                .Cast<ModularHomeUserSettings>()
                .ToList();
        }

        allSettings.Add(userSettings);

        JArray output = new(allSettings.Select(x => JObject.FromObject(x)));
        File.WriteAllText(pluginSettingsPath, output.ToString(Formatting.Indented));

        logger.LogInformation("User settings updated for user {UserId}", userId);
        return true;
    }

    public bool GetUserFeatureEnabled(Guid userId)
    {
        if (m_userFeatureEnabledStates.TryGetValue(userId, out bool enabled))
        {
            return enabled;
        }

        m_userFeatureEnabledStates[userId] = false;
        return false;
    }

    public void SetUserFeatureEnabled(Guid userId, bool enabled)
    {
        m_userFeatureEnabledStates[userId] = enabled;

        string featureEnabledPath = GetFeatureEnabledFilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(featureEnabledPath)!);
        File.WriteAllText(featureEnabledPath, JObject.FromObject(m_userFeatureEnabledStates).ToString(Formatting.Indented));
    }

    private ModularHomeUserSettings? LoadPersistedSettings(Guid userId)
    {
        string pluginSettingsPath = GetSettingsFilePath();
        if (!File.Exists(pluginSettingsPath))
        {
            return null;
        }

        JArray settingsArray = JArray.Parse(File.ReadAllText(pluginSettingsPath));
        return settingsArray
            .Select(x => JsonConvert.DeserializeObject<ModularHomeUserSettings>(x.ToString()))
            .FirstOrDefault(x => x != null && x.UserId.Equals(userId));
    }

    private string GetSettingsFilePath() =>
        Path.Combine(applicationPaths.PluginConfigurationsPath, typeof(HomeScreenSectionsPlugin).Namespace!, SettingsFileName);

    private string GetFeatureEnabledFilePath() =>
        Path.Combine(applicationPaths.PluginConfigurationsPath, typeof(HomeScreenSectionsPlugin).Namespace!, FeatureEnabledFileName);

    private static Dictionary<Guid, bool> LoadFeatureEnabledStates(IApplicationPaths paths)
    {
        string featureEnabledPath = Path.Combine(
            paths.PluginConfigurationsPath,
            typeof(HomeScreenSectionsPlugin).Namespace!,
            FeatureEnabledFileName);
        if (!File.Exists(featureEnabledPath))
        {
            return [];
        }

        return JsonConvert.DeserializeObject<Dictionary<Guid, bool>>(File.ReadAllText(featureEnabledPath))
            ?? [];
    }
}
