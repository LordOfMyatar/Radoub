using System.Collections.Generic;

namespace DialogEditor.Models
{
    /// <summary>
    /// Represents parameter declarations extracted from NWScript (.nss) comment blocks.
    /// These declarations provide hints for autocomplete and validation.
    /// </summary>
    public class ScriptParameterDeclarations
    {
        /// <summary>
        /// List of allowed parameter keys from ----KeyList---- section
        /// </summary>
        public List<string> Keys { get; set; } = new List<string>();

        /// <summary>
        /// Dictionary mapping parameter keys to their allowed values
        /// Key: Parameter name (e.g., "BASE_ITEM")
        /// Value: List of allowed values for that parameter (e.g., ["BASE_ITEM_SHORTSWORD", ...])
        /// Populated from ----ValueList-KEYNAME---- sections
        /// </summary>
        public Dictionary<string, List<string>> ValuesByKey { get; set; } = new Dictionary<string, List<string>>();

        /// <summary>
        /// Legacy: List of allowed parameter values from ----ValueList---- section (without key suffix)
        /// Maintained for backward compatibility with scripts using old format
        /// </summary>
        public List<string> Values { get; set; } = new List<string>();

        /// <summary>
        /// Tracks dependencies between parameters for dynamic value resolution.
        /// Key: Parameter name that has a dependency (e.g., "iNPCState")
        /// Value: Name of the parameter it depends on (e.g., "sQuest")
        /// Used for FROM_JOURNAL_ENTRIES(paramKey) syntax
        /// </summary>
        public Dictionary<string, string> Dependencies { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Indicates whether any declarations were found
        /// </summary>
        public bool HasDeclarations => Keys.Count > 0 || Values.Count > 0 || ValuesByKey.Count > 0;

        /// <summary>
        /// Gets values for a specific parameter key, or empty list if not found
        /// </summary>
        public List<string> GetValuesForKey(string key)
        {
            return ValuesByKey.ContainsKey(key) ? ValuesByKey[key] : new List<string>();
        }

        /// <summary>
        /// Creates an empty parameter declarations object
        /// </summary>
        public static ScriptParameterDeclarations Empty => new ScriptParameterDeclarations();
    }
}
