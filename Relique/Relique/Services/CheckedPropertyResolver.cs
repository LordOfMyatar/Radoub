using System.Collections.Generic;
using System.Linq;
using Radoub.Formats.Logging;
using Radoub.Formats.Uti;

namespace ItemEditor.Services;

/// <summary>
/// A single ticked entry in the Available Properties tree (#2405). The user ticks the exact
/// subtype they want — there is no silent "first child" default. For a property without subtypes,
/// <see cref="SubtypeIndex"/> is <see cref="NoSubtype"/> so subtype index 0 is never confused with
/// "no subtype".
/// </summary>
public readonly record struct CheckedProperty(int PropertyIndex, int SubtypeIndex)
{
    /// <summary>Sentinel for a checked property that has no subtypes.</summary>
    public const int NoSubtype = -1;

    public bool HasSubtype => SubtypeIndex != NoSubtype;
}

/// <summary>
/// Pure, FlaUI-free mapping from a set of ticked <see cref="CheckedProperty"/> pairs to the list of
/// <see cref="ItemProperty"/> to add (#2405). The View's AddCheckedProperties delegates to this so
/// the bulk-add logic — exact-subtype selection, base-item validation, move-semantics skipping —
/// is unit-testable without driving the UI.
/// </summary>
public static class CheckedPropertyResolver
{
    public sealed class Result
    {
        public List<ItemProperty> ToAdd { get; } = new();

        /// <summary>Display names of checked entries that were skipped (invalid for the base item,
        /// already assigned, or failed to build).</summary>
        public List<string> Skipped { get; } = new();
    }

    /// <summary>
    /// Resolve <paramref name="checkd"/> against the current item state. Each ticked pair becomes
    /// one <see cref="ItemProperty"/> at its exact subtype; entries that are invalid for the base
    /// item, already assigned (move semantics), or fail to build are skipped and named.
    /// </summary>
    public static Result Resolve(
        IEnumerable<CheckedProperty> checkd,
        ItemPropertyService service,
        int baseItem,
        IReadOnlyList<ItemProperty> assignedProperties)
    {
        var result = new Result();
        if (checkd == null || service == null)
            return result;

        var types = service.GetAvailablePropertyTypes();

        foreach (var entry in checkd)
        {
            var type = types.FirstOrDefault(t => t.PropertyIndex == entry.PropertyIndex);
            var displayName = type?.DisplayName ?? $"Property {entry.PropertyIndex}";

            // Defensive: skip combos the validation table marks invalid (#2166).
            if (!service.IsPropertyValidForBaseItem(entry.PropertyIndex, baseItem))
            {
                result.Skipped.Add(displayName);
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"Skipped invalid property {displayName} for base item {baseItem}");
                continue;
            }

            int subtypeIndex = entry.HasSubtype ? entry.SubtypeIndex : 0;

            // Move semantics: skip a (property, subtype) pair already assigned.
            if (!service.IsPropertyAvailable(entry.PropertyIndex, subtypeIndex, assignedProperties))
            {
                result.Skipped.Add(displayName);
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"Refused duplicate {displayName} (subtype {subtypeIndex})");
                continue;
            }

            // Default to the FIRST available cost value, not 0 — cost tables have no row 0, so a
            // hardcoded 0 wrote a blank/invalid value (e.g. Cast Spell with no uses/day). Matches the
            // right-click "default values" path. User picks a specific value via Configure… (#2406).
            int costValueIndex = 0;
            if (type != null && type.HasCostTable)
            {
                var costs = service.GetCostValues(entry.PropertyIndex);
                if (costs.Count > 0)
                    costValueIndex = costs[0].Index;
            }

            try
            {
                var property = service.CreateItemProperty(entry.PropertyIndex, subtypeIndex, costValueIndex, null);
                result.ToAdd.Add(property);
            }
            catch (System.Exception ex)
            {
                result.Skipped.Add(displayName);
                UnifiedLogger.LogApplication(LogLevel.ERROR,
                    $"Failed to add {displayName}: {ex.GetType().Name}: {ex.Message}");
            }
        }

        return result;
    }
}
