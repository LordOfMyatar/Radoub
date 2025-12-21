using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using DialogEditor.Models;
using DialogEditor.Services;
using DialogEditor.Utils;

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

        // State tracking
        private bool _isSelectingRootEntry = true; // Start by showing root entries
        private DialogNode? _currentEntry;
        private List<DialogNode> _currentReplies = new();

        // Loop detection
        private HashSet<string> _visitedStates = new();
        private int _pathDepth = 0;
        private const int MaxPathDepth = 100;

        // UI state
        private string _npcSpeaker = "";
        private string _npcSpeakerColor = SpeakerVisualHelper.ColorPalette.Orange; // Default NPC color
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

        // Coverage
        private int _totalReplies;
        private List<int> _rootEntryIndices = new();
        private Dictionary<int, HashSet<int>> _repliesPerRootEntry = new(); // entryIndex -> set of reply indices under that root

        // TTS (Issue #479)
        private readonly ITtsService _ttsService;
        private bool _ttsEnabled = true;
        private double _ttsRate = 1.0;
        private Dictionary<string, string> _speakerVoiceAssignments = new();
        private string _pcVoice = "";
        private string _defaultNpcVoice = "";

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? ConversationEnded;
        public event EventHandler? RequestClose;

        public ConversationSimulatorViewModel(Dialog dialog, string filePath)
        {
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
            _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            _conversationManager = new ConversationManager(dialog, new AlwaysTrueScriptEngine());
            _coverageTracker = CoverageTracker.Instance;
            _ttsService = TtsServiceFactory.Instance;

            Replies = new ObservableCollection<ReplyOption>();
            VoiceNames = new ObservableCollection<string>();
            SpeakerVoiceAssignments = new ObservableCollection<SpeakerVoiceMapping>();

            // Initialize TTS voices
            InitializeTts();

            // Calculate total paths and check for warnings
            AnalyzeDialogStructure();
        }

        private void InitializeTts()
        {
            // Populate voice list
            VoiceNames.Clear();
            if (_ttsService.IsAvailable)
            {
                foreach (var voice in _ttsService.GetVoiceNames())
                {
                    VoiceNames.Add(voice);
                }

                // Set defaults
                if (VoiceNames.Count > 0)
                {
                    _defaultNpcVoice = VoiceNames[0];
                    _pcVoice = VoiceNames.Count > 1 ? VoiceNames[1] : VoiceNames[0];
                }
            }

            // Collect unique speakers from dialog
            CollectSpeakers();

            OnPropertyChanged(nameof(TtsAvailable));
            OnPropertyChanged(nameof(TtsUnavailableReason));
            OnPropertyChanged(nameof(TtsInstallInstructions));
        }

        private void CollectSpeakers()
        {
            SpeakerVoiceAssignments.Clear();
            var speakerSet = new HashSet<string>();

            // Collect speakers from all entries
            foreach (var entry in _dialog.Entries)
            {
                var speaker = entry.Speaker ?? "";
                if (!string.IsNullOrEmpty(speaker) && speakerSet.Add(speaker))
                {
                    SpeakerVoiceAssignments.Add(new SpeakerVoiceMapping
                    {
                        SpeakerName = speaker,
                        VoiceName = _defaultNpcVoice,
                        IsPC = false
                    });
                }
            }

            // Add (Owner) for default NPC speaker
            SpeakerVoiceAssignments.Add(new SpeakerVoiceMapping
            {
                SpeakerName = "(Owner)",
                VoiceName = _defaultNpcVoice,
                IsPC = false
            });

            // Add (PC) for player character
            SpeakerVoiceAssignments.Add(new SpeakerVoiceMapping
            {
                SpeakerName = "(PC)",
                VoiceName = _pcVoice,
                IsPC = true
            });
        }

        public string NpcSpeaker
        {
            get => _npcSpeaker;
            private set => SetProperty(ref _npcSpeaker, value);
        }

        /// <summary>
        /// Color for the current NPC speaker (hex string like "#FF8A65")
        /// </summary>
        public string NpcSpeakerColor
        {
            get => _npcSpeakerColor;
            private set => SetProperty(ref _npcSpeakerColor, value);
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

        /// <summary>
        /// True when showing PC reply choices, false when showing NPC entry choices (root selection).
        /// Used to toggle between PC (blue circle) and NPC (orange square) indicator styling.
        /// </summary>
        public bool IsShowingPcChoices => !_isSelectingRootEntry && HasReplies;

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

        public CoverageStats Coverage => _coverageTracker.GetCoverageStats(_filePath, _totalReplies, _rootEntryIndices, _repliesPerRootEntry);

        public string CoverageDisplay => Coverage.DisplayText;

        public bool CoverageComplete => Coverage.IsComplete;

        // TTS Properties (Issue #479)
        public bool TtsAvailable => _ttsService.IsAvailable;
        public string TtsUnavailableReason => _ttsService.UnavailableReason;
        public string TtsInstallInstructions => _ttsService.InstallInstructions;
        public bool TtsSpeaking => _ttsService.IsSpeaking;
        public ObservableCollection<string> VoiceNames { get; }
        public ObservableCollection<SpeakerVoiceMapping> SpeakerVoiceAssignments { get; }

        public bool TtsEnabled
        {
            get => _ttsEnabled;
            set => SetProperty(ref _ttsEnabled, value);
        }

        public double TtsRate
        {
            get => _ttsRate;
            set => SetProperty(ref _ttsRate, Math.Clamp(value, 0.5, 2.0));
        }

        /// <summary>
        /// Speak the current NPC text using TTS.
        /// </summary>
        public void Speak()
        {
            if (!TtsAvailable || !TtsEnabled || string.IsNullOrWhiteSpace(NpcText))
                return;

            // Get the voice for the current speaker
            var voiceName = GetVoiceForSpeaker(NpcSpeaker);
            _ttsService.Speak(NpcText, voiceName, TtsRate);
            OnPropertyChanged(nameof(TtsSpeaking));
        }

        /// <summary>
        /// Stop any currently playing TTS.
        /// </summary>
        public void StopSpeaking()
        {
            _ttsService.Stop();
            OnPropertyChanged(nameof(TtsSpeaking));
        }

        private string? GetVoiceForSpeaker(string speaker)
        {
            // Check if speaker is empty (use Owner default)
            var speakerKey = string.IsNullOrEmpty(speaker) ? "(Owner)" : speaker;

            // Find matching voice assignment
            var assignment = SpeakerVoiceAssignments.FirstOrDefault(a =>
                a.SpeakerName.Equals(speakerKey, StringComparison.OrdinalIgnoreCase));

            return assignment?.VoiceName ?? _defaultNpcVoice;
        }

        /// <summary>
        /// Start or restart the conversation from the beginning.
        /// Shows all root entries for the user to choose from.
        /// </summary>
        public void StartConversation()
        {
            _pathDepth = 0;
            _visitedStates.Clear();
            _currentEntry = null;
            _currentReplies.Clear();
            LoopDetected = false;
            ShowLoopWarning = false;
            _isSelectingRootEntry = true;
            OnPropertyChanged(nameof(IsShowingPcChoices));

            if (_dialog.Starts.Count == 0)
            {
                HasEnded = true;
                StatusMessage = "Dialog has no starting entries.";
                ClearDisplay();
                return;
            }

            HasEnded = false;
            ShowRootEntrySelection();
            StatusMessage = "Select a conversation start.";
        }

        /// <summary>
        /// Display all root entries as selectable options.
        /// </summary>
        private void ShowRootEntrySelection()
        {
            NpcSpeaker = "[Conversation Starts]";
            NpcSpeakerColor = SpeakerVisualHelper.ColorPalette.Orange; // Default NPC color for menu
            NpcText = "Select which conversation branch to test:";
            ConditionScript = "";
            ActionScript = "";

            // Get current coverage to show per-entry stats
            var coverage = Coverage;

            Replies.Clear();
            for (int i = 0; i < _dialog.Starts.Count; i++)
            {
                var start = _dialog.Starts[i];
                if (start.Node == null) continue;

                var text = start.Node.DisplayText;
                if (string.IsNullOrWhiteSpace(text))
                {
                    text = $"[Entry {i}]";
                }

                // Truncate long text for display
                if (text.Length > 50)
                {
                    text = text.Substring(0, 47) + "...";
                }

                // Show condition script if present
                var conditionInfo = !string.IsNullOrEmpty(start.ScriptAppears)
                    ? $" [{start.ScriptAppears}]"
                    : "";

                // Get entry index and per-entry coverage
                var entryIndex = _dialog.GetNodeIndex(start.Node, DialogNodeType.Entry);
                var nodeKey = $"E{entryIndex}";
                var wasVisited = _coverageTracker.IsNodeVisited(_filePath, nodeKey);

                // Show per-entry coverage (e.g., "2/3")
                var entryCoverage = coverage.GetEntryCoverageText(entryIndex);
                var isEntryComplete = coverage.IsEntryComplete(entryIndex);
                var coverageIndicator = isEntryComplete ? $" ({entryCoverage})" : $" ({entryCoverage})";

                Replies.Add(new ReplyOption
                {
                    Index = i,
                    Text = $"{text}{conditionInfo}{coverageIndicator}",
                    HasCondition = !string.IsNullOrEmpty(start.ScriptAppears),
                    WasVisited = wasVisited,
                    IsComplete = isEntryComplete
                });
            }

            SelectedReplyIndex = Replies.Count > 0 ? 0 : -1;
            OnPropertyChanged(nameof(HasReplies));
            OnPropertyChanged(nameof(CoverageDisplay));
            OnPropertyChanged(nameof(Coverage));
            OnPropertyChanged(nameof(CoverageComplete));
        }

        /// <summary>
        /// Select a reply/entry and advance the conversation.
        /// </summary>
        public void SelectReply(int replyIndex)
        {
            if (HasEnded || replyIndex < 0 || replyIndex >= Replies.Count)
                return;

            SelectedReplyIndex = replyIndex;

            // Handle root entry selection
            if (_isSelectingRootEntry)
            {
                SelectRootEntry(replyIndex);
                return;
            }

            // Normal reply selection
            _pathDepth++;

            // Record the visited reply node
            if (replyIndex < _currentReplies.Count)
            {
                var reply = _currentReplies[replyIndex];
                var replyNodeIndex = _dialog.GetNodeIndex(reply, DialogNodeType.Reply);
                var nodeKey = $"R{replyNodeIndex}";
                _coverageTracker.RecordVisitedNode(_filePath, nodeKey);

                // Advance to next entry (first child of this reply)
                AdvanceToNextEntry(reply);
            }
        }

        /// <summary>
        /// Select a root entry to start the conversation.
        /// </summary>
        private void SelectRootEntry(int index)
        {
            if (index < 0 || index >= _dialog.Starts.Count)
                return;

            var start = _dialog.Starts[index];
            if (start.Node == null)
                return;

            _isSelectingRootEntry = false;
            OnPropertyChanged(nameof(IsShowingPcChoices));
            _currentEntry = start.Node;

            // Record that we visited this entry
            var entryIndex = _dialog.GetNodeIndex(_currentEntry, DialogNodeType.Entry);
            var nodeKey = $"E{entryIndex}";
            _coverageTracker.RecordVisitedNode(_filePath, nodeKey);

            // Get replies for this entry
            _currentReplies.Clear();
            foreach (var ptr in _currentEntry.Pointers)
            {
                if (ptr.Node != null)
                {
                    _currentReplies.Add(ptr.Node);
                }
            }

            UpdateDisplay();
            StatusMessage = $"Started from entry {index + 1} of {_dialog.Starts.Count}.";
        }

        /// <summary>
        /// Advance to the next NPC entry after selecting a reply.
        /// </summary>
        private void AdvanceToNextEntry(DialogNode reply)
        {
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
                return;
            }

            // Find next entry (child of this reply)
            if (reply.Pointers.Count == 0)
            {
                // Conversation ends
                HasEnded = true;
                StatusMessage = "Conversation ended.";
                ClearDisplay();
                OnPropertyChanged(nameof(CoverageDisplay));
                OnPropertyChanged(nameof(Coverage));
                OnPropertyChanged(nameof(CoverageComplete));
                ConversationEnded?.Invoke(this, EventArgs.Empty);
                return;
            }

            // Pick the first available entry (engine behavior)
            // In the future, we could show multiple NPC responses if they exist
            var nextPtr = reply.Pointers.FirstOrDefault(p => p.Node != null);
            if (nextPtr?.Node == null)
            {
                HasEnded = true;
                StatusMessage = "Conversation ended (no valid continuation).";
                ClearDisplay();
                return;
            }

            _currentEntry = nextPtr.Node;

            // Get replies for this entry
            _currentReplies.Clear();
            foreach (var ptr in _currentEntry.Pointers)
            {
                if (ptr.Node != null)
                {
                    _currentReplies.Add(ptr.Node);
                }
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
            OnPropertyChanged(nameof(CoverageComplete));
            StatusMessage = "Coverage cleared.";
            // Refresh replies to remove checkmarks
            UpdateDisplay();
        }

        private void AnalyzeDialogStructure()
        {
            // Count total replies for coverage tracking
            _totalReplies = _dialog.Replies.Count;

            // Collect root entry indices and map replies per root entry
            _rootEntryIndices.Clear();
            _repliesPerRootEntry.Clear();

            foreach (var start in _dialog.Starts)
            {
                if (start.Node != null)
                {
                    var entryIndex = _dialog.GetNodeIndex(start.Node, DialogNodeType.Entry);
                    if (entryIndex >= 0)
                    {
                        _rootEntryIndices.Add(entryIndex);

                        // Collect all reply indices reachable from this root entry
                        var replyIndices = new HashSet<int>();
                        CollectRepliesUnderNode(start.Node, replyIndices, new HashSet<DialogNode>());
                        _repliesPerRootEntry[entryIndex] = replyIndices;
                    }
                }
            }

            // Check for conditionals
            var hasConditionals = HasAnyConditionals();
            ShowNoConditionalsWarning = !hasConditionals;

            // Check for unreachable siblings (multiple NPC entries without conditions)
            ShowUnreachableSiblingsWarning = HasUnreachableSiblings();

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"ConversationSimulator: Analyzed dialog - " +
                $"totalReplies={_totalReplies}, rootEntries={_rootEntryIndices.Count}, " +
                $"hasConditionals={hasConditionals}, unreachableSiblings={ShowUnreachableSiblingsWarning}");
        }

        /// <summary>
        /// Recursively collect all reply indices reachable from a node.
        /// </summary>
        private void CollectRepliesUnderNode(DialogNode node, HashSet<int> replyIndices, HashSet<DialogNode> visited)
        {
            if (visited.Contains(node))
                return; // Avoid infinite loops from links

            visited.Add(node);

            foreach (var ptr in node.Pointers)
            {
                if (ptr.Node == null)
                    continue;

                if (ptr.Type == DialogNodeType.Reply)
                {
                    var replyIndex = _dialog.GetNodeIndex(ptr.Node, DialogNodeType.Reply);
                    if (replyIndex >= 0)
                    {
                        replyIndices.Add(replyIndex);
                    }
                }

                // Continue traversing (both entries and replies can have children)
                CollectRepliesUnderNode(ptr.Node, replyIndices, visited);
            }
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
            if (_currentEntry == null)
            {
                ClearDisplay();
                return;
            }

            // Update NPC display
            NpcSpeaker = _currentEntry.SpeakerDisplay;
            NpcSpeakerColor = SpeakerVisualHelper.GetSpeakerColor(_currentEntry.Speaker ?? "", isPC: false);
            NpcText = _currentEntry.DisplayText;

            // Update script info (from current entry)
            ActionScript = _currentEntry.ScriptAction ?? "";

            // Get condition script from the pointer that led here (if available)
            // For simplicity, clear condition script at entry level
            ConditionScript = "";

            // Record that we visited this entry
            var entryIndex = _dialog.GetNodeIndex(_currentEntry, DialogNodeType.Entry);
            var entryKey = $"E{entryIndex}";
            _coverageTracker.RecordVisitedNode(_filePath, entryKey);

            // Update available replies
            Replies.Clear();
            for (int i = 0; i < _currentReplies.Count; i++)
            {
                var reply = _currentReplies[i];
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
            OnPropertyChanged(nameof(CoverageComplete));
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
            if (_currentEntry == null) return "";

            var entryIndex = _dialog.GetNodeIndex(_currentEntry, DialogNodeType.Entry);
            var replyIndices = _currentReplies
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
        public bool IsComplete { get; set; }

        public string DisplayText => WasVisited ? $"\u2713 {Text}" : Text;
    }

    /// <summary>
    /// Maps a speaker name to a TTS voice.
    /// Issue #479 - TTS Integration Sprint
    /// </summary>
    public class SpeakerVoiceMapping : INotifyPropertyChanged
    {
        private string _speakerName = "";
        private string _voiceName = "";
        private bool _isPC;

        public string SpeakerName
        {
            get => _speakerName;
            set
            {
                if (_speakerName != value)
                {
                    _speakerName = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SpeakerName)));
                }
            }
        }

        public string VoiceName
        {
            get => _voiceName;
            set
            {
                if (_voiceName != value)
                {
                    _voiceName = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(VoiceName)));
                }
            }
        }

        public bool IsPC
        {
            get => _isPC;
            set
            {
                if (_isPC != value)
                {
                    _isPC = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPC)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
