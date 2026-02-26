using Avalonia.Controls;
using Avalonia.Interactivity;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using System;
using System.Linq;

namespace MerchantEditor.Views;

/// <summary>
/// MainWindow partial: TLK language menu population and switching (#1362).
/// </summary>
public partial class MainWindow
{
    #region Language Menu (#1362)

    /// <summary>
    /// Populates the View > Language submenu with detected languages and gender variants.
    /// Each language gets two entries: "Language (Male)" and "Language (Female)".
    /// The current selection from RadoubSettings is checked.
    /// </summary>
    private void PopulateLanguageMenu()
    {
        var languageMenu = this.FindControl<MenuItem>("LanguageMenu");
        if (languageMenu == null) return;

        languageMenu.Items.Clear();

        var settings = RadoubSettings.Instance;
        var availableLanguages = settings.GetAvailableTlkLanguages().ToList();

        if (availableLanguages.Count == 0)
        {
            var noLangItem = new MenuItem { Header = "(No languages detected)", IsEnabled = false };
            languageMenu.Items.Add(noLangItem);
            return;
        }

        var currentLang = settings.EffectiveLanguage;
        var currentFemale = settings.TlkUseFemale;

        foreach (var language in availableLanguages)
        {
            var langName = LanguageHelper.GetDisplayName(language);
            var langCode = LanguageHelper.GetLanguageCode(language);

            // Male variant
            var maleItem = new MenuItem
            {
                Header = $"{langName}",
                Tag = (langCode, false),
                Icon = (language == currentLang && !currentFemale) ? new TextBlock { Text = "✓" } : null
            };
            maleItem.Click += OnLanguageMenuItemClick;
            languageMenu.Items.Add(maleItem);

            // Female variant - check if dialogf.tlk exists
            var femaleTlkPath = settings.GetTlkPath(language, Gender.Female);
            var maleTlkPath = settings.GetTlkPath(language, Gender.Male);
            var hasFemaleVariant = femaleTlkPath != null && maleTlkPath != null
                && !string.Equals(femaleTlkPath, maleTlkPath, StringComparison.OrdinalIgnoreCase);

            if (hasFemaleVariant)
            {
                var femaleItem = new MenuItem
                {
                    Header = $"{langName} (Female)",
                    Tag = (langCode, true),
                    Icon = (language == currentLang && currentFemale) ? new TextBlock { Text = "✓" } : null
                };
                femaleItem.Click += OnLanguageMenuItemClick;
                languageMenu.Items.Add(femaleItem);
            }
        }
    }

    /// <summary>
    /// Handles language menu item selection. Updates RadoubSettings and rebuilds palette cache.
    /// </summary>
    private async void OnLanguageMenuItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem || menuItem.Tag is not (string langCode, bool useFemale))
            return;

        var settings = RadoubSettings.Instance;
        var oldLang = settings.TlkLanguage;
        var oldFemale = settings.TlkUseFemale;

        // Skip if no change
        if (langCode == oldLang && useFemale == oldFemale)
            return;

        settings.TlkLanguage = langCode;
        settings.TlkUseFemale = useFemale;

        var langDisplay = LanguageHelper.GetDisplayName(
            LanguageHelper.FromLanguageCode(langCode) ?? Language.English);
        var genderDisplay = useFemale ? " (Female)" : "";

        UnifiedLogger.LogApplication(LogLevel.INFO,
            $"Language changed to {langDisplay}{genderDisplay}");

        // Update checkmarks
        PopulateLanguageMenu();

        // Rebuild GameDataService with new TLK and rebuild palette cache
        UpdateStatusBar($"Switching to {langDisplay}{genderDisplay}...");

        try
        {
            // Reinitialize GameDataService with new language TLK
            await System.Threading.Tasks.Task.Run(() =>
            {
                _gameDataService?.ReloadConfiguration();
            });

            // Clear all downstream caches that hold resolved TLK strings
            _itemResolutionService?.ClearCache();
            _baseItemTypeService?.ClearCache();

            // Rebuild palette cache with new language
            await ClearAndReloadPaletteCacheAsync();

            // Re-populate type filter and buy restrictions with new language names
            await LoadBaseItemTypesAsync();

            // Refresh store inventory display if a file is loaded
            if (_currentStore != null)
            {
                RefreshStoreInventoryNames();

                // Re-apply buy restrictions with new language names
                PopulateBuyRestrictions();
            }

            UpdateStatusBar($"Language: {langDisplay}{genderDisplay} - Ready");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Language switch failed: {ex.Message}");
            UpdateStatusBar("Language switch failed");
        }
    }

    /// <summary>
    /// Re-resolves display names for all items in the store inventory.
    /// Called after language change to update visible names.
    /// </summary>
    private void RefreshStoreInventoryNames()
    {
        if (_itemResolutionService == null) return;

        foreach (var item in StoreItems)
        {
            var resolved = _itemResolutionService.ResolveItem(item.ResRef);
            if (resolved != null)
            {
                item.DisplayName = resolved.DisplayName;
                item.BaseItemType = resolved.BaseItemTypeName;
            }
        }
    }

    #endregion
}
