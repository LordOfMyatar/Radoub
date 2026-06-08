using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Radoub.UI.Controls;
using PlaceableEditor.ViewModels;

namespace PlaceableEditor.Views.Panels;

/// <summary>
/// Scripts &amp; Variables panel (design §5.2): 13 event scripts in two columns plus the shared
/// <see cref="VariablesPanel"/>. Faction / Conversation / Initial State / Treasure Model moved to the
/// Identity &amp; Combat panel (#2425); this panel now lives at the bottom of the editor. Stays thin —
/// script browse and script-set save/load are raised as events for the host (MainWindow) to service
/// with shared dialogs + the undo manager.
/// </summary>
public partial class BehaviorPanel : UserControl
{
    /// <summary>Number of script slots placed in the left column; the rest go right (design §5.2: 7 / 6).</summary>
    private const int LeftColumnCount = 7;

    /// <summary>Raised when a script slot's [...] button is clicked (carries the slot).</summary>
    public event EventHandler<ScriptSlotViewModel>? ScriptBrowseRequested;

    /// <summary>Raised when a script slot's [Edit] button is clicked (carries the slot).</summary>
    public event EventHandler<ScriptSlotViewModel>? ScriptEditRequested;

    /// <summary>Raised when Save Script Set is clicked.</summary>
    public event EventHandler? SaveScriptSetRequested;

    /// <summary>Raised when Load Script Set is clicked.</summary>
    public event EventHandler? LoadScriptSetRequested;

    public BehaviorPanel()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    /// <summary>The embedded shared variables editor (host wires Add/Replace/Delete to undoable commands).</summary>
    public VariablesPanel Variables => this.FindControl<VariablesPanel>("VariablesPanel")!;

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is not PlaceableViewModel vm) return;

        var left = this.FindControl<ItemsControl>("ScriptsLeft");
        var right = this.FindControl<ItemsControl>("ScriptsRight");
        if (left is null || right is null) return;

        var slots = vm.Scripts.ToList();
        left.ItemsSource = slots.Take(LeftColumnCount).ToList();
        right.ItemsSource = slots.Skip(LeftColumnCount).ToList();
    }

    private void OnBrowseScriptClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: ScriptSlotViewModel slot })
            ScriptBrowseRequested?.Invoke(this, slot);
    }

    private void OnEditScriptClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: ScriptSlotViewModel slot })
            ScriptEditRequested?.Invoke(this, slot);
    }

    private void OnSaveScriptSetClick(object? sender, RoutedEventArgs e)
        => SaveScriptSetRequested?.Invoke(this, EventArgs.Empty);

    private void OnLoadScriptSetClick(object? sender, RoutedEventArgs e)
        => LoadScriptSetRequested?.Invoke(this, EventArgs.Empty);
}
