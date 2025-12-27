using Avalonia.Input;
using Avalonia.Interactivity;
using DialogEditor.Utils;
using System;
using Radoub.Formats.Logging;
using System.Collections.Generic;

namespace DialogEditor.Services
{
    /// <summary>
    /// Manages keyboard shortcuts with data-driven configuration.
    /// Handles both tunneling (PreviewKeyDown) and bubbling (KeyDown) events.
    /// Extracted from MainWindow.axaml.cs to eliminate 147-line setup method.
    /// </summary>
    public class KeyboardShortcutManager
    {
        /// <summary>
        /// Represents a keyboard shortcut action handler
        /// </summary>
        public delegate void ShortcutAction();

        // Shortcut registries by modifier combination
        private readonly Dictionary<Key, ShortcutAction> _ctrlShortcuts = new();
        private readonly Dictionary<Key, ShortcutAction> _ctrlShiftShortcuts = new();
        private readonly Dictionary<Key, ShortcutAction> _ctrlTunnelingShortcuts = new();
        private readonly Dictionary<Key, ShortcutAction> _ctrlShiftTunnelingShortcuts = new();
        private readonly Dictionary<Key, ShortcutAction> _noModifierShortcuts = new();

        /// <summary>
        /// Registers all keyboard shortcuts with their handlers
        /// </summary>
        public void RegisterShortcuts(IKeyboardShortcutHandler handler)
        {
            // Ctrl shortcuts
            _ctrlShortcuts[Key.N] = handler.OnNew;
            _ctrlShortcuts[Key.O] = handler.OnOpen;
            _ctrlShortcuts[Key.S] = handler.OnSave;
            _ctrlShortcuts[Key.D] = handler.OnAddSmartNode;
            _ctrlShortcuts[Key.R] = handler.OnAddContextAwareReply;
            _ctrlShortcuts[Key.C] = handler.OnCopyNode;
            _ctrlShortcuts[Key.V] = handler.OnPasteAsDuplicate;
            _ctrlShortcuts[Key.L] = handler.OnPasteAsLink;
            _ctrlShortcuts[Key.X] = handler.OnCutNode;
            // Note: Ctrl+Z and Ctrl+Y are handled in tunneling phase (see _ctrlTunnelingShortcuts)
            // to intercept before TextBox's built-in undo/redo
            _ctrlShortcuts[Key.E] = handler.OnExpandSubnodes;
            _ctrlShortcuts[Key.W] = handler.OnCollapseSubnodes;
            _ctrlShortcuts[Key.J] = handler.OnGoToParentNode; // Issue #149: Jump from link to parent

            // Ctrl+Shift shortcuts
            _ctrlShiftShortcuts[Key.D] = handler.OnAddSiblingNode; // Issue #150: Add sibling node
            _ctrlShiftShortcuts[Key.T] = handler.OnCopyNodeText;
            _ctrlShiftShortcuts[Key.P] = handler.OnCopyNodeProperties;
            _ctrlShiftShortcuts[Key.S] = handler.OnCopyTreeStructure;

            // Ctrl tunneling shortcuts (intercept before TextBox handles them)
            // This ensures global undo/redo instead of TextBox's character-level undo
            _ctrlTunnelingShortcuts[Key.Z] = handler.OnUndo;
            _ctrlTunnelingShortcuts[Key.Y] = handler.OnRedo;

            // Ctrl+Shift tunneling shortcuts (intercept before TreeView)
            _ctrlShiftTunnelingShortcuts[Key.Up] = handler.OnMoveNodeUp;
            _ctrlShiftTunnelingShortcuts[Key.Down] = handler.OnMoveNodeDown;

            // No modifier shortcuts
            _noModifierShortcuts[Key.Delete] = handler.OnDeleteNode;
            _noModifierShortcuts[Key.F5] = handler.OnOpenFlowchart; // Issue #339: F5 to open flowchart
            _noModifierShortcuts[Key.F6] = handler.OnOpenConversationSimulator; // Issue #478: F6 to open conversation simulator

            UnifiedLogger.LogApplication(LogLevel.INFO, "KeyboardShortcutManager: Registered all shortcuts");
        }

        /// <summary>
        /// Handles PreviewKeyDown events (tunneling) for shortcuts that need to intercept before TreeView/TextBox
        /// </summary>
        public bool HandlePreviewKeyDown(KeyEventArgs e)
        {
            bool ctrlPressed = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            bool shiftPressed = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            bool altPressed = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"PreviewKeyDown: Key={e.Key}, Modifiers={e.KeyModifiers}");

            // Ctrl+Z/Y for undo/redo (must intercept before TextBox handles them)
            if (ctrlPressed && !shiftPressed && !altPressed)
            {
                if (_ctrlTunnelingShortcuts.TryGetValue(e.Key, out var action))
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"Ctrl+{e.Key} detected (tunneling)");
                    action();
                    return true; // Mark as handled
                }
            }

            // Ctrl+Shift+Up/Down for node reordering (must intercept before TreeView)
            if (ctrlPressed && shiftPressed && !altPressed)
            {
                if (_ctrlShiftTunnelingShortcuts.TryGetValue(e.Key, out var action))
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG,
                        $"Ctrl+Shift+{e.Key} detected (tunneling)");
                    action();
                    return true; // Mark as handled
                }
            }

            return false; // Not handled
        }

        /// <summary>
        /// Handles KeyDown events (bubbling) for standard shortcuts
        /// </summary>
        public bool HandleKeyDown(KeyEventArgs e)
        {
            bool ctrlPressed = e.KeyModifiers.HasFlag(KeyModifiers.Control);
            bool shiftPressed = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
            bool altPressed = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

            // Ctrl shortcuts (no shift)
            if (ctrlPressed && !shiftPressed && !altPressed)
            {
                if (_ctrlShortcuts.TryGetValue(e.Key, out var action))
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Ctrl+{e.Key} detected");
                    action();
                    return true;
                }
            }
            // Ctrl+Shift shortcuts
            else if (ctrlPressed && shiftPressed && !altPressed)
            {
                if (_ctrlShiftShortcuts.TryGetValue(e.Key, out var action))
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Ctrl+Shift+{e.Key} detected");
                    action();
                    return true;
                }
            }
            // No modifier shortcuts
            else if (!ctrlPressed && !shiftPressed && !altPressed)
            {
                if (_noModifierShortcuts.TryGetValue(e.Key, out var action))
                {
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"{e.Key} detected");
                    action();
                    return true;
                }
            }

            return false; // Not handled
        }

        /// <summary>
        /// Gets a human-readable list of all registered shortcuts for help display
        /// </summary>
        public List<string> GetShortcutDescriptions()
        {
            var shortcuts = new List<string>();

            // File operations
            shortcuts.Add("Ctrl+N - New Dialog");
            shortcuts.Add("Ctrl+O - Open Dialog");
            shortcuts.Add("Ctrl+S - Save Dialog");

            // Node operations
            shortcuts.Add("Ctrl+D - Add Smart Node");
            shortcuts.Add("Ctrl+Shift+D - Add Sibling Node");
            shortcuts.Add("Ctrl+R - Add Context-Aware Reply");
            shortcuts.Add("Delete - Delete Node");

            // Clipboard
            shortcuts.Add("Ctrl+C - Copy Node");
            shortcuts.Add("Ctrl+X - Cut Node");
            shortcuts.Add("Ctrl+V - Paste as Duplicate");
            shortcuts.Add("Ctrl+L - Paste as Link");

            // Advanced copy
            shortcuts.Add("Ctrl+Shift+T - Copy Node Text");
            shortcuts.Add("Ctrl+Shift+P - Copy Node Properties");
            shortcuts.Add("Ctrl+Shift+S - Copy Tree Structure");

            // History
            shortcuts.Add("Ctrl+Z - Undo");
            shortcuts.Add("Ctrl+Y - Redo");

            // Tree navigation
            shortcuts.Add("Ctrl+E - Expand Subnodes");
            shortcuts.Add("Ctrl+W - Collapse Subnodes");
            shortcuts.Add("Ctrl+J - Go to Parent Node (from link)");
            shortcuts.Add("Ctrl+Shift+Up - Move Node Up");
            shortcuts.Add("Ctrl+Shift+Down - Move Node Down");

            // View
            shortcuts.Add("F5 - Open Flowchart");
            shortcuts.Add("F6 - Open Conversation Simulator");

            return shortcuts;
        }
    }

    /// <summary>
    /// Interface for keyboard shortcut handlers.
    /// Implemented by MainWindow to provide action implementations.
    /// </summary>
    public interface IKeyboardShortcutHandler
    {
        // File operations
        void OnNew();
        void OnOpen();
        void OnSave();

        // Node operations
        void OnAddSmartNode();
        void OnAddSiblingNode(); // Issue #150: Add sibling node (Ctrl+Shift+D)
        void OnAddContextAwareReply();
        void OnDeleteNode();

        // Clipboard operations
        void OnCopyNode();
        void OnCutNode();
        void OnPasteAsDuplicate();
        void OnPasteAsLink();

        // Advanced copy
        void OnCopyNodeText();
        void OnCopyNodeProperties();
        void OnCopyTreeStructure();

        // History
        void OnUndo();
        void OnRedo();

        // Tree navigation
        void OnExpandSubnodes();
        void OnCollapseSubnodes();
        void OnMoveNodeUp();
        void OnMoveNodeDown();
        void OnGoToParentNode(); // Issue #149: Jump from link to parent node

        // View operations
        void OnOpenFlowchart(); // Issue #339: F5 to open flowchart
        void OnOpenConversationSimulator(); // Issue #478: F6 to open conversation simulator
    }
}
