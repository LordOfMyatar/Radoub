using System;
using Avalonia.Controls;
using DialogEditor.Services;
using DialogEditor.Utils;
using Radoub.Formats.Logging;

namespace Parley.Views.Helpers
{
    /// <summary>
    /// Manages speaker visual preference UI interactions for MainWindow.
    /// Extracted from MainWindow to reduce method count and improve maintainability (Epic #1219, Sprint 1.4).
    ///
    /// Handles:
    /// 1. ComboBox initialization for speaker shapes and colors
    /// 2. Shape selection change events
    /// 3. Color selection change events
    /// </summary>
    public class SpeakerVisualController
    {
        private readonly Window _window;
        private readonly Func<bool> _isPopulatingProperties;

        public SpeakerVisualController(
            Window window,
            Func<bool> isPopulatingProperties)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _isPopulatingProperties = isPopulatingProperties ?? throw new ArgumentNullException(nameof(isPopulatingProperties));
        }

        /// <summary>
        /// Populates the speaker shape and color ComboBoxes with available options.
        /// Called during window Loaded event (Issue #16, #36).
        /// </summary>
        public void InitializeComboBoxes()
        {
            // Populate Shape ComboBox with NPC shapes (Triangle, Diamond, Pentagon, Star)
            var shapeComboBox = _window.FindControl<ComboBox>("SpeakerShapeComboBox");
            if (shapeComboBox != null)
            {
                shapeComboBox.Items.Clear();
                shapeComboBox.Items.Add(SpeakerVisualHelper.SpeakerShape.Triangle);
                shapeComboBox.Items.Add(SpeakerVisualHelper.SpeakerShape.Diamond);
                shapeComboBox.Items.Add(SpeakerVisualHelper.SpeakerShape.Pentagon);
                shapeComboBox.Items.Add(SpeakerVisualHelper.SpeakerShape.Star);
            }

            // Populate Color ComboBox with color-blind friendly palette
            var colorComboBox = _window.FindControl<ComboBox>("SpeakerColorComboBox");
            if (colorComboBox != null)
            {
                colorComboBox.Items.Clear();
                colorComboBox.Items.Add(new ComboBoxItem { Content = "Orange", Tag = SpeakerVisualHelper.ColorPalette.Orange });
                colorComboBox.Items.Add(new ComboBoxItem { Content = "Purple", Tag = SpeakerVisualHelper.ColorPalette.Purple });
                colorComboBox.Items.Add(new ComboBoxItem { Content = "Teal", Tag = SpeakerVisualHelper.ColorPalette.Teal });
                colorComboBox.Items.Add(new ComboBoxItem { Content = "Amber", Tag = SpeakerVisualHelper.ColorPalette.Amber });
                colorComboBox.Items.Add(new ComboBoxItem { Content = "Pink", Tag = SpeakerVisualHelper.ColorPalette.Pink });
            }
        }

        /// <summary>
        /// Handles speaker shape ComboBox selection changes (Issue #16, #36).
        /// </summary>
        public void OnSpeakerShapeChanged(object? sender, SelectionChangedEventArgs e)
        {
            try
            {
                // Don't trigger during property population
                if (_isPopulatingProperties()) return;

                var comboBox = sender as ComboBox;
                var speakerTextBox = _window.FindControl<TextBox>("SpeakerTextBox");

                if (comboBox?.SelectedItem != null && speakerTextBox != null && !string.IsNullOrEmpty(speakerTextBox.Text))
                {
                    var speakerTag = speakerTextBox.Text.Trim();
                    if (Enum.TryParse<SpeakerVisualHelper.SpeakerShape>(comboBox.SelectedItem.ToString(), out var shape))
                    {
                        // SetSpeakerPreference fires PropertyChanged("NpcSpeakerPreferences")
                        // MainWindow.OnSettingsPropertyChanged handles both tree + flowchart refresh (#1223)
                        SettingsService.Instance.SetSpeakerPreference(speakerTag, null, shape);
                        UnifiedLogger.LogApplication(LogLevel.INFO, $"Set speaker '{speakerTag}' shape to {shape}");
                    }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error setting speaker shape: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles speaker color ComboBox selection changes (Issue #16, #36).
        /// </summary>
        public void OnSpeakerColorChanged(object? sender, SelectionChangedEventArgs e)
        {
            try
            {
                // Don't trigger during property population
                if (_isPopulatingProperties()) return;

                var comboBox = sender as ComboBox;
                var speakerTextBox = _window.FindControl<TextBox>("SpeakerTextBox");

                if (comboBox?.SelectedItem is ComboBoxItem item && speakerTextBox != null && !string.IsNullOrEmpty(speakerTextBox.Text))
                {
                    var speakerTag = speakerTextBox.Text.Trim();
                    var color = item.Tag as string;
                    if (!string.IsNullOrEmpty(color))
                    {
                        // SetSpeakerPreference fires PropertyChanged("NpcSpeakerPreferences")
                        // MainWindow.OnSettingsPropertyChanged handles both tree + flowchart refresh (#1223)
                        SettingsService.Instance.SetSpeakerPreference(speakerTag, color, null);
                        UnifiedLogger.LogApplication(LogLevel.INFO, $"Set speaker '{speakerTag}' color to {color}");
                    }
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error setting speaker color: {ex.Message}");
            }
        }
    }
}
