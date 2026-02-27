using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DialogEditor.Models;
using Radoub.Formats.Logging;

namespace DialogEditor.Views
{
    /// <summary>
    /// MainWindow partial class for field-level auto-save and debounced file auto-save.
    /// Extracted from MainWindow.Properties.cs for maintainability (#719).
    /// </summary>
    public partial class MainWindow
    {
        private void AutoSaveProperty(string propertyName)
        {
            var result = _services.PropertyAutoSave.AutoSaveProperty(_selectedNode, propertyName);

            if (result.Success)
            {
                _viewModel.HasUnsavedChanges = true;
                _viewModel.StatusMessage = result.Message;
                _viewModel.AddDebugMessage(result.Message);
            }
        }

        // Issue #74/#253: Track field value on focus, only save undo if value changes
        private void OnFieldGotFocus(object? sender, GotFocusEventArgs e)
        {
            if (_selectedNode == null || _uiState.IsPopulatingProperties) return;
            if (_viewModel.CurrentDialog == null) return;

            var control = sender as Control;
            if (control?.Name == null) return;

            // Track original value when focus enters a new field
            if (_currentEditFieldName != control.Name)
            {
                _currentEditFieldName = control.Name;
                _originalFieldValue = GetFieldValue(control);
                _undoStateSavedForCurrentEdit = false;
            }
        }

        // Issue #253: Get the current value of a field control
        private static string? GetFieldValue(Control control)
        {
            return control switch
            {
                TextBox tb => tb.Text,
                _ => null
            };
        }

        // Issue #253: Save undo state only if the field value has actually changed
        private void SaveUndoIfValueChanged(Control control)
        {
            if (_undoStateSavedForCurrentEdit) return;
            if (_viewModel.CurrentDialog == null) return;

            var currentValue = GetFieldValue(control);
            if (currentValue != _originalFieldValue)
            {
                _viewModel.SaveUndoState($"Edit {GetFieldDisplayName(control.Name ?? "")}");
                _undoStateSavedForCurrentEdit = true;
            }
        }

        // Helper to get user-friendly field name for undo description
        private static string GetFieldDisplayName(string fieldName) => fieldName switch
        {
            "SpeakerTextBox" => "Speaker",
            "TextTextBox" => "Text",
            "CommentTextBox" => "Comment",
            "SoundTextBox" => "Sound",
            "DelayTextBox" => "Delay",
            "ScriptAppearsTextBox" => "Conditional Script",
            "ScriptActionTextBox" => "Action Script",
            "ScriptEndTextBox" => "End Script",
            "ScriptAbortTextBox" => "Abort Script",
            _ => fieldName.Replace("TextBox", "")
        };

        // FIELD-LEVEL AUTO-SAVE: Save property when field loses focus
        private void OnFieldLostFocus(object? sender, RoutedEventArgs e)
        {
            if (_selectedNode == null || _selectedNode is TreeViewRootNode || _uiState.IsPopulatingProperties) return;

            // Skip auto-save during token insertion (token handler saves directly to avoid focus jump)
            if (_uiState.IsInsertingToken) return;

            var control = sender as Control;
            if (control == null) return;

            // Issue #253: Save undo state only if value actually changed
            SaveUndoIfValueChanged(control);

            // Issue #478: Only auto-save if value actually changed
            // This prevents false dirty flags when focus moves to other windows (e.g., Conversation Simulator)
            var currentValue = GetFieldValue(control);
            bool valueChanged = currentValue != _originalFieldValue;

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"📝 OnFieldLostFocus: {control.Name}, original='{_originalFieldValue?.Substring(0, Math.Min(30, _originalFieldValue?.Length ?? 0))}', current='{currentValue?.Substring(0, Math.Min(30, currentValue?.Length ?? 0))}', changed={valueChanged}");

            // Clear the edit session tracker (Issue #74)
            _currentEditFieldName = null;
            _originalFieldValue = null;
            _undoStateSavedForCurrentEdit = false;

            // Auto-save the specific property only if it changed
            if (valueChanged)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"📝 OnFieldLostFocus: Calling AutoSaveProperty for {control.Name}");
                AutoSaveProperty(control.Name ?? "");
            }
        }

        // DEBOUNCED FILE AUTO-SAVE: Trigger file save after inactivity
        private void TriggerDebouncedAutoSave()
        {
            // Phase 1 Step 6: Check if auto-save is enabled
            if (!_services.Settings.AutoSaveEnabled)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Auto-save is disabled - skipping");
                return;
            }

            // Stop and dispose existing timer
            _autoSaveTimer?.Stop();
            _autoSaveTimer?.Dispose();

            // Create new timer that fires after configured delay (Issue #62)
            var delayMs = _services.Settings.EffectiveAutoSaveIntervalMs;
            _autoSaveTimer = new System.Timers.Timer(delayMs);
            _autoSaveTimer.AutoReset = false; // Only fire once
            _autoSaveTimer.Elapsed += async (s, e) => await AutoSaveToFileAsync();
            _autoSaveTimer.Start();

            var intervalDesc = _services.Settings.AutoSaveIntervalMinutes > 0
                ? $"{_services.Settings.AutoSaveIntervalMinutes} minute(s)"
                : $"{delayMs}ms (fast debounce)";
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Debounced auto-save scheduled in {intervalDesc}");
        }

        private async Task AutoSaveToFileAsync()
        {
            // Must run on UI thread
            await global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                if (!_viewModel.HasUnsavedChanges || string.IsNullOrEmpty(_viewModel.CurrentFileName))
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "Auto-save skipped: no changes or no file loaded");
                    return;
                }

                // #826: Skip auto-save if filename exceeds Aurora Engine limit
                var filename = System.IO.Path.GetFileNameWithoutExtension(_viewModel.CurrentFileName);
                if (filename.Length > 16)
                {
                    _viewModel.StatusMessage = $"⚠ Filename '{filename}' too long ({filename.Length} chars, max 16). Use Save As to rename.";
                    UnifiedLogger.LogApplication(LogLevel.WARN, $"Auto-save skipped: filename '{filename}' exceeds 16 char limit");
                    return;
                }

                try
                {
                    // Phase 1 Step 4: Enhanced save status indicators
                    _viewModel.StatusMessage = "Auto-saving...";
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "Auto-save starting...");

                    // Issue #8: Check save result before showing success message
                    var success = await _viewModel.SaveDialogAsync(_viewModel.CurrentFileName);

                    if (success)
                    {
                        var timestamp = DateTime.Now.ToString("h:mm tt");
                        var fileName = System.IO.Path.GetFileName(_viewModel.CurrentFileName);
                        _viewModel.StatusMessage = $"✓ Auto-saved '{fileName}' at {timestamp}";

                        // Verify HasUnsavedChanges was cleared (Issue #18)
                        UnifiedLogger.LogApplication(LogLevel.DEBUG,
                            $"Auto-save completed. HasUnsavedChanges = {_viewModel.HasUnsavedChanges}, WindowTitle = '{_viewModel.WindowTitle}'");
                    }
                    else
                    {
                        // Issue #8: Save failed - show visible warning
                        // StatusMessage already set by SaveDialogAsync with specific error
                        // Prepend warning emoji so user notices
                        _viewModel.StatusMessage = $"⚠ {_viewModel.StatusMessage}";
                        UnifiedLogger.LogApplication(LogLevel.WARN, "Auto-save failed - check status message for details");
                    }
                }
                catch (Exception ex)
                {
                    _viewModel.StatusMessage = "⚠ Auto-save failed - click File → Save to retry";
                    UnifiedLogger.LogApplication(LogLevel.ERROR, $"Debounced auto-save failed: {ex.Message}");
                }
            });
        }
    }
}
