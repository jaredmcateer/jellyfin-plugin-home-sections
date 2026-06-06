using System.Collections.Concurrent;
using Jellyfin.Extensions;
using Jellyfin.Plugin.HomeScreenSections.Configuration;
using Jellyfin.Plugin.HomeScreenSections.Data;
using Jellyfin.Plugin.HomeScreenSections.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HomeScreenSections.Services;

public class SectionInstanceBuilder(
    IHomeScreenManager homeScreenManager,
    IModularHomeUserSettingsStore userSettingsStore,
    ILogger<HomeScreenSectionsPlugin> logger)
{
    public async Task BuildAsync(SectionPageBuildState buildState, Guid userId, CancellationToken cancellationToken)
    {
        ModularHomeUserSettings settings = userSettingsStore.GetUserSettings(userId);

        List<IHomeScreenSection> sectionTypes =
        [
            .. homeScreenManager.GetSectionTypes()
                .Where(x => settings.EnabledSections.Contains(x.Section ?? string.Empty))
        ];

        IGrouping<int, SectionSettings>[] groupedOrderedSections =
        [
            .. HomeScreenSectionsPlugin.Instance.Configuration.SectionSettings
                .OrderBy(x => x.OrderIndex)
                .GroupBy(x => x.OrderIndex)
        ];

        if (groupedOrderedSections.Length == 0)
        {
            return;
        }

        IEnumerable<Task> orderGroupTasks = groupedOrderedSections.Select(orderedSections =>
            BuildOrderGroupAsync(buildState, userId, orderedSections, sectionTypes, cancellationToken));

        await Task.WhenAll(orderGroupTasks).ConfigureAwait(false);
    }

    private async Task BuildOrderGroupAsync(
        SectionPageBuildState buildState,
        Guid userId,
        IGrouping<int, SectionSettings> orderedSections,
        List<IHomeScreenSection> sectionTypes,
        CancellationToken cancellationToken)
    {
        await Task.Run(() =>
        {
            ConcurrentBag<IHomeScreenSection?> tmpPluginSections = new ConcurrentBag<IHomeScreenSection?>();

            Parallel.ForEach(orderedSections, sectionSettings =>
            {
                IHomeScreenSection? sectionType =
                    sectionTypes.FirstOrDefault(x => x.Section == sectionSettings.SectionId);

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
                        logger.LogError(e,
                            "An error occurred while creating section instances for user '{UserId}' and section '{Section}'.",
                            userId,
                            sectionType.Section);
                    }
                }
            });

            List<IHomeScreenSection> sectionList = [.. tmpPluginSections.Where(x => x != null).Select(x => x!)];
            sectionList.Shuffle();

            UserSectionsData userSectionsData = buildState.Data;
            userSectionsData.OrderedSections.TryAdd(orderedSections.Key, sectionList);
            userSectionsData.SectionsInProgress.Remove(orderedSections.Key, out _);

            if (buildState.OrderIndexCompletions.TryGetValue(orderedSections.Key, out TaskCompletionSource? completion))
            {
                completion.TrySetResult();
            }
        }, cancellationToken).ConfigureAwait(false);
    }
}
