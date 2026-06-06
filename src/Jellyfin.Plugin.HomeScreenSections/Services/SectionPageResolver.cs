using System.Collections.Concurrent;
using Jellyfin.Plugin.HomeScreenSections.Configuration;
using Jellyfin.Plugin.HomeScreenSections.Data;
using Jellyfin.Plugin.HomeScreenSections.Library;
using Jellyfin.Plugin.HomeScreenSections.Model;
using MediaBrowser.Controller.Configuration;

namespace Jellyfin.Plugin.HomeScreenSections.Services;

public class SectionPageResolver(
    ITranslationManager translationManager,
    UserSectionsDataCache dataCache,
    IServerConfigurationManager configurationManager,
    SectionInstanceBuilder instanceBuilder) : ISectionPageResolver
{
    private readonly ConcurrentDictionary<Guid, SectionPageBuildState> m_buildStates = new();

    public async Task<SectionPageResolveResult> ResolvePageAsync(
        Guid userId,
        string? language,
        int page,
        int? pageSize,
        Guid? pageHash,
        CancellationToken cancellationToken = default)
    {
        EvictStaleEntries();

        if (pageHash == null)
        {
            Guid newPageHash = Guid.NewGuid();
            SectionPageBuildState buildState = GetOrCreateBuildState(userId, newPageHash);
            await buildState.BuildTask!.WaitAsync(cancellationToken).ConfigureAwait(false);

            UserSectionsData data = buildState.Data;
            data.LastAccessed = DateTime.UtcNow;

            int totalSectionCount = data.OrderedSections.SelectMany(x => x.Value).Count();
            CohesionPageResult evaluation = SectionCohesionRules.Evaluate(data, 1, totalSectionCount);

            return new SectionPageResolveResult
            {
                PageHash = newPageHash,
                Sections = [.. evaluation.Slice.Select(x => SectionToInfo(x.Section, x.OrderIndex, language))],
                IsComplete = evaluation.IsComplete
            };
        }

        SectionPageBuildState paginatedBuild = GetOrCreateBuildState(userId, pageHash.Value);
        UserSectionsData cache = paginatedBuild.Data;

        while (cache.SectionsInProgress.IsEmpty && cache.OrderedSections.IsEmpty)
        {
            if (paginatedBuild.BuildTask!.IsCompleted)
            {
                break;
            }

            await Task.Delay(1, cancellationToken).ConfigureAwait(false);
        }

        int lowestSectionIndex = Math.Min(
            !cache.OrderedSections.IsEmpty ? cache.OrderedSections.Min(x => x.Key) : int.MaxValue,
            !cache.SectionsInProgress.IsEmpty ? cache.SectionsInProgress.Min(x => x.Key) : int.MaxValue);

        int effectivePageSize = pageSize ?? cache.OrderedSections.SelectMany(x => x.Value).Count();

        for (int i = lowestSectionIndex; i <= cache.MaxOrderIndex; i++)
        {
            if (SectionCohesionRules.IsVacantOrderIndex(cache, i))
            {
                continue;
            }

            if (cache.SectionsInProgress.ContainsKey(i)
                && paginatedBuild.OrderIndexCompletions.TryGetValue(i, out TaskCompletionSource? completion))
            {
                await completion.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }

            cache.LastAccessed = DateTime.UtcNow;
            CohesionPageResult pageEvaluation = SectionCohesionRules.Evaluate(cache, page, effectivePageSize);

            if (pageEvaluation.CanReturn)
            {
                return new SectionPageResolveResult
                {
                    PageHash = pageHash.Value,
                    Sections = [.. pageEvaluation.Slice.Select(x => SectionToInfo(x.Section, x.OrderIndex, language))],
                    IsComplete = pageEvaluation.IsComplete
                };
            }
        }

        return new SectionPageResolveResult
        {
            PageHash = pageHash.Value,
            Sections = [],
            IsComplete = false
        };
    }

    private SectionPageBuildState GetOrCreateBuildState(Guid userId, Guid pageHash)
    {
        if (m_buildStates.TryGetValue(pageHash, out SectionPageBuildState? existing))
        {
            existing.Data.LastAccessed = DateTime.UtcNow;
            return existing;
        }

        return m_buildStates.GetOrAdd(pageHash, _ =>
        {
            SectionPageBuildState buildState = InitializeBuildState(userId, pageHash);
            buildState.BuildTask = instanceBuilder.BuildAsync(buildState, userId, CancellationToken.None);
            return buildState;
        });
    }

    private SectionPageBuildState InitializeBuildState(Guid userId, Guid pageHash)
    {
        IGrouping<int, SectionSettings>[] groupedOrderedSections =
        [
            .. HomeScreenSectionsPlugin.Instance.Configuration.SectionSettings
                .OrderBy(x => x.OrderIndex)
                .GroupBy(x => x.OrderIndex)
        ];

        UserSectionsData userSectionsData = new()
        {
            UserId = userId,
            MaxOrderIndex = groupedOrderedSections.Length > 0 ? groupedOrderedSections.Max(x => x.Key) : 0,
            LastAccessed = DateTime.UtcNow
        };

        dataCache.Cache.TryAdd(pageHash, userSectionsData);

        ConcurrentDictionary<int, TaskCompletionSource> completions = new();

        foreach (int orderIndex in groupedOrderedSections.Select(x => x.Key).OrderBy(x => x))
        {
            userSectionsData.SectionsInProgress.TryAdd(orderIndex, true);
            completions[orderIndex] = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        foreach (IntRange gap in SectionCohesionRules.ComputeOrderGaps(
                     [.. groupedOrderedSections.Select(x => x.Key).OrderBy(x => x)]))
        {
            userSectionsData.OrderIndicesWithoutSections.Add(gap);
        }

        return new SectionPageBuildState
        {
            Data = userSectionsData,
            OrderIndexCompletions = completions
        };
    }

    private void EvictStaleEntries()
    {
        int ttlMinutes = HomeScreenSectionsPlugin.Instance.Configuration.PageHashCacheTtlMinutes;
        if (ttlMinutes <= 0)
        {
            return;
        }

        DateTime cutoff = DateTime.UtcNow.AddMinutes(-ttlMinutes);

        foreach (KeyValuePair<Guid, UserSectionsData> entry in dataCache.Cache)
        {
            if (entry.Value.LastAccessed is null || entry.Value.LastAccessed < cutoff)
            {
                m_buildStates.TryRemove(entry.Key, out _);
                dataCache.Cache.TryRemove(entry.Key, out _);
            }
        }
    }

    private HomeScreenSectionInfo SectionToInfo(IHomeScreenSection section, int configuredOrder, string? language)
    {
        HomeScreenSectionInfo info = section.AsInfo();

        info.OrderIndex = configuredOrder;
        info.ViewMode = HomeScreenSectionsPlugin.Instance.Configuration.SectionSettings
                            .FirstOrDefault(y => y.SectionId == info.Section)?.ViewMode
                        ?? info.ViewMode
                        ?? SectionViewMode.Landscape;

        if (info.DisplayText != null)
        {
            string? translatedResult = translationManager.Translate(
                info.Section!,
                language?.Trim() ?? configurationManager.Configuration.UICulture,
                info.DisplayText,
                section.TranslationMetadata);

            info.DisplayText = translatedResult;
        }

        return info;
    }
}
