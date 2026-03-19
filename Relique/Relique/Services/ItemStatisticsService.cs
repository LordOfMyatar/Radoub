using System;
using System.Collections.Generic;
using System.Linq;
using Radoub.Formats.Uti;

namespace ItemEditor.Services;

/// <summary>
/// Generates a formatted statistics description from an item's properties list.
/// Used to auto-populate the item's description field with property summaries.
/// </summary>
public class ItemStatisticsService
{
    private readonly ItemPropertyService _propertyService;

    public ItemStatisticsService(ItemPropertyService propertyService)
    {
        _propertyService = propertyService ?? throw new ArgumentNullException(nameof(propertyService));
    }

    /// <summary>
    /// Generate a formatted statistics string from an item's properties.
    /// Each property is listed on its own line with resolved display names.
    /// </summary>
    public string GenerateStatistics(List<ItemProperty> properties)
    {
        if (properties == null || properties.Count == 0)
            return string.Empty;

        var lines = new List<string>();
        var types = _propertyService.GetAvailablePropertyTypes();

        foreach (var prop in properties)
        {
            lines.Add(ResolvePropertyLine(prop, types));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private string ResolvePropertyLine(ItemProperty prop, List<PropertyTypeInfo> types)
    {
        var type = types.FirstOrDefault(t => t.PropertyIndex == prop.PropertyName);
        var name = type?.DisplayName ?? $"Property {prop.PropertyName}";

        var parts = new List<string> { name };

        // Resolve subtype
        if (type?.HasSubtypes == true)
        {
            var subtypes = _propertyService.GetSubtypes(prop.PropertyName);
            var subtype = subtypes.FirstOrDefault(s => s.Index == prop.Subtype);
            if (subtype != null)
                parts.Add(subtype.DisplayName);
        }

        // Resolve cost value
        if (type?.HasCostTable == true)
        {
            var costValues = _propertyService.GetCostValues(prop.PropertyName);
            var cost = costValues.FirstOrDefault(c => c.Index == prop.CostValue);
            if (cost != null)
                parts.Add(cost.DisplayName);
        }

        // Resolve param value
        if (type?.HasParamTable == true && prop.Param1 != 0xFF)
        {
            var paramValues = _propertyService.GetParamValues(prop.PropertyName);
            var param = paramValues.FirstOrDefault(p => p.Index == prop.Param1Value);
            if (param != null)
                parts.Add(param.DisplayName);
        }

        return string.Join(" ", parts);
    }
}
