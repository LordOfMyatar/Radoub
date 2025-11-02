using System;
using System.Collections.Generic;
using System.Linq;
using DialogEditor.Models;
using DialogEditor.Services;
using Microsoft.Extensions.Logging;

namespace DialogEditor.Utils
{
    /// <summary>
    /// Manages GFF's compact pointer algorithm to eliminate duplicate pointer structs
    /// while maintaining logical conversation flow and tree display functionality.
    ///
    /// GFF uses a deduplication system where multiple logical pointers can reference
    /// the same physical pointer struct, achieving ~2:1 compression ratio.
    /// </summary>
    public class CompactPointerManager
    {
        private readonly ILogger _logger;
        private readonly Dictionary<PointerKey, int> _structMap; // Maps pointer targets to struct indices
        private readonly List<UniquePointer> _uniquePointers; // Deduplicated pointer structs

        public CompactPointerManager(ILogger logger)
        {
            _logger = logger;
            _structMap = new Dictionary<PointerKey, int>();
            _uniquePointers = new List<UniquePointer>();
        }

        /// <summary>
        /// Analyzes all pointers in the dialog and creates a deduplicated set of pointer structs
        /// following GFF's compact algorithm principles.
        /// </summary>
        public CompactPointerResult ProcessDialog(Dialog dialog)
        {
            UnifiedLogger.LogParser(DialogEditor.Services.LogLevel.INFO, "🎯 COMPACT POINTER MANAGER: Starting deduplication analysis");

            // Clear previous data
            _structMap.Clear();
            _uniquePointers.Clear();

            // Count original pointers for analysis
            int originalPointerCount = CountAllPointers(dialog);
            UnifiedLogger.LogParser(DialogEditor.Services.LogLevel.INFO, $"📊 Original pointer count: {originalPointerCount}");

            // Process entry pointers
            var entryMappings = ProcessEntryPointers(dialog);

            // Process reply pointers
            var replyMappings = ProcessReplyPointers(dialog);

            int deduplicatedCount = _uniquePointers.Count;
            double compressionRatio = (double)originalPointerCount / deduplicatedCount;

            UnifiedLogger.LogParser(DialogEditor.Services.LogLevel.INFO, $"✅ DEDUPLICATION COMPLETE:");
            UnifiedLogger.LogParser(DialogEditor.Services.LogLevel.INFO, $"   Original Pointers: {originalPointerCount}");
            UnifiedLogger.LogParser(DialogEditor.Services.LogLevel.INFO, $"   Deduplicated Structs: {deduplicatedCount}");
            UnifiedLogger.LogParser(DialogEditor.Services.LogLevel.INFO, $"   Compression Ratio: {compressionRatio:F1}:1");
            UnifiedLogger.LogParser(DialogEditor.Services.LogLevel.INFO, $"   Struct Reduction: {((originalPointerCount - deduplicatedCount) / (double)originalPointerCount * 100):F1}%");

            return new CompactPointerResult
            {
                UniquePointers = _uniquePointers,
                EntryStructMappings = entryMappings,
                ReplyStructMappings = replyMappings,
                OriginalCount = originalPointerCount,
                DeduplicatedCount = deduplicatedCount,
                CompressionRatio = compressionRatio
            };
        }

        private List<List<int>> ProcessEntryPointers(Dialog dialog)
        {
            var entryMappings = new List<List<int>>();

            for (int entryIdx = 0; entryIdx < dialog.Entries.Count; entryIdx++)
            {
                var entry = dialog.Entries[entryIdx];
                var structIndices = new List<int>();

                if (entry.Pointers.Count > 0)
                {
                    UnifiedLogger.LogParser(DialogEditor.Services.LogLevel.INFO, $"🔍 COMPACT DEBUG: Entry[{entryIdx}] has {entry.Pointers.Count} pointers");
                }

                for (int ptrIdx = 0; ptrIdx < entry.Pointers.Count; ptrIdx++)
                {
                    var pointer = entry.Pointers[ptrIdx];
                    UnifiedLogger.LogParser(DialogEditor.Services.LogLevel.INFO, $"🔍 COMPACT DEBUG: Entry[{entryIdx}] pointer[{ptrIdx}] → Index={pointer.Index}, Type={pointer.Type}");
                    var key = new PointerKey(pointer.Index, pointer.Type, pointer.IsLink);
                    int structIndex = GetOrCreatePointerStruct(key, pointer);
                    structIndices.Add(structIndex);
                    UnifiedLogger.LogParser(DialogEditor.Services.LogLevel.INFO, $"🔍 COMPACT DEBUG: Entry[{entryIdx}] pointer[{ptrIdx}] mapped to structIndex={structIndex}");
                }

                entryMappings.Add(structIndices);

                if (entry.Pointers.Count > 0)
                {
                    UnifiedLogger.LogParser(DialogEditor.Services.LogLevel.DEBUG, $"Entry[{entryIdx}]: {entry.Pointers.Count} logical → {structIndices.Distinct().Count()} unique structs");
                }
            }

            return entryMappings;
        }

        private List<List<int>> ProcessReplyPointers(Dialog dialog)
        {
            var replyMappings = new List<List<int>>();

            for (int replyIdx = 0; replyIdx < dialog.Replies.Count; replyIdx++)
            {
                var reply = dialog.Replies[replyIdx];
                var structIndices = new List<int>();

                UnifiedLogger.LogParser(DialogEditor.Services.LogLevel.INFO, $"🔍 COMPACT DEBUG: Reply[{replyIdx}] has {reply.Pointers.Count} pointers");
                for (int ptrIdx = 0; ptrIdx < reply.Pointers.Count; ptrIdx++)
                {
                    var pointer = reply.Pointers[ptrIdx];
                    UnifiedLogger.LogParser(DialogEditor.Services.LogLevel.INFO, $"🔍 COMPACT DEBUG: Reply[{replyIdx}] pointer[{ptrIdx}] → Index={pointer.Index}, Type={pointer.Type}");
                    var key = new PointerKey(pointer.Index, pointer.Type, pointer.IsLink);
                    int structIndex = GetOrCreatePointerStruct(key, pointer);
                    structIndices.Add(structIndex);
                    UnifiedLogger.LogParser(DialogEditor.Services.LogLevel.INFO, $"🔍 COMPACT DEBUG: Reply[{replyIdx}] pointer[{ptrIdx}] mapped to structIndex={structIndex}");
                }

                replyMappings.Add(structIndices);

                if (reply.Pointers.Count > 0)
                {
                    UnifiedLogger.LogParser(DialogEditor.Services.LogLevel.DEBUG, $"Reply[{replyIdx}]: {reply.Pointers.Count} logical → {structIndices.Distinct().Count()} unique structs");
                }
            }

            return replyMappings;
        }

        private int GetOrCreatePointerStruct(PointerKey key, DialogPtr pointer)
        {
            // Check if we already have a struct for this pointer target
            if (_structMap.TryGetValue(key, out int existingIndex))
            {
                UnifiedLogger.LogParser(DialogEditor.Services.LogLevel.DEBUG, $"🔗 REUSING struct[{existingIndex}] for {key.Type}[{key.Index}]");
                return existingIndex;
            }

            // Create new unique pointer struct
            int newIndex = _uniquePointers.Count;
            var uniquePointer = new UniquePointer
            {
                Index = key.Index,
                Type = key.Type,
                IsLink = key.IsLink,
                StructIndex = newIndex,
                LogicalPointerCount = 1 // Will be incremented for reuse
            };

            _uniquePointers.Add(uniquePointer);
            _structMap[key] = newIndex;

            UnifiedLogger.LogParser(DialogEditor.Services.LogLevel.DEBUG, $"🆕 CREATED struct[{newIndex}] for {key.Type}[{key.Index}]");
            return newIndex;
        }

        private int CountAllPointers(Dialog dialog)
        {
            int count = 0;
            foreach (var entry in dialog.Entries)
                count += entry.Pointers.Count;
            foreach (var reply in dialog.Replies)
                count += reply.Pointers.Count;
            return count;
        }
    }

    /// <summary>
    /// Represents a unique pointer target for deduplication
    /// </summary>
    public struct PointerKey : IEquatable<PointerKey>
    {
        public uint Index { get; }
        public DialogNodeType Type { get; }
        public bool IsLink { get; }

        public PointerKey(uint index, DialogNodeType type, bool isLink)
        {
            Index = index;
            Type = type;
            IsLink = isLink;
        }

        public bool Equals(PointerKey other)
        {
            return Index == other.Index && Type == other.Type && IsLink == other.IsLink;
        }

        public override bool Equals(object? obj)
        {
            return obj is PointerKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Index, Type, IsLink);
        }

        public override string ToString()
        {
            return $"{Type}[{Index}]{(IsLink ? " (Link)" : "")}";
        }
    }

    /// <summary>
    /// Represents a deduplicated pointer struct
    /// </summary>
    public class UniquePointer
    {
        public uint Index { get; set; }
        public DialogNodeType Type { get; set; }
        public bool IsLink { get; set; }
        public int StructIndex { get; set; }
        public int LogicalPointerCount { get; set; }
    }

    /// <summary>
    /// Result of compact pointer processing
    /// </summary>
    public class CompactPointerResult
    {
        public List<UniquePointer> UniquePointers { get; set; } = new();
        public List<List<int>> EntryStructMappings { get; set; } = new();
        public List<List<int>> ReplyStructMappings { get; set; } = new();
        public int OriginalCount { get; set; }
        public int DeduplicatedCount { get; set; }
        public double CompressionRatio { get; set; }
    }
}