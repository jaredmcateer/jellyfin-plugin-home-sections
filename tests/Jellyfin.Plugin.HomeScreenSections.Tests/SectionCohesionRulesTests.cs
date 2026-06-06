using Jellyfin.Plugin.HomeScreenSections.Data;
using Jellyfin.Plugin.HomeScreenSections.Library;
using Jellyfin.Plugin.HomeScreenSections.Model;
using Jellyfin.Plugin.HomeScreenSections.Model.Dto;
using Jellyfin.Plugin.HomeScreenSections.Services;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Jellyfin.Plugin.HomeScreenSections.Tests;

internal sealed class StubSection : IHomeScreenSection
{
    public StubSection(string sectionId)
    {
        Section = sectionId;
    }

    public string? Section { get; }

    public string? DisplayText { get; set; }

    public int? Limit => 1;

    public string? Route => $"/HomeScreen/Section/{Section}";

    public string? AdditionalData { get; set; }

    public object? OriginalPayload => null;

    public QueryResult<BaseItemDto> GetResults(HomeScreenSectionPayload payload, IQueryCollection queryCollection) =>
        new QueryResult<BaseItemDto>();

    public IEnumerable<IHomeScreenSection> CreateInstances(Guid? userId, int instanceCount) =>
        Enumerable.Repeat(this, instanceCount);

    public HomeScreenSectionInfo GetInfo() => new HomeScreenSectionInfo { Section = Section, DisplayText = Section };
}

internal static class UserSectionsDataBuilder
{
    public static UserSectionsData Create(
        int maxOrderIndex,
        Dictionary<int, IEnumerable<IHomeScreenSection>> orderedSections,
        HashSet<IntRange>? vacantRanges = null,
        Dictionary<int, bool>? inProgress = null)
    {
        UserSectionsData data = new UserSectionsData
        {
            UserId = Guid.NewGuid(),
            MaxOrderIndex = maxOrderIndex
        };

        foreach (KeyValuePair<int, IEnumerable<IHomeScreenSection>> entry in orderedSections)
        {
            data.OrderedSections[entry.Key] = entry.Value;
        }

        if (vacantRanges != null)
        {
            foreach (IntRange range in vacantRanges)
            {
                data.OrderIndicesWithoutSections.Add(range);
            }
        }

        if (inProgress != null)
        {
            foreach (KeyValuePair<int, bool> entry in inProgress)
            {
                data.SectionsInProgress[entry.Key] = entry.Value;
            }
        }

        return data;
    }
}

public class SectionCohesionRulesTests
{
    [Fact]
    public void ComputeOrderGaps_FindsGapsBetweenConfiguredIndices()
    {
        IReadOnlyList<IntRange> gaps = SectionCohesionRules.ComputeOrderGaps(new[] { 1, 5, 10 });

        Assert.Equal(2, gaps.Count);
        Assert.Contains(gaps, g => g.Start == 2 && g.End == 4);
        Assert.Contains(gaps, g => g.Start == 6 && g.End == 9);
    }

    [Fact]
    public void Evaluate_ConsecutiveIndices_ReturnsFullPrefix()
    {
        UserSectionsData data = UserSectionsDataBuilder.Create(
            maxOrderIndex: 2,
            orderedSections: new Dictionary<int, IEnumerable<IHomeScreenSection>>
            {
                [1] = new[] { new StubSection("A") },
                [2] = new[] { new StubSection("B") }
            });

        CohesionPageResult result = SectionCohesionRules.Evaluate(data, page: 1, pageSize: 10);

        Assert.True(result.CanReturn);
        Assert.True(result.IsComplete);
        Assert.Equal(2, result.Slice.Count);
    }

    [Fact]
    public void Evaluate_GapCoveredByVacantRange_RemainsCohesive()
    {
        UserSectionsData data = UserSectionsDataBuilder.Create(
            maxOrderIndex: 5,
            orderedSections: new Dictionary<int, IEnumerable<IHomeScreenSection>>
            {
                [1] = new[] { new StubSection("A") },
                [5] = new[] { new StubSection("B") }
            },
            vacantRanges: new HashSet<IntRange> { new IntRange { Start = 2, End = 4 } });

        CohesionPageResult result = SectionCohesionRules.Evaluate(data, page: 1, pageSize: 10);

        Assert.True(result.CanReturn);
        Assert.True(result.IsComplete);
        Assert.Equal(2, result.Slice.Count);
    }

    [Fact]
    public void Evaluate_UncoveredGap_StopsPrefix()
    {
        UserSectionsData data = UserSectionsDataBuilder.Create(
            maxOrderIndex: 3,
            orderedSections: new Dictionary<int, IEnumerable<IHomeScreenSection>>
            {
                [1] = new[] { new StubSection("A") },
                [3] = new[] { new StubSection("B") }
            });

        CohesionPageResult result = SectionCohesionRules.Evaluate(data, page: 1, pageSize: 10);

        Assert.False(result.CanReturn);
        Assert.False(result.IsComplete);
        Assert.Single(result.Slice);
    }

    [Fact]
    public void Evaluate_Pagination_ReturnsSecondPageWhenEnoughInstances()
    {
        UserSectionsData data = UserSectionsDataBuilder.Create(
            maxOrderIndex: 2,
            orderedSections: new Dictionary<int, IEnumerable<IHomeScreenSection>>
            {
                [1] = new[] { new StubSection("A"), new StubSection("B"), new StubSection("C") },
                [2] = new[] { new StubSection("D") }
            });

        CohesionPageResult page1 = SectionCohesionRules.Evaluate(data, page: 1, pageSize: 2);
        CohesionPageResult page2 = SectionCohesionRules.Evaluate(data, page: 2, pageSize: 2);

        Assert.True(page1.CanReturn);
        Assert.Equal(2, page1.Slice.Count);
        Assert.True(page2.CanReturn);
        Assert.Equal(2, page2.Slice.Count);
        Assert.Equal("C", page2.Slice[0].Section.Section);
        Assert.Equal("D", page2.Slice[1].Section.Section);
    }

    [Fact]
    public void Evaluate_InProgressBlocksReturnUntilPageFilled()
    {
        UserSectionsData data = UserSectionsDataBuilder.Create(
            maxOrderIndex: 2,
            orderedSections: new Dictionary<int, IEnumerable<IHomeScreenSection>>
            {
                [1] = new[] { new StubSection("A") }
            },
            inProgress: new Dictionary<int, bool> { [2] = true });

        CohesionPageResult result = SectionCohesionRules.Evaluate(data, page: 1, pageSize: 3);

        Assert.False(result.CanReturn);
        Assert.False(result.IsComplete);
    }

    [Fact]
    public void Evaluate_FirstGroupEnoughInstances_ReturnsWithoutWaitingForLaterGroups()
    {
        UserSectionsData data = UserSectionsDataBuilder.Create(
            maxOrderIndex: 2,
            orderedSections: new Dictionary<int, IEnumerable<IHomeScreenSection>>
            {
                [1] = new[]
                {
                    new StubSection("A"),
                    new StubSection("B"),
                    new StubSection("C")
                }
            },
            inProgress: new Dictionary<int, bool> { [2] = true });

        CohesionPageResult result = SectionCohesionRules.Evaluate(data, page: 1, pageSize: 3);

        Assert.True(result.CanReturn);
        Assert.False(result.IsComplete);
        Assert.Equal(3, result.Slice.Count);
    }

    [Fact]
    public void IsVacantOrderIndex_ReturnsTrueForGapIndices()
    {
        UserSectionsData data = UserSectionsDataBuilder.Create(
            maxOrderIndex: 5,
            orderedSections: new Dictionary<int, IEnumerable<IHomeScreenSection>>(),
            vacantRanges: new HashSet<IntRange> { new IntRange { Start = 2, End = 4 } });

        Assert.True(SectionCohesionRules.IsVacantOrderIndex(data, 3));
        Assert.False(SectionCohesionRules.IsVacantOrderIndex(data, 5));
    }
}
