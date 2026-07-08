using System.Collections.Generic;
using System.Linq;
using DialogEditor.Models;

namespace DialogEditor.ViewModels
{
    /// <summary>
    /// #2524: Back/Previous navigation for the Conversation Simulator.
    /// Maintains a history stack of navigation snapshots so the user can step back one entry
    /// per press without restarting. Coverage already recorded is preserved (Back only restores
    /// navigation position, it does not un-record visited nodes).
    /// </summary>
    public partial class ConversationSimulatorViewModel
    {
        /// <summary>
        /// Immutable snapshot of the restorable navigation state for a single step.
        /// </summary>
        private sealed class NavigationSnapshot
        {
            public bool IsSelectingRootEntry { get; init; }
            public DialogNode? CurrentEntry { get; init; }
            public IReadOnlyList<DialogNode> CurrentReplies { get; init; } = new List<DialogNode>();
            public int PathDepth { get; init; }
            public int SelectedReplyIndex { get; init; }
            public bool HasEnded { get; init; }
        }

        /// <summary>
        /// True when there is a prior step to return to. Bound to the Back button's IsEnabled.
        /// </summary>
        public bool CanGoBack => _navigationHistory.Count > 0;

        /// <summary>
        /// Captures the CURRENT navigation state onto the history stack before a forward
        /// transition. Call this immediately before advancing/selecting.
        /// </summary>
        private void PushNavigationSnapshot()
        {
            _navigationHistory.Push(new NavigationSnapshot
            {
                IsSelectingRootEntry = _isSelectingRootEntry,
                CurrentEntry = _currentEntry,
                CurrentReplies = _currentReplies.ToList(),
                PathDepth = _pathDepth,
                SelectedReplyIndex = _selectedReplyIndex,
                HasEnded = _hasEnded
            });
            OnPropertyChanged(nameof(CanGoBack));
        }

        /// <summary>
        /// Clears the navigation history (called when the conversation restarts).
        /// </summary>
        private void ResetNavigationHistory()
        {
            _navigationHistory.Clear();
            OnPropertyChanged(nameof(CanGoBack));
        }

        /// <summary>
        /// Step back to the previous entry in the current playthrough. Restores the prior NPC
        /// text and reply list, clears any ended/loop state, and does not re-speak (TTS suppressed).
        /// Coverage recorded so far is left intact.
        /// </summary>
        public void GoBack()
        {
            if (_navigationHistory.Count == 0)
                return;

            // A pending PC-reply advance would fire after we've already stepped back; cancel it.
            _isSpeakingPcReply = false;
            _pendingReplyToAdvance = null;
            _ttsService.Stop();

            var snapshot = _navigationHistory.Pop();

            _isSelectingRootEntry = snapshot.IsSelectingRootEntry;
            _currentEntry = snapshot.CurrentEntry;
            _currentReplies = snapshot.CurrentReplies.ToList();
            _pathDepth = snapshot.PathDepth;
            HasEnded = snapshot.HasEnded;

            // Returning from a leaf clears the ended/loop flags so the user can choose again.
            LoopDetected = false;
            ShowLoopWarning = false;

            _suppressAutoSpeak = true;
            try
            {
                if (_isSelectingRootEntry)
                {
                    ShowRootEntrySelection();
                }
                else
                {
                    UpdateDisplay();
                }
            }
            finally
            {
                _suppressAutoSpeak = false;
            }

            SelectedReplyIndex = snapshot.SelectedReplyIndex;
            OnPropertyChanged(nameof(IsShowingPcChoices));
            OnPropertyChanged(nameof(CanGoBack));
            StatusMessage = "Stepped back to the previous entry.";
        }
    }
}
