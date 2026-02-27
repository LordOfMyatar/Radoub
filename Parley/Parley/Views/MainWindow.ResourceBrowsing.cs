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
            // Set flag to suppress tree refresh during token insertion
            _uiState.IsInsertingToken = true;
            try
            {
                // Capture cursor position BEFORE opening dialog (OnFieldLostFocus fires when button clicked)
                var textBox = this.FindControl<TextBox>("TextTextBox");
                var savedSelStart = textBox?.SelectionStart ?? 0;
                var savedSelEnd = textBox?.SelectionEnd ?? 0;
                var savedText = textBox?.Text ?? "";

                var tokenWindow = new TokenSelectorWindow();
                var result = await tokenWindow.ShowDialog<bool>(this);

                if (result && !string.IsNullOrEmpty(tokenWindow.SelectedToken) && textBox != null)
                {
                    // Use saved values since focus was lost when dialog opened
                    var selStart = savedSelStart;
                    var selLength = savedSelEnd - savedSelStart;
                    var currentText = savedText;

                    string newText;
                    int newCursorPos;

                    // Determine if we need a space before the token
                    // Add space if: not at start, and previous char is not whitespace
                    var needsSpaceBefore = selStart > 0 &&
                        !char.IsWhiteSpace(currentText[selStart - 1]);
                    var tokenToInsert = needsSpaceBefore
                        ? " " + tokenWindow.SelectedToken
                        : tokenWindow.SelectedToken;

                    if (selLength > 0)
                    {
                        // Replace selection
                        newText = currentText.Remove(selStart, selLength).Insert(selStart, tokenToInsert);
                        newCursorPos = selStart + tokenToInsert.Length;
                    }
                    else
                    {
                        // Insert at cursor
                        newText = currentText.Insert(selStart, tokenToInsert);
                        newCursorPos = selStart + tokenToInsert.Length;
                    }

                    textBox.Text = newText;

                    // Save directly to node without tree refresh (avoids focus jump)
                    if (_selectedNode?.OriginalNode?.Text != null)
                    {
                        _selectedNode.OriginalNode.Text.Strings[0] = newText;
                        _viewModel.HasUnsavedChanges = true;
                        _viewModel.StatusMessage = "Text updated with token";
                    }

                    // Restore cursor position and focus
                    textBox.SelectionStart = newCursorPos;
                    textBox.SelectionEnd = newCursorPos;
                    textBox.Focus();
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
