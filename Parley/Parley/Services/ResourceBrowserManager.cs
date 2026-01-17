using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using DialogEditor.Models;
using DialogEditor.Utils;
using DialogEditor.ViewModels;
using DialogEditor.Views;
using System;
using Radoub.Formats.Logging;
using Radoub.UI.Services;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DialogEditor.Services
{
    /// <summary>
    /// Manages resource browser dialogs (sounds, creatures, etc.) with unified patterns.
    /// Handles recent items and browser dialog creation.
    /// Extracted from MainWindow.axaml.cs to eliminate browser pattern duplication.
    /// </summary>
    public class ResourceBrowserManager
    {
        private readonly AudioService _audioService;
        private readonly CreatureService _creatureService;
        private readonly Func<string, Control?> _findControl;
        private readonly Action<string> _setStatusMessage;
        private readonly Action<string> _autoSaveProperty;
        private readonly Func<TreeViewSafeNode?> _getSelectedNode;
        private readonly Func<string?> _getCurrentFilePath;

        // Session cache for recently used creature tags
        private readonly List<string> _recentCreatureTags = new();

        public ResourceBrowserManager(
            AudioService audioService,
            CreatureService creatureService,
            Func<string, Control?> findControl,
            Action<string> setStatusMessage,
            Action<string> autoSaveProperty,
            Func<TreeViewSafeNode?> getSelectedNode,
            Func<string?>? getCurrentFilePath = null)
        {
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
            _creatureService = creatureService ?? throw new ArgumentNullException(nameof(creatureService));
            _findControl = findControl ?? throw new ArgumentNullException(nameof(findControl));
            _setStatusMessage = setStatusMessage ?? throw new ArgumentNullException(nameof(setStatusMessage));
            _autoSaveProperty = autoSaveProperty ?? throw new ArgumentNullException(nameof(autoSaveProperty));
            _getSelectedNode = getSelectedNode ?? throw new ArgumentNullException(nameof(getSelectedNode));
            _getCurrentFilePath = getCurrentFilePath ?? (() => null);
        }

        /// <summary>
        /// Opens sound browser dialog and updates sound field
        /// </summary>
        public async Task BrowseSoundAsync(Window owner)
        {
            // Phase 2 Fix: Don't allow sound browser when no node selected or ROOT selected
            var selectedNode = _getSelectedNode();
            if (selectedNode == null)
            {
                _setStatusMessage("Please select a dialog node first");
                return;
            }

            if (selectedNode is TreeViewRootNode)
            {
                _setStatusMessage("Cannot assign sounds to ROOT. Select a dialog node instead.");
                return;
            }

            try
            {
                var soundBrowser = new SoundBrowserWindow();
                var result = await soundBrowser.ShowDialog<string?>(owner);

                if (!string.IsNullOrEmpty(result))
                {
                    // Update the sound field with selected sound
                    var soundTextBox = _findControl("SoundTextBox") as TextBox;
                    if (soundTextBox != null)
                    {
                        soundTextBox.Text = result;
                        // Trigger auto-save
                        _autoSaveProperty("SoundTextBox");
                    }
                    _setStatusMessage($"Selected sound: {result}");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error opening sound browser: {ex.Message}");
                _setStatusMessage($"Error opening sound browser: {ex.Message}");
            }
        }

        /// <summary>
        /// Opens creature browser dialog and updates speaker field.
        /// Issue #5: Loads creatures lazily on first access to avoid slow startup.
        /// </summary>
        public async Task BrowseCreatureAsync(Window owner)
        {
            // Don't allow creature browser when no node selected or ROOT selected
            var selectedNode = _getSelectedNode();
            if (selectedNode == null)
            {
                _setStatusMessage("Please select a dialog node first");
                return;
            }

            if (selectedNode is TreeViewRootNode)
            {
                _setStatusMessage("Cannot assign creatures to ROOT. Select a dialog node instead.");
                return;
            }

            try
            {
                // Issue #5: Lazy load creatures on first access
                var creatures = _creatureService.GetAllCreatures();

                if (creatures.Count == 0)
                {
                    // Try to load creatures from current dialog's module directory
                    var currentFilePath = _getCurrentFilePath();
                    if (!string.IsNullOrEmpty(currentFilePath))
                    {
                        var moduleDirectory = Path.GetDirectoryName(currentFilePath);
                        if (!string.IsNullOrEmpty(moduleDirectory) && Directory.Exists(moduleDirectory))
                        {
                            _setStatusMessage("Loading creatures...");
                            UnifiedLogger.LogApplication(LogLevel.INFO, $"Lazy loading creatures from: {UnifiedLogger.SanitizePath(moduleDirectory)}");

                            // Pass game data directory for 2DA lookups (portraits.2da, soundset.2da, classes.2da)
                            var gameDataPath = GetGameDataPath();
                            creatures = await _creatureService.ScanCreaturesAsync(moduleDirectory, gameDataPath);

                            if (creatures.Count > 0)
                            {
                                _setStatusMessage($"Loaded {creatures.Count} creature{(creatures.Count == 1 ? "" : "s")}");
                            }
                        }
                    }
                }

                if (creatures.Count == 0)
                {
                    // Show helpful message with instructions
                    var message = "No creatures found.\n\n" +
                                "To use creature browser:\n" +
                                "• Place .utc files in the same folder as your .dlg file, OR\n" +
                                "• Specify module directory in Settings\n\n" +
                                "You can still type creature tags manually.";

                    var msgBox = new Window
                    {
                        Title = "No Creatures Available",
                        Width = 400,
                        Height = 250,
                        Content = new TextBlock
                        {
                            Text = message,
                            Margin = new Thickness(20)
                        },
                        WindowStartupLocation = WindowStartupLocation.CenterOwner
                    };

                    await msgBox.ShowDialog(owner);

                    _setStatusMessage("No creatures found - see message for details");
                    UnifiedLogger.LogApplication(LogLevel.WARN, "No creatures available for picker");
                    return;
                }

                var creaturePicker = new CreaturePickerWindow(creatures, _recentCreatureTags);
                var result = await creaturePicker.ShowDialog<bool>(owner);

                if (result && !string.IsNullOrEmpty(creaturePicker.SelectedTag))
                {
                    var selectedTag = creaturePicker.SelectedTag;

                    // Update the Speaker field with selected tag
                    var speakerTextBox = _findControl("SpeakerTextBox") as TextBox;
                    if (speakerTextBox != null)
                    {
                        speakerTextBox.Text = selectedTag;
                        // Trigger auto-save
                        _autoSaveProperty("SpeakerTextBox");
                    }

                    // Add to recent tags (avoid duplicates, max 10)
                    AddToRecentTags(selectedTag);

                    _setStatusMessage($"Selected creature: {selectedTag}");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error opening creature picker: {ex.Message}");
                _setStatusMessage($"Error opening creature picker: {ex.Message}");
            }
        }

        /// <summary>
        /// Adds a creature tag to recent tags list
        /// </summary>
        private void AddToRecentTags(string tag)
        {
            // Remove if already exists (move to front)
            _recentCreatureTags.Remove(tag);

            // Add to front
            _recentCreatureTags.Insert(0, tag);

            // Keep max 10 recent tags
            if (_recentCreatureTags.Count > 10)
            {
                _recentCreatureTags.RemoveAt(_recentCreatureTags.Count - 1);
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Added to recent creature tags: {tag}");

            // Update the dropdown
            UpdateRecentCreatureTagsDropdown();
        }

        /// <summary>
        /// Updates the recent creature tags dropdown
        /// </summary>
        private void UpdateRecentCreatureTagsDropdown()
        {
            var dropdown = _findControl("RecentCreatureTagsComboBox") as ComboBox;
            if (dropdown != null)
            {
                dropdown.ItemsSource = _recentCreatureTags.ToList();
            }
        }

        /// <summary>
        /// Gets recent creature tags for external access
        /// </summary>
        public IReadOnlyList<string> GetRecentCreatureTags() => _recentCreatureTags.AsReadOnly();

        /// <summary>
        /// Gets the game data path for 2DA file lookups.
        /// Searches for data folder containing 2DA files.
        /// </summary>
        private static string? GetGameDataPath()
        {
            var settings = SettingsService.Instance;
            var basePath = settings.BaseGameInstallPath;

            if (string.IsNullOrEmpty(basePath) || !Directory.Exists(basePath))
                return null;

            // NWN:EE stores 2DA files in data/ folder
            var dataPath = Path.Combine(basePath, "data");
            if (Directory.Exists(dataPath))
            {
                // Verify 2DA files exist
                var has2DA = Directory.GetFiles(dataPath, "*.2da", SearchOption.TopDirectoryOnly).Length > 0;
                if (has2DA)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Game data path: {UnifiedLogger.SanitizePath(dataPath)}");
                    return dataPath;
                }
            }

            // Fallback: check if 2DA files are in base path directly
            var baseDirHas2DA = Directory.GetFiles(basePath, "*.2da", SearchOption.TopDirectoryOnly).Length > 0;
            if (baseDirHas2DA)
            {
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Game data path (base): {UnifiedLogger.SanitizePath(basePath)}");
                return basePath;
            }

            UnifiedLogger.LogApplication(LogLevel.DEBUG, "No 2DA files found in game data path");
            return null;
        }
    }
}
