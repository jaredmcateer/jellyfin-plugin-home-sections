using Jellyfin.Plugin.HomeScreenSections.HomeScreen.Sections;
using Jellyfin.Plugin.HomeScreenSections.HomeScreen.Sections.Latest;
using Jellyfin.Plugin.HomeScreenSections.HomeScreen.Sections.Persons;
using Jellyfin.Plugin.HomeScreenSections.HomeScreen.Sections.RecentlyAdded;
using Jellyfin.Plugin.HomeScreenSections.HomeScreen.Sections.Upcoming;
using Jellyfin.Plugin.HomeScreenSections.Library;
using Jellyfin.Plugin.HomeScreenSections.Model.Dto;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.HomeScreenSections.HomeScreen;

/// <summary>
/// Manager for the Modular Home Screen section registry.
/// </summary>
public class HomeScreenManager(IServiceProvider serviceProvider) : IHomeScreenManager
{
    private readonly Dictionary<string, IHomeScreenSection> m_delegates = [];

    public void RegisterBuiltInResultsDelegates()
    {
        RegisterResultsDelegate<MyMediaSection>();

        RegisterResultsDelegate<ContinueWatchingSection>();
        RegisterResultsDelegate<NextUpSection>();
        RegisterResultsDelegate<ContinueWatchingNextUpSection>();

        RegisterResultsDelegate<RecentlyAddedMoviesSection>();
        RegisterResultsDelegate<RecentlyAddedShowsSection>();
        RegisterResultsDelegate<RecentlyAddedAlbumsSection>();
        RegisterResultsDelegate<RecentlyAddedArtistsSection>();
        RegisterResultsDelegate<RecentlyAddedBooksSection>();
        RegisterResultsDelegate<RecentlyAddedAudioBooksSection>();
        RegisterResultsDelegate<RecentlyAddedMusicVideosSection>();

        RegisterResultsDelegate<LatestMoviesSection>();
        RegisterResultsDelegate<LatestShowsSection>();
        RegisterResultsDelegate<LatestAlbumsSection>();
        RegisterResultsDelegate<LatestBooksSection>();
        RegisterResultsDelegate<LatestAudioBooksSection>();
        RegisterResultsDelegate<LatestMusicVideoSection>();

        RegisterResultsDelegate<BecauseYouWatchedSection>();
        RegisterResultsDelegate<LiveTvSection>();
        RegisterResultsDelegate<MyListSection>();
        RegisterResultsDelegate<WatchAgainSection>();

        RegisterResultsDelegate<DiscoverSection>();
        RegisterResultsDelegate<DiscoverMoviesSection>();
        RegisterResultsDelegate<DiscoverTvSection>();

        RegisterResultsDelegate<UpcomingShowsSection>();
        RegisterResultsDelegate<UpcomingMoviesSection>();
        RegisterResultsDelegate<UpcomingMusicSection>();
        RegisterResultsDelegate<UpcomingBooksSection>();

        RegisterResultsDelegate<GenreSection>();
        RegisterResultsDelegate<MyRequestsSection>();
    }

    public IEnumerable<IHomeScreenSection> GetSectionTypes() => m_delegates.Values;

    public IHomeScreenSection? GetSection(string sectionName) => m_delegates.GetValueOrDefault(sectionName);

    public QueryResult<BaseItemDto> InvokeResultsDelegate(string key, HomeScreenSectionPayload payload, IQueryCollection queryCollection)
    {
        if (m_delegates.TryGetValue(key, out IHomeScreenSection? section))
        {
            return section.GetResults(payload, queryCollection);
        }

        return new QueryResult<BaseItemDto>([]);
    }

    public void RegisterResultsDelegate<T>() where T : IHomeScreenSection
    {
        T handler = ActivatorUtilities.CreateInstance<T>(serviceProvider);
        RegisterResultsDelegate(handler);
    }

    public void RegisterResultsDelegate<T>(T handler) where T : IHomeScreenSection
    {
        if (handler.Section != null)
        {
            m_delegates[handler.Section] = handler;
        }
    }

    public void RegisterResultsDelegate(Type homeScreenSectionType)
    {
        IHomeScreenSection handler = (IHomeScreenSection)ActivatorUtilities.CreateInstance(serviceProvider, homeScreenSectionType);

        if (handler.Section == null)
        {
            return;
        }

        if (m_delegates.ContainsKey(handler.Section))
        {
            throw new Exception($"Section type '{handler.Section}' has already been registered to type '{m_delegates[handler.Section].GetType().FullName}'.");
        }

        m_delegates.Add(handler.Section, handler);
    }
}
