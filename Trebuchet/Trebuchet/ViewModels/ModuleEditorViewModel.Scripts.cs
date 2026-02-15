using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using Radoub.Formats.Logging;
using Radoub.Formats.Settings;
using Radoub.UI.Views;
using RadoubLauncher.Services;

namespace RadoubLauncher.ViewModels;

// Script browsing, editing, creation, and TLK browsing
public partial class ModuleEditorViewModel
{
    /// <summary>
    /// Browse for a custom TLK file.
    /// </summary>
    [RelayCommand]
    private async Task BrowseCustomTlkAsync()
    {
        if (_parentWindow == null) return;

        var storage = _parentWindow.StorageProvider;

        // Build list of suggested starting locations
        var suggestedLocations = new List<IStorageFolder>();

        // Prefer NWN documents tlk folder
        var nwnPath = RadoubSettings.Instance.NeverwinterNightsPath;
        if (!string.IsNullOrEmpty(nwnPath))
        {
            var tlkFolder = Path.Combine(nwnPath, "tlk");
            if (Directory.Exists(tlkFolder))
            {
                var folder = await storage.TryGetFolderFromPathAsync(tlkFolder);
                if (folder != null)
                    suggestedLocations.Add(folder);
            }
        }

        // Also try module directory
        if (!string.IsNullOrEmpty(_modulePath))
        {
            var moduleDir = Directory.Exists(_modulePath) ? _modulePath : Path.GetDirectoryName(_modulePath);
            if (!string.IsNullOrEmpty(moduleDir) && Directory.Exists(moduleDir))
            {
                var folder = await storage.TryGetFolderFromPathAsync(moduleDir);
                if (folder != null)
                    suggestedLocations.Add(folder);
            }
        }

        var options = new FilePickerOpenOptions
        {
            Title = "Select Custom TLK File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("TLK Files") { Patterns = new[] { "*.tlk" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            },
            SuggestedStartLocation = suggestedLocations.FirstOrDefault()
        };

        var result = await storage.OpenFilePickerAsync(options);

        if (result.Count > 0)
        {
            var selectedFile = result[0];
            var filePath = selectedFile.TryGetLocalPath();
            if (!string.IsNullOrEmpty(filePath))
            {
                // Store just the name without extension (as IFO expects)
                CustomTlk = Path.GetFileNameWithoutExtension(filePath);
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Selected custom TLK: {CustomTlk}");
            }
        }
    }

    /// <summary>
    /// Browse for a script using the shared ScriptBrowserWindow.
    /// </summary>
    /// <param name="scriptFieldName">The name of the script property to update (e.g., "OnModuleLoad")</param>
    [RelayCommand]
    private async Task BrowseScriptAsync(string scriptFieldName)
    {
        if (_parentWindow == null) return;

        // Create context with current module's working directory
        var context = new TrebuchetScriptBrowserContext(_workingDirectoryPath ?? _modulePath, _gameDataService);
        var browser = new ScriptBrowserWindow(context);

        var result = await browser.ShowDialog<string?>(_parentWindow);

        if (!string.IsNullOrEmpty(result))
        {
            // Use reflection to set the script property
            SetScriptProperty(scriptFieldName, result);
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Selected script for {scriptFieldName}: {result}");
        }
    }

    /// <summary>
    /// Open a script file in the system's default editor.
    /// If the file doesn't exist, offers to create it.
    /// </summary>
    /// <param name="scriptFieldName">The name of the script property to edit</param>
    [RelayCommand]
    private async Task EditScriptAsync(string scriptFieldName)
    {
        var scriptName = GetScriptProperty(scriptFieldName);
        if (string.IsNullOrEmpty(scriptName))
        {
            StatusText = "No script to edit - select or enter a script name first";
            return;
        }

        // Find the script file in the module directory
        var searchDir = _workingDirectoryPath ?? _modulePath;
        if (string.IsNullOrEmpty(searchDir) || !Directory.Exists(searchDir))
        {
            StatusText = "Module directory not available";
            return;
        }

        var scriptPath = Path.Combine(searchDir, $"{scriptName}.nss");
        if (!File.Exists(scriptPath))
        {
            // Offer to create the file
            if (_parentWindow != null && await ConfirmCreateScriptAsync(scriptName))
            {
                try
                {
                    // Create empty script file with header comment
                    var scriptContent = $$"""
                        // {{scriptName}}.nss
                        // Created by Trebuchet Module Editor

                        void main()
                        {

                        }
                        """;
                    await File.WriteAllTextAsync(scriptPath, scriptContent);
                    UnifiedLogger.LogApplication(LogLevel.INFO, $"Created new script: {UnifiedLogger.SanitizePath(scriptPath)}");
                    StatusText = $"Created {scriptName}.nss";
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to create script: {ex.Message}");
                    StatusText = $"Failed to create script: {ex.Message}";
                    return;
                }
            }
            else
            {
                return; // User cancelled or no parent window
            }
        }

        try
        {
            // Open with system default editor
            var psi = new ProcessStartInfo
            {
                FileName = scriptPath,
                UseShellExecute = true
            };
            Process.Start(psi)?.Dispose();
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Opened script in editor: {UnifiedLogger.SanitizePath(scriptPath)}");
            StatusText = $"Opened {scriptName}.nss in editor";
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to open script: {ex.Message}");
            StatusText = $"Failed to open script: {ex.Message}";
        }
    }

    /// <summary>
    /// Show confirmation dialog to create a new script file.
    /// </summary>
    private async Task<bool> ConfirmCreateScriptAsync(string scriptName)
    {
        if (_parentWindow == null) return false;

        var tcs = new TaskCompletionSource<bool>();

        var messageText = new Avalonia.Controls.TextBlock
        {
            Text = $"Script file '{scriptName}.nss' does not exist.\n\nDo you want to create it?",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        };

        var createButton = new Avalonia.Controls.Button
        {
            Content = "Create",
            Margin = new Avalonia.Thickness(8, 0, 0, 0),
            Padding = new Avalonia.Thickness(16, 6)
        };

        var cancelButton = new Avalonia.Controls.Button
        {
            Content = "Cancel",
            Padding = new Avalonia.Thickness(16, 6)
        };

        var buttonPanel = new Avalonia.Controls.StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 16, 0, 0),
            Children = { cancelButton, createButton }
        };

        var contentPanel = new Avalonia.Controls.StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Children = { messageText, buttonPanel }
        };

        var dialog = new Avalonia.Controls.Window
        {
            Title = "Create Script?",
            Width = 350,
            SizeToContent = Avalonia.Controls.SizeToContent.Height,
            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = contentPanel
        };

        createButton.Click += (_, _) => { tcs.TrySetResult(true); dialog.Close(); };
        cancelButton.Click += (_, _) => { tcs.TrySetResult(false); dialog.Close(); };
        dialog.Closed += (_, _) => tcs.TrySetResult(false); // Handle window close button

        dialog.Show(_parentWindow);
        return await tcs.Task;
    }

    /// <summary>
    /// Allowed script property names for reflection-based access.
    /// Prevents arbitrary property modification via SetScriptProperty/GetScriptProperty.
    /// </summary>
    private static readonly HashSet<string> AllowedScriptProperties = new(StringComparer.Ordinal)
    {
        nameof(OnModuleLoad), nameof(OnClientEnter), nameof(OnClientLeave),
        nameof(OnHeartbeat), nameof(OnAcquireItem), nameof(OnActivateItem),
        nameof(OnUnacquireItem), nameof(OnPlayerDeath), nameof(OnPlayerDying),
        nameof(OnPlayerRest), nameof(OnPlayerEquipItem), nameof(OnPlayerUnequipItem),
        nameof(OnPlayerLevelUp), nameof(OnUserDefined), nameof(OnSpawnButtonDown),
        nameof(OnCutsceneAbort), nameof(OnModuleStart), nameof(OnPlayerChat),
        nameof(OnPlayerTarget), nameof(OnPlayerGuiEvent), nameof(OnPlayerTileAction),
        nameof(OnNuiEvent)
    };

    /// <summary>
    /// Set a script property by name using reflection.
    /// Only allows properties in the AllowedScriptProperties whitelist.
    /// </summary>
    private void SetScriptProperty(string propertyName, string value)
    {
        if (!AllowedScriptProperties.Contains(propertyName))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Rejected attempt to set non-script property: {propertyName}");
            return;
        }

        var property = GetType().GetProperty(propertyName);
        if (property != null && property.CanWrite)
        {
            property.SetValue(this, value);
        }
    }

    /// <summary>
    /// Get a script property value by name using reflection.
    /// Only allows properties in the AllowedScriptProperties whitelist.
    /// </summary>
    private string? GetScriptProperty(string propertyName)
    {
        if (!AllowedScriptProperties.Contains(propertyName))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Rejected attempt to read non-script property: {propertyName}");
            return null;
        }

        var property = GetType().GetProperty(propertyName);
        return property?.GetValue(this) as string;
    }
}
