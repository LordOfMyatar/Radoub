using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using DialogEditor.Models;
using Radoub.Formats.Common;
using Radoub.Formats.Logging;
using Radoub.UI.Controls;

namespace DialogEditor.Views
{
    /// <summary>
    /// MainWindow partial class for sound playback event handlers.
    /// Extracted from MainWindow.axaml.cs (#1221).
    /// </summary>
    public partial class MainWindow
    {
        // Current soundset ID for play button (#916)
        private ushort _currentSoundsetId = ushort.MaxValue;

        private async void OnBrowseSoundClick(object? sender, RoutedEventArgs e)
        {
            // Phase 2 Fix: Don't allow sound browser when no node selected or ROOT selected
            if (_selectedNode == null)
            {
                _viewModel.StatusMessage = "Please select a dialog node first";
                return;
            }

            if (_selectedNode is TreeViewRootNode)
            {
                _viewModel.StatusMessage = "Cannot assign sounds to ROOT. Select a dialog node instead.";
                return;
            }

            try
            {
                var soundBrowser = new SoundBrowserWindow(_viewModel.CurrentFileName, _services.GameData);
                var result = await soundBrowser.ShowDialog<string?>(this);

                if (!string.IsNullOrEmpty(result))
                {
                    // Update the sound field with selected sound
                    var soundTextBox = this.FindControl<TextBox>("SoundTextBox");
                    if (soundTextBox != null)
                    {
                        soundTextBox.Text = result;
                        // Trigger auto-save
                        AutoSaveProperty("SoundTextBox");
                    }
                    _viewModel.StatusMessage = $"Selected sound: {result}";
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error opening sound browser: {ex.Message}");
                _viewModel.StatusMessage = $"Error opening sound browser: {ex.Message}";
            }
        }

        /// <summary>
        /// Plays sound from property panel. Issue #895 fix: Now searches HAK/BIF archives too.
        /// </summary>
        private async void OnPlaySoundClick(object? sender, RoutedEventArgs e)
        {
            var soundTextBox = this.FindControl<TextBox>("SoundTextBox");
            var soundFileName = soundTextBox?.Text?.Trim();

            if (string.IsNullOrEmpty(soundFileName))
            {
                _viewModel.StatusMessage = "No sound file specified";
                return;
            }

            // Disable play button during playback
            var playButton = this.FindControl<Button>("PlaySoundButton");
            if (playButton != null) playButton.IsEnabled = false;

            _viewModel.StatusMessage = $"Loading: {soundFileName}...";

            var result = await _services.SoundPlayback.PlaySoundAsync(soundFileName);

            if (result.Success)
            {
                _viewModel.StatusMessage = $"Playing: {soundFileName}{result.SourceLabel}";
            }
            else
            {
                _viewModel.StatusMessage = $"⚠ {result.ErrorMessage}";
                if (playButton != null) playButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// Handles playback stopped event to re-enable play button.
        /// </summary>
        private void OnSoundPlaybackStopped(object? sender, EventArgs e)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var playButton = this.FindControl<Button>("PlaySoundButton");
                if (playButton != null) playButton.IsEnabled = true;

                var soundsetPlayButton = this.FindControl<Button>("SoundsetPlayButton");
                if (soundsetPlayButton != null) soundsetPlayButton.IsEnabled = true;

                // Clear "Playing" message if it's still showing
                if (_viewModel.StatusMessage?.StartsWith("Playing:") == true)
                {
                    _viewModel.StatusMessage = "";
                }
            });
        }

        /// <summary>
        /// Plays a sound from the NPC's soundset (#916).
        /// </summary>
        private async void OnSoundsetPlayClick(object? sender, RoutedEventArgs e)
        {
            var typeCombo = this.FindControl<ComboBox>("SoundsetTypeComboBox");
            var playButton = this.FindControl<Button>("SoundsetPlayButton");

            if (typeCombo?.SelectedItem is not SoundsetTypeItem selectedType)
            {
                _viewModel.StatusMessage = "Select a sound type to play";
                return;
            }

            if (_currentSoundsetId == ushort.MaxValue)
            {
                _viewModel.StatusMessage = "No soundset available";
                return;
            }

            // Get the soundset
            var ssf = _services.GameData.GetSoundset(_currentSoundsetId);
            if (ssf == null)
            {
                _viewModel.StatusMessage = $"Cannot load soundset ID {_currentSoundsetId}";
                return;
            }

            // Get the sound entry
            var entry = ssf.GetEntry(selectedType.SoundType);
            if (entry == null || !entry.HasSound)
            {
                _viewModel.StatusMessage = $"No sound for '{selectedType.Name}'";
                return;
            }

            // Disable play button during playback
            if (playButton != null) playButton.IsEnabled = false;

            _viewModel.StatusMessage = $"Loading: {entry.ResRef}...";

            // First try SoundPlaybackService (handles loose files, HAK, and cached BIF)
            var result = await _services.SoundPlayback.PlaySoundAsync(entry.ResRef);

            if (result.Success)
            {
                _viewModel.StatusMessage = $"Playing: {entry.ResRef}{result.SourceLabel}";
                return;
            }

            // Fallback: Try loading directly from GameDataService (BIF archives)
            // This works even when SoundBrowserIncludeBifFiles is disabled
            var soundData = _services.GameData.FindResource(entry.ResRef, ResourceTypes.Wav);
            if (soundData != null)
            {
                // Log first bytes for format diagnosis
                var headerBytes = soundData.Length >= 16 ? soundData[..16] : soundData;
                var hex = BitConverter.ToString(headerBytes).Replace("-", " ");
                var ascii = new string(headerBytes.Select(b => b >= 32 && b < 127 ? (char)b : '.').ToArray());
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Found sound in BIF: {entry.ResRef} ({soundData.Length} bytes) - Header: {hex} | {ascii}");
                try
                {
                    // Extract to temp file and play
                    var tempPath = Path.Combine(Path.GetTempPath(), $"ssf_{entry.ResRef}.wav");
                    await File.WriteAllBytesAsync(tempPath, soundData);
                    UnifiedLogger.LogApplication(LogLevel.DEBUG, $"Wrote temp file: {tempPath}");
                    _services.Audio.Play(tempPath);
                    _viewModel.StatusMessage = $"Playing: {entry.ResRef} (from BIF)";
                    return;
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to play BIF sound '{entry.ResRef}': {ex.GetType().Name}: {ex.Message}");
                    _viewModel.StatusMessage = $"Error: {ex.Message}";
                    if (playButton != null) playButton.IsEnabled = true;
                }
            }
            else
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Sound not found in GameDataService: {entry.ResRef}");
            }

            _viewModel.StatusMessage = $"Sound not found: {entry.ResRef}";
            if (playButton != null) playButton.IsEnabled = true;
        }
    }
}
