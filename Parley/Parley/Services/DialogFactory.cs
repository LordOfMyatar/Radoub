using System;
using Avalonia.Controls;
using Radoub.UI.Services;
using System.Threading.Tasks;

namespace DialogEditor.Services
{
    /// <summary>
    /// Factory for creating common dialog windows.
    /// Delegates to shared DialogHelper from Radoub.UI with Parley-specific behavior.
    /// </summary>
    public class DialogFactory
    {
        private readonly Window _owner;
        private readonly ISettingsService _settings;

        public DialogFactory(Window owner, ISettingsService settings)
        {
            _owner = owner;
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Shows a Yes/No confirmation dialog.
        /// </summary>
        /// <param name="title">Dialog title</param>
        /// <param name="message">Message to display</param>
        /// <param name="showDontAskAgain">If true, shows "Don't show this again" checkbox (Issue #14)</param>
        /// <returns>True if user clicked Yes, false if No</returns>
        public async Task<bool> ShowConfirmDialogAsync(string title, string message, bool showDontAskAgain = false)
        {
            return await DialogHelper.ShowConfirmAsync(
                _owner,
                title,
                message,
                showDontAskAgain,
                onDontAskAgain: () => _settings.ShowDeleteConfirmation = false
            );
        }

        /// <summary>
        /// Issue #8: Shows error dialog with option to Save As when save fails.
        /// </summary>
        /// <param name="errorMessage">Error message to display</param>
        /// <returns>True if user wants to Save As, false to cancel</returns>
        public async Task<bool> ShowSaveErrorDialogAsync(string errorMessage)
        {
            return await DialogHelper.ShowSaveErrorAsync(_owner, errorMessage);
        }
    }
}
