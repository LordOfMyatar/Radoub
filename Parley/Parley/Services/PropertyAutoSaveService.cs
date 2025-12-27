using Avalonia.Controls;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Logging;
using DialogEditor.Utils;
using DialogEditor.ViewModels;
using System;
using System.Collections.Generic;

namespace DialogEditor.Services
{
    /// <summary>
    /// Handles automatic saving of dialog node properties when UI controls change.
    /// Extracted from MainWindow.axaml.cs to reduce complexity and improve testability.
    /// </summary>
    public class PropertyAutoSaveService
    {
        private readonly Func<string, Control?> _findControl;
        private readonly Action _refreshTreeDisplay;
        private readonly Action<string, bool> _loadScriptPreview;
        private readonly Action<bool> _clearScriptPreview;
        private readonly Action _triggerDebouncedAutoSave;

        /// <summary>
        /// Property save handlers - maps control names to save actions
        /// </summary>
        private readonly Dictionary<string, Action<TreeViewSafeNode>> _propertyHandlers;

        public PropertyAutoSaveService(
            Func<string, Control?> findControl,
            Action refreshTreeDisplay,
            Action<string, bool> loadScriptPreview,
            Action<bool> clearScriptPreview,
            Action triggerDebouncedAutoSave)
        {
            _findControl = findControl ?? throw new ArgumentNullException(nameof(findControl));
            _refreshTreeDisplay = refreshTreeDisplay ?? throw new ArgumentNullException(nameof(refreshTreeDisplay));
            _loadScriptPreview = loadScriptPreview ?? throw new ArgumentNullException(nameof(loadScriptPreview));
            _clearScriptPreview = clearScriptPreview ?? throw new ArgumentNullException(nameof(clearScriptPreview));
            _triggerDebouncedAutoSave = triggerDebouncedAutoSave ?? throw new ArgumentNullException(nameof(triggerDebouncedAutoSave));

            _propertyHandlers = InitializeHandlers();
        }

        /// <summary>
        /// Auto-saves a property based on the control name that changed
        /// </summary>
        public AutoSaveResult AutoSaveProperty(TreeViewSafeNode? selectedNode, string propertyName)
        {
            if (selectedNode == null)
                return AutoSaveResult.NotSaved("No node selected");

            if (!_propertyHandlers.TryGetValue(propertyName, out var handler))
                return AutoSaveResult.NotSaved($"Unknown property: {propertyName}");

            try
            {
                handler(selectedNode);
                _triggerDebouncedAutoSave();
                return AutoSaveResult.Saved(GetDisplayName(propertyName));
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"AutoSaveProperty failed for {propertyName}: {ex.Message}");
                return AutoSaveResult.NotSaved($"Error saving {propertyName}");
            }
        }

        private Dictionary<string, Action<TreeViewSafeNode>> InitializeHandlers()
        {
            return new Dictionary<string, Action<TreeViewSafeNode>>
            {
                ["SpeakerTextBox"] = SaveSpeaker,
                ["TextTextBox"] = SaveText,
                ["CommentTextBox"] = SaveComment,
                ["SoundTextBox"] = SaveSound,
                ["ScriptAppearsTextBox"] = SaveConditionalScript,
                ["ScriptTextBox"] = SaveActionScript,
                ["ScriptActionTextBox"] = SaveActionScript,
                ["QuestTextBox"] = SaveQuest,
                ["QuestEntryTextBox"] = SaveQuestEntry,
                ["AnimationComboBox"] = SaveAnimation,
                ["AnimationLoopCheckBox"] = SaveAnimationLoop,
                ["DelayTextBox"] = SaveDelay
            };
        }

        private void SaveSpeaker(TreeViewSafeNode node)
        {
            var control = _findControl("SpeakerTextBox") as TextBox;
            if (control != null && !control.IsReadOnly)
            {
                node.OriginalNode.Speaker = control.Text ?? "";
                _refreshTreeDisplay(); // Update tree to show new speaker name
            }
        }

        private void SaveText(TreeViewSafeNode node)
        {
            var control = _findControl("TextTextBox") as TextBox;
            if (control != null && node.OriginalNode.Text != null)
            {
                var newText = control.Text ?? "";
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"💾 SaveText: Updating text to '{newText.Substring(0, Math.Min(50, newText.Length))}...'");
                node.OriginalNode.Text.Strings[0] = newText;
                UnifiedLogger.LogApplication(LogLevel.DEBUG, "💾 SaveText: Calling _refreshTreeDisplay()");
                _refreshTreeDisplay(); // Update tree display

                // Notify FlowView and other subscribers of the text change
                DialogChangeEventBus.Instance.PublishNodeModified(node.OriginalNode, "TextChanged");
            }
        }

        private void SaveComment(TreeViewSafeNode node)
        {
            var control = _findControl("CommentTextBox") as TextBox;
            if (control != null)
            {
                // Issue #12: For link nodes, save to LinkComment on the pointer
                // instead of the original node's Comment
                bool isChildCheck = node.IsChild;
                bool hasSourcePointer = node.SourcePointer != null;
                string newValue = control.Text ?? "";

                UnifiedLogger.LogApplication(LogLevel.DEBUG,
                    $"💾 SaveComment: IsChild={isChildCheck}, HasSourcePointer={hasSourcePointer}, " +
                    $"NewValue='{newValue}', NodeType={node.GetType().Name}");

                if (isChildCheck && hasSourcePointer)
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"💾 Saving to LINK comment (SourcePointer.LinkComment)");
                    node.SourcePointer!.LinkComment = newValue;
                }
                else
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"💾 Saving to NODE comment (DialogNode.Comment)");
                    node.OriginalNode.Comment = newValue;
                }
            }
        }

        private void SaveSound(TreeViewSafeNode node)
        {
            var control = _findControl("SoundTextBox") as TextBox;
            if (control != null)
            {
                node.OriginalNode.Sound = control.Text ?? "";
            }
        }

        private void SaveConditionalScript(TreeViewSafeNode node)
        {
            var control = _findControl("ScriptAppearsTextBox") as TextBox;
            if (control != null && node.SourcePointer != null)
            {
                node.SourcePointer.ScriptAppears = control.Text ?? "";

                // Reload script preview when script name changes
                if (!string.IsNullOrWhiteSpace(control.Text))
                {
                    _loadScriptPreview(control.Text, true);
                }
                else
                {
                    _clearScriptPreview(true);
                }
            }
        }

        private void SaveActionScript(TreeViewSafeNode node)
        {
            var control = _findControl("ScriptActionTextBox") as TextBox;
            if (control != null)
            {
                node.OriginalNode.ScriptAction = control.Text ?? "";

                // Reload script preview when script name changes
                if (!string.IsNullOrWhiteSpace(control.Text))
                {
                    _loadScriptPreview(control.Text, false);
                }
                else
                {
                    _clearScriptPreview(false);
                }
            }
        }

        private void SaveQuest(TreeViewSafeNode node)
        {
            var control = _findControl("QuestTextBox") as TextBox;
            if (control != null)
            {
                node.OriginalNode.Quest = control.Text ?? "";
            }
        }

        private void SaveQuestEntry(TreeViewSafeNode node)
        {
            var control = _findControl("QuestEntryTextBox") as TextBox;
            if (control != null)
            {
                // Parse as uint, use uint.MaxValue if empty or invalid
                if (string.IsNullOrWhiteSpace(control.Text))
                {
                    node.OriginalNode.QuestEntry = uint.MaxValue;
                }
                else if (uint.TryParse(control.Text, out uint entryId))
                {
                    node.OriginalNode.QuestEntry = entryId;
                }
            }
        }

        private void SaveAnimation(TreeViewSafeNode node)
        {
            var control = _findControl("AnimationComboBox") as ComboBox;
            if (control != null && control.SelectedItem is DialogAnimation selectedAnimation)
            {
                node.OriginalNode.Animation = selectedAnimation;
            }
        }

        private void SaveAnimationLoop(TreeViewSafeNode node)
        {
            var control = _findControl("AnimationLoopCheckBox") as CheckBox;
            if (control != null && control.IsChecked.HasValue)
            {
                node.OriginalNode.AnimationLoop = control.IsChecked.Value;
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"AnimationLoop set to: {node.OriginalNode.AnimationLoop}");
            }
            else if (control != null)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, "AnimationLoop CheckBox IsChecked is null!");
            }
        }

        private void SaveDelay(TreeViewSafeNode node)
        {
            var control = _findControl("DelayTextBox") as TextBox;
            if (control != null)
            {
                // Parse as uint, use uint.MaxValue if empty or invalid
                if (string.IsNullOrWhiteSpace(control.Text))
                {
                    node.OriginalNode.Delay = uint.MaxValue;
                }
                else if (uint.TryParse(control.Text, out uint delayMs))
                {
                    node.OriginalNode.Delay = delayMs;
                }
            }
        }

        private string GetDisplayName(string propertyName)
        {
            return propertyName switch
            {
                "SpeakerTextBox" => "Speaker",
                "TextTextBox" => "Text",
                "CommentTextBox" => "Comment",
                "SoundTextBox" => "Sound",
                "ScriptAppearsTextBox" => "Conditional Script",
                "ScriptTextBox" or "ScriptActionTextBox" => "Script Action",
                "QuestTextBox" => "Quest",
                "QuestEntryTextBox" => "Quest Entry",
                "AnimationComboBox" => "Animation",
                "AnimationLoopCheckBox" => "Animation Loop",
                "DelayTextBox" => "Delay",
                _ => propertyName
            };
        }
    }

    /// <summary>
    /// Result of an auto-save operation
    /// </summary>
    public class AutoSaveResult
    {
        public bool Success { get; init; }
        public string DisplayName { get; init; }
        public string Message { get; init; }

        private AutoSaveResult(bool success, string displayName, string message)
        {
            Success = success;
            DisplayName = displayName;
            Message = message;
        }

        public static AutoSaveResult Saved(string displayName) =>
            new(true, displayName, $"{displayName} saved");

        public static AutoSaveResult NotSaved(string reason) =>
            new(false, "", reason);
    }
}
