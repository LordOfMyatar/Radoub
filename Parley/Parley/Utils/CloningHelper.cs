using System.Collections.Generic;
using DialogEditor.Models;

namespace DialogEditor.Utils
{
    /// <summary>
    /// Utility class for cloning dialog model objects.
    /// Centralizes cloning logic to avoid duplication across services.
    /// </summary>
    public static class CloningHelper
    {
        /// <summary>
        /// Creates a deep copy of a LocString object.
        /// </summary>
        /// <param name="original">The LocString to clone, or null</param>
        /// <returns>A new LocString with copied values, or empty LocString if null</returns>
        public static LocString CloneLocString(LocString? original)
        {
            if (original == null)
                return new LocString();

            var clone = new LocString();
            foreach (var kvp in original.Strings)
            {
                clone.Strings[kvp.Key] = kvp.Value;
            }
            return clone;
        }

        /// <summary>
        /// Creates a shallow clone of a DialogNode (without Pointers).
        /// Pointers must be cloned separately to handle circular references properly.
        /// </summary>
        /// <param name="original">The node to clone</param>
        /// <returns>A new DialogNode with copied properties but empty Pointers list</returns>
        public static DialogNode CreateShallowNodeClone(DialogNode original)
        {
            return new DialogNode
            {
                Type = original.Type,
                Text = CloneLocString(original.Text),
                Speaker = original.Speaker ?? string.Empty,
                Comment = original.Comment ?? string.Empty,
                Sound = original.Sound ?? string.Empty,
                ScriptAction = original.ScriptAction ?? string.Empty,
                Animation = original.Animation,
                AnimationLoop = original.AnimationLoop,
                Delay = original.Delay,
                Quest = original.Quest ?? string.Empty,
                QuestEntry = original.QuestEntry,
                ActionParams = new Dictionary<string, string>(original.ActionParams ?? new Dictionary<string, string>()),
                Pointers = new List<DialogPtr>() // Empty - must be populated by caller
            };
        }
    }
}
