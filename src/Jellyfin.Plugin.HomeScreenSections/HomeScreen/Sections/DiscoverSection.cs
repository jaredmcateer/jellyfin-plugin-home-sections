using Jellyfin.Plugin.HomeScreenSections.Configuration;
using Jellyfin.Plugin.HomeScreenSections.Helpers;
using Jellyfin.Plugin.HomeScreenSections.Library;
using Jellyfin.Plugin.HomeScreenSections.Model.Dto;
using Jellyfin.Plugin.HomeScreenSections.Model.Jellyseerr;
using Jellyfin.Plugin.HomeScreenSections.Services;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.HomeScreenSections.HomeScreen.Sections;

public class DiscoverSection(
    IJellyseerrClient jellyseerrClient,
    ImageCacheService imageCacheService) : IHomeScreenSection
{
    public virtual string? Section => "Discover";

    public virtual string? DisplayText { get; set; } = "Discover";
    public int? Limit => 1;
    public string? Route => null;
    public string? AdditionalData { get; set; }
    public object? OriginalPayload { get; } = null;

    protected virtual string JellyseerEndpoint => "/api/v1/discover/trending";

    public QueryResult<BaseItemDto> GetResults(HomeScreenSectionPayload payload, IQueryCollection queryCollection)
    {
        List<BaseItemDto> returnItems = [];
        string? jellyseerrUrl = HomeScreenSectionsPlugin.Instance.Configuration.JellyseerrUrl;
        string? jellyseerrExternalUrl = HomeScreenSectionsPlugin.Instance.Configuration.JellyseerrExternalUrl;
        string? jellyseerrDisplayUrl = !string.IsNullOrEmpty(jellyseerrExternalUrl) ? jellyseerrExternalUrl : jellyseerrUrl;

        if (string.IsNullOrEmpty(jellyseerrUrl) || string.IsNullOrEmpty(jellyseerrDisplayUrl))
        {
            return new QueryResult<BaseItemDto>();
        }

        string[] preferredLanguages = HomeScreenSectionsPlugin.Instance.Configuration.JellyseerrPreferredLanguages?
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

        int page = 1;
        do
        {
            IReadOnlyList<JellyseerrDiscoverItem> pageItems = jellyseerrClient
                .GetDiscoverPageAsync(payload.UserId, JellyseerEndpoint, page)
                .GetAwaiter()
                .GetResult();

            foreach (JellyseerrDiscoverItem item in pageItems)
            {
                if (preferredLanguages.Length > 0 &&
                    !preferredLanguages.Contains(item.OriginalLanguage))
                {
                    continue;
                }

                string dateTimeString = item.ReleaseDate ?? "1970-01-01";
                if (string.IsNullOrWhiteSpace(dateTimeString))
                {
                    dateTimeString = "1970-01-01";
                }

                string posterPath = item.PosterPath ?? "404";
                string cachedImageUrl = GetCachedImageUrl($"https://image.tmdb.org/t/p/w600_and_h900_bestv2{posterPath}");

                returnItems.Add(new BaseItemDto
                {
                    Name = item.Name,
                    OriginalTitle = item.OriginalName,
                    SourceType = item.MediaType,
                    CommunityRating = item.CommunityRating,
                    ProviderIds = new Dictionary<string, string>
                    {
                        { "JellyseerrRoot", jellyseerrDisplayUrl },
                        { "Jellyseerr", item.Id.ToString() },
                        { "JellyseerrPoster", cachedImageUrl }
                    },
                    PremiereDate = DateTime.Parse(dateTimeString)
                });
            }

            if (pageItems.Count == 0)
            {
                break;
            }

            page++;
        }
        while (returnItems.Count < 20);

        return new QueryResult<BaseItemDto>
        {
            Items = returnItems,
            StartIndex = 0,
            TotalRecordCount = returnItems.Count
        };
    }

    protected string GetCachedImageUrl(string sourceUrl)
    {
        return ImageCacheHelper.GetCachedImageUrl(imageCacheService, sourceUrl);
    }

    public IEnumerable<IHomeScreenSection> CreateInstances(Guid? userId, int instanceCount)
    {
        yield return this;
    }

    public HomeScreenSectionInfo GetInfo()
    {
        return new HomeScreenSectionInfo
        {
            Section = Section,
            DisplayText = DisplayText,
            AdditionalData = AdditionalData,
            Route = Route,
            Limit = Limit ?? 1,
            OriginalPayload = OriginalPayload,
            ViewMode = SectionViewMode.Portrait,
            AllowViewModeChange = false
        };
    }
}
