using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Quartermaster.Services;
using Radoub.Formats.Logging;
using Radoub.Formats.Services;
using Radoub.UI.Services;
using Radoub.UI.ViewModels;
using DialogHelper = Quartermaster.Views.Helpers.DialogHelper;

namespace Quartermaster.Views;

/// <summary>
/// Window lifecycle: startup, service initialization, window position, and closing.
/// </summary>
public partial class MainWindow
{
    #region Window Lifecycle

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        Opened -= OnWindowOpened;

        // Create cancellation token for async operations
        _windowCts = new CancellationTokenSource();

        UpdateStatus("Initializing...");
        UpdateRecentFilesMenu();

        // Fire and forget - don't block UI thread
        // Service init and all loading happens in background
        _ = InitializeAndLoadAsync(_windowCts.Token);
    }

    private async Task InitializeAndLoadAsync(CancellationToken token)
    {
        try
        {
            // Initialize services on background thread - this is the expensive part
            await InitializeServicesAsync();

            token.ThrowIfCancellationRequested();

            // Now initialize panels that depend on services (must be on UI thread)
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                InitializePanels();
                PopulateLanguageMenu();
            });

            token.ThrowIfCancellationRequested();

            // Fire-and-forget cache and item loading in parallel
            _ = InitializeCachesAsync(token);
            StartGameItemsLoad(token);

            await HandleStartupFileAsync();

            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
            {
                UpdateStatus("Ready");
            });
        }
        catch (OperationCanceledException)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Window initialization cancelled");
        }
    }

    private async Task InitializeServicesAsync()
    {
        if (_servicesInitialized) return;

        // Run the expensive GameDataService initialization on a background thread
        await Task.Run(() =>
        {
            _gameDataService = new GameDataService();
            _creatureDisplayService = new CreatureDisplayService(_gameDataService);
            _itemViewModelFactory = new ItemViewModelFactory(_gameDataService);
            _itemIconService = new ItemIconService(_gameDataService);
            _audioService = new AudioService();

            if (_gameDataService.IsConfigured)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, "GameDataService initialized - BIF lookup enabled");
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, "GameDataService not configured - BIF lookup disabled");
            }
        });

        _servicesInitialized = true;
        UnifiedLogger.LogApplication(LogLevel.INFO, "Quartermaster services initialized");
    }

    private async Task InitializeCachesAsync(CancellationToken token)
    {
        if (GameData.IsConfigured)
        {
            UpdateStatus("Loading game data caches...");
            try
            {
                token.ThrowIfCancellationRequested();
                await DisplayService.InitializeCachesAsync();
                UnifiedLogger.LogApplication(LogLevel.INFO, "Game data caches initialized");
            }
            catch (OperationCanceledException)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Cache initialization cancelled");
                return;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Cache initialization failed: {ex.Message}");
            }
            UpdateStatus("Ready");
        }
    }

    private async Task HandleStartupFileAsync()
    {
        var options = CommandLineService.Options;

        if (string.IsNullOrEmpty(options.FilePath))
            return;

        if (!File.Exists(options.FilePath))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Command line file not found: {UnifiedLogger.SanitizePath(options.FilePath)}");
            UpdateStatus($"File not found: {Path.GetFileName(options.FilePath)}");
            return;
        }

        UnifiedLogger.LogApplication(LogLevel.INFO, $"Loading file from command line: {UnifiedLogger.SanitizePath(options.FilePath)}");
        await LoadFile(options.FilePath);
    }

    private void RestoreWindowPosition()
    {
        var settings = SettingsService.Instance;
        Radoub.UI.Services.WindowPositionHelper.Restore(this, settings);

        // Restore sidebar width
        if (MainGrid.ColumnDefinitions.Count > 0)
        {
            MainGrid.ColumnDefinitions[0].Width = new GridLength(settings.SidebarWidth);
        }
    }

    private void SaveWindowPosition()
    {
        var settings = SettingsService.Instance;
        Radoub.UI.Services.WindowPositionHelper.Save(this, settings);

        // Save sidebar width
        if (MainGrid.ColumnDefinitions.Count > 0)
        {
            settings.SidebarWidth = MainGrid.ColumnDefinitions[0].Width.Value;
        }

        // Save creature browser panel size (#1145)
        SaveCreatureBrowserPanelSize();
    }

    private async void OnWindowClosing(object? sender, WindowClosingEventArgs e)
    {
        var shouldClose = await Radoub.UI.Services.FileOperationsHelper.HandleClosingAsync(
            this, e, _documentState.IsDirty, async () => { await SaveFile(); return true; });

        if (shouldClose)
        {
            _documentState.ClearDirty();

            // Cancel all async operations
            _windowCts?.Cancel();
            _windowCts?.Dispose();

            SaveWindowPosition();
            _audioService?.Dispose();
            _gameDataService?.Dispose();

            if (e.Cancel)
            {
                // HandleClosingAsync set Cancel=true, we need to re-close
                Close();
            }
        }
    }

    #endregion
}
