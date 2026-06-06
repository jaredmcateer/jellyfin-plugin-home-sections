using Jellyfin.Plugin.HomeScreenSections.Configuration;
using Jellyfin.Plugin.HomeScreenSections.Library;
using Jellyfin.Plugin.HomeScreenSections.Services;
using Xunit;

namespace Jellyfin.Plugin.HomeScreenSections.Tests;

public class ModularHomeSettingsMergeRulesTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    [Fact]
    public void CreateDefaults_IncludesLockedAndDefaultEnabledFromAdmin()
    {
        SectionSettings[] admin =
        [
            Section("LatestMovies", enabled: true, allowUserOverride: true),
            Section("MyMedia", enabled: true, allowUserOverride: false),
            Section("DiscoverMovies", enabled: false, allowUserOverride: false)
        ];

        ModularHomeUserSettings defaults = ModularHomeSettingsMergeRules.CreateDefaults(UserId, admin);

        Assert.Equal(UserId, defaults.UserId);
        Assert.Empty(defaults.EnabledSections);
        Assert.Equal(["MyMedia", "DiscoverMovies"], defaults.LockedSections);
        Assert.Equal(["LatestMovies", "MyMedia"], defaults.DefaultEnabledSections);
    }

    [Fact]
    public void ApplyEffectiveSettings_WhenEnabledEmpty_FillsAdminDefaults()
    {
        SectionSettings[] admin =
        [
            Section("LatestMovies", enabled: true, allowUserOverride: true),
            Section("MyMedia", enabled: false, allowUserOverride: true)
        ];

        ModularHomeUserSettings settings = ModularHomeSettingsMergeRules.CreateDefaults(UserId, admin);

        ModularHomeUserSettings effective = ModularHomeSettingsMergeRules.ApplyEffectiveSettings(settings, admin);

        Assert.Equal(["LatestMovies"], effective.EnabledSections);
    }

    [Fact]
    public void ApplyEffectiveSettings_ForcesLockedSectionOn()
    {
        SectionSettings[] admin =
        [
            Section("MyMedia", enabled: true, allowUserOverride: false)
        ];

        ModularHomeUserSettings settings = new()
        {
            UserId = UserId,
            EnabledSections = ["LatestMovies"]
        };

        ModularHomeUserSettings effective = ModularHomeSettingsMergeRules.ApplyEffectiveSettings(settings, admin);

        Assert.Contains("MyMedia", effective.EnabledSections);
        Assert.Contains("LatestMovies", effective.EnabledSections);
    }

    [Fact]
    public void ApplyEffectiveSettings_ForcesLockedSectionOff()
    {
        SectionSettings[] admin =
        [
            Section("DiscoverMovies", enabled: false, allowUserOverride: false)
        ];

        ModularHomeUserSettings settings = new()
        {
            UserId = UserId,
            EnabledSections = ["DiscoverMovies", "LatestMovies"]
        };

        ModularHomeUserSettings effective = ModularHomeSettingsMergeRules.ApplyEffectiveSettings(settings, admin);

        Assert.DoesNotContain("DiscoverMovies", effective.EnabledSections);
        Assert.Contains("LatestMovies", effective.EnabledSections);
    }

    [Fact]
    public void ApplyEffectiveSettings_PreservesUserChoiceForUnlockedSections()
    {
        SectionSettings[] admin =
        [
            Section("LatestMovies", enabled: true, allowUserOverride: true),
            Section("MyMedia", enabled: true, allowUserOverride: true)
        ];

        ModularHomeUserSettings settings = new()
        {
            UserId = UserId,
            EnabledSections = ["LatestMovies"]
        };

        ModularHomeUserSettings effective = ModularHomeSettingsMergeRules.ApplyEffectiveSettings(settings, admin);

        Assert.Equal(["LatestMovies"], effective.EnabledSections);
    }

    private static SectionSettings Section(string sectionId, bool enabled, bool allowUserOverride) => new()
    {
        SectionId = sectionId,
        Enabled = enabled,
        AllowUserOverride = allowUserOverride
    };
}
