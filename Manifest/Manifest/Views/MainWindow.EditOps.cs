using Avalonia.Controls;
using Avalonia.Interactivity;
using Radoub.Formats.Common;
using Radoub.Formats.Gff;
using Radoub.Formats.Jrl;
using Radoub.Formats.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Manifest.Views;

/// <summary>
/// MainWindow partial: Edit operations (add/delete categories and entries) and tree view management.
/// </summary>
public partial class MainWindow
{
    #region Edit Operations

    private void OnAddCategoryClick(object? sender, RoutedEventArgs e)
    {
        if (_currentJrl == null) return;

        var name = new CExoLocString();
        name.SetString(0, "New Category");

        // Generate unique tag - find next available suffix
        var existingTags = _currentJrl.Categories.Select(c => c.Tag).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var uniqueTag = GenerateUniqueTag("new_category", existingTags);

        var newCategory = new JournalCategory
        {
            Name = name,
            Tag = uniqueTag,
            Priority = 1,
            XP = 0,
            Comment = ""
        };

        _currentJrl.Categories.Add(newCategory);
        MarkDirty();
        UpdateTree();
        UpdateStatusBarCounts();

        // Select the new category and focus the name field
        SelectNewCategory(newCategory);

        UnifiedLogger.LogJournal(LogLevel.INFO, $"Added new category with tag: {uniqueTag}");
    }

    private static string GenerateUniqueTag(string baseTag, HashSet<string> existingTags)
    {
        // Try base tag first (for empty journal)
        if (!existingTags.Contains(baseTag))
            return baseTag;

        // Find next available suffix starting from 001
        for (int i = 1; i < 1000; i++)
        {
            var candidate = $"{baseTag}_{i:D3}";
            if (!existingTags.Contains(candidate))
                return candidate;
        }

        // Fallback with timestamp if somehow all 999 are taken
        return $"{baseTag}_{DateTime.Now.Ticks}";
    }

    private void OnAddEntryClick(object? sender, RoutedEventArgs e)
    {
        if (_currentJrl == null) return;

        JournalCategory? category = null;

        if (_selectedItem is CategoryTreeItem catItem)
        {
            category = catItem.Category;
        }
        else if (_selectedItem is EntryTreeItem entItem)
        {
            category = entItem.ParentCategory;
        }

        if (category == null) return;

        // Auto-increment ID by 100 (allows inserting entries between)
        uint nextId = category.Entries.Count > 0
            ? ((category.Entries.Max(e => e.ID) / 100) + 1) * 100
            : 100;

        var entryText = new CExoLocString();
        entryText.SetString(0, "");

        var newEntry = new JournalEntry
        {
            ID = nextId,
            Text = entryText,
            End = false
        };

        category.Entries.Add(newEntry);
        MarkDirty();
        UpdateTree();
        UpdateStatusBarCounts();

        // Select the new entry and focus the text field
        SelectNewEntry(newEntry, category);

        UnifiedLogger.LogJournal(LogLevel.INFO, $"Added new entry with ID {nextId}");
    }

    private void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (_currentJrl == null || _selectedItem == null) return;

        if (_selectedItem is CategoryTreeItem catItem)
        {
            _currentJrl.Categories.Remove(catItem.Category);
            UnifiedLogger.LogJournal(LogLevel.INFO, $"Deleted category: {catItem.Category.Tag}");
        }
        else if (_selectedItem is EntryTreeItem entItem)
        {
            entItem.ParentCategory.Entries.Remove(entItem.Entry);
            UnifiedLogger.LogJournal(LogLevel.INFO, $"Deleted entry: {entItem.Entry.ID}");
        }

        _selectedItem = null;
        MarkDirty();
        UpdateTree();
        UpdatePropertyPanel();
        UpdateStatusBarCounts();
    }

    private void SelectNewCategory(JournalCategory category)
    {
        // Find and select the tree item for this category
        foreach (var treeItem in JournalTree.Items.OfType<TreeViewItem>())
        {
            if (treeItem.Tag is CategoryTreeItem catItem && catItem.Category == category)
            {
                JournalTree.SelectedItem = treeItem;
                treeItem.Focus();
                // Focus the name box after a brief delay to let UI update
                Avalonia.Threading.Dispatcher.UIThread.Post(() => CategoryNameBox.Focus());
                return;
            }
        }
    }

    private void SelectNewEntry(JournalEntry entry, JournalCategory category)
    {
        // Find and select the tree item for this entry
        foreach (var catTreeItem in JournalTree.Items.OfType<TreeViewItem>())
        {
            if (catTreeItem.Tag is CategoryTreeItem catItem && catItem.Category == category)
            {
                foreach (var entTreeItem in catTreeItem.Items.OfType<TreeViewItem>())
                {
                    if (entTreeItem.Tag is EntryTreeItem entItem && entItem.Entry == entry)
                    {
                        JournalTree.SelectedItem = entTreeItem;
                        entTreeItem.Focus();
                        // Focus the text box after a brief delay to let UI update
                        Avalonia.Threading.Dispatcher.UIThread.Post(() => EntryTextBox.Focus());
                        return;
                    }
                }
            }
        }
    }

    #endregion

    #region Tree View

    private void UpdateTree()
    {
        JournalTree.Items.Clear();

        if (_currentJrl == null) return;

        foreach (var category in _currentJrl.Categories)
        {
            var catItem = new CategoryTreeItem(category);

            var catNode = new TreeViewItem
            {
                Header = catItem.DisplayName,
                Tag = catItem,
                IsExpanded = true
            };

            // Sort entries numerically by ID
            foreach (var entry in category.Entries.OrderBy(e => e.ID))
            {
                var entItem = new EntryTreeItem(entry, category);
                var entNode = new TreeViewItem
                {
                    Header = entItem.DisplayName,
                    Tag = entItem
                };
                catNode.Items.Add(entNode);
            }

            JournalTree.Items.Add(catNode);
        }
    }

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (JournalTree.SelectedItem is TreeViewItem treeItem)
        {
            _selectedItem = treeItem.Tag;
        }
        else
        {
            _selectedItem = null;
        }

        UpdatePropertyPanel();
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(CanAddEntry));
    }

    #endregion
}
