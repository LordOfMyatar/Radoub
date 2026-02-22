using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Quartermaster.Services;
using Radoub.Formats.Logging;
using Radoub.UI.Services;
using Radoub.UI.Views;

namespace Quartermaster.Views.Panels;

/// <summary>
/// Browse dialog handlers: conversation, portrait, and soundset browsers.
/// </summary>
public partial class CharacterPanel
{
    #region Browse Dialogs

    private async void OnBrowseConversationClick(object? sender, RoutedEventArgs e)
    {
        var context = new QuartermasterScriptBrowserContext(_currentFilePath, _gameDataService);
        var browser = new DialogBrowserWindow(context);

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window parentWindow)
        {
            var result = await browser.ShowDialog<string?>(parentWindow);
            if (!string.IsNullOrEmpty(result) && _conversationTextBox != null)
                _conversationTextBox.Text = result;
        }
    }

    private void OnClearConversationClick(object? sender, RoutedEventArgs e)
    {
        if (_conversationTextBox != null)
            _conversationTextBox.Text = "";
    }

    private async void OnBrowsePortraitClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (_gameDataService == null || _itemIconService == null)
            {
                UnifiedLogger.Log(LogLevel.WARN, "CharacterPanel: Cannot open portrait browser - missing services", "CharacterPanel", "UI");
                return;
            }

            UnifiedLogger.Log(LogLevel.INFO, "CharacterPanel: Opening portrait browser", "CharacterPanel", "UI");
            var browser = new PortraitBrowserWindow(_gameDataService, _itemIconService);

            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is Window parentWindow)
            {
                var result = await browser.ShowDialog<ushort?>(parentWindow);
                UnifiedLogger.Log(LogLevel.INFO, $"CharacterPanel: Portrait browser returned: {(result.HasValue ? result.Value.ToString() : "null")}", "CharacterPanel", "UI");

                if (result.HasValue)
                {
                    SelectPortrait(result.Value);
                    // Trigger change event
                    if (_currentCreature != null)
                    {
                        _currentCreature.PortraitId = result.Value;
                        // Also clear the Portrait string field since we're using PortraitId now
                        _currentCreature.Portrait = string.Empty;
                        CharacterChanged?.Invoke(this, EventArgs.Empty);
                        PortraitChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.Log(LogLevel.ERROR, $"CharacterPanel: OnBrowsePortraitClick crashed: {ex}", "CharacterPanel", "UI");
        }
    }

    private async void OnBrowseSoundSetClick(object? sender, RoutedEventArgs e)
    {
        if (_gameDataService == null || _audioService == null)
            return;

        var browser = new SoundsetBrowserWindow(_gameDataService, _audioService);

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is Window parentWindow)
        {
            var result = await browser.ShowDialog<ushort?>(parentWindow);
            if (result.HasValue)
            {
                SelectSoundSet(result.Value);
                // Trigger change event
                if (_currentCreature != null)
                {
                    _currentCreature.SoundSetFile = result.Value;
                    CharacterChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }

    #endregion
}
