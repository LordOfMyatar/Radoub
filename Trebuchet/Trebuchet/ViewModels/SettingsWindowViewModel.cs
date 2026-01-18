using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
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
    private static readonly IBrush SuccessBrush = new SolidColorBrush(Color.Parse("#4CAF50"));
    private static readonly IBrush ErrorBrush = new SolidColorBrush(Color.Parse("#F44336"));

    [ObservableProperty]
    private string _gameInstallPath = "";

    [ObservableProperty]
    private string _nwnDocumentsPath = "";

    [ObservableProperty]
    private string _gameInstallValidation = "";

    [ObservableProperty]
    private IBrush _gameInstallValidationColor = SuccessBrush;

    [ObservableProperty]
    private string _nwnDocumentsValidation = "";

    [ObservableProperty]
    private IBrush _nwnDocumentsValidationColor = SuccessBrush;

    public bool HasGameInstallValidation => !string.IsNullOrEmpty(GameInstallValidation);
    public bool HasNwnDocumentsValidation => !string.IsNullOrEmpty(NwnDocumentsValidation);

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

        // Validate existing paths
        if (!string.IsNullOrEmpty(GameInstallPath))
        {
            ValidateGameInstallPath(GameInstallPath);
        }
        if (!string.IsNullOrEmpty(NwnDocumentsPath))
        {
            ValidateNwnDocumentsPath(NwnDocumentsPath);
        }
    }

    partial void OnGameInstallPathChanged(string value)
    {
        ValidateGameInstallPath(value);
    }

    partial void OnNwnDocumentsPathChanged(string value)
    {
        ValidateNwnDocumentsPath(value);
    }

    private void ValidateGameInstallPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            GameInstallValidation = "";
            OnPropertyChanged(nameof(HasGameInstallValidation));
            return;
        }

        var result = ResourcePathHelper.ValidateBaseGamePathWithMessage(path);
        GameInstallValidation = result.Message;
        GameInstallValidationColor = result.IsValid ? SuccessBrush : ErrorBrush;
        OnPropertyChanged(nameof(HasGameInstallValidation));
    }

    private void ValidateNwnDocumentsPath(string path)
    {
        if (string.IsNullOrEmpty(path))
        {
            NwnDocumentsValidation = "";
            OnPropertyChanged(nameof(HasNwnDocumentsValidation));
            return;
        }

        var result = ResourcePathHelper.ValidateGamePathWithMessage(path);
        NwnDocumentsValidation = result.Message;
        NwnDocumentsValidationColor = result.IsValid ? SuccessBrush : ErrorBrush;
        OnPropertyChanged(nameof(HasNwnDocumentsValidation));
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
            Title = "Select NWN Base Game Installation (contains data\\ folder)",
            AllowMultiple = false
        });

        if (folder.Count > 0)
        {
            GameInstallPath = folder[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    private void AutoDetectGamePath()
    {
        var detected = ResourcePathHelper.AutoDetectBaseGamePath();
        if (!string.IsNullOrEmpty(detected))
        {
            GameInstallPath = detected;
        }
        else
        {
            GameInstallValidation = "Could not auto-detect. Please browse manually.";
            GameInstallValidationColor = ErrorBrush;
            OnPropertyChanged(nameof(HasGameInstallValidation));
        }
    }

    [RelayCommand]
    private async Task BrowseDocumentsPath()
    {
        var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var defaultPath = System.IO.Path.Combine(documentsPath, "Neverwinter Nights");

        var folder = await _window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select NWN Documents Folder (Documents\\Neverwinter Nights)",
            AllowMultiple = false,
            SuggestedStartLocation = System.IO.Directory.Exists(defaultPath)
                ? await _window.StorageProvider.TryGetFolderFromPathAsync(new Uri(defaultPath))
                : await _window.StorageProvider.TryGetFolderFromPathAsync(new Uri(documentsPath))
        });

        if (folder.Count > 0)
        {
            NwnDocumentsPath = folder[0].Path.LocalPath;
        }
    }

    [RelayCommand]
    private void AutoDetectDocumentsPath()
    {
        var detected = ResourcePathHelper.AutoDetectGamePath();
        if (!string.IsNullOrEmpty(detected))
        {
            NwnDocumentsPath = detected;
        }
        else
        {
            NwnDocumentsValidation = "Could not auto-detect. Please browse manually.";
            NwnDocumentsValidationColor = ErrorBrush;
            OnPropertyChanged(nameof(HasNwnDocumentsValidation));
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

        _window.Close();
    }

    [RelayCommand]
    private void Cancel()
    {
        _window.Close();
    }
}
