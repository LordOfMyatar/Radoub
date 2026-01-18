using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Radoub.Formats.Settings;
using Radoub.UI.Services;
using RadoubLauncher.Services;

namespace RadoubLauncher.ViewModels;

public partial class SettingsWindowViewModel : ObservableObject
{
    private readonly Window _window;

    [ObservableProperty]
    private string _gameInstallPath = "";

    [ObservableProperty]
    private string _nwnDocumentsPath = "";

    [ObservableProperty]
    private string _selectedLanguage = "English";

    [ObservableProperty]
    private string _selectedGender = "Male";

    [ObservableProperty]
    private string _selectedTheme = "Light";

    [ObservableProperty]
    private double _fontSizeScale = 1.0;

    public string FontSizePercentText => $"{(int)(FontSizeScale * 100)}%";

    public ObservableCollection<string> AvailableLanguages { get; } = new()
    {
        "English", "French", "German", "Italian", "Spanish", "Polish", "Korean", "Chinese Traditional", "Chinese Simplified", "Japanese"
    };

    public ObservableCollection<string> AvailableGenders { get; } = new()
    {
        "Male", "Female"
    };

    public ObservableCollection<string> AvailableThemes { get; } = new();

    public ObservableCollection<ToolInfo> DetectedTools { get; }

    public SettingsWindowViewModel(Window window)
    {
        _window = window;

        // Load current settings
        LoadSettings();

        // Get detected tools
        DetectedTools = new ObservableCollection<ToolInfo>(ToolLauncherService.Instance.Tools);

        // Load available themes
        foreach (var theme in ThemeManager.Instance.AvailableThemes)
        {
            AvailableThemes.Add(theme.Plugin.Name);
        }

        // Set current theme
        var currentTheme = ThemeManager.Instance.CurrentTheme;
        if (currentTheme != null && AvailableThemes.Contains(currentTheme.Plugin.Name))
        {
            SelectedTheme = currentTheme.Plugin.Name;
        }
    }

    private void LoadSettings()
    {
        var sharedSettings = RadoubSettings.Instance;
        var localSettings = SettingsService.Instance;

        GameInstallPath = sharedSettings.BaseGameInstallPath ?? "";
        NwnDocumentsPath = sharedSettings.NeverwinterNightsPath ?? "";
        SelectedLanguage = sharedSettings.TlkLanguage ?? "English";
        SelectedGender = sharedSettings.TlkUseFemale ? "Female" : "Male";
        FontSizeScale = localSettings.FontSizeScale;
    }

    partial void OnFontSizeScaleChanged(double value)
    {
        OnPropertyChanged(nameof(FontSizePercentText));
    }

    [RelayCommand]
    private async Task BrowseGamePath()
    {
        var folder = await _window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select NWN Installation Folder",
            AllowMultiple = false
        });

        if (folder.Count > 0)
        {
            GameInstallPath = folder[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    private async Task BrowseDocumentsPath()
    {
        var folder = await _window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select NWN Documents Folder",
            AllowMultiple = false
        });

        if (folder.Count > 0)
        {
            NwnDocumentsPath = folder[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    private void Save()
    {
        var sharedSettings = RadoubSettings.Instance;
        var localSettings = SettingsService.Instance;

        sharedSettings.BaseGameInstallPath = GameInstallPath;
        sharedSettings.NeverwinterNightsPath = NwnDocumentsPath;
        sharedSettings.TlkLanguage = SelectedLanguage;
        sharedSettings.TlkUseFemale = SelectedGender == "Female";
        localSettings.FontSizeScale = FontSizeScale;

        // Apply theme if changed
        var selectedThemeInfo = ThemeManager.Instance.AvailableThemes
            .FirstOrDefault(t => t.Plugin.Name == SelectedTheme);
        if (selectedThemeInfo != null)
        {
            ThemeManager.Instance.ApplyTheme(selectedThemeInfo.Plugin.Id);
        }

        // Font size is applied via theme manager when it reapplies the theme
        // For now, store the setting - a restart may be needed

        _window.Close();
    }

    [RelayCommand]
    private void Cancel()
    {
        _window.Close();
    }
}
