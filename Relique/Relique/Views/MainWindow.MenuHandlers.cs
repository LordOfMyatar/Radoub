using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ItemEditor.Services;
using Radoub.Formats.Settings;
using Radoub.UI.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace ItemEditor.Views;

public partial class MainWindow
{
    // --- Menu Handlers ---

    private async void OnNewItemClick(object? sender, RoutedEventArgs e)
    {
        if (_isDirty)
        {
            var result = await PromptSaveChangesAsync();
            if (result == SavePromptResult.Cancel) return;
            if (result == SavePromptResult.Save && !await SaveCurrentFileAsync()) return;
        }
        await ShowNewItemWizard();
    }

    private async Task ShowNewItemWizard()
    {
        var wizard = new NewItemWizardWindow(_gameDataService, _baseItemTypes);
        await wizard.ShowDialog(this);

        if (!string.IsNullOrEmpty(wizard.CreatedFilePath))
        {
            if (wizard.OpenInEditor)
                await OpenFileAsync(wizard.CreatedFilePath);
            UpdateStatus($"Created: {Path.GetFileName(wizard.CreatedFilePath)}");
        }
    }

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        if (_isDirty)
        {
            var result = await PromptSaveChangesAsync();
            if (result == SavePromptResult.Cancel) return;
            if (result == SavePromptResult.Save && !await SaveCurrentFileAsync()) return;
        }

        var files = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Open Item Blueprint",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType("Item Blueprint") { Patterns = new[] { "*.uti" } },
                new Avalonia.Platform.Storage.FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        if (files.Count > 0)
        {
            await OpenFileAsync(files[0].Path.LocalPath);
        }
    }

    private async void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_currentItem != null && !string.IsNullOrEmpty(_currentFilePath))
        {
            await SaveCurrentFileAsync();
        }
        else if (_currentItem != null)
        {
            await SaveAsAsync();
        }
    }

    private async void OnSaveAsClick(object? sender, RoutedEventArgs e)
    {
        await SaveAsAsync();
    }

    private async void OnCloseFileClick(object? sender, RoutedEventArgs e)
    {
        if (_isDirty)
        {
            var result = await PromptSaveChangesAsync();
            if (result == SavePromptResult.Cancel) return;
            if (result == SavePromptResult.Save && !await SaveCurrentFileAsync()) return;
        }

        // Unhook ViewModel events before clearing
        if (_itemViewModel != null)
        {
            _itemViewModel.PropertyChanged -= OnItemPropertyChanged;
        }

        _currentItem = null;
        _currentFilePath = null;
        _itemViewModel = null;
        _documentState.ClearDirty();
        PopulateEditor();
        OnPropertyChanged(nameof(HasFile));
        UpdateStatus("Ready");
    }

    private void OnExitClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void OnToggleItemBrowserClick(object? sender, RoutedEventArgs e)
    {
        SetItemBrowserPanelVisible(!ItemBrowserPanel.IsVisible);
    }

    private void SetItemBrowserPanelVisible(bool visible)
    {
        var browserColumn = OuterContentGrid.ColumnDefinitions[0];
        var splitterColumn = OuterContentGrid.ColumnDefinitions[1];

        if (visible)
        {
            browserColumn.Width = new Avalonia.Controls.GridLength(250, Avalonia.Controls.GridUnitType.Pixel);
            splitterColumn.Width = new Avalonia.Controls.GridLength(4, Avalonia.Controls.GridUnitType.Pixel);
            ItemBrowserPanel.IsVisible = true;
            ItemBrowserSplitter.IsVisible = true;
        }
        else
        {
            browserColumn.Width = new Avalonia.Controls.GridLength(0, Avalonia.Controls.GridUnitType.Pixel);
            splitterColumn.Width = new Avalonia.Controls.GridLength(0, Avalonia.Controls.GridUnitType.Pixel);
            ItemBrowserPanel.IsVisible = false;
            ItemBrowserSplitter.IsVisible = false;
        }

        UpdateItemBrowserMenuState();
    }

    private void UpdateItemBrowserMenuState()
    {
        if (ItemBrowserMenuItem != null)
        {
            ItemBrowserMenuItem.Icon = ItemBrowserPanel.IsVisible ? new TextBlock { Text = "✓" } : null;
        }
    }

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        // TODO (#1706): Settings window
        UpdateStatus("Settings not yet implemented");
    }

    private void OnEditSettingsFileClick(object? sender, RoutedEventArgs e)
    {
        var settingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Radoub", "RadoubSettings.json");

        if (!File.Exists(settingsPath))
        {
            UpdateStatus("Settings file not found");
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(settingsPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Radoub.Formats.Logging.UnifiedLogger.LogApplication(
                Radoub.Formats.Logging.LogLevel.WARN, $"Failed to open settings file: {ex.Message}");
            UpdateStatus("Could not open settings file");
        }
    }

    private void OnToggleUseRadoubThemeClick(object? sender, RoutedEventArgs e)
    {
        var settings = SettingsService.Instance;
        settings.UseSharedTheme = !settings.UseSharedTheme;
        UpdateUseRadoubThemeMenuState();
        Radoub.UI.Services.ThemeManager.Instance.ApplyEffectiveTheme(settings.CurrentThemeId, settings.UseSharedTheme);
    }

    private void UpdateUseRadoubThemeMenuState()
    {
        var menuItem = this.FindControl<MenuItem>("UseRadoubThemeMenuItem");
        if (menuItem != null)
            menuItem.Icon = SettingsService.Instance.UseSharedTheme ? new TextBlock { Text = "✓" } : null;
    }

    private void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        var aboutWindow = Radoub.UI.Views.AboutWindow.Create(new Radoub.UI.Views.AboutWindowConfig
        {
            ToolName = "Relique",
            Version = Radoub.UI.Utils.VersionHelper.GetVersion()
        });
        aboutWindow.Show(this);
    }

    // --- Keyboard Shortcuts ---

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // F4 — toggle item browser (no modifier)
        if (e.Key == Key.F4 && e.KeyModifiers == KeyModifiers.None)
        {
            OnToggleItemBrowserClick(sender, e);
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers == KeyModifiers.Control)
        {
            switch (e.Key)
            {
                case Key.O:
                    OnOpenClick(sender, e);
                    e.Handled = true;
                    break;
                case Key.S:
                    if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                        OnSaveAsClick(sender, e);
                    else
                        OnSaveClick(sender, e);
                    e.Handled = true;
                    break;
                case Key.F:
                    if (HasFile)
                    {
                        OnFindClick(sender, e);
                        e.Handled = true;
                    }
                    break;
                case Key.H:
                    if (HasFile)
                    {
                        OnFindReplaceClick(sender, e);
                        e.Handled = true;
                    }
                    break;
            }
        }
        else if (e.KeyModifiers == KeyModifiers.None)
        {
            switch (e.Key)
            {
                case Key.F3:
                    this.FindControl<SearchBar>("FileSearchBar")?.FindNext();
                    e.Handled = true;
                    break;
            }
        }
        else if (e.KeyModifiers == KeyModifiers.Shift)
        {
            switch (e.Key)
            {
                case Key.F3:
                    this.FindControl<SearchBar>("FileSearchBar")?.FindPrevious();
                    e.Handled = true;
                    break;
            }
        }
    }

    #region Search

    private void OnFindClick(object? sender, RoutedEventArgs e)
    {
        this.FindControl<SearchBar>("FileSearchBar")?.Show(_currentFilePath);
    }

    private void OnFindReplaceClick(object? sender, RoutedEventArgs e)
    {
        this.FindControl<SearchBar>("FileSearchBar")?.ShowReplace(_currentFilePath);
    }

    private async void OnSearchFileModified(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentFilePath))
        {
            await OpenFileAsync(_currentFilePath);
            UpdateStatus("File reloaded after replace.");
        }
    }

    #endregion

    // --- Title Bar ---

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    // --- Utility Methods ---

    private void UpdateStatus(string text)
    {
        StatusText.Text = text;
    }

    private void UpdateModuleIndicator()
    {
        var modulePath = RadoubSettings.Instance.CurrentModulePath;
        if (!string.IsNullOrEmpty(modulePath))
        {
            var name = Path.GetFileNameWithoutExtension(modulePath);
            ModuleIndicator.Text = $"Module: {name}";
        }
        else
        {
            ModuleIndicator.Text = "No module";
        }
    }

    // --- Dialogs ---

    private async Task<SavePromptResult> PromptSaveChangesAsync()
    {
        var dialog = new Window
        {
            Title = "Save Changes?",
            Width = 350,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var result = SavePromptResult.Cancel;

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = "Save changes before closing?", Margin = new Thickness(0, 0, 0, 16) });

        var buttons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = 8 };

        var saveBtn = new Button { Content = "Save" };
        saveBtn.Click += (_, _) => { result = SavePromptResult.Save; dialog.Close(); };

        var dontSaveBtn = new Button { Content = "Don't Save" };
        dontSaveBtn.Click += (_, _) => { result = SavePromptResult.DontSave; dialog.Close(); };

        var cancelBtn = new Button { Content = "Cancel" };
        cancelBtn.Click += (_, _) => { result = SavePromptResult.Cancel; dialog.Close(); };

        buttons.Children.Add(saveBtn);
        buttons.Children.Add(dontSaveBtn);
        buttons.Children.Add(cancelBtn);
        panel.Children.Add(buttons);

        dialog.Content = panel;
        await dialog.ShowDialog(this);
        return result;
    }

    private async Task ShowErrorAsync(string message)
    {
        var dialog = new Window
        {
            Title = "Error",
            Width = 400,
            Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var panel = new StackPanel { Margin = new Thickness(16) };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });

        var okBtn = new Button { Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Margin = new Thickness(0, 16, 0, 0) };
        okBtn.Click += (_, _) => dialog.Close();
        panel.Children.Add(okBtn);

        dialog.Content = panel;
        await dialog.ShowDialog(this);
    }
}
