using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ConvoEditor.Models
{
    public enum ConversationNodeType
    {
        Root,
        Entry,
        Reply
    }

    public class ConversationNode
    {
        public int Id { get; set; }
        public ConversationNodeType Type { get; set; }
        public string Text { get; set; } = string.Empty;
        public string Speaker { get; set; } = string.Empty;
        public uint StrRef { get; set; } = 0xFFFFFFFF; // String reference ID for debugging
        public string? Comment { get; set; }
        public bool IsRoot { get; set; }
        
        // Scripting and conditions (NWN DLG format)
        public string? ConditionsScript { get; set; }  // "Text Appears when..." tab
        public string? ActionsScript { get; set; }     // "Actions Taken" tab
        public Dictionary<string, string> ConditionsParameters { get; set; } = new();
        public Dictionary<string, string> ActionsParameters { get; set; } = new();
        
        // Animation and audio (NWN DLG format)
        public int? AnimationId { get; set; }         // Animation ID (DWORD in spec)
        public string? PlayAnimation { get; set; }     // Animation ID as string (legacy)
        public bool AnimLoop { get; set; }             // Animation looping (obsolete but in spec)
        public string? PlaySound { get; set; }         // Sound file (CResRef)
        
        // Journal/Quest system (NWN DLG format)
        public string? JournalCategory { get; set; }   // Quest category (CExoString)
        public string? JournalEntryText { get; set; }  // Journal entry text content
        public int? QuestEntryId { get; set; }         // Journal entry ID (DWORD)
        public string? QuestTag { get; set; }          // Quest tag (legacy)
        
        // Timing and display (NWN DLG format)
        public uint? Delay { get; set; } = 0xFFFFFFFF; // Line delay (DWORD, 0xFFFFFFFF = default)
        public bool PreventZoomIn { get; set; }        // Camera zoom prevention (BYTE)
        
        // Sync Struct fields (for linked dialogs)
        public string? ActiveScript { get; set; }      // Conditional visibility script (CResRef)
        public bool IsLink { get; set; }               // IsChild field - true if link (BYTE)
        public string? LinkComment { get; set; }       // Comment for linked lines (CExoString)
        
        // Conversation-level fields (Top Level Struct)
        public uint? DelayEntry { get; set; }          // Seconds to wait before showing entries
        public uint? DelayReply { get; set; }          // Seconds to wait before showing replies
        public string? EndConversationScript { get; set; }  // Script on normal end (CResRef)
        public string? EndConverAbortScript { get; set; }   // Script on abort (CResRef)
        public uint? NumWords { get; set; }            // Word count (DWORD, informational)
        
        // Legacy compatibility properties
        public string? Script 
        { 
            get => ActionsScript; 
            set => ActionsScript = value; 
        }
        public string? Condition 
        { 
            get => ConditionsScript; 
            set => ConditionsScript = value; 
        }
        public string? Animation 
        { 
            get => PlayAnimation ?? AnimationId?.ToString(); 
            set => PlayAnimation = value; 
        }
        public string? Sound 
        { 
            get => PlaySound; 
            set => PlaySound = value; 
        }
        public string? JournalEntry 
        { 
            get => JournalEntryText; 
            set => JournalEntryText = value; 
        }
        public string? Quest 
        { 
            get => QuestTag; 
            set => QuestTag = value; 
        }
        
        // Conversation flow
        public bool EndConversation { get; set; }
        public bool ContinueConversation { get; set; }
        public bool IsQuestEntry { get; set; }
        
        // Tree structure
        public ConversationNode? Parent { get; set; }
        public ObservableCollection<ConversationNode> Children { get; set; } = new();
        
        // Links to other parts of the conversation
        public List<int> LinkedNodeIds { get; set; } = new();
        
        // Display helpers
        public string DisplayText => Type switch
        {
            ConversationNodeType.Root => "Root",
            ConversationNodeType.Entry => $"[{Speaker}] \"{Text}\" (StrRef:{StrRef})",
            ConversationNodeType.Reply => $"[{Speaker}] \"{Text}\" (StrRef:{StrRef})",
            _ => Text
        };

        public string ShortDisplayText
        {
            get
            {
                if (Type == ConversationNodeType.Root) return "Root";
                
                var displayText = Text;
                    
                return $"[{Speaker}] \"{displayText}\" (StrRef:{StrRef})";
            }
        }
        
        /// <summary>
        /// Short text for display in TreeView without speaker brackets
        /// </summary>
        public string ShortText
        {
            get
            {
                if (Type == ConversationNodeType.Root) return "Root";
                
                var displayText = Text;
                    
                return displayText;
            }
        }
        
        /// <summary>
        /// Visual indicator showing connection information
        /// </summary>
        public string ConnectionIndicator
        {
            get
            {
                if (EndConversation) return "[END]";
                if (IsDeadEnd) return "[DEAD END]";
                if (HasMultipleChoices) return "⮁";
                if (Children.Count == 1) return "→";
                if (LinkedNodeIds.Count > 0) return "🔗";
                return string.Empty;
            }
        }

        public bool IsDeadEnd => Children.Count == 0 && !EndConversation && LinkedNodeIds.Count == 0;
        
        public bool HasMultipleChoices => Children.Count > 1;

        // Factory methods
        public static ConversationNode CreateRoot()
        {
            return new ConversationNode
            {
                Id = 0,
                Type = ConversationNodeType.Root,
                Text = "Root",
                IsRoot = true,
                Speaker = "Root"
            };
        }

        public static ConversationNode CreateEntry(string text, string speaker = "Owner")
        {
            return new ConversationNode
            {
                Type = ConversationNodeType.Entry,
                Text = text,
                Speaker = speaker
            };
        }

        public static ConversationNode CreateReply(string text, string speaker = "PC")
        {
            return new ConversationNode
            {
                Type = ConversationNodeType.Reply,
                Text = text,
                Speaker = speaker
            };
        }

        public void AddChild(ConversationNode child)
        {
            child.Parent = this;
            Children.Add(child);
        }

        public void RemoveChild(ConversationNode child)
        {
            child.Parent = null;
            Children.Remove(child);
        }

        public override string ToString()
        {
            return DisplayText;
        }
    }
}
