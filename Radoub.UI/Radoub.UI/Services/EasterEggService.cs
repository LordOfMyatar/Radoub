using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Radoub.UI.Services
{
    /// <summary>
    /// Tracks easter egg unlock progress across Radoub tools.
    /// The Sea-Sick theme unlocks when all three tools have been launched at least once.
    /// </summary>
    public class EasterEggService
    {
        private static EasterEggService? _instance;
        private static readonly object _lock = new();

        private readonly string _stateFilePath;
        private EasterEggState _state;

        public static EasterEggService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new EasterEggService();
                    }
                }
                return _instance;
            }
        }

        private EasterEggService()
        {
            var radoubFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Radoub");

            Directory.CreateDirectory(radoubFolder);
            _stateFilePath = Path.Combine(radoubFolder, ".easter-eggs.json");
            _state = LoadState();
        }

        /// <summary>
        /// Records that a tool has been launched. Call this from each tool's App.axaml.cs.
        /// </summary>
        public void RecordToolLaunch(string toolName)
        {
            var normalized = toolName.ToLowerInvariant();

            if (!_state.ToolsLaunched.Contains(normalized))
            {
                _state.ToolsLaunched.Add(normalized);
                SaveState();
            }
        }

        /// <summary>
        /// Returns true if the Sea-Sick easter egg theme should be available.
        /// Requires Parley, Manifest, and Quartermaster to have been launched at least once each.
        /// </summary>
        public bool IsSeaSickUnlocked()
        {
            return _state.ToolsLaunched.Contains("parley") &&
                   _state.ToolsLaunched.Contains("manifest") &&
                   _state.ToolsLaunched.Contains("quartermaster");
        }

        /// <summary>
        /// Gets a list of tools that still need to be launched to unlock Sea-Sick.
        /// </summary>
        public List<string> GetMissingTools()
        {
            var missing = new List<string>();

            if (!_state.ToolsLaunched.Contains("parley"))
                missing.Add("Parley");
            if (!_state.ToolsLaunched.Contains("manifest"))
                missing.Add("Manifest");
            if (!_state.ToolsLaunched.Contains("quartermaster"))
                missing.Add("Quartermaster");

            return missing;
        }

        private EasterEggState LoadState()
        {
            try
            {
                if (File.Exists(_stateFilePath))
                {
                    var json = File.ReadAllText(_stateFilePath);
                    return JsonSerializer.Deserialize<EasterEggState>(json) ?? new EasterEggState();
                }
            }
            catch
            {
                // Silently fail - easter eggs shouldn't crash the app
            }

            return new EasterEggState();
        }

        private void SaveState()
        {
            try
            {
                var json = JsonSerializer.Serialize(_state, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_stateFilePath, json);
            }
            catch
            {
                // Silently fail - easter eggs shouldn't crash the app
            }
        }

        private class EasterEggState
        {
            public HashSet<string> ToolsLaunched { get; set; } = new();
        }
    }
}
