using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DialogEditor.Models;
using DialogEditor.ViewModels;
using Radoub.Formats.Logging;

namespace DialogEditor.Views
{
    /// <summary>
    /// MainWindow partial class for resource browsing, token insertion, and quest UI handlers.
    /// Extracted from MainWindow.Properties.cs for maintainability (#719).
    /// </summary>
    public partial class MainWindow
    {
        #region Resource Browser Handlers

        private async void OnBrowseCreatureClick(object? sender, RoutedEventArgs e)
        {
            await _services.ResourceBrowser.BrowseCreatureAsync(this);
        }

        private async void OnInsertTokenClick(object? sender, RoutedEventArgs e)
        {
            // #2032: IsInsertingToken still suppresses OnFieldLostFocus auto-save
            // (the token dialog steals focus, which would otherwise re-fire AutoSaveProperty
            // on the same unchanged value). Tree rebuild is no longer the risk since
            // PropertyAutoSaveService now uses DialogChangeKind.TextOnly, but the lost-focus
            // guard still matters to avoid a redundant save pass.
            _uiState.IsInsertingToken = true;
            try
            {
                // Capture cursor position BEFORE opening dialog (focus is lost when button clicked)
                var textBox = this.FindControl<TextBox>("TextTextBox");
                var savedSelStart = textBox?.SelectionStart ?? 0;
                var savedSelEnd = textBox?.SelectionEnd ?? 0;
                var savedText = textBox?.Text ?? "";

                var tokenWindow = new TokenSelectorWindow();
                var result = await tokenWindow.ShowDialog<bool>(this);

                if (result && !string.IsNullOrEmpty(tokenWindow.SelectedToken) && textBox != null)
                {
                    var selStart = savedSelStart;
                    var selLength = savedSelEnd - savedSelStart;
                    var currentText = savedText;

                    // Determine if we need spaces around the token
                    var needsSpaceBefore = selStart > 0 &&
                        !char.IsWhiteSpace(currentText[selStart - 1]);
                    var needsSpaceAfter = selStart < currentText.Length &&
                        !char.IsWhiteSpace(currentText[selStart]);
                    var tokenToInsert = (needsSpaceBefore ? " " : "") +
                        tokenWindow.SelectedToken +
                        (needsSpaceAfter ? " " : "");

                    string newText;
                    int newCursorPos;
                    if (selLength > 0)
                    {
                        newText = currentText.Remove(selStart, selLength).Insert(selStart, tokenToInsert);
                        newCursorPos = selStart + tokenToInsert.Length;
                    }
                    else
                    {
                        newText = currentText.Insert(selStart, tokenToInsert);
                        newCursorPos = selStart + tokenToInsert.Length;
                    }

                    textBox.Text = newText;

                    // #2032: Update node, notify bindings, publish TextOnly so FlowView
                    // / TreeView repaint in place. No tree rebuild — focus stays put.
                    if (_selectedNode?.OriginalNode?.Text != null)
                    {
                        _selectedNode.OriginalNode.Text.Strings[0] = newText;
                        _selectedNode.NotifyTextChanged();
                        _viewModel.HasUnsavedChanges = true;
                        _viewModel.StatusMessage = "Text updated with token";

                        DialogEditor.Services.DialogChangeEventBus.Instance.PublishNodeModified(
                            _selectedNode.OriginalNode,
                            "TokenInserted",
                            DialogEditor.Services.DialogChangeKind.TextOnly);
                    }

                    // Restore cursor position after the token dialog finishes closing.
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        textBox.Focus();
                        textBox.SelectionStart = newCursorPos;
                        textBox.SelectionEnd = newCursorPos;
                    }, Avalonia.Threading.DispatcherPriority.Background);
                }
            }
            catch (Exception ex)
            {
                _viewModel.StatusMessage = $"Error inserting token: {ex.Message}";
            }
            finally
            {
                _uiState.IsInsertingToken = false;
            }
        }

        private void OnRecentCreatureTagSelected(object? sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (_selectedNode == null || _selectedNode is TreeViewRootNode)
                {
                    return;
                }

                var comboBox = sender as ComboBox;
                if (comboBox?.SelectedItem is string selectedTag && !string.IsNullOrEmpty(selectedTag))
                {
                    // Populate Speaker field
                    var speakerTextBox = this.FindControl<TextBox>("SpeakerTextBox");
                    if (speakerTextBox != null)
                    {
                        speakerTextBox.Text = selectedTag;
                        AutoSaveProperty("SpeakerTextBox");
                    }

                    _viewModel.StatusMessage = $"Selected recent tag: {selectedTag}";
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Applied recent creature tag: {selectedTag}");

                    // Clear selection so same tag can be selected again
                    comboBox.SelectedItem = null;
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error selecting recent tag: {ex.Message}");
            }
        }

        // Speaker visual event handlers - delegates to SpeakerVisualController (#1223)
        private void OnSpeakerShapeChanged(object? sender, SelectionChangedEventArgs e)
            => _controllers.SpeakerVisual.OnSpeakerShapeChanged(sender, e);

        private void OnSpeakerColorChanged(object? sender, SelectionChangedEventArgs e)
            => _controllers.SpeakerVisual.OnSpeakerColorChanged(sender, e);

        #endregion

        #region Quest UI Handlers

        private void OnQuestTagTextChanged(object? sender, TextChangedEventArgs e) =>
            _controllers.Quest.OnQuestTagTextChanged(sender, e);

        private void OnQuestTagLostFocus(object? sender, RoutedEventArgs e) =>
            _controllers.Quest.OnQuestTagLostFocus(sender, e);

        private void OnQuestEntryTextChanged(object? sender, TextChangedEventArgs e) =>
            _controllers.Quest.OnQuestEntryTextChanged(sender, e);

        private void OnQuestEntryLostFocus(object? sender, RoutedEventArgs e) =>
            _controllers.Quest.OnQuestEntryLostFocus(sender, e);

        private void OnBrowseQuestClick(object? sender, RoutedEventArgs e) =>
            _controllers.Quest.OnBrowseQuestClick(sender, e);

        private void OnBrowseQuestEntryClick(object? sender, RoutedEventArgs e) =>
            _controllers.Quest.OnBrowseQuestEntryClick(sender, e);

        private void OnClearQuestTagClick(object? sender, RoutedEventArgs e) =>
            _controllers.Quest.OnClearQuestTagClick(sender, e);

        private void OnClearQuestEntryClick(object? sender, RoutedEventArgs e) =>
            _controllers.Quest.OnClearQuestEntryClick(sender, e);

        #endregion
    }
}
