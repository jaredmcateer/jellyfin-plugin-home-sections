using System.Net;
using System.Reflection;
using System.Runtime.CompilerServices;
using Jellyfin.Database.Implementations.Entities;
using Jellyfin.Plugin.HomeScreenSections.Configuration;
using Jellyfin.Plugin.HomeScreenSections.Model;
using Jellyfin.Plugin.HomeScreenSections.Model.Jellyseerr;
using Jellyfin.Plugin.HomeScreenSections.Services;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Jellyfin.Plugin.HomeScreenSections.Tests;

public class JellyseerrClientTests
{
    private static readonly Guid TestUserId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    [Fact]
    public async Task GetDiscoverPageAsync_WhenUnconfigured_ReturnsEmpty()
    {
        using JellyseerrTestContext context = CreateContext(
            new PluginConfiguration(),
            new StubHttpHandler(),
            CreateUser("alice"));

        IReadOnlyList<JellyseerrDiscoverItem> items = await context.Client.GetDiscoverPageAsync(
            TestUserId,
            "/api/v1/discover/trending",
            page: 1);

        Assert.Empty(items);
    }

    [Fact]
    public async Task GetDiscoverPageAsync_ParsesRequestableDiscoverItems()
    {
        StubHttpHandler handler = new();
        handler.EnqueueUserLookup("alice", jellyseerrUserId: 7);
        handler.Enqueue(_ => JsonResponse("""
            {
              "results": [
                {
                  "id": 42,
                  "mediaType": "movie",
                  "title": "Test Movie",
                  "originalTitle": "Test Movie Original",
                  "originalLanguage": "en",
                  "posterPath": "/poster.jpg",
                  "releaseDate": "2024-01-02",
                  "vote_average": 7.5,
                  "adult": false
                },
                {
                  "id": 99,
                  "mediaType": "movie",
                  "title": "Already Available",
                  "mediaInfo": { "id": 1 },
                  "adult": false
                },
                {
                  "id": 100,
                  "mediaType": "movie",
                  "title": "Adult Movie",
                  "adult": true
                }
              ]
            }
            """));

        using JellyseerrTestContext context = CreateContext(ConfiguredPluginConfiguration(), handler, CreateUser("alice"));

        IReadOnlyList<JellyseerrDiscoverItem> items = await context.Client.GetDiscoverPageAsync(
            TestUserId,
            "/api/v1/discover/movies",
            page: 1);

        JellyseerrDiscoverItem item = Assert.Single(items);
        Assert.Equal(42, item.Id);
        Assert.Equal("movie", item.MediaType);
        Assert.Equal("Test Movie", item.Name);
        Assert.Equal("Test Movie Original", item.OriginalName);
        Assert.Equal("en", item.OriginalLanguage);
        Assert.Equal("/poster.jpg", item.PosterPath);
        Assert.Equal("2024-01-02", item.ReleaseDate);
        Assert.Equal(7.5f, item.CommunityRating);
    }

    [Fact]
    public async Task GetUserRequestsAsync_ParsesJellyfinMediaIds()
    {
        StubHttpHandler handler = new();
        handler.EnqueueUserLookup("alice", jellyseerrUserId: 7);
        handler.Enqueue(_ => JsonResponse("""
            {
              "results": [
                { "media": { "jellyfinMediaId": "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee" } },
                { "media": { "jellyfinMediaId": null } }
              ]
            }
            """));

        using JellyseerrTestContext context = CreateContext(ConfiguredPluginConfiguration(), handler, CreateUser("alice"));

        IReadOnlyList<JellyseerrRequestMedia> items = await context.Client.GetUserRequestsAsync(TestUserId, take: 100);

        JellyseerrRequestMedia item = Assert.Single(items);
        Assert.Equal("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", item.JellyfinMediaId);
    }

    [Fact]
    public async Task SubmitRequestAsync_WhenUnconfigured_ReturnsNotConfigured()
    {
        using JellyseerrTestContext context = CreateContext(
            new PluginConfiguration(),
            new StubHttpHandler(),
            CreateUser("alice"));

        JellyseerrSubmitResult? result = await context.Client.SubmitRequestAsync(
            TestUserId,
            new DiscoverRequestPayload { MediaId = 1, MediaType = "movie" });

        Assert.NotNull(result);
        Assert.False(result!.IsConfigured);
        Assert.False(result.UserResolved);
    }

    [Fact]
    public async Task SubmitRequestAsync_WhenUserUnresolved_ReturnsBadRequestSignal()
    {
        StubHttpHandler handler = new();
        handler.Enqueue(_ => JsonResponse("""{ "results": [] }"""));

        using JellyseerrTestContext context = CreateContext(ConfiguredPluginConfiguration(), handler, CreateUser("alice"));

        JellyseerrSubmitResult? result = await context.Client.SubmitRequestAsync(
            TestUserId,
            new DiscoverRequestPayload { MediaId = 1, MediaType = "movie" });

        Assert.NotNull(result);
        Assert.True(result!.IsConfigured);
        Assert.False(result.UserResolved);
    }

    [Fact]
    public async Task SubmitRequestAsync_PassthroughResponseBody()
    {
        StubHttpHandler handler = new();
        handler.EnqueueUserLookup("alice", jellyseerrUserId: 7);
        handler.Enqueue(_ => JsonResponse("""{ "requestId": 123 }""", HttpStatusCode.Created, "application/json; charset=utf-8"));

        using JellyseerrTestContext context = CreateContext(ConfiguredPluginConfiguration(), handler, CreateUser("alice"));

        JellyseerrSubmitResult? result = await context.Client.SubmitRequestAsync(
            TestUserId,
            new DiscoverRequestPayload { MediaId = 1, MediaType = "movie" });

        Assert.NotNull(result);
        Assert.True(result!.IsConfigured);
        Assert.True(result.UserResolved);
        Assert.Contains("requestId", result.Content, StringComparison.Ordinal);
        Assert.Equal("application/json", result.ContentType);
        Assert.Equal(201, result.StatusCode);
    }

    private static JellyseerrTestContext CreateContext(
        PluginConfiguration configuration,
        StubHttpHandler handler,
        User user)
    {
        SetPluginConfiguration(configuration);
        HttpClient httpClient = new(handler, disposeHandler: true);
        Mock<IUserManager> userManager = new();
        userManager.Setup(m => m.GetUserById(TestUserId)).Returns(user);
        JellyseerrClient client = new(NullLogger<JellyseerrClient>.Instance, httpClient, userManager.Object);
        return new JellyseerrTestContext(client);
    }

    private static PluginConfiguration ConfiguredPluginConfiguration() => new()
    {
        JellyseerrUrl = "http://jellyseerr.test",
        JellyseerrApiKey = "test-api-key"
    };

    private static User CreateUser(string username)
    {
        User user = new(username, "jellyfin", Guid.NewGuid().ToString())
        {
            Id = TestUserId
        };
        return user;
    }

    private static void SetPluginConfiguration(PluginConfiguration configuration)
    {
        HomeScreenSectionsPlugin plugin = (HomeScreenSectionsPlugin)RuntimeHelpers.GetUninitializedObject(typeof(HomeScreenSectionsPlugin));
        typeof(BasePlugin<PluginConfiguration>)
            .GetProperty(nameof(BasePlugin<PluginConfiguration>.Configuration))!
            .SetValue(plugin, configuration);

        PropertyInfo instanceProperty = typeof(HomeScreenSectionsPlugin)
            .GetProperty(nameof(HomeScreenSectionsPlugin.Instance), BindingFlags.Public | BindingFlags.Static)!;
        instanceProperty.SetValue(null, plugin);
    }

    private static HttpResponseMessage JsonResponse(string json, HttpStatusCode statusCode = HttpStatusCode.OK, string contentType = "application/json")
    {
        HttpResponseMessage response = new(statusCode)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, contentType.Split(';')[0].Trim())
        };
        return response;
    }

    private sealed class JellyseerrTestContext(JellyseerrClient client) : IDisposable
    {
        public JellyseerrClient Client { get; } = client;

        public void Dispose()
        {
            typeof(HomeScreenSectionsPlugin)
                .GetProperty(nameof(HomeScreenSectionsPlugin.Instance), BindingFlags.Public | BindingFlags.Static)!
                .SetValue(null, null);
        }
    }

    private sealed class StubHttpHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responses = new();

        public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
        {
            _responses.Enqueue(responseFactory);
        }

        public void EnqueueUserLookup(string jellyfinUsername, int jellyseerrUserId)
        {
            Enqueue(_ => JsonResponse($$"""
                {
                  "results": [
                    { "id": {{jellyseerrUserId}}, "jellyfinUsername": "{{jellyfinUsername}}" }
                  ]
                }
                """));
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (_responses.Count == 0)
            {
                throw new InvalidOperationException($"Unexpected HTTP request: {request.Method} {request.RequestUri}");
            }

            return Task.FromResult(_responses.Dequeue()(request));
        }
    }
}
