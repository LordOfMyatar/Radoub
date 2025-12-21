using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DialogEditor.Models;
using DialogEditor.Services;

namespace DialogEditor.ViewModels
{
    /// <summary>
    /// ViewModel for the Conversation Simulator window.
    /// Manages stepping through dialog branches and tracking coverage.
    /// Issue #478 - Conversation Simulator Sprint 1
    /// </summary>
    public class ConversationSimulatorViewModel : INotifyPropertyChanged
    {
        private readonly ConversationManager _conversationManager;
        private readonly Dialog _dialog;
        private readonly string _filePath;
        private readonly CoverageTracker _coverageTracker;

        // Loop detection
        private HashSet<string> _visitedStates = new();
        private int _pathDepth = 0;
        private const int MaxPathDepth = 100;

        // UI state
        private string _npcSpeaker = "";
        private string _npcText = "";
        private string _conditionScript = "";
        private string _actionScript = "";
        private string _statusMessage = "";
        private bool _hasEnded;
        private bool _loopDetected;
        private int _selectedReplyIndex = -1;

        // Warnings
        private bool _showNoConditionalsWarning;
        private bool _showUnreachableSiblingsWarning;
        private bool _showLoopWarning;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? ConversationEnded;
        public event EventHandler? RequestClose;

        public ConversationSimulatorViewModel(Dialog dialog, string filePath)
        {
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _conversationManager = new ConversationManager(dialog, new AlwaysTrueScriptEngine());
            _coverageTracker = CoverageTracker.Instance;

            Replies = new ObservableCollection<ReplyOption>();

            // Calculate total paths and check for warnings
            AnalyzeDialogStructure();
        }

        public string NpcSpeaker
        {
            get => _npcSpeaker;
            private set => SetProperty(ref _npcSpeaker, value);
        }

        public string NpcText
        {
            get => _npcText;
            private set => SetProperty(ref _npcText, value);
        }

        public string ConditionScript
        {
            get => _conditionScript;
            private set => SetProperty(ref _conditionScript, value);
        }

        public string ActionScript
        {
            get => _actionScript;
            private set => SetProperty(ref _actionScript, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value);
        }

        public bool HasEnded
        {
            get => _hasEnded;
            private set => SetProperty(ref _hasEnded, value);
        }

        public bool LoopDetected
        {
            get => _loopDetected;
            private set => SetProperty(ref _loopDetected, value);
        }

        public int SelectedReplyIndex
        {
            get => _selectedReplyIndex;
            set => SetProperty(ref _selectedReplyIndex, value);
        }

        public ObservableCollection<ReplyOption> Replies { get; }

        public bool HasReplies => Replies.Count > 0;

        public bool ShowNoConditionalsWarning
        {
            get => _showNoConditionalsWarning;
            private set => SetProperty(ref _showNoConditionalsWarning, value);
        }

        public bool ShowUnreachableSiblingsWarning
        {
            get => _showUnreachableSiblingsWarning;
            private set => SetProperty(ref _showUnreachableSiblingsWarning, value);
        }

        public bool ShowLoopWarning
        {
            get => _showLoopWarning;
            private set => SetProperty(ref _showLoopWarning, value);
        }

        public CoverageStats Coverage => _coverageTracker.GetCoverageStats(_filePath);

        public string CoverageDisplay => Coverage.DisplayText;

        /// <summary>
        /// Start or restart the conversation from the beginning.
        /// </summary>
        public void StartConversation()
        {
            _pathDepth = 0;
            _visitedStates.Clear();
            LoopDetected = false;
            ShowLoopWarning = false;

            _conversationManager.StartConversation();

            if (_conversationManager.HasEnded)
            {
                HasEnded = true;
                StatusMessage = "Conversation has no valid starting entry.";
                ClearDisplay();
                return;
            }

            HasEnded = false;
            UpdateDisplay();
            StatusMessage = "Conversation started.";
        }

        /// <summary>
        /// Select a reply and advance to the next NPC entry.
        /// </summary>
        public void SelectReply(int replyIndex)
        {
            if (HasEnded || replyIndex < 0 || replyIndex >= Replies.Count)
                return;

            SelectedReplyIndex = replyIndex;
            _pathDepth++;

            // Record the visited reply node
            var reply = _conversationManager.CurrentReplies[replyIndex];
            var replyNodeIndex = _dialog.GetNodeIndex(reply, DialogNodeType.Reply);
            var nodeKey = $"R{replyNodeIndex}";
            _coverageTracker.RecordVisitedNode(_filePath, nodeKey);

            // Check for loop before advancing
            var stateKey = GetStateKey();
            if (_visitedStates.Contains(stateKey))
            {
                LoopDetected = true;
                ShowLoopWarning = true;
                StatusMessage = "Loop detected! You've been here before.";
            }
            else
            {
                _visitedStates.Add(stateKey);
            }

            // Check for excessive depth
            if (_pathDepth > MaxPathDepth)
            {
                LoopDetected = true;
                ShowLoopWarning = true;
                StatusMessage = "Maximum path depth reached. Possible infinite loop.";
            }

            // Advance the conversation
            _conversationManager.PickReply(replyIndex);

            if (_conversationManager.HasEnded)
            {
                HasEnded = true;
                StatusMessage = "Conversation ended.";
                ClearDisplay();
                OnPropertyChanged(nameof(CoverageDisplay));
                OnPropertyChanged(nameof(Coverage));
                ConversationEnded?.Invoke(this, EventArgs.Empty);
                return;
            }

            UpdateDisplay();
        }

        /// <summary>
        /// Skip the current entry (auto-advance with first reply if only one option).
        /// </summary>
        public void Skip()
        {
            if (HasEnded)
                return;

            if (Replies.Count == 1)
            {
                SelectReply(0);
            }
            else if (Replies.Count == 0)
            {
                // No replies available - conversation ends
                HasEnded = true;
                StatusMessage = "No replies available.";
                ClearDisplay();
            }
        }

        /// <summary>
        /// Exit the conversation without recording coverage.
        /// </summary>
        public void Exit()
        {
            _conversationManager.AbortConversation();
            HasEnded = true;
            StatusMessage = "Conversation aborted.";
            ClearDisplay();
            RequestClose?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Clear coverage data for the current file.
        /// </summary>
        public void ClearCoverage()
        {
            _coverageTracker.ClearCoverage(_filePath);
            OnPropertyChanged(nameof(CoverageDisplay));
            OnPropertyChanged(nameof(Coverage));
            StatusMessage = "Coverage cleared.";
            // Refresh replies to remove checkmarks
            UpdateDisplay();
        }

        private void AnalyzeDialogStructure()
        {
            // Check for conditionals
            var hasConditionals = HasAnyConditionals();
            ShowNoConditionalsWarning = !hasConditionals;

            // Check for unreachable siblings (multiple NPC entries without conditions)
            ShowUnreachableSiblingsWarning = HasUnreachableSiblings();

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"ConversationSimulator: Analyzed dialog - " +
                $"hasConditionals={hasConditionals}, unreachableSiblings={ShowUnreachableSiblingsWarning}");
        }

        private bool HasAnyConditionals()
        {
            // Check if any pointer has a ScriptAppears condition
            foreach (var start in _dialog.Starts)
            {
                if (!string.IsNullOrEmpty(start.ScriptAppears))
                    return true;

                if (start.Node != null && HasConditionalsRecursive(start.Node))
                    return true;
            }

            return false;
        }

        private bool HasConditionalsRecursive(DialogNode node)
        {
            foreach (var ptr in node.Pointers)
            {
                if (!string.IsNullOrEmpty(ptr.ScriptAppears))
                    return true;

                if (ptr.Node != null && !ptr.IsLink)
                {
                    if (HasConditionalsRecursive(ptr.Node))
                        return true;
                }
            }

            return false;
        }

        private bool HasUnreachableSiblings()
        {
            // Check NPC entries (not PC replies) for siblings without conditions
            // When multiple NPC entries exist as children of the same parent,
            // and none have conditions, only the first is reachable

            foreach (var entry in _dialog.Entries)
            {
                var siblings = entry.Pointers
                    .Where(p => p.Type == DialogNodeType.Entry && !p.IsLink)
                    .ToList();

                if (siblings.Count > 1)
                {
                    var unconditionedCount = siblings.Count(p => string.IsNullOrEmpty(p.ScriptAppears));
                    if (unconditionedCount > 1)
                        return true;
                }
            }

            foreach (var reply in _dialog.Replies)
            {
                var siblings = reply.Pointers
                    .Where(p => p.Type == DialogNodeType.Entry && !p.IsLink)
                    .ToList();

                if (siblings.Count > 1)
                {
                    var unconditionedCount = siblings.Count(p => string.IsNullOrEmpty(p.ScriptAppears));
                    if (unconditionedCount > 1)
                        return true;
                }
            }

            return false;
        }

        private void UpdateDisplay()
        {
            var currentEntry = _conversationManager.CurrentEntry;
            if (currentEntry == null)
            {
                ClearDisplay();
                return;
            }

            // Update NPC display
            NpcSpeaker = currentEntry.SpeakerDisplay;
            NpcText = currentEntry.DisplayText;

            // Update script info (from current entry)
            ActionScript = currentEntry.ScriptAction ?? "";

            // Get condition script from the pointer that led here (if available)
            // For simplicity, clear condition script at entry level
            ConditionScript = "";

            // Update available replies
            Replies.Clear();
            var replies = _conversationManager.CurrentReplies;
            for (int i = 0; i < replies.Count; i++)
            {
                var reply = replies[i];
                var text = reply.DisplayText;
                if (string.IsNullOrWhiteSpace(text))
                {
                    text = "[Continue]";
                }

                // Check if this reply was visited before
                var replyNodeIndex = _dialog.GetNodeIndex(reply, DialogNodeType.Reply);
                var nodeKey = $"R{replyNodeIndex}";
                var wasVisited = _coverageTracker.IsNodeVisited(_filePath, nodeKey);

                Replies.Add(new ReplyOption
                {
                    Index = i,
                    Text = text,
                    HasCondition = false,
                    WasVisited = wasVisited
                });
            }

            SelectedReplyIndex = Replies.Count > 0 ? 0 : -1;
            OnPropertyChanged(nameof(HasReplies));
            OnPropertyChanged(nameof(CoverageDisplay));
            OnPropertyChanged(nameof(Coverage));
        }

        private void ClearDisplay()
        {
            NpcSpeaker = "";
            NpcText = "";
            ConditionScript = "";
            ActionScript = "";
            Replies.Clear();
            SelectedReplyIndex = -1;
            OnPropertyChanged(nameof(HasReplies));
        }

        private string GetStateKey()
        {
            // Create a state key based on current entry + available replies
            var entry = _conversationManager.CurrentEntry;
            if (entry == null) return "";

            var entryIndex = _dialog.GetNodeIndex(entry, DialogNodeType.Entry);
            var replyIndices = _conversationManager.CurrentReplies
                .Select(r => _dialog.GetNodeIndex(r, DialogNodeType.Reply))
                .OrderBy(i => i);

            return $"E{entryIndex}:{string.Join(",", replyIndices)}";
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// Represents a reply option in the simulator.
    /// </summary>
    public class ReplyOption
    {
        public int Index { get; set; }
        public string Text { get; set; } = "";
        public bool HasCondition { get; set; }
        public bool WasVisited { get; set; }

        public string DisplayText => WasVisited ? $"\u2713 {Text}" : Text;
    }
}
