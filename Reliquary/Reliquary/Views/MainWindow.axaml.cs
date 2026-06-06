using System;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using Radoub.Formats.Logging;
using Radoub.UI.Services;
using PlaceableEditor.Views.Panels;

namespace PlaceableEditor.Views;

/// <summary>
/// Reliquary main window. Sprint 4 ships a demoable skeleton: browser sidebar +
/// four stubbed content panels. Editor wiring lands in Sprints 5-6. Organized as
/// partial classes (QM/Relique pattern): this file owns construction and menu
/// handlers; MainWindow.Lifecycle.cs owns window/browser lifecycle.
/// </summary>
public partial class MainWindow : Window
{
    private readonly DocumentState _documentState = new("Reliquary");

    /// <summary>Current file path, tracked by the shared <see cref="DocumentState"/> (drives the title bar).</summary>
    private string? _currentFilePath
    {
        get => _documentState.CurrentFilePath;
        set => _documentState.CurrentFilePath = value;
    }

    /// <summary>True while loading a file — suppresses dirty marking from binding-driven VM updates.</summary>
    private bool _isLoading
    {
        get => _documentState.IsLoading;
        set => _documentState.IsLoading = value;
    }

    private bool _isDirty => _documentState.IsDirty;

    public MainWindow()
    {
        InitializeComponent();

        _documentState.DirtyStateChanged += () => Title = _documentState.GetTitle();
        Title = _documentState.GetTitle();

        WireBrowserPanel();
        WireEditor();
        WireServices();
        WireInventory();

        // Tunnel so F4 reaches us before a focused child (ComboBox etc.) can consume it.
        AddHandler(KeyDownEvent, OnWindowKeyDownTunnel, Avalonia.Interactivity.RoutingStrategies.Tunnel);

        Closing += OnWindowClosing;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void UpdateStatus(string message)
    {
        var status = this.FindControl<TextBlock>("StatusBar");
        if (status != null)
            status.Text = message;
    }

    private void OnExitClick(object? sender, RoutedEventArgs e) => Close();

    /// <summary>
    /// Window-level keyboard shortcuts. MenuItem InputGesture only renders the hint text in
    /// Avalonia — it does not register a global accelerator — so the gestures are dispatched here.
    /// </summary>
    /// <summary>Tunnel handler: catch F4 (toggle browser) before a focused child consumes it.</summary>
    private void OnWindowKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F4 && e.KeyModifiers == KeyModifiers.None)
        {
            var browser = this.FindControl<PlaceableBrowserPanel>("PlaceableBrowserPanel");
            if (browser != null) browser.IsCollapsed = !browser.IsCollapsed;
            e.Handled = true;
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        // Require Control; allow an optional Shift (for Ctrl+Shift+S = Save As).
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;

        // When a text editor has focus, Ctrl+Z/Y belong to the TextBox's own undo stack — text
        // edits flow through binding, not the document UndoRedoManager, so intercepting here would
        // run an unrelated document undo and desync the text field. Let the TextBox handle them.
        bool textFocused = TopLevel.GetTopLevel(this)?.FocusManager?.GetFocusedElement() is TextBox;

        switch (e.Key)
        {
            case Key.S:
                if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
                    OnSaveAsClick(sender, e);
                else
                    OnSaveClick(sender, e);
                e.Handled = true;
                break;
            case Key.O:
                OnOpenClick(sender, e);
                e.Handled = true;
                break;
            case Key.Z:
                if (textFocused) break; // let the TextBox undo its own edits
                OnUndoClick(sender, e);
                e.Handled = true;
                break;
            case Key.Y:
                if (textFocused) break;
                OnRedoClick(sender, e);
                e.Handled = true;
                break;
        }
    }

    private async void OnOpenClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Open Placeable",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new Avalonia.Platform.Storage.FilePickerFileType("Placeable Blueprint") { Patterns = new[] { "*.utp" } },
                new Avalonia.Platform.Storage.FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        var path = files.Count > 0 ? files[0].TryGetLocalPath() : null;
        if (string.IsNullOrEmpty(path)) return;
        if (!await ConfirmDiscardAsync()) return; // prompt before discarding unsaved edits
        LoadPlaceable(path);
    }

    private void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            var aboutWindow = Radoub.UI.Views.AboutWindow.Create(new Radoub.UI.Views.AboutWindowConfig
            {
                ToolName = "Reliquary",
                Version = Radoub.UI.Utils.VersionHelper.GetVersion(),
                Subtitle = "Placeable Blueprint Editor"
            });
            aboutWindow.Show(this);
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Reliquary: About window failed: {ex.Message}");
        }
    }
}
