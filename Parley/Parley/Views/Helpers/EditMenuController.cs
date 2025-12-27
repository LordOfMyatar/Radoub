using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Logging;
using DialogEditor.ViewModels;

namespace Parley.Views.Helpers
{
    /// <summary>
    /// Manages all Edit menu operations for MainWindow.
    /// Extracted from MainWindow to reduce method size and improve maintainability (Epic #457, Sprint 5).
    ///
    /// Handles:
    /// 1. Undo/Redo operations
    /// 2. Cut/Copy/Paste node operations
    /// 3. Copy to clipboard operations (text, properties, tree structure)
    /// </summary>
    public class EditMenuController
    {
        private readonly Window _window;
        private readonly Func<MainViewModel> _getViewModel;
        private readonly Func<TreeViewSafeNode?> _getSelectedNode;

        public EditMenuController(
            Window window,
            Func<MainViewModel> getViewModel,
            Func<TreeViewSafeNode?> getSelectedNode)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _getViewModel = getViewModel ?? throw new ArgumentNullException(nameof(getViewModel));
            _getSelectedNode = getSelectedNode ?? throw new ArgumentNullException(nameof(getSelectedNode));
        }

        private MainViewModel ViewModel => _getViewModel();
        private TreeViewSafeNode? SelectedNode => _getSelectedNode();

        #region Undo/Redo

        public void OnUndoClick(object? sender, RoutedEventArgs e)
            => ViewModel.Undo();

        public void OnRedoClick(object? sender, RoutedEventArgs e)
            => ViewModel.Redo();

        #endregion

        #region Cut/Copy/Paste Node Operations

        public void OnCutNodeClick(object? sender, RoutedEventArgs e)
        {
            var selectedNode = SelectedNode;
            if (selectedNode == null)
            {
                ViewModel.StatusMessage = "Please select a node to cut";
                return;
            }

            ViewModel.CutNode(selectedNode);
        }

        public void OnCopyNodeClick(object? sender, RoutedEventArgs e)
        {
            var selectedNode = SelectedNode;
            if (selectedNode == null)
            {
                ViewModel.StatusMessage = "Please select a node to copy";
                return;
            }

            ViewModel.CopyNode(selectedNode);
        }

        public void OnPasteAsDuplicateClick(object? sender, RoutedEventArgs e)
        {
            var selectedNode = SelectedNode;
            if (selectedNode == null)
            {
                ViewModel.StatusMessage = "Please select a parent node to paste under";
                return;
            }

            ViewModel.PasteAsDuplicate(selectedNode);
        }

        public async void OnPasteAsLinkClick(object? sender, RoutedEventArgs e)
        {
            var selectedNode = SelectedNode;
            if (selectedNode == null)
            {
                ViewModel.StatusMessage = "Please select a parent node to paste link under";
                return;
            }

            // Issue #123: Check if clipboard is from Cut operation
            if (ViewModel.ClipboardWasCut)
            {
                var result = await ShowPasteAsLinkAfterCutDialog();
                switch (result)
                {
                    case PasteAfterCutChoice.UndoCut:
                        ViewModel.Undo();
                        ViewModel.StatusMessage = "Cut operation undone. Please copy the node again, then paste as link.";
                        return;
                    case PasteAfterCutChoice.PasteAsCopy:
                        ViewModel.PasteAsDuplicate(selectedNode);
                        return;
                    case PasteAfterCutChoice.Cancel:
                    default:
                        ViewModel.StatusMessage = "Paste as link cancelled";
                        return;
                }
            }

            ViewModel.PasteAsLink(selectedNode);
        }

        #endregion

        #region Copy to Clipboard Operations

        public async void OnCopyNodeTextClick(object? sender, RoutedEventArgs e)
        {
            var selectedNode = SelectedNode;
            var text = ViewModel.GetNodeText(selectedNode);

            if (!string.IsNullOrEmpty(text))
            {
                var clipboard = TopLevel.GetTopLevel(_window)?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(text);
                    ViewModel.StatusMessage = "Copied node text to clipboard";
                    UnifiedLogger.LogApplication(LogLevel.INFO, "Copied node text to clipboard");
                }
            }
            else
            {
                ViewModel.StatusMessage = "No node selected or node has no text";
            }
        }

        public async void OnCopyNodePropertiesClick(object? sender, RoutedEventArgs e)
        {
            var selectedNode = SelectedNode;
            var properties = ViewModel.GetNodeProperties(selectedNode);

            if (!string.IsNullOrEmpty(properties))
            {
                var clipboard = TopLevel.GetTopLevel(_window)?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(properties);
                    ViewModel.StatusMessage = "Copied node properties to clipboard";
                    UnifiedLogger.LogApplication(LogLevel.INFO, "Copied node properties to clipboard");
                }
            }
            else
            {
                ViewModel.StatusMessage = "No node selected";
            }
        }

        public async void OnCopyTreeStructureClick(object? sender, RoutedEventArgs e)
        {
            var treeStructure = ViewModel.GetTreeStructure();

            if (!string.IsNullOrEmpty(treeStructure))
            {
                var clipboard = TopLevel.GetTopLevel(_window)?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(treeStructure);
                    ViewModel.StatusMessage = "Copied tree structure to clipboard";
                    UnifiedLogger.LogApplication(LogLevel.INFO, "Copied tree structure to clipboard");
                }
            }
            else
            {
                ViewModel.StatusMessage = "No dialog loaded";
            }
        }

        #endregion

        #region Dialog Helpers

        private enum PasteAfterCutChoice { Cancel, UndoCut, PasteAsCopy }

        /// <summary>
        /// Issue #123: Shows dialog when user tries Paste as Link after Cut operation.
        /// </summary>
        private async Task<PasteAfterCutChoice> ShowPasteAsLinkAfterCutDialog()
        {
            var dialog = new Window
            {
                Title = "Cannot Paste as Link After Cut",
                MinWidth = 450,
                MaxWidth = 600,
                SizeToContent = SizeToContent.WidthAndHeight,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            var panel = new StackPanel { Margin = new Thickness(20) };

            panel.Children.Add(new TextBlock
            {
                Text = "Cannot paste as link after a Cut operation.\n\n" +
                       "Links reference the original node, but Cut will delete it.\n\n" +
                       "Choose an option:",
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                MaxWidth = 560,
                Margin = new Thickness(0, 0, 0, 20)
            });

            var buttonPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Spacing = 10
            };

            var result = PasteAfterCutChoice.Cancel;

            var undoButton = new Button { Content = "Undo Cut", MinWidth = 100 };
            undoButton.Click += (s, e) => { result = PasteAfterCutChoice.UndoCut; dialog.Close(); };

            var copyButton = new Button { Content = "Paste as Copy", MinWidth = 100 };
            copyButton.Click += (s, e) => { result = PasteAfterCutChoice.PasteAsCopy; dialog.Close(); };

            var cancelButton = new Button { Content = "Cancel", MinWidth = 80 };
            cancelButton.Click += (s, e) => { result = PasteAfterCutChoice.Cancel; dialog.Close(); };

            buttonPanel.Children.Add(undoButton);
            buttonPanel.Children.Add(copyButton);
            buttonPanel.Children.Add(cancelButton);

            panel.Children.Add(buttonPanel);
            dialog.Content = panel;

            await dialog.ShowDialog(_window);
            return result;
        }

        #endregion
    }
}
