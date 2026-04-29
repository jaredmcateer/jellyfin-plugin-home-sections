using System.Text.Json.Serialization;
using System.Xml.Serialization;
using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.HomeScreenSections.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        /// <summary>
        /// Configuration version for tracking migrations.
        /// </summary>
        public string ConfigVersion { get; set; } = string.Empty;

        public bool Enabled { get; set; } = false;

        public bool AllowUserOverride { get; set; } = true;

        public string? LibreTranslateUrl { get; set; } = "";

        public string? LibreTranslateApiKey { get; set; } = "";
        
        public string? JellyseerrUrl { get; set; } = "";

        public string? JellyseerrApiKey { get; set; } = "";
        
        public string? JellyseerrPreferredLanguages { get; set; } = "en";
        
        public string? DefaultMoviesLibraryId { get; set; } = "";
        
        public string? DefaultTvShowsLibraryId { get; set; } = "";
        
        public string? DefaultMusicLibraryId { get; set; } = "";
        
        public string? DefaultBooksLibraryId { get; set; } = "";
        
        public string? DefaultMusicVideosLibraryId { get; set; } = "";

        public ArrConfig Sonarr { get; set; } = new ArrConfig { UpcomingTimeframeValue = 1, UpcomingTimeframeUnit = TimeframeUnit.Weeks };

        public ArrConfig Radarr { get; set; } = new ArrConfig { UpcomingTimeframeValue = 3, UpcomingTimeframeUnit = TimeframeUnit.Months };

        public ArrConfig Lidarr { get; set; } = new ArrConfig { UpcomingTimeframeValue = 6, UpcomingTimeframeUnit = TimeframeUnit.Months };

        public ArrConfig Readarr { get; set; } = new ArrConfig { UpcomingTimeframeValue = 1, UpcomingTimeframeUnit = TimeframeUnit.Years };

        public string DateFormat { get; set; } = "YYYY/MM/DD";

        public string DateDelimiter { get; set; } = "/";
        
        public bool DeveloperMode { get; set; } = false;

        public int CacheBustCounter { get; set; } = 0;

        public int CacheTimeoutSeconds { get; set; } = 86400;

        public bool OverrideStreamyfinHome { get; set; } = false;

        public SectionSettings[] SectionSettings { get; set; } = Array.Empty<SectionSettings>();
        
        public ExperimentalSettings Experimental { get; set; } = new ExperimentalSettings();
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SectionViewMode
    {
        Portrait,
        Landscape,
        Square,
        Small
    }

    public enum TimeframeUnit
    {
        Days,
        Weeks,
        Months,
        Years
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PluginConfigurationType
    {
        Checkbox,
        Dropdown,
        TextBox,
        NumberBox
    }

    public class PluginConfigurationOption
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public PluginConfigurationType Type { get; set; }
        public bool AllowUserOverride { get; set; } = false;
        public bool IsAdvanced { get; set; } = false;
        public object? DefaultValue { get; set; }
        public string[]? DropdownOptions { get; set; }
        public string[]? DropdownLabels { get; set; }
        
        public string? Placeholder { get; set; }
        public int? MinLength { get; set; }
        public int? MaxLength { get; set; }
        public double? MinValue { get; set; }
        public double? MaxValue { get; set; }
        public double? Step { get; set; }
        public string? Pattern { get; set; }
        public string? ValidationMessage { get; set; }
        public bool Required { get; set; } = false;
    }
    
    [XmlType("PluginConfigurationEntry")]
    public class PluginConfigurationEntry
    {
        [XmlAttribute("Key")]
        public string Key { get; set; } = string.Empty;
        
        [XmlAttribute("Value")]
        public string? Value { get; set; }
        
        [XmlAttribute("Type")]
        public string Type { get; set; } = "string";
        
        [XmlAttribute("AllowUserOverride")]
        public bool AllowUserOverride { get; set; } = true;
    }
    
    public class SectionSettings
    {
        public string SectionId { get; set; } = string.Empty;
        
        public bool Enabled { get; set; }
        
        public bool AllowUserOverride { get; set; }
        
        public int LowerLimit { get; set; }
        
        public int UpperLimit { get; set; }

        public int OrderIndex { get; set; }
        
        public SectionViewMode ViewMode { get; set; } = SectionViewMode.Landscape;

        // Deprecated
        public bool HideWatchedItems { get; set; } = false;

        [XmlArray("PluginConfigurations")]
        [XmlArrayItem("Entry")]
        public PluginConfigurationEntry[] PluginConfigurations { get; set; } = Array.Empty<PluginConfigurationEntry>();
        
        public T? GetAdminConfig<T>(string key, T? defaultValue = default)
        {
            var entry = PluginConfigurations?.FirstOrDefault(x => x.Key == key);
            if (entry?.Value == null) 
            {
                return defaultValue;
            }
            
            try
            {
                var result = entry.Type.ToLower() switch
                {
                    "boolean" or "bool" or "checkbox" => (T)(object)bool.Parse(entry.Value),
                    "integer" or "int32" or "int" => (T)(object)int.Parse(entry.Value),
                    "double" or "number" or "numberbox" => (T)(object)double.Parse(entry.Value),
                    "decimal" => (T)(object)decimal.Parse(entry.Value),
                    "string" => (T)(object)entry.Value,
                    _ => ConvertValue<T>(entry.Value, defaultValue)
                };
                
                return result;
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// Helper method to convert values when type doesn't match expected patterns.
        /// </summary>
        private static T? ConvertValue<T>(string value, T? defaultValue)
        {
            try
            {
                return (T?)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
        
        public void SetAdminConfig<T>(string key, T? value)
        {
            var entries = PluginConfigurations?.ToList() ?? new List<PluginConfigurationEntry>();
            var existingEntry = entries.FirstOrDefault(x => x.Key == key);
            
            string typeName = typeof(T).Name.ToLower() switch
            {
                "boolean" => "boolean",
                "int32" => "integer",
                "double" => "double",
                "decimal" => "decimal",
                "string" => "string",
                _ => typeof(T).Name.ToLower()
            };
            
            if (existingEntry != null)
            {
                existingEntry.Value = value?.ToString();
                existingEntry.Type = typeName;
            }
            else
            {
                entries.Add(new PluginConfigurationEntry
                {
                    Key = key,
                    Value = value?.ToString(),
                    Type = typeName
                });
            }
            
            PluginConfigurations = entries.ToArray();
            m_configLookup = null;
        }
        
        [XmlIgnore]
        private Dictionary<string, PluginConfigurationEntry>? m_configLookup;
        
        [XmlIgnore]
        private Dictionary<string, PluginConfigurationEntry> ConfigLookup => 
            m_configLookup ??= PluginConfigurations?.ToDictionary(x => x.Key, StringComparer.OrdinalIgnoreCase) ?? new Dictionary<string, PluginConfigurationEntry>(StringComparer.OrdinalIgnoreCase);
        
        /// <summary>
        /// Gets whether a configuration option allows user override.
        /// </summary>
        public bool IsUserOverrideAllowedUnified(string key)
        {
            return ConfigLookup.TryGetValue(key, out var entry) && entry.AllowUserOverride;
        }
        
        /// <summary>
        /// Gets configuration value and permission in a single operation.
        /// </summary>
        public (T? value, bool allowOverride) GetConfigWithPermission<T>(string key, T? defaultValue = default)
        {
            if (!ConfigLookup.TryGetValue(key, out var entry))
                return (defaultValue, false);
            
            try
            {
                T? value = defaultValue;
                if (entry.Value != null)
                {
                    value = entry.Type.ToLower() switch
                    {
                        "boolean" or "bool" or "checkbox" => (T)(object)bool.Parse(entry.Value),
                        "integer" or "int32" or "int" => (T)(object)int.Parse(entry.Value),
                        "double" or "number" => (T)(object)double.Parse(entry.Value),
                        "decimal" => (T)(object)decimal.Parse(entry.Value),
                        "string" => (T)(object)entry.Value,
                        _ => ConvertValue<T>(entry.Value, defaultValue)
                    };
                }
                
                return (value, entry.AllowUserOverride);
            }
            catch
            {
                return (defaultValue, entry.AllowUserOverride);
            }
        }
        
        /// <summary>
        /// Sets configuration value with user override permission.
        /// </summary>
        public void SetAdminConfigWithPermission<T>(string key, T? value, bool allowUserOverride = true)
        {
            var entries = PluginConfigurations?.ToList() ?? new List<PluginConfigurationEntry>();
            var existingEntry = entries.FirstOrDefault(x => x.Key == key);
            
            string typeName = typeof(T).Name.ToLower() switch
            {
                "boolean" => "boolean",
                "int32" => "integer",
                "double" => "double",
                "decimal" => "decimal",
                "string" => "string",
                _ => typeof(T).Name.ToLower()
            };
            
            if (existingEntry != null)
            {
                existingEntry.Value = value?.ToString();
                existingEntry.Type = typeName;
                existingEntry.AllowUserOverride = allowUserOverride;
            }
            else
            {
                entries.Add(new PluginConfigurationEntry
                {
                    Key = key,
                    Value = value?.ToString(),
                    Type = typeName,
                    AllowUserOverride = allowUserOverride
                });
            }
            
            PluginConfigurations = entries.ToArray();
            m_configLookup = null;
        }

        /// <summary>
        /// Gets whether the section is enabled by admin configuration.
        /// </summary>
        public bool IsEnabledByAdmin()
        {
            return GetAdminConfig<bool>("Enabled", true);
        }
    }
    
    public class ArrConfig
    {
        public string? ApiKey { get; set; } = "";
        public string? Url { get; set; } = "";
        public int UpcomingTimeframeValue { get; set; }
        public TimeframeUnit UpcomingTimeframeUnit { get; set; }
    }
    
    /// <summary>
    /// Experimental performance optimization flags.
    /// All flags default to false for safe upgrades.
    /// Activation requires both Master flag AND individual feature flag.
    /// </summary>
    public class ExperimentalSettings
    {
        /// <summary>
        /// Master switch for all experimental performance features.
        /// Individual features also require their specific flag to be enabled.
        /// </summary>
        public bool EnableExperimentalPerformance { get; set; } = false;
        
        /// <summary>
        /// Use async image caching to avoid blocking request threads during image downloads and processing.
        /// </summary>
        public bool UseAsyncImageCaching { get; set; } = false;
        
        /// <summary>
        /// Replace busy-wait polling with awaitable completion signaling in section loading.
        /// </summary>
        public bool UseSectionCompletionSignaling { get; set; } = false;
        
        /// <summary>
        /// Reuse HttpClient instances instead of creating new ones per request.
        /// </summary>
        public bool UseHttpClientReuse { get; set; } = false;
        
        /// <summary>
        /// Debounce image cache index writes instead of writing after every image cache.
        /// </summary>
        public bool UseDebouncedImageCacheIndexWrites { get; set; } = false;
        
        /// <summary>
        /// Execute ContinueWatching and NextUp queries in parallel instead of sequentially.
        /// </summary>
        public bool UseParallelContinueWatchingNextUp { get; set; } = false;
        
        /// <summary>
        /// Cache expensive section results with TTL-based invalidation.
        /// </summary>
        public bool UseSectionResultCaching { get; set; } = false;
        
        /// <summary>
        /// Throttle concurrent section requests from the client to reduce burst load.
        /// </summary>
        public bool UseClientSectionRequestThrottling { get; set; } = false;
        
        /// <summary>
        /// Helper to check if a feature is effectively enabled (requires master flag + feature flag).
        /// </summary>
        public bool IsFeatureEnabled(bool featureFlag)
        {
            return EnableExperimentalPerformance && featureFlag;
        }
    }
}