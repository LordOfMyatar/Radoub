using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace DialogEditor.Models
{
    public enum DialogNodeType
    {
        Entry,
        Reply
    }

    public enum DialogAnimation : uint
    {
        Default = 0,
        Taunt = 28,
        Greeting = 29,
        Listen = 30,
        Worship = 33,
        Salute = 34,
        Bow = 35,
        Steal = 37,
        TalkNormal = 38,
        TalkPleading = 39,
        TalkForceful = 40,
        TalkLaugh = 41,
        Victory1 = 44,
        Victory2 = 45,
        Victory3 = 46,
        LookFar = 48,
        Drink = 70,
        Read = 71,
        None = 88
    }

    public class LocString
    {
        [JsonProperty("Strings")]
        public Dictionary<int, string> Strings { get; set; } = new();
        
        [JsonIgnore]
        public string DefaultText { get; set; } = string.Empty;
        
        public void Add(int languageId, string text, bool feminine = false)
        {
            var key = feminine ? languageId + 1000 : languageId;
            Strings[key] = text;
            if (languageId == 0 && !feminine)
                DefaultText = text;
        }
        
        public string? Get(int languageId = 0, bool feminine = false)
        {
            var key = feminine ? languageId + 1000 : languageId;
            return Strings.TryGetValue(key, out var text) ? text : null;
        }
        
        public string GetDefault() => Get(0) ?? string.Empty;
        
        public Dictionary<int, string> GetAllStrings() => new(Strings);
        
        [JsonIgnore]
        public bool IsEmpty => Strings.Count == 0;
    }

    public class DialogPtr
    {
        [JsonIgnore]
        public Dialog? Parent { get; set; }
        public DialogNodeType Type { get; set; } = DialogNodeType.Entry;
        public uint Index { get; set; } = uint.MaxValue;
        public DialogNode? Node { get; set; }

        public string ScriptAppears { get; set; } = string.Empty;
        public Dictionary<string, string> ConditionParams { get; set; } = new();

        // 2025-10-21: Preserve original GFF struct type for round-trip editing
        // GFF uses frequency-based type assignment (Type-0 = most common pattern)
        [JsonIgnore]
        public DialogEditor.Parsers.GffStruct? OriginalGffStruct { get; set; }
        
        public bool IsStart { get; set; }
        public bool IsLink { get; set; }
        public string Comment { get; set; } = string.Empty;
        public string LinkComment { get; set; } = string.Empty; // Bioware spec: Link-specific comment (IsChild=1)
        
        // Display properties for UI binding
        [JsonIgnore]
        public string TypeDisplay => Type == DialogNodeType.Entry ? (!string.IsNullOrEmpty(Node?.Speaker) ? Node.Speaker : "Owner") : "PC";
        
        [JsonIgnore]
        public string DisplayText => Node?.DisplayText ?? "[Missing Node]";
        
        public DialogPtr? AddPtr(DialogPtr ptr, bool isLink = false)
        {
            if (Parent == null || Node == null) return null;
            
            if (isLink)
            {
                var newPtr = Parent.CreatePtr();
                if (newPtr != null)
                {
                    CopyTo(newPtr);
                    newPtr.IsLink = true;
                    Node.Pointers.Add(newPtr);
                }
                return newPtr;
            }
            else
            {
                Node.Pointers.Add(ptr);
                // Add all subnodes to parent's internal lists
                var subnodes = GetAllSubnodes();
                foreach (var subnode in subnodes)
                {
                    Parent.AddNodeInternal(subnode, subnode.Type);
                }
                return ptr;
            }
        }
        
        public DialogPtr? AddString(string value, int languageId = 0, bool feminine = false)
        {
            if (Parent == null) return null;
            
            var ptr = Parent.CreatePtr();
            if (ptr == null) return null;
            
            ptr.Type = Type == DialogNodeType.Entry ? DialogNodeType.Reply : DialogNodeType.Entry;
            ptr.Node = Parent.CreateNode(ptr.Type);
            if (ptr.Node != null)
            {
                ptr.Node.Text.Add(languageId, value, feminine);
            }
            return AddPtr(ptr);
        }
        
        public List<DialogNode> GetAllSubnodes()
        {
            var subnodes = new List<DialogNode>();
            if (!IsLink && Node != null)
            {
                subnodes.Add(Node);
                foreach (var pointer in Node.Pointers)
                {
                    subnodes.AddRange(pointer.GetAllSubnodes());
                }
            }
            return subnodes;
        }
        
        public string? GetConditionParam(string key)
        {
            return ConditionParams.TryGetValue(key, out var value) ? value : null;
        }
        
        public void SetConditionParam(string key, string value)
        {
            ConditionParams[key] = value;
        }
        
        public DialogPtr? Copy()
        {
            if (Parent == null) return null;
            
            var result = Parent.CreatePtr();
            if (result == null) return null;
            
            CopyTo(result);
            if (!IsLink && Node != null)
            {
                result.Node = Node.Copy();
            }
            return result;
        }
        
        private void CopyTo(DialogPtr target)
        {
            target.Parent = Parent;
            target.Type = Type;
            target.Index = Index;
            target.Node = Node;
            target.ScriptAppears = ScriptAppears;
            target.ConditionParams = new Dictionary<string, string>(ConditionParams);
            target.IsStart = IsStart;
            target.IsLink = IsLink; // Preserve IsLink/IsChild flag for tree traversal
            target.Comment = Comment;
            target.LinkComment = LinkComment; // Preserve link-specific comment
        }
    }

    public class DialogNode
    {
        [JsonIgnore]
        public Dialog? Parent { get; set; }
        public DialogNodeType Type { get; set; }
        
        public string Comment { get; set; } = string.Empty;
        public string Quest { get; set; } = string.Empty;
        public string Speaker { get; set; } = string.Empty;
        public uint QuestEntry { get; set; } = uint.MaxValue;
        public string ScriptAction { get; set; } = string.Empty;
        public string Sound { get; set; } = string.Empty;
        public LocString Text { get; set; } = new();
        public DialogAnimation Animation { get; set; } = DialogAnimation.Default;
        public bool AnimationLoop { get; set; } = false;
        public uint Delay { get; set; } = uint.MaxValue;
        
        public List<DialogPtr> Pointers { get; set; } = new();
        public Dictionary<string, string> ActionParams { get; set; } = new();

        // 2025-10-21: Preserve original GFF struct type for round-trip editing
        // GFF uses frequency-based type assignment (Type-0 = most common pattern)
        [JsonIgnore]
        public DialogEditor.Parsers.GffStruct? OriginalGffStruct { get; set; }

        // Display properties for UI binding
        [JsonIgnore]
        public string TypeDisplay => Type == DialogNodeType.Entry ? (!string.IsNullOrEmpty(Speaker) ? Speaker : "Owner") : "PC";
        
        [JsonIgnore]  
        public string DisplayText => Text.GetDefault().Trim();
        
        [JsonIgnore]
        public string SpeakerDisplay 
        {
            get
            {
                if (Type == DialogNodeType.Reply)
                {
                    return "PC";
                }
                else
                {
                    if (!string.IsNullOrEmpty(Speaker))
                    {
                        return Speaker;
                    }
                    else
                    {
                        // For Entry nodes without Speaker tag, check if this is a multi-NPC conversation
                        return IsMultiNpcConversation() ? "NPC" : "Owner";
                    }
                }
            }
        }
        
        public string? GetActionParam(string key)
        {
            return ActionParams.TryGetValue(key, out var value) ? value : null;
        }
        
        public void SetActionParam(string key, string value)
        {
            ActionParams[key] = value;
        }
        
        public DialogNode? Copy()
        {
            if (Parent == null) return null;
            
            var result = Parent.CreateNode(Type);
            if (result == null) return null;
            
            result.Comment = Comment;
            result.Quest = Quest;
            result.Speaker = Speaker;
            result.QuestEntry = QuestEntry;
            result.ScriptAction = ScriptAction;
            result.Sound = Sound;
            result.Text = new LocString();
            foreach (var kvp in Text.GetAllStrings())
            {
                result.Text.Add(kvp.Key, kvp.Value);
            }
            result.Animation = Animation;
            result.AnimationLoop = AnimationLoop;
            result.Delay = Delay;
            result.ActionParams = new Dictionary<string, string>(ActionParams);
            
            // Copy pointers
            foreach (var pointer in Pointers)
            {
                var copiedPtr = pointer.Copy();
                if (copiedPtr != null)
                {
                    result.Pointers.Add(copiedPtr);
                }
            }
            
            return result;
        }

        private bool IsMultiNpcConversation()
        {
            // Check if the parent dialog has multiple different speakers among Entry nodes
            if (Parent == null)
                return false;

            var speakers = new HashSet<string>();
            foreach (var entry in Parent.Entries)
            {
                if (!string.IsNullOrEmpty(entry.Speaker))
                {
                    speakers.Add(entry.Speaker);
                }
            }
            
            // If there are 2 or more distinct speakers, it's a multi-NPC conversation
            return speakers.Count >= 2;
        }
    }

    public class Dialog
    {
        private static uint _nextNodeId = 0;
        private Dictionary<uint, DialogNode> _nodePool = new();
        private Dictionary<uint, DialogPtr> _ptrPool = new();

        public List<DialogNode> Entries { get; } = new();
        public List<DialogNode> Replies { get; } = new();

        public string ScriptAbort { get; set; } = "nw_walk_wp";
        public string ScriptEnd { get; set; } = "nw_walk_wp";
        public List<DialogPtr> Starts { get; } = new();

        public uint DelayEntry { get; set; } = 0;
        public uint DelayReply { get; set; } = 0;
        public uint NumWords { get; set; } = 0; // Bioware spec: Number of words counted in conversation

        public bool PreventZoom { get; set; } = false;
        public bool IsValid { get; private set; } = true;

        // 2025-10-21: Preserve root GFF struct for round-trip editing
        // GFF uses frequency-based type assignment (Type-0 = most common pattern)
        [JsonIgnore]
        public DialogEditor.Parsers.GffStruct? OriginalRootGffStruct { get; set; }
        
        public DialogPtr? Add()
        {
            var ptr = CreatePtr();
            if (ptr == null) return null;
            
            ptr.Type = DialogNodeType.Entry;
            ptr.Node = CreateNode(ptr.Type);
            Starts.Add(ptr);
            return ptr;
        }
        
        public DialogPtr? AddPtr(DialogPtr ptr, bool isLink = false)
        {
            if (isLink)
            {
                var newPtr = CreatePtr();
                if (newPtr != null)
                {
                    // Copy properties
                    newPtr.Type = ptr.Type;
                    newPtr.Node = ptr.Node;
                    newPtr.ScriptAppears = ptr.ScriptAppears;
                    newPtr.ConditionParams = new Dictionary<string, string>(ptr.ConditionParams);
                    newPtr.IsLink = true;
                    newPtr.Comment = ptr.Comment;
                }
                return newPtr;
            }
            else
            {
                var subnodes = ptr.GetAllSubnodes();
                foreach (var subnode in subnodes)
                {
                    AddNodeInternal(subnode, subnode.Type);
                }
                return ptr;
            }
        }
        
        public DialogPtr? AddString(string value, int languageId = 0, bool feminine = false)
        {
            var ptr = CreatePtr();
            if (ptr == null) return null;
            
            ptr.Type = DialogNodeType.Entry;
            ptr.Node = CreateNode(ptr.Type);
            if (ptr.Node != null)
            {
                ptr.Node.Text.Add(languageId, value, feminine);
            }
            return ptr;
        }
        
        public DialogNode? CreateNode(DialogNodeType type)
        {
            var id = ++_nextNodeId;
            var node = new DialogNode
            {
                Parent = this,
                Type = type
            };
            _nodePool[id] = node;
            return node;
        }
        
        public DialogPtr? CreatePtr()
        {
            var id = ++_nextNodeId;
            var ptr = new DialogPtr
            {
                Parent = this
            };
            _ptrPool[id] = ptr;
            return ptr;
        }
        
        public void AddNodeInternal(DialogNode node, DialogNodeType type)
        {
            var targetList = type == DialogNodeType.Entry ? Entries : Replies;
            if (!targetList.Contains(node))
            {
                targetList.Add(node);
            }
        }
        
        public void RemoveNodeInternal(DialogNode node, DialogNodeType type)
        {
            var targetList = type == DialogNodeType.Entry ? Entries : Replies;
            targetList.Remove(node);
        }
        
        public int GetNodeIndex(DialogNode node, DialogNodeType type)
        {
            var targetList = type == DialogNodeType.Entry ? Entries : Replies;
            return targetList.IndexOf(node);
        }
    }
}