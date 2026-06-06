using System.Reflection;
using System.Runtime.CompilerServices;
using Jellyfin.Plugin.HomeScreenSections.Configuration;
using Jellyfin.Plugin.HomeScreenSections.Library;
using Jellyfin.Plugin.HomeScreenSections.Services;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.HomeScreenSections.Tests;

public class ModularHomeUserSettingsStoreTests : IDisposable
{
    private static readonly Guid UserId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private readonly string m_configDirectory;
    private readonly ModularHomeUserSettingsStore m_store;

    public ModularHomeUserSettingsStoreTests()
    {
        m_configDirectory = Path.Combine(Path.GetTempPath(), "hss-settings-tests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(m_configDirectory);

        Mock<IApplicationPaths> applicationPaths = new();
        applicationPaths.Setup(x => x.PluginConfigurationsPath).Returns(m_configDirectory);

        SetPluginConfiguration(new PluginConfiguration
        {
            SectionSettings =
            [
                new SectionSettings
                {
                    SectionId = "LatestMovies",
                    Enabled = true,
                    AllowUserOverride = true
                },
                new SectionSettings
                {
                    SectionId = "MyMedia",
                    Enabled = true,
                    AllowUserOverride = false
                }
            ]
        });

        m_store = new ModularHomeUserSettingsStore(applicationPaths.Object, NullLogger<ModularHomeUserSettingsStore>.Instance);
    }

    [Fact]
    public void GetUserSettings_WhenNoPersistedRow_ReturnsEffectiveDefaults()
    {
        ModularHomeUserSettings settings = m_store.GetUserSettings(UserId);

        Assert.Equal(UserId, settings.UserId);
        Assert.Contains("LatestMovies", settings.EnabledSections);
        Assert.Contains("MyMedia", settings.EnabledSections);
    }

    [Fact]
    public void UpdateUserSettings_FirstSave_PersistsUserRow()
    {
        ModularHomeUserSettings userSettings = new()
        {
            UserId = UserId,
            EnabledSections = ["LatestMovies"]
        };

        m_store.UpdateUserSettings(UserId, userSettings);

        string settingsPath = Path.Combine(m_configDirectory, "Jellyfin.Plugin.HomeScreenSections", "ModularHomeSettings.json");
        Assert.True(File.Exists(settingsPath));
        Assert.Contains("LatestMovies", File.ReadAllText(settingsPath));
    }

    [Fact]
    public void UpdateUserSettings_ThenGet_AppliesEffectiveMerge()
    {
        m_store.UpdateUserSettings(UserId, new ModularHomeUserSettings
        {
            UserId = UserId,
            EnabledSections = ["LatestMovies"]
        });

        ModularHomeUserSettings settings = m_store.GetUserSettings(UserId);

        Assert.Contains("LatestMovies", settings.EnabledSections);
        Assert.Contains("MyMedia", settings.EnabledSections);
    }

    [Fact]
    public void SetUserFeatureEnabled_PersistsToFile()
    {
        m_store.SetUserFeatureEnabled(UserId, true);

        string featurePath = Path.Combine(m_configDirectory, "Jellyfin.Plugin.HomeScreenSections", "userFeatureEnabled.json");
        Assert.True(File.Exists(featurePath));
        Assert.Contains("true", File.ReadAllText(featurePath));
        Assert.True(m_store.GetUserFeatureEnabled(UserId));
    }

    public void Dispose()
    {
        typeof(HomeScreenSectionsPlugin)
            .GetProperty(nameof(HomeScreenSectionsPlugin.Instance), BindingFlags.Public | BindingFlags.Static)!
            .SetValue(null, null);

        if (Directory.Exists(m_configDirectory))
        {
            Directory.Delete(m_configDirectory, recursive: true);
        }
    }

    private static void SetPluginConfiguration(PluginConfiguration configuration)
    {
        HomeScreenSectionsPlugin plugin = (HomeScreenSectionsPlugin)RuntimeHelpers.GetUninitializedObject(typeof(HomeScreenSectionsPlugin));
        typeof(BasePlugin<PluginConfiguration>)
            .GetProperty(nameof(BasePlugin<PluginConfiguration>.Configuration))!
            .SetValue(plugin, configuration);

        typeof(HomeScreenSectionsPlugin)
            .GetProperty(nameof(HomeScreenSectionsPlugin.Instance), BindingFlags.Public | BindingFlags.Static)!
            .SetValue(null, plugin);
    }
}
