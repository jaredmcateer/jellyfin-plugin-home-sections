using System.Collections.Concurrent;
using Jellyfin.Plugin.HomeScreenSections.Configuration;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Querying;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HomeScreenSections.Services;

/// <summary>
/// Provides TTL-based caching for expensive section results.
/// Reduces load on external APIs (Jellyseerr, *arr services) and improves response times.
/// </summary>
public class SectionResultCache(ILogger<SectionResultCache> logger)
{
    private readonly ILogger<SectionResultCache> m_logger = logger;
    private readonly ConcurrentDictionary<string, CacheEntry> m_cache = new();
    private readonly TimeSpan m_defaultTtl = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets cached result if valid, otherwise returns null.
    /// </summary>
    public QueryResult<BaseItemDto>? GetCachedResult(string cacheKey)
    {
        if (!IsFeatureEnabled())
        {
            return null;
        }

        if (m_cache.TryGetValue(cacheKey, out CacheEntry? entry))
        {
            if (entry.ExpiresAt > DateTime.UtcNow)
            {
                m_logger.LogDebug("Cache hit for key: {CacheKey}", cacheKey);
                return entry.Result;
            }

            // Expired entry, remove it
            m_cache.TryRemove(cacheKey, out _);
            m_logger.LogDebug("Cache expired for key: {CacheKey}", cacheKey);
        }

        return null;
    }

    /// <summary>
    /// Stores result in cache with default TTL.
    /// </summary>
    public void CacheResult(string cacheKey, QueryResult<BaseItemDto> result)
    {
        CacheResult(cacheKey, result, m_defaultTtl);
    }

    /// <summary>
    /// Stores result in cache with custom TTL.
    /// </summary>
    public void CacheResult(string cacheKey, QueryResult<BaseItemDto> result, TimeSpan ttl)
    {
        if (!IsFeatureEnabled())
        {
            return;
        }

        CacheEntry entry = new()
        {
            Result = result,
            CachedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(ttl)
        };

        m_cache[cacheKey] = entry;
        m_logger.LogDebug("Cached result for key: {CacheKey}, TTL: {TTL}s", cacheKey, ttl.TotalSeconds);
    }

    /// <summary>
    /// Clears all cached entries.
    /// </summary>
    public void ClearCache()
    {
        int count = m_cache.Count;
        m_cache.Clear();
        m_logger.LogInformation("Cleared {Count} cached section results", count);
    }

    /// <summary>
    /// Removes expired entries from cache.
    /// </summary>
    public void CleanupExpired()
    {
        DateTime now = DateTime.UtcNow;
        List<string> expiredKeys = [.. m_cache
            .Where(kvp => kvp.Value.ExpiresAt <= now)
            .Select(kvp => kvp.Key)];

        foreach (string key in expiredKeys)
        {
            m_cache.TryRemove(key, out _);
        }

        if (expiredKeys.Count > 0)
        {
            m_logger.LogDebug("Cleaned up {Count} expired cache entries", expiredKeys.Count);
        }
    }

    /// <summary>
    /// Generates cache key for a section based on section type and user-independent parameters.
    /// </summary>
    public static string GenerateCacheKey(string sectionType, params object[] parameters)
    {
        string paramString = string.Join("_", parameters.Select(p => p?.ToString() ?? "null"));
        return $"{sectionType}_{paramString}";
    }

    private static bool IsFeatureEnabled()
    {
        PluginConfiguration? config = HomeScreenSectionsPlugin.Instance?.Configuration;
        return config?.Experimental?.IsFeatureEnabled(config.Experimental.UseSectionResultCaching) == true;
    }

    private class CacheEntry
    {
        public QueryResult<BaseItemDto> Result { get; set; } = new();
        public DateTime CachedAt { get; set; }
        public DateTime ExpiresAt { get; set; }
    }
}
