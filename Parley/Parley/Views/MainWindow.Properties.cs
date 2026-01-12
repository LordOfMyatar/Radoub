using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Logging;
using DialogEditor.ViewModels;

namespace DialogEditor.Views
{
    /// <summary>
    /// MainWindow partial class for property panel handlers and auto-save logic.
    /// Extracted from MainWindow.axaml.cs for maintainability (#535).
    /// </summary>
    public partial class MainWindow
    {
        // Issue #74: Track if we've already saved undo state for current edit session
        private string? _currentEditFieldName = null;
        // Issue #253: Track original value to avoid blank undo entries
        private string? _originalFieldValue = null;
        private bool _undoStateSavedForCurrentEdit = false;

        private void SaveCurrentNodeProperties()
        {
            if (_selectedNode == null || _selectedNode is TreeViewRootNode)
            {
                return;
            }

            var dialogNode = _selectedNode.OriginalNode;

            // Issue #342: Use SafeControlFinder for cleaner null-safe control access
            // Update Speaker (only if editable)
            _controls.WithControl<TextBox>("SpeakerTextBox", tb =>
            {
                if (!tb.IsReadOnly)
                    dialogNode.Speaker = tb.Text ?? "";
            });

            // Update Text
            _controls.WithControl<TextBox>("TextTextBox", tb =>
            {
                if (dialogNode.Text != null)
                    dialogNode.Text.Strings[0] = tb.Text ?? "";
            });

            // Update Comment - Issue #12: Save to LinkComment for link nodes
            _controls.WithControl<TextBox>("CommentTextBox", tb =>
            {
                if (_selectedNode.IsChild && _selectedNode.SourcePointer != null)
                    _selectedNode.SourcePointer.LinkComment = tb.Text ?? "";
                else
                    dialogNode.Comment = tb.Text ?? "";
            });

            // Update Sound
            _controls.WithControl<TextBox>("SoundTextBox", tb => dialogNode.Sound = tb.Text ?? "");

            // Update Script
            _controls.WithControl<TextBox>("ScriptActionTextBox", tb => dialogNode.ScriptAction = tb.Text ?? "");

            // Update Conditional Script (on DialogPtr)
            if (_selectedNode.SourcePointer != null)
            {
                _controls.WithControl<TextBox>("ScriptAppearsTextBox", tb =>
                    _selectedNode.SourcePointer.ScriptAppears = tb.Text ?? "");
            }

            // Update Quest
            _controls.WithControl<TextBox>("QuestTextBox", tb => dialogNode.Quest = tb.Text ?? "");

            // Update Animation
            _controls.WithControl<ComboBox>("AnimationComboBox", cb =>
            {
                if (cb.SelectedItem is DialogAnimation selectedAnimation)
                    dialogNode.Animation = selectedAnimation;
            });

            // Update AnimationLoop
            _controls.WithControl<CheckBox>("AnimationLoopCheckBox", cb =>
            {
                if (cb.IsChecked.HasValue)
                    dialogNode.AnimationLoop = cb.IsChecked.Value;
            });

            // Update Quest Entry
            _controls.WithControl<TextBox>("QuestEntryTextBox", tb =>
            {
                if (string.IsNullOrWhiteSpace(tb.Text))
                    dialogNode.QuestEntry = uint.MaxValue;
                else if (uint.TryParse(tb.Text, out uint entryId))
                    dialogNode.QuestEntry = entryId;
            });

            // Update Delay
            _controls.WithControl<TextBox>("DelayTextBox", tb =>
            {
                if (string.IsNullOrWhiteSpace(tb.Text))
                    dialogNode.Delay = uint.MaxValue;
                else if (uint.TryParse(tb.Text, out uint delayMs))
                    dialogNode.Delay = delayMs;
            });

            // CRITICAL FIX: Save script parameters from UI before saving file
            // Update action parameters (on DialogNode)
            _services.ParameterUI.UpdateActionParamsFromUI(dialogNode);

            // Update condition parameters (on DialogPtr if available)
            if (_selectedNode.SourcePointer != null)
            {
                _services.ParameterUI.UpdateConditionParamsFromUI(_selectedNode.SourcePointer);
            }
        }

        private void PopulatePropertiesPanel(TreeViewSafeNode node)
        {
            // CRITICAL FIX: Prevent auto-save during programmatic updates
            _uiState.IsPopulatingProperties = true;

            // CRITICAL FIX: Clear all fields FIRST to prevent stale data
            _services.PropertyPopulator.ClearAllFields();

            // Populate Conversation Settings (dialog-level properties) - always populate these
            _services.PropertyPopulator.PopulateConversationSettings(_viewModel.CurrentDialog);

            // Issue #19: If ROOT node selected, keep only conversation settings enabled
            // All node-specific properties should remain disabled
            if (node is TreeViewRootNode)
            {
                _uiState.IsPopulatingProperties = false;
                return; // Node fields remain disabled from ClearAllFields
            }

            var dialogNode = node.OriginalNode;

            // Debug: Log node type for Issue #12 investigation
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"🔍 PopulatePropertiesPanel: NodeType={node.GetType().Name}, IsChild={node.IsChild}, " +
                $"HasSourcePointer={node.SourcePointer != null}, DisplayText='{node.DisplayText}'");

            // Populate all node properties using helper
            _services.PropertyPopulator.PopulateNodeType(dialogNode);
            _services.PropertyPopulator.PopulateSpeaker(dialogNode);
            _services.PropertyPopulator.PopulateBasicProperties(dialogNode, node);
            _services.PropertyPopulator.PopulateAnimation(dialogNode);
            _services.PropertyPopulator.PopulateIsChildIndicator(node);

            // Populate scripts with callbacks for async operations
            _services.PropertyPopulator.PopulateScripts(dialogNode, node,
                (script, isCondition) => _ = LoadParameterDeclarationsAsync(script, isCondition),
                (script, isCondition) => _ = LoadScriptPreviewAsync(script, isCondition),
                (isCondition) => ClearScriptPreview(isCondition));

            // Populate quest fields
            _services.PropertyPopulator.PopulateQuest(dialogNode);

            // Populate script parameters
            _services.PropertyPopulator.PopulateParameterGrids(dialogNode, node.SourcePointer, AddParameterRow);

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Populated properties for node: {dialogNode.DisplayText}");

            // Re-enable auto-save after population complete
            _uiState.IsPopulatingProperties = false;
        }

        private void OnPropertyChanged(object? sender, RoutedEventArgs e)
        {
            if (_selectedNode == null) return;

            var dialogNode = _selectedNode.OriginalNode;
            var textBox = sender as TextBox;

            if (textBox == null) return;

            // Determine which property changed based on control name
            switch (textBox.Name)
            {
                case "SpeakerTextBox":
                    dialogNode.Speaker = textBox.Text ?? "";
                    _viewModel.HasUnsavedChanges = true;
                    // Refresh tree to update node color if speaker changed
                    _viewModel.StatusMessage = "Speaker updated";
                    break;

                case "TextTextBox":
                    if (dialogNode.Text != null)
                    {
                        // Update the default language string (0)
                        dialogNode.Text.Strings[0] = textBox.Text ?? "";
                        _viewModel.HasUnsavedChanges = true;
                        // Refresh tree to show new text
                        RefreshTreeDisplay();
                        _viewModel.StatusMessage = "Text updated";
                    }
                    break;

                case "SoundTextBox":
                    dialogNode.Sound = textBox.Text ?? "";
                    _viewModel.HasUnsavedChanges = true;
                    _viewModel.StatusMessage = "Sound updated";
                    break;

                case "ScriptTextBox":
                    dialogNode.ScriptAction = textBox.Text ?? "";
                    _viewModel.HasUnsavedChanges = true;
                    _viewModel.StatusMessage = "Script updated";
                    break;

                case "CommentTextBox":
                    // Issue #12: Save to LinkComment for link nodes
                    if (_selectedNode.IsChild && _selectedNode.SourcePointer != null)
                    {
                        _selectedNode.SourcePointer.LinkComment = textBox.Text ?? "";
                    }
                    else
                    {
                        dialogNode.Comment = textBox.Text ?? "";
                    }
                    _viewModel.HasUnsavedChanges = true;
                    _viewModel.StatusMessage = "Comment updated";
                    break;

                case "QuestTextBox":
                    dialogNode.Quest = textBox.Text ?? "";
                    _viewModel.HasUnsavedChanges = true;
                    _viewModel.StatusMessage = "Quest updated";
                    break;
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Property '{textBox.Name}' changed for node: {dialogNode.DisplayText}");
        }

        // FIELD-LEVEL AUTO-SAVE: Event handlers for immediate save
        private void OnAnimationSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"OnAnimationSelectionChanged: _selectedNode={_selectedNode != null}, _uiState.IsPopulatingProperties={_uiState.IsPopulatingProperties}");

            if (_selectedNode != null && !_uiState.IsPopulatingProperties)
            {
                var comboBox = sender as ComboBox;
                if (comboBox != null)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"OnAnimationSelectionChanged: ComboBox SelectedItem type={comboBox.SelectedItem?.GetType().Name ?? "null"}, value={comboBox.SelectedItem}");
                }

                // Delay auto-save to ensure SelectedItem has fully updated
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, "OnAnimationSelectionChanged: Dispatcher.Post executing AutoSaveProperty");
                    AutoSaveProperty("AnimationComboBox");
                }, global::Avalonia.Threading.DispatcherPriority.Normal);
            }
        }

        // INPUT VALIDATION: Only allow integers in Delay field
        private void OnDelayTextChanged(object? sender, global::Avalonia.Controls.TextChangedEventArgs e)
        {
            if (sender is not TextBox textBox) return;

            // Allow empty string (will be treated as 0)
            if (string.IsNullOrWhiteSpace(textBox.Text)) return;

            // Filter out non-numeric characters
            var filteredText = new string(textBox.Text.Where(char.IsDigit).ToArray());

            if (textBox.Text != filteredText)
            {
                var caretIndex = textBox.CaretIndex;
                textBox.Text = filteredText;
                // Restore caret position (or move to end if text got shorter)
                textBox.CaretIndex = Math.Min(caretIndex, filteredText.Length);
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
            if (_selectedNode == null || _uiState.IsPopulatingProperties) return;

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

        // DEBOUNCED FILE AUTO-SAVE: Trigger file save after inactivity
        private void TriggerDebouncedAutoSave()
        {
            // Phase 1 Step 6: Check if auto-save is enabled
            if (!SettingsService.Instance.AutoSaveEnabled)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "Auto-save is disabled - skipping");
                return;
            }

            // Stop and dispose existing timer
            _autoSaveTimer?.Stop();
            _autoSaveTimer?.Dispose();

            // Create new timer that fires after configured delay (Issue #62)
            var delayMs = SettingsService.Instance.EffectiveAutoSaveIntervalMs;
            _autoSaveTimer = new System.Timers.Timer(delayMs);
            _autoSaveTimer.AutoReset = false; // Only fire once
            _autoSaveTimer.Elapsed += async (s, e) => await AutoSaveToFileAsync();
            _autoSaveTimer.Start();

            var intervalDesc = SettingsService.Instance.AutoSaveIntervalMinutes > 0
                ? $"{SettingsService.Instance.AutoSaveIntervalMinutes} minute(s)"
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
                    _viewModel.StatusMessage = "💾 Auto-saving...";
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

        // MANUAL SAVE: Keep for compatibility, but properties already saved by auto-save
        private async void OnSaveChangesClick(object? sender, RoutedEventArgs e)
        {
            if (_selectedNode == null)
            {
                _viewModel.StatusMessage = "No node selected";
                return;
            }

            var dialogNode = _selectedNode.OriginalNode;

            // CRITICAL FIX: Save ALL editable properties, not just Speaker and Text

            // Update Speaker
            var speakerTextBox = this.FindControl<TextBox>("SpeakerTextBox");
            if (speakerTextBox != null && !speakerTextBox.IsReadOnly)
            {
                dialogNode.Speaker = speakerTextBox.Text ?? "";
            }

            // Update Text
            var textTextBox = this.FindControl<TextBox>("TextTextBox");
            if (textTextBox != null && dialogNode.Text != null)
            {
                dialogNode.Text.Strings[0] = textTextBox.Text ?? "";
            }

            // Update Comment - Issue #12: Save to LinkComment for link nodes
            var commentTextBox = this.FindControl<TextBox>("CommentTextBox");
            if (commentTextBox != null)
            {
                if (_selectedNode.IsChild && _selectedNode.SourcePointer != null)
                {
                    _selectedNode.SourcePointer.LinkComment = commentTextBox.Text ?? "";
                }
                else
                {
                    dialogNode.Comment = commentTextBox.Text ?? "";
                }
            }

            // Update Sound
            var soundTextBox = this.FindControl<TextBox>("SoundTextBox");
            if (soundTextBox != null)
            {
                dialogNode.Sound = soundTextBox.Text ?? "";
            }

            // Update Script Action
            var scriptTextBox = this.FindControl<TextBox>("ScriptActionTextBox");
            if (scriptTextBox != null)
            {
                dialogNode.ScriptAction = scriptTextBox.Text ?? "";
            }

            // Update Quest
            var questTextBox = this.FindControl<TextBox>("QuestTextBox");
            if (questTextBox != null)
            {
                dialogNode.Quest = questTextBox.Text ?? "";
            }

            // Update Animation
            var animationComboBox = this.FindControl<ComboBox>("AnimationComboBox");
            if (animationComboBox != null && animationComboBox.SelectedItem is DialogAnimation selectedAnimation)
            {
                dialogNode.Animation = selectedAnimation;
            }

            // Update Animation Loop
            var animationLoopCheckBox = this.FindControl<CheckBox>("AnimationLoopCheckBox");
            if (animationLoopCheckBox != null && animationLoopCheckBox.IsChecked.HasValue)
            {
                dialogNode.AnimationLoop = animationLoopCheckBox.IsChecked.Value;
            }

            _viewModel.HasUnsavedChanges = true;

            // Refresh tree WITHOUT collapsing
            RefreshTreeDisplayPreserveState();

            // CRITICAL: Save to file immediately
            if (!string.IsNullOrEmpty(_viewModel.CurrentFileName))
            {
                await _viewModel.SaveDialogAsync(_viewModel.CurrentFileName);
                _viewModel.StatusMessage = "All changes saved to file";
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Node properties saved: {dialogNode.DisplayText}");
            }
            else
            {
                _viewModel.StatusMessage = "Changes saved to memory (use File → Save to persist)";
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Node updated: {dialogNode.DisplayText}");
            }
        }

        private void OnConversationSettingChanged(object? sender, RoutedEventArgs e)
        {
            if (_viewModel.CurrentDialog == null) return;

            var preventZoomCheckBox = this.FindControl<CheckBox>("PreventZoomCheckBox");
            var scriptEndTextBox = this.FindControl<TextBox>("ScriptEndTextBox");
            var scriptAbortTextBox = this.FindControl<TextBox>("ScriptAbortTextBox");

            if (preventZoomCheckBox != null)
            {
                _viewModel.CurrentDialog.PreventZoom = preventZoomCheckBox.IsChecked ?? false;
            }

            if (scriptEndTextBox != null)
            {
                _viewModel.CurrentDialog.ScriptEnd = scriptEndTextBox.Text?.Trim() ?? string.Empty;
            }

            if (scriptAbortTextBox != null)
            {
                _viewModel.CurrentDialog.ScriptAbort = scriptAbortTextBox.Text?.Trim() ?? string.Empty;
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"Conversation settings updated: PreventZoom={_viewModel.CurrentDialog.PreventZoom}, " +
                $"ScriptEnd='{_viewModel.CurrentDialog.ScriptEnd}', ScriptAbort='{_viewModel.CurrentDialog.ScriptAbort}'");
        }
    }
}
