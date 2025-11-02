using System;
using System.Collections.Generic;
using DialogEditor.Models;
using Microsoft.Extensions.Logging;

namespace DialogEditor.Utils
{
    /// <summary>
    /// Robust field index tracking system for complex dialog files.
    /// Prevents field assignment conflicts and misalignment in large structures.
    /// </summary>
    public class FieldIndexTracker
    {
        private uint _currentIndex;
        private readonly List<FieldAllocation> _allocations;
        private readonly ILogger _logger;

        public uint CurrentIndex => _currentIndex;

        public FieldIndexTracker(uint startIndex, ILogger logger)
        {
            _currentIndex = startIndex;
            _allocations = new List<FieldAllocation>();
            _logger = logger;
        }

        /// <summary>
        /// Allocate a range of field indices for a specific purpose
        /// </summary>
        public FieldRange AllocateRange(uint count, string purpose, string details = "")
        {
            var range = new FieldRange(_currentIndex, count);
            var allocation = new FieldAllocation
            {
                Range = range,
                Purpose = purpose,
                Details = details,
                AllocatedAt = DateTime.Now
            };

            _allocations.Add(allocation);
            _currentIndex += count;

            _logger.LogInformation($"🎯 FIELD ALLOCATION: {purpose} - Range[{range.Start}-{range.End}] ({count} fields) - {details}");

            return range;
        }

        /// <summary>
        /// Reserve field indices without incrementing (for structs that will be fixed later)
        /// </summary>
        public FieldRange ReserveRange(uint count, string purpose, string details = "")
        {
            var range = new FieldRange(_currentIndex, count);
            var allocation = new FieldAllocation
            {
                Range = range,
                Purpose = purpose + " (RESERVED)",
                Details = details,
                AllocatedAt = DateTime.Now
            };

            _allocations.Add(allocation);
            // Don't increment _currentIndex for reservations

            _logger.LogInformation($"🔒 FIELD RESERVATION: {purpose} - Range[{range.Start}-{range.End}] ({count} fields) - {details}");

            return range;
        }

        /// <summary>
        /// Validate that a field index falls within expected allocated ranges
        /// </summary>
        public bool ValidateFieldIndex(uint fieldIndex, string context)
        {
            foreach (var allocation in _allocations)
            {
                if (allocation.Range.Contains(fieldIndex))
                {
                    _logger.LogDebug($"✅ FIELD VALIDATION: {context} field[{fieldIndex}] belongs to {allocation.Purpose}");
                    return true;
                }
            }

            _logger.LogWarning($"⚠️ FIELD VALIDATION: {context} field[{fieldIndex}] not in any allocated range!");
            return false;
        }

        /// <summary>
        /// Get allocation audit trail for debugging
        /// </summary>
        public void LogAllocationSummary()
        {
            _logger.LogInformation($"📊 FIELD INDEX ALLOCATION SUMMARY (Total: {_currentIndex} fields):");
            foreach (var allocation in _allocations)
            {
                var rangeStr = $"[{allocation.Range.Start}-{allocation.Range.End}]";
                _logger.LogInformation($"   {rangeStr.PadRight(12)} {allocation.Purpose} - {allocation.Details}");
            }
        }

        /// <summary>
        /// Check for allocation conflicts (overlapping ranges)
        /// </summary>
        public List<string> DetectConflicts()
        {
            var conflicts = new List<string>();

            for (int i = 0; i < _allocations.Count; i++)
            {
                for (int j = i + 1; j < _allocations.Count; j++)
                {
                    var a = _allocations[i];
                    var b = _allocations[j];

                    if (a.Range.Overlaps(b.Range))
                    {
                        conflicts.Add($"CONFLICT: {a.Purpose} [{a.Range.Start}-{a.Range.End}] overlaps {b.Purpose} [{b.Range.Start}-{b.Range.End}]");
                    }
                }
            }

            return conflicts;
        }
    }

    public class FieldRange
    {
        public uint Start { get; }
        public uint Count { get; }
        public uint End => Start + Count - 1;

        public FieldRange(uint start, uint count)
        {
            Start = start;
            Count = count;
        }

        public bool Contains(uint index) => index >= Start && index <= End;
        public bool Overlaps(FieldRange other) => Start <= other.End && End >= other.Start;
    }

    public class FieldAllocation
    {
        public FieldRange Range { get; set; }
        public string Purpose { get; set; }
        public string Details { get; set; }
        public DateTime AllocatedAt { get; set; }
    }
}