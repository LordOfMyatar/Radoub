using System;
using System.IO;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Radoub.Formats.Logging;
using Radoub.Formats.Utp;
using Radoub.UI.Controls;
using Radoub.UI.Undo;
using Radoub.UI.ViewModels;
using PlaceableEditor.Commands;
using PlaceableEditor.ViewModels;
using PlaceableEditor.Views.Panels;

namespace PlaceableEditor.Views;

/// <summary>
/// Editor integration for Reliquary's MainWindow (#2295): loads a UTP into a
/// <see cref="PlaceableViewModel"/>, wires the two content panels' DataContext, and routes every
/// mutation through a shared <see cref="UndoRedoManager"/> (Ctrl+Z/Ctrl+Y). The VariablesPanel is
/// undo-agnostic (#2293) — its Add/Delete events become <see cref="AddVariableCommand"/> /
/// <see cref="RemoveVariableCommand"/> here.
/// </summary>
public partial class MainWindow
{
    private readonly UndoRedoManager _undo = new();
    private PlaceableViewModel? _placeable;
    private bool _editorWired;

    /// <summary>Connect the undo manager + panel events once (called from construction).</summary>
    private void WireEditor()
    {
        if (_editorWired) return;
        _editorWired = true;

        _undo.StateChanged += (_, _) => RefreshUndoMenu();
        RefreshUndoMenu();

        var behavior = this.FindControl<BehaviorPanel>("BehaviorPanel");
        if (behavior != null)
        {
            behavior.Variables.AddRequested += OnVariableAddRequested;
            behavior.Variables.DeleteRequested += OnVariableDeleteRequested;
            behavior.ScriptBrowseRequested += OnScriptBrowseRequested;
        }
    }

    /// <summary>Load a UTP file into the editor, wrap it in a VM, and bind the panels.</summary>
    private void LoadPlaceable(string filePath)
    {
        try
        {
            var utp = UtpReader.Read(filePath);
            _placeable = new PlaceableViewModel(utp);
            _currentFilePath = filePath;

            var identity = this.FindControl<IdentityCombatPanel>("IdentityCombatPanel");
            var behavior = this.FindControl<BehaviorPanel>("BehaviorPanel");
            if (identity != null) identity.DataContext = _placeable;
            if (behavior != null)
            {
                behavior.DataContext = _placeable;
                behavior.Variables.Variables = new System.Collections.ObjectModel.ObservableCollection<VariableViewModel>(
                    _placeable.VarTable.Select(VariableViewModel.FromVariable));
            }

            PopulateAppearanceAndPreview(); // appearance combo + 3D model (when game data configured)

            _undo.Clear(); // fresh history per file
            RefreshUndoMenu();
            UpdateStatus($"Loaded {Path.GetFileName(filePath)}");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Reliquary: failed to load {UnifiedLogger.SanitizePath(filePath)}: {ex.Message}");
            UpdateStatus($"Could not load {Path.GetFileName(filePath)}: {ex.Message}");
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e) => SavePlaceable();

    private void SavePlaceable()
    {
        if (_placeable is null || string.IsNullOrEmpty(_currentFilePath))
        {
            UpdateStatus("Nothing to save — open a placeable first.");
            return;
        }

        try
        {
            UtpWriter.Write(_placeable.WriteToUtp(), _currentFilePath);
            UpdateStatus($"Saved {Path.GetFileName(_currentFilePath)}");
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Reliquary: saved {UnifiedLogger.SanitizePath(_currentFilePath)}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"Reliquary: save failed for {UnifiedLogger.SanitizePath(_currentFilePath)}: {ex.Message}");
            UpdateStatus($"Save failed: {ex.Message}");
        }
    }

    // --- Undo / Redo ---

    private void OnUndoClick(object? sender, RoutedEventArgs e) => _undo.Undo();

    private void OnRedoClick(object? sender, RoutedEventArgs e) => _undo.Redo();

    private void RefreshUndoMenu()
    {
        var undoItem = this.FindControl<MenuItem>("UndoMenuItem");
        var redoItem = this.FindControl<MenuItem>("RedoMenuItem");
        if (undoItem != null)
        {
            undoItem.IsEnabled = _undo.CanUndo;
            undoItem.Header = _undo.CanUndo ? $"_Undo {_undo.UndoDescription}" : "_Undo";
        }
        if (redoItem != null)
        {
            redoItem.IsEnabled = _undo.CanRedo;
            redoItem.Header = _undo.CanRedo ? $"_Redo {_undo.RedoDescription}" : "_Redo";
        }
    }

    // --- Variable mutations (undo-agnostic panel → undoable commands) ---

    private void OnVariableAddRequested(object? sender, VariableAddRequestedEventArgs e)
    {
        if (_placeable is null || sender is not VariablesPanel panel) return;

        var existing = panel.Variables.Select(v => v.Name);
        var vm = new VariableViewModel
        {
            Name = VariableViewModel.NextDefaultName(existing),
            Type = Radoub.Formats.Gff.VariableType.Int,
            ValueText = "0"
        };

        _undo.Execute(new AddVariableCommand(_placeable.VarTable, panel.Variables, vm));
        panel.SelectedVariable = vm;
        panel.FocusSelectedName();
    }

    private void OnVariableDeleteRequested(object? sender, VariableDeleteRequestedEventArgs e)
    {
        if (_placeable is null || sender is not VariablesPanel panel) return;

        // Remove each selected variable as its own undoable step (newest index first keeps indices valid).
        foreach (var vm in e.Variables)
            _undo.Execute(new RemoveVariableCommand(_placeable.VarTable, panel.Variables, vm));
    }

    // --- Scripts ---

    private async void OnScriptBrowseRequested(object? sender, ScriptSlotViewModel slot)
    {
        var context = new PlaceableEditor.Services.ReliquaryScriptBrowserContext(_currentFilePath);
        var browser = new Radoub.UI.Views.ScriptBrowserWindow(context);
        var result = await browser.ShowDialog<string?>(this);
        if (string.IsNullOrEmpty(result)) return;

        var old = slot.ResRef;
        _undo.Execute(new SetFieldCommand<string>(
            () => slot.ResRef, v => slot.ResRef = v, result, $"set {slot.EventName} script"));
        _ = old; // captured by the command at Do-time
    }
}
