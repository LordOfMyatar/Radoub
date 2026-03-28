using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Radoub.UI.Services;

namespace Manifest.Views;

/// <summary>
/// SettingsWindow core: constructor and close. Theme/font managed by Trebuchet.
/// Split into partial files:
///   - SettingsWindow.Paths.cs: Game path, user path, module configuration
///   - SettingsWindow.Dictionary.cs: Spell check and dictionary management
/// </summary>
public partial class SettingsWindow : Window
{
    private bool _isLoading = true;

    public SettingsWindow()
    {
        InitializeComponent();
        LoadSettings();
        _isLoading = false;
    }

    private void LoadSettings()
    {
        // Load module configuration (#1325, #1322)
        LoadModuleConfiguration();

        // Load game path from shared settings
        LoadGamePath();

        // Load spell check settings
        LoadSpellCheckSettings();

        // Load dictionary settings
        LoadDictionarySettings();
    }

    #region Theme-Aware Colors

    private IBrush GetErrorBrush() => BrushManager.GetErrorBrush(this);
    private IBrush GetSuccessBrush() => BrushManager.GetSuccessBrush(this);
    private IBrush GetWarningBrush() => BrushManager.GetWarningBrush(this);

    #endregion

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
