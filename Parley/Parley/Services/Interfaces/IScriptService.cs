using System.Collections.Generic;
using System.Threading.Tasks;
using DialogEditor.Models;

namespace DialogEditor.Services
{
    /// <summary>
    /// Interface for script file discovery, content loading, and parameter extraction.
    /// #1230: Phase 3 - Service interface extraction for dependency injection.
    /// </summary>
    public interface IScriptService
    {
        /// <summary>
        /// Gets the file path of a script by name.
        /// </summary>
        /// <param name="scriptName">Name of the script (without .nss extension).</param>
        /// <returns>Full file path or null if not found.</returns>
        string? GetScriptFilePath(string scriptName);

        /// <summary>
        /// Gets the content of a script by name.
        /// Cache is validated against file modification time.
        /// </summary>
        /// <param name="scriptName">Name of the script (without .nss extension).</param>
        /// <returns>Script content or null if not found.</returns>
        Task<string?> GetScriptContentAsync(string scriptName);

        /// <summary>
        /// Gets all available script search paths.
        /// </summary>
        List<string> GetScriptSearchPaths();

        /// <summary>
        /// Gets all available scripts in the configured directories.
        /// </summary>
        Task<List<string>> GetAvailableScriptsAsync();

        /// <summary>
        /// Gets parameter declarations from a script's comment blocks.
        /// </summary>
        /// <param name="scriptName">Name of the script (without .nss extension).</param>
        /// <returns>Parameter declarations or empty if not found.</returns>
        Task<ScriptParameterDeclarations> GetParameterDeclarationsAsync(string scriptName);

        /// <summary>
        /// Clears the script and parameter caches.
        /// </summary>
        void ClearCache();

        /// <summary>
        /// Gets cache statistics for debugging.
        /// </summary>
        (int ScriptCount, int ParameterCount) GetCacheStats();

        /// <summary>
        /// Generates a realistic script preview based on the script name and parameters.
        /// </summary>
        Task<string> GenerateScriptPreviewAsync(string scriptName, Dictionary<string, string> parameters, bool isConditional = true);
    }
}
