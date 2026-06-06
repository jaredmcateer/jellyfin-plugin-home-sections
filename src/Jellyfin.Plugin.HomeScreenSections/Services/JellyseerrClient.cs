using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Jellyfin.Plugin.HomeScreenSections.Configuration;
using Jellyfin.Plugin.HomeScreenSections.Library;
using Jellyfin.Plugin.HomeScreenSections.Model;
using Jellyfin.Plugin.HomeScreenSections.Model.Jellyseerr;
using MediaBrowser.Controller.Library;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HomeScreenSections.Services;

public class JellyseerrClient(
    ILogger<JellyseerrClient> logger,
    HttpClient httpClient,
    IUserManager userManager) : IJellyseerrClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static PluginConfiguration Config =>
        HomeScreenSectionsPlugin.Instance?.Configuration ?? new PluginConfiguration();

    public async Task<IReadOnlyList<JellyseerrDiscoverItem>> GetDiscoverPageAsync(
        Guid jellyfinUserId,
        string discoverEndpoint,
        int page,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetApiConfig(out string baseUrl, out string apiKey))
        {
            return [];
        }

        int? jellyseerrUserId = await ResolveJellyseerrUserIdAsync(jellyfinUserId, baseUrl, apiKey, cancellationToken)
            .ConfigureAwait(false);
        if (jellyseerrUserId == null)
        {
            return [];
        }

        string requestPath = $"{discoverEndpoint.TrimStart('/')}?page={page}";
        using HttpRequestMessage request = CreateGetRequest(baseUrl, requestPath, apiKey, jellyseerrUserId.Value);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch Jellyseerr discover page from {Endpoint}", discoverEndpoint);
            return [];
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Jellyseerr discover request failed. Endpoint: {Endpoint}, Status: {StatusCode}",
                discoverEndpoint,
                response.StatusCode);
            return [];
        }

        string jsonRaw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ParseDiscoverPage(jsonRaw);
    }

    public async Task<IReadOnlyList<JellyseerrRequestMedia>> GetUserRequestsAsync(
        Guid jellyfinUserId,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetApiConfig(out string baseUrl, out string apiKey))
        {
            return [];
        }

        int? jellyseerrUserId = await ResolveJellyseerrUserIdAsync(jellyfinUserId, baseUrl, apiKey, cancellationToken)
            .ConfigureAwait(false);
        if (jellyseerrUserId == null)
        {
            return [];
        }

        using HttpRequestMessage request = CreateGetRequest(
            baseUrl,
            $"/api/v1/user/{jellyseerrUserId}/requests?take={take}",
            apiKey,
            jellyseerrUserId.Value);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to fetch Jellyseerr user requests for user {UserId}", jellyfinUserId);
            return [];
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Jellyseerr user requests failed for user {UserId}. Status: {StatusCode}",
                jellyfinUserId,
                response.StatusCode);
            return [];
        }

        string jsonRaw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ParseUserRequests(jsonRaw);
    }

    public async Task<JellyseerrSubmitResult?> SubmitRequestAsync(
        Guid jellyfinUserId,
        DiscoverRequestPayload payload,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetApiConfig(out string baseUrl, out string apiKey))
        {
            return new JellyseerrSubmitResult
            {
                IsConfigured = false,
                UserResolved = false
            };
        }

        int? jellyseerrUserId = await ResolveJellyseerrUserIdAsync(jellyfinUserId, baseUrl, apiKey, cancellationToken)
            .ConfigureAwait(false);
        if (jellyseerrUserId == null)
        {
            return new JellyseerrSubmitResult
            {
                IsConfigured = true,
                UserResolved = false
            };
        }

        using HttpRequestMessage request = new(HttpMethod.Post, BuildUri(baseUrl, "/api/v1/request"));
        request.Headers.Add("X-Api-Key", apiKey);
        request.Headers.Add("X-Api-User", jellyseerrUserId.Value.ToString());

        if (payload.MediaType == "tv")
        {
            request.Content = JsonContent.Create(new JellyseerrTvShowRequestPayload
            {
                MediaId = payload.MediaId,
                MediaType = payload.MediaType,
                Seasons = "all"
            });
        }
        else
        {
            request.Content = JsonContent.Create(new JellyseerrRequestPayload
            {
                MediaId = payload.MediaId,
                MediaType = payload.MediaType
            });
        }

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to submit Jellyseerr request for user {UserId}", jellyfinUserId);
            return new JellyseerrSubmitResult
            {
                IsConfigured = true,
                UserResolved = true,
                Content = ex.Message,
                ContentType = "text/plain",
                StatusCode = 500
            };
        }

        string content = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        string contentType = response.Content.Headers.ContentType?.MediaType ?? "application/json";

        return new JellyseerrSubmitResult
        {
            IsConfigured = true,
            UserResolved = true,
            Content = content,
            ContentType = contentType,
            StatusCode = (int)response.StatusCode
        };
    }

    private async Task<int?> ResolveJellyseerrUserIdAsync(
        Guid jellyfinUserId,
        string baseUrl,
        string apiKey,
        CancellationToken cancellationToken)
    {
        User? user = userManager.GetUserById(jellyfinUserId);
        if (user == null)
        {
            logger.LogWarning("Jellyfin user {UserId} not found for Jellyseerr lookup", jellyfinUserId);
            return null;
        }

        using HttpRequestMessage request = CreateGetRequest(
            baseUrl,
            $"/api/v1/user?q={Uri.EscapeDataString(user.Username)}",
            apiKey,
            jellyseerrUserId: null);

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resolve Jellyseerr user for {Username}", user.Username);
            return null;
        }

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning(
                "Jellyseerr user lookup failed for {Username}. Status: {StatusCode}",
                user.Username,
                response.StatusCode);
            return null;
        }

        string jsonRaw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        JellyseerrUserSearchResponse? searchResponse = JsonSerializer.Deserialize<JellyseerrUserSearchResponse>(jsonRaw, JsonOptions);
        return searchResponse?.Results?
            .FirstOrDefault(x => string.Equals(x.JellyfinUsername, user.Username, StringComparison.Ordinal))?
            .Id;
    }

    private static bool TryGetApiConfig(out string baseUrl, out string apiKey)
    {
        baseUrl = Config.JellyseerrUrl ?? string.Empty;
        apiKey = Config.JellyseerrApiKey ?? string.Empty;
        return !string.IsNullOrEmpty(baseUrl) && !string.IsNullOrEmpty(apiKey);
    }

    private static HttpRequestMessage CreateGetRequest(
        string baseUrl,
        string path,
        string apiKey,
        int? jellyseerrUserId)
    {
        HttpRequestMessage request = new(HttpMethod.Get, BuildUri(baseUrl, path));
        request.Headers.Add("X-Api-Key", apiKey);
        if (jellyseerrUserId != null)
        {
            request.Headers.Add("X-Api-User", jellyseerrUserId.Value.ToString());
        }

        return request;
    }

    private static Uri BuildUri(string baseUrl, string path)
    {
        string normalizedPath = path.StartsWith('/') ? path : $"/{path}";
        return new Uri($"{baseUrl.TrimEnd('/')}{normalizedPath}");
    }

    private static IReadOnlyList<JellyseerrDiscoverItem> ParseDiscoverPage(string jsonRaw)
    {
        List<JellyseerrDiscoverItem> items = [];
        using JsonDocument document = JsonDocument.Parse(jsonRaw);
        if (!document.RootElement.TryGetProperty("results", out JsonElement results) ||
            results.ValueKind != JsonValueKind.Array)
        {
            return items;
        }

        foreach (JsonElement item in results.EnumerateArray())
        {
            if (item.TryGetProperty("adult", out JsonElement adultElement) &&
                adultElement.ValueKind == JsonValueKind.True)
            {
                continue;
            }

            if (item.TryGetProperty("mediaInfo", out JsonElement mediaInfoElement) &&
                mediaInfoElement.ValueKind != JsonValueKind.Null)
            {
                continue;
            }

            string? releaseDate = GetStringProperty(item, "firstAirDate") ?? GetStringProperty(item, "releaseDate");
            float? rating = GetFloatProperty(item, "vote_average") ?? GetFloatProperty(item, "voteAverage");

            items.Add(new JellyseerrDiscoverItem
            {
                Id = item.GetProperty("id").GetInt32(),
                MediaType = GetStringProperty(item, "mediaType"),
                Name = GetStringProperty(item, "title") ?? GetStringProperty(item, "name"),
                OriginalName = GetStringProperty(item, "originalTitle") ?? GetStringProperty(item, "originalName"),
                OriginalLanguage = GetStringProperty(item, "originalLanguage"),
                PosterPath = GetStringProperty(item, "posterPath"),
                ReleaseDate = releaseDate,
                CommunityRating = rating > 0 ? rating : null
            });
        }

        return items;
    }

    private static IReadOnlyList<JellyseerrRequestMedia> ParseUserRequests(string jsonRaw)
    {
        List<JellyseerrRequestMedia> items = [];
        using JsonDocument document = JsonDocument.Parse(jsonRaw);
        if (!document.RootElement.TryGetProperty("results", out JsonElement results) ||
            results.ValueKind != JsonValueKind.Array)
        {
            return items;
        }

        foreach (JsonElement item in results.EnumerateArray())
        {
            if (!item.TryGetProperty("media", out JsonElement mediaElement) ||
                mediaElement.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            string? jellyfinMediaId = GetStringProperty(mediaElement, "jellyfinMediaId");
            if (string.IsNullOrEmpty(jellyfinMediaId))
            {
                continue;
            }

            items.Add(new JellyseerrRequestMedia
            {
                JellyfinMediaId = jellyfinMediaId
            });
        }

        return items;
    }

    private static string? GetStringProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value))
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static float? GetFloatProperty(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetSingle(out float single))
        {
            return single;
        }

        return null;
    }

    private sealed class JellyseerrUserSearchResponse
    {
        [JsonPropertyName("results")]
        public List<JellyseerrUserResult>? Results { get; set; }
    }

    private sealed class JellyseerrUserResult
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("jellyfinUsername")]
        public string? JellyfinUsername { get; set; }
    }
}
