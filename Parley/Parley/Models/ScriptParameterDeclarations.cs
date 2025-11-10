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
        /// List of allowed parameter values from ----ValueList---- section
        /// </summary>
        public List<string> Values { get; set; } = new List<string>();

        /// <summary>
        /// Indicates whether any declarations were found
        /// </summary>
        public bool HasDeclarations => Keys.Count > 0 || Values.Count > 0;

        /// <summary>
        /// Creates an empty parameter declarations object
        /// </summary>
        public static ScriptParameterDeclarations Empty => new ScriptParameterDeclarations();
    }
}
