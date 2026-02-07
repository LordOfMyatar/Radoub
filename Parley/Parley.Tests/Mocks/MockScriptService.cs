using DialogEditor.Models;
using DialogEditor.Services;

namespace Parley.Tests.Mocks
{
    /// <summary>
    /// Mock IScriptService for unit testing.
    /// Allows setup of script data without filesystem access.
    /// </summary>
    public class MockScriptService : IScriptService
    {
        private readonly Dictionary<string, string> _scriptPaths = new();
        private readonly Dictionary<string, string> _scriptContents = new();
        private readonly Dictionary<string, ScriptParameterDeclarations> _parameterDeclarations = new();
        private readonly List<string> _searchPaths = new();
        private readonly List<string> _availableScripts = new();

        public string? GetScriptFilePath(string scriptName)
        {
            return _scriptPaths.TryGetValue(scriptName, out var path) ? path : null;
        }

        public Task<string?> GetScriptContentAsync(string scriptName)
        {
            var content = _scriptContents.TryGetValue(scriptName, out var c) ? c : null;
            return Task.FromResult(content);
        }

        public List<string> GetScriptSearchPaths()
        {
            return new List<string>(_searchPaths);
        }

        public Task<List<string>> GetAvailableScriptsAsync()
        {
            return Task.FromResult(new List<string>(_availableScripts));
        }

        public Task<ScriptParameterDeclarations> GetParameterDeclarationsAsync(string scriptName)
        {
            var decl = _parameterDeclarations.TryGetValue(scriptName, out var d)
                ? d
                : ScriptParameterDeclarations.Empty;
            return Task.FromResult(decl);
        }

        public void ClearCache()
        {
            _scriptPaths.Clear();
            _scriptContents.Clear();
            _parameterDeclarations.Clear();
        }

        public (int ScriptCount, int ParameterCount) GetCacheStats()
        {
            return (_scriptContents.Count, _parameterDeclarations.Count);
        }

        public Task<string> GenerateScriptPreviewAsync(string scriptName, Dictionary<string, string> parameters, bool isConditional = true)
        {
            var paramStr = string.Join(", ", parameters.Select(p => $"{p.Key}={p.Value}"));
            var preview = isConditional
                ? $"// Conditional: {scriptName}({paramStr})"
                : $"// Action: {scriptName}({paramStr})";
            return Task.FromResult(preview);
        }

        /// <summary>
        /// Register a script with path and content for testing.
        /// </summary>
        public void AddScript(string scriptName, string filePath, string content)
        {
            _scriptPaths[scriptName] = filePath;
            _scriptContents[scriptName] = content;
            if (!_availableScripts.Contains(scriptName))
                _availableScripts.Add(scriptName);
        }

        /// <summary>
        /// Register parameter declarations for a script.
        /// </summary>
        public void AddParameterDeclarations(string scriptName, ScriptParameterDeclarations declarations)
        {
            _parameterDeclarations[scriptName] = declarations;
        }

        /// <summary>
        /// Add a search path.
        /// </summary>
        public void AddSearchPath(string path)
        {
            _searchPaths.Add(path);
        }
    }
}
