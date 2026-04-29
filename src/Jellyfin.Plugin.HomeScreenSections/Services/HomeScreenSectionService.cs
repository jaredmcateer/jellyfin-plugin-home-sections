using System.Collections.Concurrent;
using Jellyfin.Extensions;
using Jellyfin.Plugin.HomeScreenSections.Configuration;
using Jellyfin.Plugin.HomeScreenSections.Helpers;
using Jellyfin.Plugin.HomeScreenSections.HomeScreen;
using Jellyfin.Plugin.HomeScreenSections.Library;
using Jellyfin.Plugin.HomeScreenSections.Model.Dto;
using MediaBrowser.Common.Extensions;
using MediaBrowser.Controller;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HomeScreenSections.Services;

public class HomeScreenSectionService
{
    private readonly IDisplayPreferencesManager m_displayPreferencesManager;
    private readonly IHomeScreenManager m_homeScreenManager;
    private readonly ILogger<HomeScreenSectionsPlugin> m_logger;

    public HomeScreenSectionService(IDisplayPreferencesManager displayPreferencesManager,
        IHomeScreenManager homeScreenManager, ILogger<HomeScreenSectionsPlugin> logger)
    {
        m_displayPreferencesManager = displayPreferencesManager;
        m_homeScreenManager = homeScreenManager;
        m_logger = logger;
    }
    
    public List<HomeScreenSectionInfo> GetSectionsForUser(Guid userId, string? language)
    {
        PluginConfiguration? config = HomeScreenSectionsPlugin.Instance?.Configuration;
        bool useAsyncFlow = config?.Experimental?.IsFeatureEnabled(config.Experimental.UseSectionCompletionSignaling) == true;

        if (useAsyncFlow)
        {
            return GetSectionsForUserAsync(userId, language).GetAwaiter().GetResult();
        }

        return GetSectionsForUserSync(userId, language);
    }

    private List<HomeScreenSectionInfo> GetSectionsForUserSync(Guid userId, string? language)
    {
        // string displayPreferencesId = "usersettings";
        // Guid itemId = displayPreferencesId.GetMD5();
        //
        // DisplayPreferences displayPreferences = m_displayPreferencesManager.GetDisplayPreferences(userId, itemId, "emby");
        ModularHomeUserSettings? settings = m_homeScreenManager.GetUserSettings(userId);

        List<IHomeScreenSection> sectionTypes = m_homeScreenManager.GetSectionTypes().Where(x => settings?.EnabledSections.Contains(x.Section ?? string.Empty) ?? false).ToList();

        List<IHomeScreenSection> sectionInstances = new List<IHomeScreenSection>();

        // List<string> homeSectionOrderTypes = new List<string>();
        // if (HomeScreenSectionsPlugin.Instance.Configuration.AllowUserOverride)
        // {
        //     foreach (HomeSection section in displayPreferences.HomeSections.OrderBy(x => x.Order))
        //     {
        //         switch (section.Type)
        //         {
        //             case HomeSectionType.SmallLibraryTiles:
        //                 homeSectionOrderTypes.Add("MyMedia");
        //                 break;
        //             case HomeSectionType.Resume:
        //                 homeSectionOrderTypes.Add("ContinueWatching");
        //                 break;
        //             case HomeSectionType.LatestMedia:
        //                 homeSectionOrderTypes.Add("LatestMovies");
        //                 homeSectionOrderTypes.Add("LatestShows");
        //                 break;
        //             case HomeSectionType.NextUp:
        //                 homeSectionOrderTypes.Add("NextUp");
        //                 break;
        //         }
        //     }
        // }

        // foreach (string type in homeSectionOrderTypes)
        // {
        //     IHomeScreenSection? sectionType = sectionTypes.FirstOrDefault(x => x.Section == type);
        //
        //     if (sectionType != null)
        //     {
        //         if (sectionType.Limit > 1)
        //         {
        //             SectionSettings? sectionSettings = HomeScreenSectionsPlugin.Instance.Configuration.SectionSettings.FirstOrDefault(x =>
        //                 x.SectionId == sectionType.Section);
        //
        //             Random rnd = new Random();
        //             int instanceCount = rnd.Next(sectionSettings?.LowerLimit ?? 0, sectionSettings?.UpperLimit ?? sectionType.Limit ?? 1);
        //
        //             for (int i = 0; i < instanceCount; ++i)
        //             {
        //                 sectionInstances.Add(sectionType.CreateInstance(userId, sectionInstances.Where(x => x.GetSectionType() == sectionType.GetSectionType())));
        //             }
        //         }
        //         else if (sectionType.Limit == 1)
        //         {
        //             sectionInstances.Add(sectionType.CreateInstance(userId));
        //         }
        //     }
        // }
        //
        // sectionTypes.RemoveAll(x => homeSectionOrderTypes.Contains(x.Section ?? string.Empty));

        IEnumerable<IGrouping<int, SectionSettings>> groupedOrderedSections = HomeScreenSectionsPlugin.Instance.Configuration.SectionSettings
            .OrderBy(x => x.OrderIndex)
            .GroupBy(x => x.OrderIndex);

        ConcurrentDictionary<int, List<IHomeScreenSection>> groupedSections = new ConcurrentDictionary<int, List<IHomeScreenSection>>();
        Parallel.ForEach(groupedOrderedSections, orderedSections =>
        {
            ConcurrentBag<IHomeScreenSection?> tmpPluginSections = new ConcurrentBag<IHomeScreenSection?>(); // we want these randomly distributed among each other.
            
            Parallel.ForEach(orderedSections, sectionSettings =>
            {
                if (!sectionSettings.IsEnabledByAdmin())
                {
                    return;
                }
                
                IHomeScreenSection? sectionType = sectionTypes.FirstOrDefault(x => x.Section == sectionSettings.SectionId);

                if (sectionType != null)
                {
                    int instanceCount = 1;
                    if (sectionType.Limit > 1)
                    {
                        Random rnd = new Random();
                        instanceCount = rnd.Next(sectionSettings.LowerLimit, sectionSettings.UpperLimit);
                    }

                    try
                    {
                        IEnumerable<IHomeScreenSection> instances = sectionType.CreateInstances(userId, instanceCount);

                        foreach (IHomeScreenSection sectionInstance in instances)
                        {
                            tmpPluginSections.Add(sectionInstance);
                        }
                    }
                    catch (Exception e)
                    {
                        // Adding an error log here to stop issues like #128 from completely breaking the home screen.
                        // Whatever this section is won't work, but the rest of the home screen will still work.
                        m_logger.LogError(e, $"An error occurred while creating section instances for user '{userId}' and section '{sectionType.Section}'.");
                    }
                }
            });
            
            List<IHomeScreenSection> sectionList = tmpPluginSections.Where(x => x != null).Select(x => x!).ToList();
            sectionList.Shuffle();

            groupedSections.TryAdd(orderedSections.Key, sectionList);
        });

        foreach (int key in groupedSections.Keys.OrderBy(x => x))
        {
            sectionInstances.AddRange(groupedSections[key]);
        }

        return sectionInstances.Where(x => x != null).Select(x =>
        {
            HomeScreenSectionInfo info = x.AsInfo();

            SectionSettings? sectionSettings = HomeScreenSectionsPlugin.Instance.Configuration.SectionSettings.FirstOrDefault(s => s.SectionId == info.Section);
            info.ViewMode = sectionSettings?.ViewMode ?? info.ViewMode ?? SectionViewMode.Landscape;
            string? displayTextToUse = null;

            if (sectionSettings != null && !string.IsNullOrEmpty(info.Section))
            {
                var tempPayload = new HomeScreenSectionPayload
                {
                    UserId = settings?.UserId ?? Guid.Empty,
                    UserSettings = settings
                };

                string headerDisplay = tempPayload.GetEffectiveStringConfig(info.Section, "SectionHeaderDisplay", "ShowWithNavigation");

                if (headerDisplay == "Hide")
                {
                    info.DisplayText = string.Empty;
                }
                else
                {
                    displayTextToUse = tempPayload.GetEffectiveStringConfig(info.Section, "CustomDisplayText", "");
                    if (!string.IsNullOrWhiteSpace(displayTextToUse))
                    {
                        info.DisplayText = displayTextToUse;
                    }
                }

                // Handle route button visibility
                if (headerDisplay == "ShowWithoutNavigation" || headerDisplay == "Hide")
                {
                    info.Route = null;
                }
            }
            else if (!string.IsNullOrWhiteSpace(displayTextToUse))
            {
                info.DisplayText = displayTextToUse;
            }

            if (language != "en" && !string.IsNullOrEmpty(language?.Trim()) && info.DisplayText != null)
            {
                string? translatedResult = TranslationHelper.TranslateAsync(info.DisplayText, "en", language.Trim())
                    .GetAwaiter().GetResult();

                info.DisplayText = translatedResult;
            }

            return info;
        }).ToList();
    }

    private async Task<List<HomeScreenSectionInfo>> GetSectionsForUserAsync(Guid userId, string? language)
    {
        ModularHomeUserSettings? settings = m_homeScreenManager.GetUserSettings(userId);

        List<IHomeScreenSection> sectionTypes = m_homeScreenManager.GetSectionTypes().Where(x => settings?.EnabledSections.Contains(x.Section ?? string.Empty) ?? false).ToList();

        IEnumerable<IGrouping<int, SectionSettings>> groupedOrderedSections = HomeScreenSectionsPlugin.Instance.Configuration.SectionSettings
            .OrderBy(x => x.OrderIndex)
            .GroupBy(x => x.OrderIndex);

        ConcurrentDictionary<int, List<IHomeScreenSection>> groupedSections = new();
        Parallel.ForEach(groupedOrderedSections, orderedSections =>
        {
            ConcurrentBag<IHomeScreenSection?> tmpPluginSections = [];

            Parallel.ForEach(orderedSections, sectionSettings =>
            {
                if (!sectionSettings.IsEnabledByAdmin())
                {
                    return;
                }

                IHomeScreenSection? sectionType = sectionTypes.FirstOrDefault(x => x.Section == sectionSettings.SectionId);

                if (sectionType != null)
                {
                    int instanceCount = 1;
                    if (sectionType.Limit > 1)
                    {
                        Random rnd = new();
                        instanceCount = rnd.Next(sectionSettings.LowerLimit, sectionSettings.UpperLimit);
                    }

                    try
                    {
                        IEnumerable<IHomeScreenSection> instances = sectionType.CreateInstances(userId, instanceCount);

                        foreach (IHomeScreenSection sectionInstance in instances)
                        {
                            tmpPluginSections.Add(sectionInstance);
                        }
                    }
                    catch (Exception e)
                    {
                        m_logger.LogError(e, "An error occurred while creating section instances for user '{userId}' and section '{sectionId}'.", userId, sectionSettings.SectionId);
                    }
                }
            });

            List<IHomeScreenSection> sectionList = [.. tmpPluginSections.Where(x => x != null).Select(x => x!)];
            sectionList.Shuffle();

            groupedSections.TryAdd(orderedSections.Key, sectionList);
        });

        List<IHomeScreenSection> sectionInstances = [];
        foreach (int key in groupedSections.Keys.OrderBy(x => x))
        {
            sectionInstances.AddRange(groupedSections[key]);
        }

        // Build section infos without translations first
        List<(HomeScreenSectionInfo Info, string? OriginalText)> sectionsWithText = [.. sectionInstances.Where(x => x != null).Select(x =>
        {
            HomeScreenSectionInfo info = x.AsInfo();

            SectionSettings? sectionSettings = HomeScreenSectionsPlugin.Instance.Configuration.SectionSettings.FirstOrDefault(s => s.SectionId == info.Section);
            info.ViewMode = sectionSettings?.ViewMode ?? info.ViewMode ?? SectionViewMode.Landscape;
            string? displayTextToUse = null;

            if (sectionSettings != null && !string.IsNullOrEmpty(info.Section))
            {
                HomeScreenSectionPayload tempPayload = new()
                {
                    UserId = settings?.UserId ?? Guid.Empty,
                    UserSettings = settings
                };

                string headerDisplay = tempPayload.GetEffectiveStringConfig(info.Section, "SectionHeaderDisplay", "ShowWithNavigation");

                if (headerDisplay == "Hide")
                {
                    info.DisplayText = string.Empty;
                }
                else
                {
                    displayTextToUse = tempPayload.GetEffectiveStringConfig(info.Section, "CustomDisplayText", "");
                    if (!string.IsNullOrWhiteSpace(displayTextToUse))
                    {
                        info.DisplayText = displayTextToUse;
                    }
                }

                if (headerDisplay == "ShowWithoutNavigation" || headerDisplay == "Hide")
                {
                    info.Route = null;
                }
            }
            else if (!string.IsNullOrWhiteSpace(displayTextToUse))
            {
                info.DisplayText = displayTextToUse;
            }

            return (info, info.DisplayText);
        })];

        // Translate all sections in parallel if needed
        if (language != "en" && !string.IsNullOrEmpty(language?.Trim()))
        {
            List<Task<string?>> translationTasks = [.. sectionsWithText
                .Where(x => x.OriginalText != null)
                .Select(x => TranslationHelper.TranslateAsync(x.OriginalText!, "en", language.Trim()))];

            string?[] translations = await Task.WhenAll(translationTasks);

            int translationIndex = 0;
            for (int i = 0; i < sectionsWithText.Count; i++)
            {
                if (sectionsWithText[i].OriginalText != null)
                {
                    sectionsWithText[i].Info.DisplayText = translations[translationIndex++];
                }
            }
        }

        return [.. sectionsWithText.Select(x => x.Info)];
    }
}