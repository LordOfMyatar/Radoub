using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.UI.Services;
using RadoubLauncher.Services;

namespace RadoubLauncher.ViewModels;

/// <summary>
/// First-run / welcome-back configuration wizard (#1020). Surfaces the settings
/// that have no good default (today: the game path) and presents a summary of all
/// key settings the user can review and edit. Settings with good defaults (theme,
/// font, logging, backup) are shown for review but never force the wizard open.
///
/// Steps are linear: [Game Path] → [Appearance] → [Logging] → [Backup] → [Summary].
/// The summary's Edit links jump back to any step. On finish, all registered gap
/// keys are acknowledged so the wizard does not re-nag.
/// </summary>
public partial class FirstRunWizardViewModel : ObservableObject
{
    // Stable gap keys (persisted in RadoubSettings.AcknowledgedWizardGaps).
    public const string GapGamePath = "gamePath";
    public const string GapAppearance = "appearance";
    public const string GapLogging = "logging";
    public const string GapBackup = "backup";

    private static readonly string[] StepTitles =
        { "Game Path", "Appearance", "Logging", "Backup", "Summary" };

    private readonly Window? _window;
    private readonly bool _headless;

    public FirstRunWizardViewModel(Window window, WizardMode mode)
        : this(window, mode, headless: false) { }

    private FirstRunWizardViewModel(Window? window, WizardMode mode, bool headless)
    {
        _window = window;
        _headless = headless;
        Mode = mode;

        foreach (var level in new[] { "TRACE", "DEBUG", "INFO", "WARN", "ERROR" })
            AvailableLogLevels.Add(level);

        // ThemeManager is only available once a tool has initialized it. In headless
        // tests it isn't, so guard the theme list population.
        if (!headless)
        {
            foreach (var theme in ThemeManager.Instance.AvailableThemes)
                AvailableThemes.Add(theme.Plugin.Name);
        }

        LoadFromSettings();
    }

    /// <summary>Headless factory for unit tests (no Window, no ThemeManager).</summary>
    public static FirstRunWizardViewModel CreateForTest(WizardMode mode) =>
        new(window: null, mode, headless: true);

    public WizardMode Mode { get; }

    public string WelcomeHeading => Mode == WizardMode.WelcomeBack
        ? "Welcome back"
        : "Welcome to the Radoub toolset";

    public string WelcomeBlurb => Mode == WizardMode.WelcomeBack
        ? "We added a setting that needs your input. Review the values below — you can change any of these later in Settings."
        : "Let's get a few things set up. You can change any of these later in Settings.";

    // Step navigation

    [ObservableProperty]
    private int _stepIndex;

    public int LastStepIndex => StepTitles.Length - 1;
    public string CurrentStepTitle => StepTitles[StepIndex];
    public bool CanGoBack => StepIndex > 0;
    public bool IsLastStep => StepIndex == LastStepIndex;
    public bool IsSummary => StepIndex == LastStepIndex;

    partial void OnStepIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentStepTitle));
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(IsLastStep));
        OnPropertyChanged(nameof(IsSummary));

        // Rebuild the summary only when the summary step is shown. Doing it on every
        // value change (slider drags fire continuously) thrashed the bound
        // collection and crashed the render loop when the summary's item bindings
        // re-materialized mid-drag.
        if (IsSummary)
            RefreshSummary();
    }

    [RelayCommand]
    private void Next()
    {
        if (StepIndex < LastStepIndex)
            StepIndex++;
    }

    [RelayCommand]
    private void Back()
    {
        if (StepIndex > 0)
            StepIndex--;
    }

    /// <summary>Jump to a step by index — used by the summary's Edit links.</summary>
    [RelayCommand]
    private void GoToStep(int index)
    {
        if (index >= 0 && index <= LastStepIndex)
            StepIndex = index;
    }

    // Game Path

    [ObservableProperty]
    private string _gameInstallPath = "";

    [ObservableProperty]
    private string _gamePathValidation = "";

    public bool HasGamePathValidation => !string.IsNullOrEmpty(GamePathValidation);

    partial void OnGameInstallPathChanged(string value)
    {
        var result = ResourcePathDetector.ValidateBaseGamePathWithMessage(value);
        GamePathValidation = result.Message;
        OnPropertyChanged(nameof(HasGamePathValidation));
    }

    [RelayCommand]
    private async Task BrowseGamePath()
    {
        if (_window == null) return; // headless (test) — no picker

        var folder = await _window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select NWN Base Game Installation (contains data\\ folder)",
            AllowMultiple = false
        });

        if (folder.Count > 0)
            GameInstallPath = folder[0].Path.LocalPath;
    }

    [RelayCommand]
    private void AutoDetectGamePath()
    {
        var detected = ResourcePathDetector.AutoDetectBaseGamePath();
        if (!string.IsNullOrEmpty(detected))
            GameInstallPath = detected;
        else
            GamePathValidation = "Could not auto-detect. Please browse manually.";
        OnPropertyChanged(nameof(HasGamePathValidation));
    }

    // Appearance

    public ObservableCollection<string> AvailableThemes { get; } = new();

    [ObservableProperty]
    private string _selectedTheme = "";

    [ObservableProperty]
    private double _fontSizePoints = 14.0;

    public string FontSizePointsText => $"{(int)FontSizePoints}pt";

    partial void OnFontSizePointsChanged(double value)
    {
        OnPropertyChanged(nameof(FontSizePointsText));
    }

    // Logging

    public ObservableCollection<string> AvailableLogLevels { get; } = new();

    [ObservableProperty]
    private string _selectedLogLevel = "INFO";

    // Backup

    // Slider binds to BackupRetentionSlider (a double) to avoid a fragile
    // double→int two-way binding; the int property below is derived from it and is
    // what gets persisted/clamped.
    [ObservableProperty]
    private double _backupRetentionSlider = 30.0;

    public int BackupRetentionDays
    {
        get => (int)Math.Round(BackupRetentionSlider);
        set => BackupRetentionSlider = Math.Max(1, Math.Min(90, value));
    }

    public string BackupRetentionText => $"{BackupRetentionDays} day{(BackupRetentionDays == 1 ? "" : "s")}";

    partial void OnBackupRetentionSliderChanged(double value)
    {
        OnPropertyChanged(nameof(BackupRetentionDays));
        OnPropertyChanged(nameof(BackupRetentionText));
    }

    // Shortcut offer

    [ObservableProperty]
    private bool _createDesktopShortcut;

    [ObservableProperty]
    private string _shortcutResultMessage = "";

    public bool HasShortcutResult => !string.IsNullOrEmpty(ShortcutResultMessage);

    // Summary — built only when the summary step is shown (see OnStepIndexChanged).

    public ObservableCollection<WizardSummaryRow> SummaryRows { get; } = new();

    private void RefreshSummary()
    {
        SummaryRows.Clear();
        SummaryRows.Add(MakeRow("Game path",
            string.IsNullOrEmpty(GameInstallPath) ? "(not set)" : GameInstallPath, 0));
        SummaryRows.Add(MakeRow("Theme", SelectedTheme, 1));
        SummaryRows.Add(MakeRow("Font size", FontSizePointsText, 1));
        SummaryRows.Add(MakeRow("Log level", SelectedLogLevel, 2));
        SummaryRows.Add(MakeRow("Backup retention", BackupRetentionText, 3));
    }

    // Each row carries its own jump command so the summary's Edit button binds to
    // the row, not to a fragile $parent[ItemsControl]-cast back to this view model.
    private WizardSummaryRow MakeRow(string label, string value, int stepIndex) =>
        new(label, value, stepIndex, new RelayCommand(() => GoToStep(stepIndex)));

    private void LoadFromSettings()
    {
        var shared = RadoubSettings.Instance;
        GameInstallPath = shared.BaseGameInstallPath;
        FontSizePoints = shared.SharedFontSize;
        BackupRetentionDays = shared.BackupRetentionDays;
        SelectedLogLevel = shared.SharedLogLevel.ToString();

        if (!_headless)
        {
            var current = ThemeManager.Instance.CurrentTheme;
            SelectedTheme = current != null && AvailableThemes.Contains(current.Plugin.Name)
                ? current.Plugin.Name
                : AvailableThemes.FirstOrDefault() ?? "";
        }

        RefreshSummary();
    }

    /// <summary>
    /// Apply all settings, optionally create the desktop shortcut, acknowledge the
    /// wizard gaps, and signal completion. Returns true when the window should close.
    /// </summary>
    [RelayCommand]
    private void Finish()
    {
        var shared = RadoubSettings.Instance;

        if (!string.IsNullOrEmpty(GameInstallPath))
            shared.BaseGameInstallPath = GameInstallPath;
        shared.SharedFontSize = FontSizePoints;
        shared.BackupRetentionDays = BackupRetentionDays;
        if (Enum.TryParse<LogLevel>(SelectedLogLevel, out var logLevel))
            shared.SharedLogLevel = logLevel;

        var themeInfo = ThemeManager.Instance.AvailableThemes
            .FirstOrDefault(t => t.Plugin.Name == SelectedTheme);
        if (themeInfo != null)
        {
            shared.SharedThemeId = themeInfo.Plugin.Id;
            ThemeManager.Instance.ApplyTheme(themeInfo.Plugin.Id);
        }

        if (CreateDesktopShortcut)
        {
            var iconPath = ResolveIconPath();
            var result = DesktopShortcutService.CreateForCurrentApp(iconPath);
            ShortcutResultMessage = result.Success
                ? $"Shortcut created: {result.Path}"
                : $"Shortcut failed: {result.Error}";
            OnPropertyChanged(nameof(HasShortcutResult));
        }

        // Acknowledge every registered gap so the wizard does not re-nag (#1020).
        shared.AcknowledgeWizardGaps(new[] { GapGamePath, GapAppearance, GapLogging, GapBackup });

        Completed?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke(this, EventArgs.Empty);

    private static string? ResolveIconPath()
    {
        var baseDir = AppContext.BaseDirectory;
        var ico = System.IO.Path.Combine(baseDir, "Assets", "Trebuchet.ico");
        return System.IO.File.Exists(ico) ? ico : null;
    }

    /// <summary>Raised when Finish completes — the window closes.</summary>
    public event EventHandler? Completed;

    /// <summary>Raised when the user cancels.</summary>
    public event EventHandler? Cancelled;

    /// <summary>The gap keys this wizard run covers (acknowledged on finish/cancel).</summary>
    public static IReadOnlyList<string> AllGapKeys =>
        new[] { GapGamePath, GapAppearance, GapLogging, GapBackup };
}

/// <summary>
/// One row on the summary page: label, current value, the step it edits, and a
/// self-contained Jump command that navigates to that step (so the Edit button
/// binds to the row, not via a fragile $parent-cast back to the view model).
/// </summary>
public sealed record WizardSummaryRow(string Label, string Value, int StepIndex, ICommand Jump);
