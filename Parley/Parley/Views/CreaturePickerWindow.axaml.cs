using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Logging;

namespace DialogEditor.Views
{
    public partial class CreaturePickerWindow : Window
    {
        private List<CreatureInfo> _allCreatures;
        private List<CreatureInfo> _filteredCreatures;
        private CreatureInfo? _selectedCreature;
        private List<string> _recentTags;

        public string? SelectedTag => _selectedCreature?.Tag;
        public bool HasSelection => _selectedCreature != null;

        // Parameterless constructor for XAML/Avalonia runtime
        public CreaturePickerWindow() : this(new List<CreatureInfo>(), null)
        {
        }

        public CreaturePickerWindow(List<CreatureInfo> creatures, List<string>? recentTags = null)
        {
            InitializeComponent();
            _allCreatures = creatures ?? new List<CreatureInfo>();
            _filteredCreatures = new List<CreatureInfo>(_allCreatures);
            _recentTags = recentTags ?? new List<string>();

            LoadCreatures();
            LoadRecentTags();
        }

        private void LoadCreatures()
        {
            try
            {
                if (_allCreatures.Count == 0)
                {
                    CreatureCountLabel.Text = "⚠ No creatures loaded. Load a module with UTC files first.";
                    CreatureCountLabel.Foreground = new SolidColorBrush(Colors.Orange);
                    UnifiedLogger.LogApplication(LogLevel.WARN, "Creature Picker: No creatures available");
                    return;
                }

                UpdateCreatureList();
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load creatures: {ex.Message}");
                CreatureCountLabel.Text = $"❌ Error loading creatures: {ex.Message}";
                CreatureCountLabel.Foreground = new SolidColorBrush(Colors.Red);
            }
        }

        private void LoadRecentTags()
        {
            try
            {
                RecentTagsListBox.Items.Clear();

                if (_recentTags.Count == 0)
                {
                    RecentTagsListBox.Items.Add(new ListBoxItem
                    {
                        Content = "(no recent tags)",
                        IsEnabled = false,
                        Foreground = new SolidColorBrush(Colors.Gray)
                    });
                    return;
                }

                foreach (var tag in _recentTags)
                {
                    RecentTagsListBox.Items.Add(tag);
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load recent tags: {ex.Message}");
            }
        }

        private void UpdateCreatureList()
        {
            CreaturesListBox.Items.Clear();

            foreach (var creature in _filteredCreatures)
            {
                CreaturesListBox.Items.Add(creature);
            }

            CreatureCountLabel.Text = $"{_filteredCreatures.Count} creature{(_filteredCreatures.Count == 1 ? "" : "s")}";
            CreatureCountLabel.Foreground = Foreground; // Reset to default
        }

        private void OnSearchTextChanged(object? sender, TextChangedEventArgs e)
        {
            try
            {
                var searchText = SearchBox.Text?.ToLowerInvariant() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(searchText))
                {
                    _filteredCreatures = new List<CreatureInfo>(_allCreatures);
                }
                else
                {
                    _filteredCreatures = _allCreatures
                        .Where(c =>
                            c.Tag.ToLowerInvariant().Contains(searchText) ||
                            c.DisplayName.ToLowerInvariant().Contains(searchText))
                        .ToList();
                }

                UpdateCreatureList();
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Search failed: {ex.Message}");
            }
        }

        private void OnRecentTagSelected(object? sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (RecentTagsListBox.SelectedItem is string selectedTag)
                {
                    // Find creature with this tag
                    var creature = _allCreatures.FirstOrDefault(c =>
                        c.Tag.Equals(selectedTag, StringComparison.OrdinalIgnoreCase));

                    if (creature != null)
                    {
                        // Select in main list
                        CreaturesListBox.SelectedItem = creature;
                        CreaturesListBox.ScrollIntoView(creature);
                    }
                    else
                    {
                        // Tag not found in current creatures (from different module)
                        // Just populate selection display
                        _selectedCreature = new CreatureInfo { Tag = selectedTag };
                        SelectedCreatureLabel.Text = $"(from recent) {selectedTag}";
                        SelectedTagLabel.Text = selectedTag;
                    }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Recent tag selection failed: {ex.Message}");
            }
        }

        private void OnCreatureSelected(object? sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (CreaturesListBox.SelectedItem is CreatureInfo creature)
                {
                    _selectedCreature = creature;
                    SelectedCreatureLabel.Text = creature.DisplayName;
                    SelectedTagLabel.Text = creature.Tag;
                }
                else
                {
                    _selectedCreature = null;
                    SelectedCreatureLabel.Text = "(none)";
                    SelectedTagLabel.Text = "(none)";
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Creature selection failed: {ex.Message}");
            }
        }

        private void OnOkClick(object? sender, RoutedEventArgs e)
        {
            if (_selectedCreature != null)
            {
                UnifiedLogger.LogApplication(LogLevel.INFO,
                    $"Creature Picker: Selected '{_selectedCreature.Tag}' ({_selectedCreature.DisplayName})");
                Close(true);
            }
        }

        private void OnCancelClick(object? sender, RoutedEventArgs e)
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, "Creature Picker: Cancelled");
            _selectedCreature = null;
            Close(false);
        }
    }
}
