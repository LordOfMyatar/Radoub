using Avalonia.Threading;
using DialogEditor.Models;
using DialogEditor.Services;
using Parley.Models;

namespace DialogEditor.ViewModels
{
    /// <summary>
    /// MainViewModel partial - Scrap Operations (Restore, Clear, Update)
    /// </summary>
    public partial class MainViewModel
    {
        /// <summary>
        /// Restore a node from the scrap back to the dialog.
        /// Automatically restores entire batch if entry is a batch root with children.
        /// </summary>
        public bool RestoreFromScrap(string entryId, TreeViewSafeNode? selectedParent)
        {
            if (CurrentDialog == null) return false;

            SaveUndoState("Restore from Scrap");

            // Check if this is a batch root with children - if so, restore entire batch
            var entry = _scrapManager.GetEntryById(entryId);
            RestoreResult result;

            if (entry != null && entry.IsBatchRoot && entry.ChildCount > 0)
            {
                // Restore entire batch (node + all children/orphans)
                result = _scrapManager.RestoreBatchFromScrap(entryId, CurrentDialog, selectedParent, _indexManager);
            }
            else
            {
                // Single node restore
                result = _scrapManager.RestoreFromScrap(entryId, CurrentDialog, selectedParent, _indexManager);
            }

            StatusMessage = result.StatusMessage;

            if (result.Success)
            {
                RefreshTreeViewAndMarkDirty();
            }

            return result.Success;
        }

        /// <summary>
        /// Clear all scrap entries
        /// </summary>
        public void ClearAllScrap()
        {
            _scrapManager.ClearAllScrap();
            OnPropertyChanged(nameof(ScrapCount));
            OnPropertyChanged(nameof(ScrapTabHeader));
            UpdateScrapBadgeVisibility();
        }

        /// <summary>
        /// Update scrap entries when file changes
        /// </summary>
        private void UpdateScrapForCurrentFile()
        {
            if (CurrentFileName == null) return;

            var entries = _scrapManager.GetScrapForFile(CurrentFileName);

            // Update the collection on UI thread
            Dispatcher.UIThread.Post(() =>
            {
                ScrapEntries.Clear();
                foreach (var entry in entries)
                {
                    ScrapEntries.Add(entry);
                }
                OnPropertyChanged(nameof(ScrapCount));
                OnPropertyChanged(nameof(ScrapTabHeader));
                UpdateScrapBadgeVisibility();
            });
        }

        /// <summary>
        /// Update the visibility of the scrap badge in the UI
        /// </summary>
        private void UpdateScrapBadgeVisibility()
        {
            // This will be handled by binding in the UI based on ScrapCount > 0
            // The badge visibility is controlled by the IsVisible binding in XAML
        }
    }
}
