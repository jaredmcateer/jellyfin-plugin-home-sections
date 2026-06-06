using Jellyfin.Plugin.HomeScreenSections.Configuration;
using Jellyfin.Plugin.HomeScreenSections.Helpers;
using Jellyfin.Plugin.HomeScreenSections.JellyfinVersionSpecific;
using Jellyfin.Plugin.HomeScreenSections.Library;
using Jellyfin.Plugin.HomeScreenSections.Model.Dto;
using Jellyfin.Plugin.HomeScreenSections.Model.Jellyseerr;
using MediaBrowser.Controller.Dto;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Http;

namespace Jellyfin.Plugin.HomeScreenSections.HomeScreen.Sections;

public class MyRequestsSection(
    IJellyseerrClient jellyseerrClient,
    IUserManager userManager,
    ILibraryManager libraryManager,
    IDtoService dtoService) : IHomeScreenSection
{
    public string? Section => "MyJellyseerrRequests";

    public string? DisplayText { get; set; } = "My Requests";

    public int? Limit => 1;

    public string? Route => null;

    public string? AdditionalData { get; set; } = null;

    public object? OriginalPayload { get; } = null;

    public QueryResult<BaseItemDto> GetResults(HomeScreenSectionPayload payload, IQueryCollection queryCollection)
    {
        DtoOptions dtoOptions = new()
        {
            Fields =
            [
                ItemFields.PrimaryImageAspectRatio,
                ItemFields.MediaSourceCount
            ]
        };

        if (string.IsNullOrEmpty(HomeScreenSectionsPlugin.Instance.Configuration.JellyseerrUrl))
        {
            return new QueryResult<BaseItemDto>();
        }

        User? user = userManager.GetUserById(payload.UserId);
        if (user == null)
        {
            return new QueryResult<BaseItemDto>();
        }

        IReadOnlyList<JellyseerrRequestMedia> requestedMedia = jellyseerrClient
            .GetUserRequestsAsync(payload.UserId, take: 100)
            .GetAwaiter()
            .GetResult();

        if (requestedMedia.Count == 0)
        {
            return new QueryResult<BaseItemDto>();
        }

        VirtualFolderInfo[] folders = libraryManager.GetVirtualFolders()
            .FilterToUserPermitted(libraryManager, user);

        IEnumerable<string?> jellyfinItemIds = requestedMedia.Select(x => x.JellyfinMediaId);

        PluginConfiguration? config = HomeScreenSectionsPlugin.Instance?.Configuration;
        SectionSettings? sectionSettings = config?.SectionSettings.FirstOrDefault(x => x.SectionId == Section);
        bool hideWatchedItems = sectionSettings?.HideWatchedItems == true;

        IEnumerable<BaseItem> items = folders.SelectMany(x =>
        {
            return libraryManager.GetItemList(new InternalItemsQuery(user)
            {
                ItemIds = jellyfinItemIds.Select(y => Guid.Parse(y ?? Guid.Empty.ToString())).ToArray(),
                Recursive = true,
                EnableTotalRecordCount = false,
                ParentId = Guid.Parse(x.ItemId ?? Guid.Empty.ToString())
            });
        }).OrderByDescending(item => item.DateCreated);

        if (hideWatchedItems)
        {
            items = items.Where(item => !item.IsPlayedVersionSpecific(user));
        }

        return new QueryResult<BaseItemDto>(dtoService.GetBaseItemDtos(items.Take(16).ToArray(), dtoOptions, user));
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
            ViewMode = SectionViewMode.Landscape,
            AllowViewModeChange = true,
            AllowHideWatched = true
        };
    }
}
