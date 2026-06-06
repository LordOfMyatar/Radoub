using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Radoub.UI.Controls;
using PlaceableEditor.ViewModels;

namespace PlaceableEditor.Views.Panels;

/// <summary>
/// Behavior panel (design §5.2): 13 event scripts in two columns, advanced behavior fields, and
/// the shared <see cref="VariablesPanel"/>. Stays thin — script browse, script-set save/load, and
/// conversation-edit dispatch are raised as events for the host (MainWindow) to service with shared
/// dialogs + the undo manager. Faction list is populated by the host (no hardcoded factions).
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

    /// <summary>Raised when the conversation Edit→Parley button is clicked (Sprint 7 dispatch).</summary>
    public event EventHandler? EditConversationRequested;

    /// <summary>Raised when the conversation Browse… button is clicked (#2373; host opens the shared DialogBrowser).</summary>
    public event EventHandler? ConversationBrowseRequested;

    /// <summary>Raised when the user picks a faction (carries the faction id) for the host to wrap in undo.</summary>
    public event EventHandler<uint>? FactionChanged;

    /// <summary>Suppresses <see cref="FactionChanged"/> while the host populates/preselects the combo.</summary>
    private bool _suppressFactionEvent;

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

    private void OnEditConversationClick(object? sender, RoutedEventArgs e)
        => EditConversationRequested?.Invoke(this, EventArgs.Empty);

    private void OnBrowseConversationClick(object? sender, RoutedEventArgs e)
        => ConversationBrowseRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>
    /// Fill the Faction combo from the host-provided list (loaded from the module's repute.fac) and
    /// preselect <paramref name="selectedId"/>. The host owns the list + undo wrapping; the combo
    /// only raises <see cref="FactionChanged"/> on user edits, never during this populate.
    /// </summary>
    public void PopulateFactions(System.Collections.Generic.IReadOnlyList<(ushort Id, string Name)> factions, uint selectedId)
    {
        var combo = this.FindControl<ComboBox>("FactionCombo");
        if (combo is null) return;

        _suppressFactionEvent = true;
        try
        {
            var items = factions.Select(f => new FactionItem(f.Id, f.Name)).ToList();
            combo.ItemsSource = items;
            combo.SelectedItem = items.FirstOrDefault(i => i.Id == selectedId);
        }
        finally
        {
            _suppressFactionEvent = false;
        }
    }

    private void OnFactionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressFactionEvent) return;
        if (sender is ComboBox { SelectedItem: FactionItem item })
            FactionChanged?.Invoke(this, item.Id);
    }

    /// <summary>Display item for the Faction combo: shows the name, carries the faction id.</summary>
    private sealed record FactionItem(ushort Id, string Name)
    {
        public override string ToString() => Name;
    }
}
