using System;
using System.Threading.Tasks;
using Avalonia.Threading;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Logging;

namespace DialogEditor.ViewModels
{
    /// <summary>
    /// MainViewModel partial - File Operations (New, Open, Save, Close, Reload)
    /// </summary>
    public partial class MainViewModel
    {
        public async Task LoadDialogAsync(string filePath)
        {
            try
            {
                IsLoading = true;
                StatusMessage = $"Loading {System.IO.Path.GetFileName(filePath)}...";

                UnifiedLogger.LogApplication(LogLevel.INFO, $"Loading dialog from: {UnifiedLogger.SanitizePath(filePath)}");

                // Ensure GameResourceService is initialized before parsing (for TLK StrRef resolution)
                _ = GameResourceService.Instance.IsAvailable;

                // Phase 4 Refactoring: Use DialogFileService facade instead of DialogParser directly
                var dialogService = new DialogFileService();
                CurrentDialog = await dialogService.LoadFromFileAsync(filePath);

                if (CurrentDialog != null)
                {
                    // Rebuild LinkRegistry for the loaded dialog
                    CurrentDialog.RebuildLinkRegistry();

                    // Reset global tracking for link detection when loading new dialog
                    TreeViewSafeNode.ResetGlobalTracking();

                    // Clear undo history when loading new file
                    _undoRedoService.Clear();

                    // Clear tree selection when loading new file
                    SelectedTreeNode = null;

                    CurrentFileName = filePath;
                    HasUnsavedChanges = false; // Clear dirty flag when loading
                    LastSavedTime = ""; // Clear last saved time on load
                    StatusMessage = $"Dialog loaded successfully: {CurrentDialog.Entries.Count} entries, {CurrentDialog.Replies.Count} replies";

                    // Add to recent files
                    SettingsService.Instance.AddRecentFile(filePath);

                    // Populate the dialog nodes for the tree view (must run on UI thread)
                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        PopulateDialogNodes();
                    });

                    // Update scrap entries for the newly loaded file
                    UpdateScrapForCurrentFile();

                    // Validate the loaded dialog
                    var validation = dialogService.ValidateStructure(CurrentDialog);
                    if (validation.Warnings.Count > 0)
                    {
                        foreach (var warning in validation.Warnings)
                        {
                            UnifiedLogger.LogApplication(LogLevel.WARN, $"Dialog validation: {warning}");
                        }
                    }
                }
                else
                {
                    StatusMessage = "Failed to load dialog file";
                    DialogNodes.Clear();
                }

                UnifiedLogger.LogApplication(LogLevel.INFO, "Dialog loading completed");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error loading dialog: {ex.Message}";
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to load dialog: {ex.Message}");
                CurrentDialog = null;
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Saves dialog to file. Returns true if successful, false otherwise.
        /// Issue #8: Now returns bool so callers can check result.
        /// </summary>
        public async Task<bool> SaveDialogAsync(string filePath)
        {
            if (CurrentDialog == null)
            {
                StatusMessage = "No dialog to save";
                return false;
            }

            try
            {
                IsLoading = true;
                StatusMessage = $"Saving {System.IO.Path.GetFileName(filePath)}...";

                // Use DialogSaveService for all save logic
                var result = await _saveService.SaveDialogAsync(CurrentDialog, filePath);

                if (result.Success)
                {
                    CurrentFileName = filePath;
                    HasUnsavedChanges = false; // Clear dirty flag on successful save

                    // Update last saved time (Issue #62)
                    LastSavedTime = $"Last saved: {DateTime.Now:h:mm:ss tt}";

                    StatusMessage = result.StatusMessage;
                    return true;
                }
                else
                {
                    StatusMessage = result.StatusMessage;
                    return false;
                }
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error saving dialog: {ex.Message}";
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to save dialog in MainViewModel: {ex.Message}");
                return false;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void NewDialog()
        {
            try
            {
                UnifiedLogger.LogApplication(LogLevel.INFO, "Creating new blank dialog");

                // Create blank dialog with root structure
                CurrentDialog = new Models.Dialog();
                CurrentFileName = null; // No filename until user saves (this will also clear scrap via setter)
                HasUnsavedChanges = false; // Start clean
                LastSavedTime = ""; // Clear last saved time
                SelectedTreeNode = null; // Clear selection
                SelectedScrapEntry = null; // Clear scrap selection

                // Populate empty tree with just ROOT node
                Dispatcher.UIThread.Post(() =>
                {
                    PopulateDialogNodes();
                });

                StatusMessage = "New blank dialog created";
                UnifiedLogger.LogApplication(LogLevel.INFO, "New blank dialog created successfully");
            }
            catch (Exception ex)
            {
                StatusMessage = $"Error creating new dialog: {ex.Message}";
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to create new dialog: {ex.Message}");
                CurrentDialog = null;
            }
        }

        public void CloseDialog()
        {
            CurrentDialog = null;
            CurrentFileName = null;
            HasUnsavedChanges = false;
            LastSavedTime = ""; // Clear last saved time
            DialogNodes.Clear();
            SelectedScrapEntry = null; // Clear scrap selection
            StatusMessage = "Dialog closed";
            UnifiedLogger.LogApplication(LogLevel.INFO, "Dialog file closed");
        }

        /// <summary>
        /// Reloads the current dialog file, useful when TLK settings change.
        /// </summary>
        public async Task ReloadCurrentDialogAsync()
        {
            if (string.IsNullOrEmpty(CurrentFileName)) return;

            var filePath = CurrentFileName;

            // Invalidate the resolver to force TLK reload with new settings
            GameResourceService.Instance.InvalidateResolver();

            await LoadDialogAsync(filePath);
        }
    }
}
