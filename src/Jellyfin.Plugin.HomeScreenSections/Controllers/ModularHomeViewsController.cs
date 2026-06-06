using Jellyfin.Plugin.HomeScreenSections.Configuration;
using Jellyfin.Plugin.HomeScreenSections.Library;
using Jellyfin.Plugin.HomeScreenSections.Services;
using MediaBrowser.Model;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Querying;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.HomeScreenSections.Controllers;

/// <summary>
/// API controller for Modular Home plugin.
/// </summary>
[ApiController]
[Route("[controller]")]
public class ModularHomeViewsController(
    ILogger<ModularHomeViewsController> logger,
    IHomeScreenManager homeScreenManager,
    IModularHomeUserSettingsStore userSettingsStore,
    ITranslationManager translationManager) : ControllerBase
{
    /// <summary>
    /// Get the view for the plugin.
    /// </summary>
    /// <param name="viewName">The view identifier.</param>
    /// <returns>View.</returns>
    [HttpGet("{viewName}")]
    [Authorize]
    public ActionResult GetView([FromRoute] string viewName)
    {
        return ServeView(viewName);
    }

    /// <summary>
    /// Get the section types that are registered in Modular Home.
    /// </summary>
    /// <param name="language">Optional language code for translating section display names.</param>
    /// <returns>Array of <see cref="HomeScreenSectionInfo"/>.</returns>
    [HttpGet("Sections")]
    [Authorize]
    public QueryResult<HomeScreenSectionInfo> GetSectionTypes([FromQuery] string? language = null)
    {
        List<HomeScreenSectionInfo> items = [];

        foreach (IHomeScreenSection section in homeScreenManager.GetSectionTypes())
        {
            HomeScreenSectionInfo item = section.GetInfo();
            item.ViewMode ??= SectionViewMode.Landscape;

            if (!string.IsNullOrWhiteSpace(language) && item.DisplayText != null)
            {
                item.DisplayText = translationManager.Translate(
                    item.Section!,
                    language.Trim(),
                    item.DisplayText,
                    section.TranslationMetadata);
            }

            items.Add(item);
        }

        return new QueryResult<HomeScreenSectionInfo>(null, items.Count, items);
    }

    /// <summary>
    /// Get the user settings for Modular Home.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <returns><see cref="ModularHomeUserSettings"/>.</returns>
    [HttpGet("UserSettings")]
    [Authorize]
    public ActionResult<ModularHomeUserSettings> GetUserSettings([FromQuery] Guid userId) =>
        userSettingsStore.GetUserSettings(userId);

    /// <summary>
    /// Get the translation pack for the given language.
    /// </summary>
    /// <param name="language">Language code (e.g. "en", "de").</param>
    /// <returns>Dictionary of translation keys to translated strings.</returns>
    [HttpGet("Translations")]
    [Authorize]
    public ActionResult<IDictionary<string, string>> GetTranslations([FromQuery] string language = "en")
    {
        IDictionary<string, string>? translations = translationManager.GetTranslationPack(language.Trim());
        return Ok(translations ?? (Dictionary<string, string>)[]);
    }

    /// <summary>
    /// Update the user settings for Modular Home.
    /// </summary>
    /// <param name="obj">Instance of <see cref="ModularHomeUserSettings" />.</param>
    /// <returns>Status.</returns>
    [HttpPost("UserSettings")]
    [Authorize]
    public ActionResult UpdateSettings([FromBody] ModularHomeUserSettings obj)
    {
        userSettingsStore.UpdateUserSettings(obj.UserId, obj);
        return Ok();
    }

    private ActionResult ServeView(string viewName)
    {
        if (HomeScreenSectionsPlugin.Instance == null)
        {
            return BadRequest("No plugin instance found");
        }

        IEnumerable<PluginPageInfo> pages = HomeScreenSectionsPlugin.Instance.GetViews();

        if (pages == null)
        {
            return NotFound("Pages is null or empty");
        }

        PluginPageInfo? view = pages.FirstOrDefault(pageInfo => pageInfo?.Name == viewName, null);

        if (view == null)
        {
            return NotFound("No matching view found");
        }

        Stream? stream = HomeScreenSectionsPlugin.Instance.GetType().Assembly.GetManifestResourceStream(view.EmbeddedResourcePath);

        if (stream == null)
        {
            logger.LogError("Failed to get resource {Resource}", view.EmbeddedResourcePath);
            return NotFound();
        }

        return File(stream, MimeTypes.GetMimeType(view.EmbeddedResourcePath));
    }
}
