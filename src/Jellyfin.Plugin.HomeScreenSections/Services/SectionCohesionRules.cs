using Jellyfin.Plugin.HomeScreenSections.Data;
using Jellyfin.Plugin.HomeScreenSections.Library;

namespace Jellyfin.Plugin.HomeScreenSections.Services;

public sealed class CohesionPageResult
{
    public bool CanReturn { get; init; }

    public bool IsComplete { get; init; }

    public IReadOnlyList<(IHomeScreenSection Section, int OrderIndex)> Slice { get; init; } = [];
}

public static class SectionCohesionRules
{
    public static IReadOnlyList<IntRange> ComputeOrderGaps(IReadOnlyList<int> orderIndices)
    {
        if (orderIndices.Count < 2)
        {
            return [];
        }

        List<IntRange> gaps = [];
        for (int i = 1; i < orderIndices.Count; i++)
        {
            int prevIndex = orderIndices[i - 1];
            int currentIndex = orderIndices[i];

            if (currentIndex - prevIndex > 1)
            {
                gaps.Add(new IntRange
                {
                    Start = prevIndex + 1,
                    End = currentIndex - 1
                });
            }
        }

        return gaps;
    }

    public static bool IsVacantOrderIndex(UserSectionsData data, int orderIndex)
    {
        return data.OrderIndicesWithoutSections.Any(x => x.Contains(orderIndex));
    }

    public static CohesionPageResult Evaluate(UserSectionsData data, int page, int pageSize)
    {
        int[] orderedKeys = [.. data.OrderedSections.Keys.OrderBy(x => x)];

        List<(IHomeScreenSection Section, int ConfiguredOrder)> sectionsToReturn = [];
        bool isComplete = true;

        for (int i = 0; i < orderedKeys.Length; i++)
        {
            int key = orderedKeys[i];
            int prevKey = i > 0 ? orderedKeys[i - 1] : orderedKeys[i] - 1;

            bool cohesive = (key - prevKey) == 1;
            if (prevKey > 0 && key - prevKey > 1)
            {
                if (data.OrderIndicesWithoutSections.Any(x => x.Contains(key - 1) && x.Contains(prevKey + 1)))
                {
                    cohesive = true;
                }
            }

            if (cohesive)
            {
                sectionsToReturn.AddRange(data.OrderedSections[key].Select(x => (x, key)));
            }
            else
            {
                isComplete = false;
                break;
            }
        }

        List<(IHomeScreenSection Section, int ConfiguredOrder)> pageSlice = [.. sectionsToReturn
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            ];

        bool canReturn = (isComplete && data.SectionsInProgress.IsEmpty) || pageSlice.Count == pageSize;

        return new CohesionPageResult
        {
            CanReturn = canReturn,
            IsComplete = isComplete && data.SectionsInProgress.IsEmpty,
            Slice = pageSlice
        };
    }
}
