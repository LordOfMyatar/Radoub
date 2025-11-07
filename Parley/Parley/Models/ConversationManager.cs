using System;
using System.Collections.Generic;
using System.Linq;

namespace DialogEditor.Models
{
    /// <summary>
    /// Conversation state management based on xoreos DLGFile implementation
    /// Handles proper conversation flow and script evaluation
    /// </summary>
    public class ConversationManager
    {
        public static readonly uint EndLine = 0xFFFFFFFE;
        public static readonly uint InvalidLine = 0xFFFFFFFF;

        private readonly Dialog _dialog;
        private readonly IScriptEngine? _scriptEngine;

        // Current conversation state (xoreos-style)
        private DialogNode? _currentEntry;
        private List<DialogNode> _currentReplies = new();
        private bool _ended = true;

        public ConversationManager(Dialog dialog, IScriptEngine? scriptEngine = null)
        {
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
            _scriptEngine = scriptEngine;
        }

        public bool HasEnded => _ended;
        public DialogNode? CurrentEntry => _currentEntry;
        public IReadOnlyList<DialogNode> CurrentReplies => _currentReplies.AsReadOnly();

        /// <summary>
        /// Start conversation by evaluating starting entries and finding the first active one
        /// </summary>
        public void StartConversation()
        {
            AbortConversation();

            _currentEntry = null;
            _currentReplies.Clear();

            if (EvaluateEntries(_dialog.Starts, out var activeEntry))
            {
                _currentEntry = activeEntry;
                EvaluateReplies(_currentEntry?.Pointers ?? new List<DialogPtr>(), _currentReplies);

                // Run entry scripts
                RunScript(_currentEntry?.ScriptAction ?? string.Empty);
            }

            _ended = false;
        }

        /// <summary>
        /// Abort the current conversation
        /// </summary>
        public void AbortConversation()
        {
            if (_ended) return;

            RunScript(_dialog.ScriptAbort);

            _currentEntry = null;
            _currentReplies.Clear();
            _ended = true;
        }

        /// <summary>
        /// Pick a reply option by index into the current replies list
        /// </summary>
        public void PickReply(int replyIndex)
        {
            if (_ended || replyIndex < 0 || replyIndex >= _currentReplies.Count)
                return;

            var selectedReply = _currentReplies[replyIndex];

            // Check if this reply ends the conversation
            if (selectedReply.Pointers.Count == 0)
            {
                RunScript(_dialog.ScriptEnd);
                _ended = true;
                return;
            }

            // Run reply script
            RunScript(selectedReply.ScriptAction);

            // Evaluate next entries from this reply
            if (EvaluateEntries(selectedReply.Pointers, out var nextEntry))
            {
                _currentEntry = nextEntry;
                _currentReplies.Clear();

                // Run entry scripts
                RunScript(_currentEntry?.ScriptAction ?? string.Empty);

                // Evaluate available replies for the new entry
                EvaluateReplies(_currentEntry?.Pointers ?? new List<DialogPtr>(), _currentReplies);
            }
            else
            {
                // No valid next entry found - end conversation
                RunScript(_dialog.ScriptEnd);
                _ended = true;
            }
        }

        /// <summary>
        /// Get a one-liner (first active non-branching entry)
        /// Useful for barks or simple interactions
        /// </summary>
        public DialogNode? GetOneLiner()
        {
            foreach (var startPtr in _dialog.Starts)
            {
                if (startPtr.Node != null &&
                    startPtr.Node.Pointers.Count == 0 &&
                    EvaluatePointer(startPtr))
                {
                    return startPtr.Node;
                }
            }
            return null;
        }

        /// <summary>
        /// Evaluate a list of dialog pointers and find the first active entry
        /// Based on xoreos evaluateEntries()
        /// </summary>
        private bool EvaluateEntries(IEnumerable<DialogPtr> entries, out DialogNode? activeEntry)
        {
            activeEntry = null;

            foreach (var ptr in entries)
            {
                // Check if this pointer's conditions are met
                if (EvaluatePointer(ptr) && ptr.Node != null)
                {
                    activeEntry = ptr.Node;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Evaluate a list of dialog pointers and collect all active replies
        /// Based on xoreos evaluateReplies()
        /// </summary>
        private bool EvaluateReplies(IEnumerable<DialogPtr> entries, List<DialogNode> activeReplies)
        {
            activeReplies.Clear();

            foreach (var ptr in entries)
            {
                // Check if this pointer's conditions are met
                if (EvaluatePointer(ptr) && ptr.Node != null)
                {
                    activeReplies.Add(ptr.Node);
                }
            }

            return activeReplies.Count > 0;
        }

        /// <summary>
        /// Evaluate a dialog pointer to determine if it should be available
        /// Handles both ScriptAppears and ConditionParams
        /// </summary>
        private bool EvaluatePointer(DialogPtr pointer)
        {
            // First check ScriptAppears (traditional script file)
            if (!EvaluateScript(pointer.ScriptAppears))
                return false;

            // Then check ConditionParams (parameter-based conditions)
            if (!EvaluateConditionParams(pointer.ConditionParams))
                return false;

            return true;
        }

        /// <summary>
        /// Evaluate a script condition - returns true if script should make this option available
        /// </summary>
        private bool EvaluateScript(string scriptName)
        {
            if (string.IsNullOrEmpty(scriptName))
                return true; // No script means always available

            if (_scriptEngine == null)
                return true; // No script engine - assume all conditions pass

            try
            {
                return _scriptEngine.EvaluateCondition(scriptName);
            }
            catch (Exception)
            {
                // Script evaluation failed - log warning and assume condition fails
                return false;
            }
        }

        /// <summary>
        /// Evaluate condition parameters (quest states, costs, etc.)
        /// For now, return true for all to show complete dialogue tree
        /// Phase 3 Feature: Implement proper condition evaluation for script parameters
        /// </summary>
        private bool EvaluateConditionParams(Dictionary<string, string> conditionParams)
        {
            if (conditionParams.Count == 0)
                return true; // No conditions means always available

            // For testing purposes, show all dialogue options
            // In a real implementation, this would check:
            // - Quest states (sQuest, iQuestState)
            // - Player resources (iCost, Cost)
            // - Character attributes and flags
            // - Game world state

            return true; // Show all options for complete tree visualization
        }

        /// <summary>
        /// Run an action script
        /// </summary>
        private void RunScript(string scriptName)
        {
            if (string.IsNullOrEmpty(scriptName) || _scriptEngine == null)
                return;

            try
            {
                _scriptEngine.RunAction(scriptName);
            }
            catch (Exception)
            {
                // Script execution failed - log warning but continue
            }
        }

        /// <summary>
        /// Get debug information about current conversation state
        /// </summary>
        public string GetDebugInfo()
        {
            var info = new List<string>
            {
                $"Ended: {_ended}",
                $"Current Entry: {_currentEntry?.DisplayText ?? "null"}",
                $"Available Replies: {_currentReplies.Count}"
            };

            for (int i = 0; i < _currentReplies.Count; i++)
            {
                info.Add($"  Reply[{i}]: {_currentReplies[i].DisplayText}");
            }

            return string.Join("\n", info);
        }
    }

    /// <summary>
    /// Interface for script engine integration
    /// Implement this to provide actual script evaluation
    /// </summary>
    public interface IScriptEngine
    {
        /// <summary>
        /// Evaluate a conditional script - return true if condition is met
        /// </summary>
        bool EvaluateCondition(string scriptName);

        /// <summary>
        /// Run an action script
        /// </summary>
        void RunAction(string scriptName);
    }

    /// <summary>
    /// Simple script engine that always returns true (for testing)
    /// </summary>
    public class AlwaysTrueScriptEngine : IScriptEngine
    {
        public bool EvaluateCondition(string scriptName) => true;
        public void RunAction(string scriptName) { }
    }

    /// <summary>
    /// Script engine for debugging that logs all script calls
    /// </summary>
    public class DebugScriptEngine : IScriptEngine
    {
        private readonly Action<string> _logger;

        public DebugScriptEngine(Action<string> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public bool EvaluateCondition(string scriptName)
        {
            _logger($"Evaluating condition script: {scriptName}");
            return true; // Always pass for debugging
        }

        public void RunAction(string scriptName)
        {
            _logger($"Running action script: {scriptName}");
        }
    }
}