using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Radoub.Formats.Logging;

namespace DialogEditor.Services
{
    /// <summary>
    /// Manages recent files list for Parley.
    /// Extracted from SettingsService for single responsibility (#719).
    /// </summary>
    public class RecentFilesService : INotifyPropertyChanged
    {
        public static RecentFilesService Instance { get; } = new RecentFilesService();

        private const int DefaultMaxRecentFiles = 10;

        private List<string> _recentFiles = new List<string>();
        private int _maxRecentFiles = DefaultMaxRecentFiles;

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Called when settings need to be saved. SettingsService subscribes to this.
        /// </summary>
        public event Action? SettingsChanged;

        private RecentFilesService()
        {
            // Initialized by SettingsService.LoadSettings()
        }

        public List<string> RecentFiles
        {
            get => _recentFiles.ToList(); // Return a copy to prevent external modification
        }

        public int MaxRecentFiles
        {
            get => _maxRecentFiles;
            set
            {
                if (SetProperty(ref _maxRecentFiles, Math.Max(1, Math.Min(20, value))))
                {
                    TrimRecentFiles();
                    SettingsChanged?.Invoke();
                }
            }
        }

        /// <summary>
        /// Initializes the service with loaded settings data.
        /// Called by SettingsService during LoadSettings().
        /// </summary>
        public void Initialize(List<string> recentFiles, int maxRecentFiles)
        {
            _recentFiles = recentFiles ?? new List<string>();
            _maxRecentFiles = Math.Max(1, Math.Min(20, maxRecentFiles));
        }

        public void AddRecentFile(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
                return;

            // Remove if already exists (to move to top)
            _recentFiles.Remove(filePath);

            // Add to beginning
            _recentFiles.Insert(0, filePath);

            // Trim to max allowed
            TrimRecentFiles();

            OnPropertyChanged(nameof(RecentFiles));
            SettingsChanged?.Invoke();

            UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Added recent file: {Path.GetFileName(filePath)}");
        }

        public void RemoveRecentFile(string filePath)
        {
            if (_recentFiles.Remove(filePath))
            {
                OnPropertyChanged(nameof(RecentFiles));
                SettingsChanged?.Invoke();
                UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Removed recent file: {Path.GetFileName(filePath)}");
            }
        }

        public void ClearRecentFiles()
        {
            if (_recentFiles.Count > 0)
            {
                _recentFiles.Clear();
                OnPropertyChanged(nameof(RecentFiles));
                SettingsChanged?.Invoke();
                UnifiedLogger.LogApplication(LogLevel.INFO, "Cleared all recent files");
            }
        }

        public void CleanupRecentFiles()
        {
            var originalCount = _recentFiles.Count;
            _recentFiles.RemoveAll(file => !File.Exists(file));

            if (_recentFiles.Count != originalCount)
            {
                OnPropertyChanged(nameof(RecentFiles));
                SettingsChanged?.Invoke();
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Cleaned up {originalCount - _recentFiles.Count} non-existent recent files");
            }
        }

        private void TrimRecentFiles()
        {
            while (_recentFiles.Count > MaxRecentFiles)
            {
                _recentFiles.RemoveAt(_recentFiles.Count - 1);
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}
