using System.Collections.Generic;
using System.Linq;

namespace DialogEditor.Models
{
    /// <summary>
    /// Represents creature info extracted from UTC (creature) files.
    /// Supports NWN 1.69+ format (20+ years post-release, extended class support).
    /// Focus: Tag extraction for Speaker/Listener fields + basic display info.
    /// </summary>
    public class CreatureInfo
    {
        public string Tag { get; set; } = string.Empty;
        public LocString? FirstName { get; set; }
        public LocString? LastName { get; set; }
        public LocString? Description { get; set; }
        public ushort PortraitId { get; set; } = 0;

        /// <summary>
        /// ClassList from UTC file.
        /// Original spec says "up to 3" but NWN 1.69+ may support more.
        /// Real-world validation required.
        /// </summary>
        public List<CreatureClass> Classes { get; set; } = new();

        /// <summary>
        /// Display string for UI: "FirstName LastName" or just Tag if no name.
        /// Null-safe for creatures without localized names.
        /// </summary>
        public string DisplayName
        {
            get
            {
                var first = FirstName?.GetDefault() ?? "";
                var last = LastName?.GetDefault() ?? "";
                var name = $"{first} {last}".Trim();
                return string.IsNullOrEmpty(name) ? Tag : name;
            }
        }

        /// <summary>
        /// Class summary for UI tooltips.
        /// Format: "Fighter Lv5 / Wizard Lv3" or "Class0 Lv5 / Class1 Lv3" if names unavailable.
        /// Note: NWN 1.69+ supports more than original 3-class limit.
        /// </summary>
        public string ClassSummary
        {
            get
            {
                if (Classes.Count == 0) return "";
                return string.Join(" / ", Classes.Select(c => c.DisplayText));
            }
        }
    }

    /// <summary>
    /// Represents a single class entry in creature's ClassList.
    /// Corresponds to StructID 2 in UTC files.
    /// </summary>
    public class CreatureClass
    {
        /// <summary>
        /// Index into classes.2da
        /// </summary>
        public int ClassId { get; set; }

        /// <summary>
        /// Level in this class (SHORT in GFF)
        /// </summary>
        public short Level { get; set; }

        /// <summary>
        /// Class name from classes.2da (if available).
        /// Null if classes.2da not loaded or class ID not found.
        /// </summary>
        public string? ClassName { get; set; }

        /// <summary>
        /// Display string: "ClassName Lv5" or "Class{id} Lv5" if name unavailable.
        /// </summary>
        public string DisplayText
        {
            get
            {
                var classDisplay = !string.IsNullOrEmpty(ClassName) ? ClassName : $"Class{ClassId}";
                return $"{classDisplay} Lv{Level}";
            }
        }
    }
}
