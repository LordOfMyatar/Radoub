using System;
using System.Linq;
using DialogEditor.Models;
using DialogEditor.Services;
using DialogEditor.Utils;

namespace DialogEditor.ViewModels
{
    /// <summary>
    /// Conversation flow: starting, advancing, selecting replies, skipping, and exiting.
    /// </summary>
    public partial class ConversationSimulatorViewModel
    {
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

            // Issue #484: Calculate unreachable siblings for root entries
            var unreachableIndices = Models.TreeViewValidation.CalculateUnreachableSiblings(_dialog.Starts);

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
                    IsComplete = isEntryComplete,
                    IsUnreachable = unreachableIndices.Contains(i)
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

                // Speak PC reply if auto-speak enabled
                if (AutoSpeak && TtsAvailable && TtsEnabled)
                {
                    var pcText = reply.DisplayText;
                    if (!string.IsNullOrWhiteSpace(pcText))
                    {
                        // Store pending reply and speak PC text first
                        // Advancement will happen when PC speech completes
                        _isSpeakingPcReply = true;
                        _pendingReplyToAdvance = reply;
                        var pcVoice = GetVoiceForSpeaker("(PC)");
                        _ttsService.Speak(pcText, pcVoice, TtsRate);
                        return; // Don't advance yet - wait for speech to complete
                    }
                }

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
    }
}
