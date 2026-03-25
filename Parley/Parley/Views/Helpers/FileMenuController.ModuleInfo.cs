using System;
using System.IO;
using Avalonia.Controls;
using DialogEditor.Parsers;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.UI.Controls;
using Radoub.UI.Services;

namespace Parley.Views.Helpers
{
    /// <summary>
    /// FileMenuController partial: Module info display and working directory resolution.
    /// Split from FileMenuController.cs (#1540).
    /// </summary>
    public partial class FileMenuController
    {
        #region Module Info Display

        /// <summary>
        /// Update module info bar with current file's module information.
        /// </summary>
        public void UpdateModuleInfo(string dialogFilePath)
        {
            try
            {
                var moduleDirectory = Path.GetDirectoryName(dialogFilePath);
                if (string.IsNullOrEmpty(moduleDirectory))
                {
                    ClearModuleInfo();
                    return;
                }

                // Get module name from module.ifo
                var moduleName = ModuleInfoParser.GetModuleName(moduleDirectory);

                // Sanitize path for display
                var displayPath = PathHelper.SanitizePathForDisplay(moduleDirectory);

                // Update UI - info colors when module is active (#1321: merged into status bar)
                _controls.WithControl<StatusBarControl>("StatusBar", sb =>
                {
                    sb.ModuleIndicator = moduleName ?? Path.GetFileName(moduleDirectory);
                });

                UnifiedLogger.LogApplication(LogLevel.INFO, $"Module info updated: {moduleName ?? "(unnamed)"} | {displayPath}");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to update module info: {ex.Message}");
                ClearModuleInfo();
            }
        }

        /// <summary>
        /// Clear module info display.
        /// </summary>
        public void ClearModuleInfo()
        {
            _controls.WithControl<StatusBarControl>("StatusBar", sb =>
            {
                sb.ModuleIndicator = "No module selected";
            });
        }

        /// <summary>
        /// Initialize module info from RadoubSettings.CurrentModulePath if set.
        /// Called on startup when no dialog file is loaded.
        /// </summary>
        public void InitializeModuleInfoFromSettings()
        {
            try
            {
                var modulePath = Radoub.Formats.Settings.RadoubSettings.Instance.CurrentModulePath;

                // Validate this is a real module path, not just the modules parent directory (#1327)
                if (!Radoub.Formats.Settings.RadoubSettings.IsValidModulePath(modulePath))
                {
                    ClearModuleInfo();
                    return;
                }

                // Resolve to working directory if it's a .mod file
                string? workingDir = null;
                if (File.Exists(modulePath) && modulePath.EndsWith(".mod", StringComparison.OrdinalIgnoreCase))
                {
                    workingDir = FindWorkingDirectory(modulePath);
                }
                else if (Directory.Exists(modulePath))
                {
                    workingDir = modulePath;
                }

                if (string.IsNullOrEmpty(workingDir))
                {
                    ClearModuleInfo();
                    return;
                }

                // Get module name from module.ifo
                var moduleName = ModuleInfoParser.GetModuleName(workingDir);

                // Sanitize path for display
                var displayPath = PathHelper.SanitizePathForDisplay(workingDir);

                // Update UI - info colors when module is active (#1321: merged into status bar)
                _controls.WithControl<TextBlock>("ModuleNameTextBlock", tb =>
                {
                    tb.Text = moduleName ?? Path.GetFileName(workingDir);
                    tb.Foreground = BrushManager.GetInfoBrush(tb);
                });

                UnifiedLogger.LogApplication(LogLevel.INFO, $"Module info from settings: {moduleName ?? "(unnamed)"} | {displayPath}");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to initialize module info from settings: {ex.Message}");
                ClearModuleInfo();
            }
        }

        /// <summary>
        /// Find the unpacked working directory for a .mod file.
        /// Checks for module name folder, temp0, or temp1.
        /// </summary>
        private static string? FindWorkingDirectory(string modFilePath)
        {
            var moduleName = Path.GetFileNameWithoutExtension(modFilePath);
            var moduleDir = Path.GetDirectoryName(modFilePath);

            if (string.IsNullOrEmpty(moduleDir))
                return null;

            // Check in priority order (same as Trebuchet)
            var candidates = new[]
            {
                Path.Combine(moduleDir, moduleName),
                Path.Combine(moduleDir, "temp0"),
                Path.Combine(moduleDir, "temp1")
            };

            foreach (var candidate in candidates)
            {
                if (Directory.Exists(candidate) &&
                    File.Exists(Path.Combine(candidate, "module.ifo")))
                {
                    return candidate;
                }
            }

            return null;
        }

        #endregion
    }
}
