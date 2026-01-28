using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.UI.Models;
using Radoub.UI.Services;

namespace RadoubLauncher.ViewModels;

/// <summary>
/// ViewModel for the Theme Editor window.
/// Allows creating and editing custom themes with live preview.
/// </summary>
public class ThemeEditorViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? CloseRequested;

    // Theme Info
    private string _themeName = "My Custom Theme";
    private string _themeId = "org.radoub.theme.custom";
    private string _themeAuthor = "";
    private string _themeDescription = "";
    private string _selectedBaseTheme = "Dark";

    // Core Colors
    private string _backgroundColor = "#1E1E1E";
    private string _sidebarColor = "#252526";
    private string _textColor = "#D4D4D4";
    private string _accentColor = "#007ACC";
    private string _selectionColor = "#264F78";
    private string _borderColor = "#3C3C3C";

    // Status Colors
    private string _successColor = "#4CAF50";
    private string _warningColor = "#FB8C00";
    private string _errorColor = "#F44336";
    private string _infoColor = "#2196F3";

    // Font Settings
    private string _primaryFont = "Segoe UI";
    private string _monospaceFont = "Cascadia Code";
    private decimal _fontSize = 14;

    // Preset
    private ThemeManifest? _selectedPreset;

    public ThemeEditorViewModel()
    {
        BaseThemeOptions = new ObservableCollection<string> { "Light", "Dark" };
        PresetThemes = new ObservableCollection<ThemeManifest>();

        LoadPresetThemes();

        ExportCommand = new RelayCommand(ExportTheme);
        CloseCommand = new RelayCommand(Close);
        LoadPresetCommand = new RelayCommand(LoadPreset);
    }

    #region Commands

    public ICommand ExportCommand { get; }
    public ICommand CloseCommand { get; }
    public ICommand LoadPresetCommand { get; }

    #endregion

    #region Theme Info Properties

    public string ThemeName
    {
        get => _themeName;
        set => SetProperty(ref _themeName, value);
    }

    public string ThemeId
    {
        get => _themeId;
        set => SetProperty(ref _themeId, value);
    }

    public string ThemeAuthor
    {
        get => _themeAuthor;
        set => SetProperty(ref _themeAuthor, value);
    }

    public string ThemeDescription
    {
        get => _themeDescription;
        set => SetProperty(ref _themeDescription, value);
    }

    public ObservableCollection<string> BaseThemeOptions { get; }

    public string SelectedBaseTheme
    {
        get => _selectedBaseTheme;
        set
        {
            if (SetProperty(ref _selectedBaseTheme, value))
            {
                // Auto-set default colors based on base theme
                if (value == "Light" && _backgroundColor == "#1E1E1E")
                {
                    BackgroundColor = "#FFFFFF";
                    SidebarColor = "#F3F3F3";
                    TextColor = "#1E1E1E";
                    SelectionColor = "#ADD6FF";
                    BorderColor = "#CCCCCC";
                }
                else if (value == "Dark" && _backgroundColor == "#FFFFFF")
                {
                    BackgroundColor = "#1E1E1E";
                    SidebarColor = "#252526";
                    TextColor = "#D4D4D4";
                    SelectionColor = "#264F78";
                    BorderColor = "#3C3C3C";
                }
            }
        }
    }

    public ObservableCollection<ThemeManifest> PresetThemes { get; }

    public ThemeManifest? SelectedPreset
    {
        get => _selectedPreset;
        set => SetProperty(ref _selectedPreset, value);
    }

    #endregion

    #region Core Color Properties

    public string BackgroundColor
    {
        get => _backgroundColor;
        set
        {
            if (SetProperty(ref _backgroundColor, value))
            {
                OnPropertyChanged(nameof(BackgroundBrush));
                OnPropertyChanged(nameof(PreviewBackgroundBrush));
            }
        }
    }

    public string SidebarColor
    {
        get => _sidebarColor;
        set
        {
            if (SetProperty(ref _sidebarColor, value))
            {
                OnPropertyChanged(nameof(SidebarBrush));
                OnPropertyChanged(nameof(PreviewSidebarBrush));
            }
        }
    }

    public string TextColor
    {
        get => _textColor;
        set
        {
            if (SetProperty(ref _textColor, value))
            {
                OnPropertyChanged(nameof(TextBrush));
                OnPropertyChanged(nameof(PreviewTextBrush));
            }
        }
    }

    public string AccentColor
    {
        get => _accentColor;
        set
        {
            if (SetProperty(ref _accentColor, value))
            {
                OnPropertyChanged(nameof(AccentBrush));
                OnPropertyChanged(nameof(PreviewAccentBrush));
            }
        }
    }

    public string SelectionColor
    {
        get => _selectionColor;
        set
        {
            if (SetProperty(ref _selectionColor, value))
            {
                OnPropertyChanged(nameof(SelectionBrush));
                OnPropertyChanged(nameof(PreviewSelectionBrush));
            }
        }
    }

    public string BorderColor
    {
        get => _borderColor;
        set
        {
            if (SetProperty(ref _borderColor, value))
            {
                OnPropertyChanged(nameof(BorderColorBrush));
                OnPropertyChanged(nameof(PreviewBorderBrush));
            }
        }
    }

    #endregion

    #region Status Color Properties

    public string SuccessColor
    {
        get => _successColor;
        set
        {
            if (SetProperty(ref _successColor, value))
                OnPropertyChanged(nameof(SuccessBrush));
        }
    }

    public string WarningColor
    {
        get => _warningColor;
        set
        {
            if (SetProperty(ref _warningColor, value))
                OnPropertyChanged(nameof(WarningBrush));
        }
    }

    public string ErrorColor
    {
        get => _errorColor;
        set
        {
            if (SetProperty(ref _errorColor, value))
                OnPropertyChanged(nameof(ErrorBrush));
        }
    }

    public string InfoColor
    {
        get => _infoColor;
        set
        {
            if (SetProperty(ref _infoColor, value))
                OnPropertyChanged(nameof(InfoBrush));
        }
    }

    #endregion

    #region Font Properties

    public string PrimaryFont
    {
        get => _primaryFont;
        set => SetProperty(ref _primaryFont, value);
    }

    public string MonospaceFont
    {
        get => _monospaceFont;
        set => SetProperty(ref _monospaceFont, value);
    }

    public decimal FontSize
    {
        get => _fontSize;
        set => SetProperty(ref _fontSize, value);
    }

    #endregion

    #region Brush Properties (for preview)

    public IBrush BackgroundBrush => ParseBrush(_backgroundColor, Colors.Gray);
    public IBrush SidebarBrush => ParseBrush(_sidebarColor, Colors.DarkGray);
    public IBrush TextBrush => ParseBrush(_textColor, Colors.White);
    public IBrush AccentBrush => ParseBrush(_accentColor, Colors.DodgerBlue);
    public IBrush SelectionBrush => ParseBrush(_selectionColor, Colors.SteelBlue);
    public IBrush BorderColorBrush => ParseBrush(_borderColor, Colors.DimGray);
    public IBrush SuccessBrush => ParseBrush(_successColor, Colors.Green);
    public IBrush WarningBrush => ParseBrush(_warningColor, Colors.Orange);
    public IBrush ErrorBrush => ParseBrush(_errorColor, Colors.Red);
    public IBrush InfoBrush => ParseBrush(_infoColor, Colors.Blue);

    // Preview-specific brushes (same as above, separate for clarity in XAML)
    public IBrush PreviewBackgroundBrush => BackgroundBrush;
    public IBrush PreviewSidebarBrush => SidebarBrush;
    public IBrush PreviewTextBrush => TextBrush;
    public IBrush PreviewAccentBrush => AccentBrush;
    public IBrush PreviewSelectionBrush => SelectionBrush;
    public IBrush PreviewBorderBrush => BorderColorBrush;

    private static IBrush ParseBrush(string? colorString, Color fallback)
    {
        if (string.IsNullOrEmpty(colorString))
            return new SolidColorBrush(fallback);

        try
        {
            return new SolidColorBrush(Color.Parse(colorString));
        }
        catch (FormatException)
        {
            // Color.Parse failed - use fallback color
            return new SolidColorBrush(fallback);
        }
    }

    #endregion

    #region Methods

    private void LoadPresetThemes()
    {
        try
        {
            // Get themes from ThemeManager
            var themeManager = ThemeManager.Instance;
            themeManager.DiscoverThemes();

            PresetThemes.Clear();
            foreach (var theme in themeManager.AvailableThemes)
            {
                PresetThemes.Add(theme);
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Loaded {PresetThemes.Count} preset themes");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to load preset themes: {ex.Message}");
        }
    }

    private void LoadPreset()
    {
        if (_selectedPreset == null)
            return;

        // Copy theme info
        ThemeName = _selectedPreset.Plugin.Name + " (Copy)";
        ThemeId = _selectedPreset.Plugin.Id + ".custom";
        ThemeAuthor = _selectedPreset.Plugin.Author;
        ThemeDescription = _selectedPreset.Plugin.Description;
        SelectedBaseTheme = _selectedPreset.BaseTheme;

        // Copy colors
        if (_selectedPreset.Colors != null)
        {
            BackgroundColor = _selectedPreset.Colors.Background ?? _backgroundColor;
            SidebarColor = _selectedPreset.Colors.Sidebar ?? _sidebarColor;
            TextColor = _selectedPreset.Colors.Text ?? _textColor;
            AccentColor = _selectedPreset.Colors.Accent ?? _accentColor;
            SelectionColor = _selectedPreset.Colors.Selection ?? _selectionColor;
            BorderColor = _selectedPreset.Colors.Border ?? _borderColor;
            SuccessColor = _selectedPreset.Colors.Success ?? _successColor;
            WarningColor = _selectedPreset.Colors.Warning ?? _warningColor;
            ErrorColor = _selectedPreset.Colors.Error ?? _errorColor;
            InfoColor = _selectedPreset.Colors.Info ?? _infoColor;
        }

        // Copy fonts
        if (_selectedPreset.Fonts != null)
        {
            PrimaryFont = _selectedPreset.Fonts.Primary ?? _primaryFont;
            MonospaceFont = _selectedPreset.Fonts.Monospace ?? _monospaceFont;
            FontSize = _selectedPreset.Fonts.Size ?? (int)_fontSize;
        }

        UnifiedLogger.LogApplication(LogLevel.INFO, $"Loaded preset theme: {_selectedPreset.Plugin.Name}");
    }

    private void ExportTheme()
    {
        try
        {
            var manifest = BuildThemeManifest();
            var json = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            // Export to shared themes folder
            var themesPath = RadoubSettings.Instance.GetSharedThemesPath();
            var safeFileName = SanitizeFileName(_themeId) + ".json";
            var filePath = Path.Combine(themesPath, safeFileName);

            File.WriteAllText(filePath, json);

            // Update shared theme setting to use this theme
            RadoubSettings.Instance.SharedThemeId = _themeId;

            // Refresh themes and apply
            ThemeManager.Instance.RefreshThemes();
            ThemeManager.Instance.ApplyTheme(_themeId);

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Exported theme to: {UnifiedLogger.SanitizePath(filePath)}");
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to export theme: {ex.Message}");
        }
    }

    private ThemeManifest BuildThemeManifest()
    {
        return new ThemeManifest
        {
            ManifestVersion = "1.0",
            Plugin = new ThemePluginInfo
            {
                Id = _themeId,
                Name = _themeName,
                Version = "1.0.0",
                Author = _themeAuthor,
                Description = _themeDescription,
                Type = "theme"
            },
            BaseTheme = _selectedBaseTheme,
            Colors = new ThemeColors
            {
                Background = _backgroundColor,
                Sidebar = _sidebarColor,
                Text = _textColor,
                Accent = _accentColor,
                Selection = _selectionColor,
                Border = _borderColor,
                Success = _successColor,
                Warning = _warningColor,
                Error = _errorColor,
                Info = _infoColor
            },
            Fonts = new ThemeFonts
            {
                Primary = _primaryFont,
                Monospace = _monospaceFont,
                Size = (int)_fontSize
            }
        };
    }

    private static string SanitizeFileName(string input)
    {
        var invalid = Path.GetInvalidFileNameChars();
        foreach (var c in invalid)
        {
            input = input.Replace(c, '_');
        }
        return input.Replace('.', '_');
    }

    private void Close()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region INotifyPropertyChanged

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}
