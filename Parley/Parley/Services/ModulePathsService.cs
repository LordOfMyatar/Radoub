using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Radoub.Formats.Logging;

namespace DialogEditor.Services
{
    /// <summary>
    /// Manages recent module paths MRU list for Parley.
    /// Extracted from SettingsService for single responsibility (#1269).
    /// </summary>
    public class ModulePathsService : INotifyPropertyChanged
    {
        private List<string> _modulePaths = new List<string>();

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Called when settings need to be saved. SettingsService subscribes to this.
        /// </summary>
        public event Action? SettingsChanged;

        /// <summary>
        /// Initializes the service with loaded settings data.
        /// Called by SettingsService during LoadSettings().
        /// </summary>
        public void Initialize(List<string> modulePaths)
        {
            _modulePaths = modulePaths ?? new List<string>();
        }

        public List<string> ModulePaths
        {
            get => _modulePaths.ToList(); // Return a copy to prevent external modification
        }

        /// <summary>
        /// Internal access to the backing list for serialization by SettingsService.
        /// </summary>
        internal List<string> ModulePathsInternal => _modulePaths;

        public void AddModulePath(string path)
        {
            if (!string.IsNullOrWhiteSpace(path) && !_modulePaths.Contains(path))
            {
                _modulePaths.Add(path);
                OnPropertyChanged(nameof(ModulePaths));
                SettingsChanged?.Invoke();
            }
        }

        public void RemoveModulePath(string path)
        {
            if (_modulePaths.Remove(path))
            {
                OnPropertyChanged(nameof(ModulePaths));
                SettingsChanged?.Invoke();
            }
        }

        public void ClearModulePaths()
        {
            if (_modulePaths.Count > 0)
            {
                _modulePaths.Clear();
                OnPropertyChanged(nameof(ModulePaths));
                SettingsChanged?.Invoke();
                UnifiedLogger.LogApplication(LogLevel.INFO, "Cleared all recent module paths");
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
