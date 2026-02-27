using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DialogEditor.Models;

using Radoub.Formats.Logging;
using DialogEditor.ViewModels;

namespace DialogEditor.Views
{
    /// <summary>
    /// MainWindow partial class for property panel handlers and manual save logic.
    /// Extracted from MainWindow.axaml.cs for maintainability (#535).
    ///
    /// Related partial files: MainWindow.AutoSave.cs, MainWindow.ResourceBrowsing.cs.
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
            _services.PropertyPopulator.PopulateConversationSettings(_viewModel.CurrentDialog, _viewModel.CurrentFilePath);

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
            _services.PropertyPopulator.PopulateSpeaker(dialogNode, _services.Creature);
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

        #region Parameter Panel Handlers

        private void OnAddConditionsParamClick(object? sender, RoutedEventArgs e)
        {
            _services.ParameterUI.OnAddConditionsParamClick();
        }

        private void OnAddActionsParamClick(object? sender, RoutedEventArgs e)
        {
            _services.ParameterUI.OnAddActionsParamClick();
        }

        private async void OnSuggestConditionsParamClick(object? sender, RoutedEventArgs e)
            => await _controllers.ParameterBrowser.OnSuggestConditionsParamClickAsync();

        private async void OnSuggestActionsParamClick(object? sender, RoutedEventArgs e)
            => await _controllers.ParameterBrowser.OnSuggestActionsParamClickAsync();

        #endregion

        #region Script Browser Delegation Methods
        // These methods delegate to controllers/services but are kept as instance methods
        // because they're passed as callbacks to PropertyAutoSaveService and PropertyPanelPopulator
        // during construction before the controllers are initialized.

        private Task LoadParameterDeclarationsAsync(string scriptName, bool isCondition)
            => _controllers.ParameterBrowser.LoadParameterDeclarationsAsync(scriptName, isCondition);

        private Task LoadScriptPreviewAsync(string scriptName, bool isCondition)
            => _services.ScriptPreview.LoadScriptPreviewAsync(scriptName, isCondition);

        private void ClearScriptPreview(bool isCondition)
            => _services.ScriptPreview.ClearScriptPreview(isCondition);

        // Issue #664: Population wrapper - always passes focusNewRow=false to prevent focus stealing
        private void AddParameterRow(StackPanel parent, string key, string value, bool isCondition)
            => _services.ParameterUI.AddParameterRow(parent, key, value, isCondition, focusNewRow: false);

        #endregion
    }
}
