using System;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using DialogEditor.Services;
using Radoub.Dictionary;
using Radoub.Formats.Logging;

namespace DialogEditor.Views.Controllers
{
    /// <summary>
    /// Controller for UI/Appearance settings section in SettingsWindow.
    /// Handles: Scrollbar, flowchart node display, NPC coloring, warnings, spell check.
    /// Font/theme settings removed — now managed by RadoubSettings (Trebuchet is sole authority).
    /// </summary>
    public class UISettingsController
    {
        private readonly Window _window;
        private readonly Func<bool> _isInitializing;
        private readonly ISettingsService _settings;
        private readonly DictionarySettingsService _dictionarySettings;

        public UISettingsController(Window window, Func<bool> isInitializing, ISettingsService settings, DictionarySettingsService dictionarySettings)
        {
            _window = window;
            _isInitializing = isInitializing;
            _settings = settings;
            _dictionarySettings = dictionarySettings;
        }

        public void LoadSettings()
        {
            var settings = _settings;

            var allowScrollbarAutoHideCheckBox = _window.FindControl<CheckBox>("AllowScrollbarAutoHideCheckBox");
            if (allowScrollbarAutoHideCheckBox != null)
            {
                allowScrollbarAutoHideCheckBox.IsChecked = settings.AllowScrollbarAutoHide;
            }

            var enableNpcTagColoringCheckBox = _window.FindControl<CheckBox>("EnableNpcTagColoringCheckBox");
            if (enableNpcTagColoringCheckBox != null)
            {
                enableNpcTagColoringCheckBox.IsChecked = settings.EnableNpcTagColoring;
            }

            var simulatorShowWarningsCheckBox = _window.FindControl<CheckBox>("SimulatorShowWarningsCheckBox");
            if (simulatorShowWarningsCheckBox != null)
            {
                simulatorShowWarningsCheckBox.IsChecked = settings.SimulatorShowWarnings;
            }

            var spellCheckEnabledCheckBox = _window.FindControl<CheckBox>("SpellCheckEnabledCheckBox");
            if (spellCheckEnabledCheckBox != null)
            {
                spellCheckEnabledCheckBox.IsChecked = _dictionarySettings.SpellCheckEnabled;
            }

            // Flowchart node max lines (#813)
            var flowchartNodeMaxLinesSlider = _window.FindControl<Slider>("FlowchartNodeMaxLinesSlider");
            var flowchartNodeMaxLinesLabel = _window.FindControl<TextBlock>("FlowchartNodeMaxLinesLabel");
            if (flowchartNodeMaxLinesSlider != null)
            {
                flowchartNodeMaxLinesSlider.Value = settings.FlowchartNodeMaxLines;
            }
            if (flowchartNodeMaxLinesLabel != null)
            {
                var lines = settings.FlowchartNodeMaxLines;
                flowchartNodeMaxLinesLabel.Text = lines == 1 ? "1 line" : $"{lines} lines";
            }

            // Flowchart node width (#906)
            var flowchartNodeWidthSlider = _window.FindControl<Slider>("FlowchartNodeWidthSlider");
            var flowchartNodeWidthLabel = _window.FindControl<TextBlock>("FlowchartNodeWidthLabel");
            if (flowchartNodeWidthSlider != null)
            {
                flowchartNodeWidthSlider.Value = settings.FlowchartNodeWidth;
            }
            if (flowchartNodeWidthLabel != null)
            {
                flowchartNodeWidthLabel.Text = $"{settings.FlowchartNodeWidth} px";
            }

            var externalEditorPathTextBox = _window.FindControl<TextBox>("ExternalEditorPathTextBox");
            if (externalEditorPathTextBox != null)
            {
                externalEditorPathTextBox.Text = settings.ExternalEditorPath;
            }
        }

        public void ApplySettings()
        {
            var settings = _settings;

            var externalEditorPathTextBox = _window.FindControl<TextBox>("ExternalEditorPathTextBox");
            if (externalEditorPathTextBox != null)
            {
                settings.ExternalEditorPath = externalEditorPathTextBox.Text ?? "";
            }
        }

        public void OnAllowScrollbarAutoHideChanged(object? sender, RoutedEventArgs e)
        {
            if (_isInitializing()) return;

            var checkbox = sender as CheckBox;
            if (checkbox != null)
            {
                _settings.AllowScrollbarAutoHide = checkbox.IsChecked == true;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Scrollbar auto-hide preference: {checkbox.IsChecked}");
            }
        }

        public void OnEnableNpcTagColoringChanged(object? sender, RoutedEventArgs e)
        {
            if (_isInitializing()) return;

            var checkbox = sender as CheckBox;
            if (checkbox != null)
            {
                _settings.EnableNpcTagColoring = checkbox.IsChecked == true;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"NPC tag coloring: {(checkbox.IsChecked == true ? "enabled" : "disabled")}");
            }
        }

        public void OnSimulatorShowWarningsChanged(object? sender, RoutedEventArgs e)
        {
            if (_isInitializing()) return;

            var checkbox = sender as CheckBox;
            if (checkbox != null)
            {
                _settings.SimulatorShowWarnings = checkbox.IsChecked == true;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Dialog warnings: {(checkbox.IsChecked == true ? "enabled" : "disabled")}");
            }
        }

        public void OnSpellCheckEnabledChanged(object? sender, RoutedEventArgs e)
        {
            if (_isInitializing()) return;

            var checkbox = sender as CheckBox;
            if (checkbox != null)
            {
                _dictionarySettings.SpellCheckEnabled = checkbox.IsChecked == true;
            }
        }

        public void OnFlowchartNodeMaxLinesChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            var flowchartNodeMaxLinesLabel = _window.FindControl<TextBlock>("FlowchartNodeMaxLinesLabel");
            if (flowchartNodeMaxLinesLabel != null && sender is Slider slider)
            {
                var lines = (int)slider.Value;
                flowchartNodeMaxLinesLabel.Text = lines == 1 ? "1 line" : $"{lines} lines";
            }

            if (!_isInitializing() && sender is Slider s)
            {
                _settings.FlowchartNodeMaxLines = (int)s.Value;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Flowchart node max lines: {(int)s.Value}");
            }
        }

        public void OnFlowchartNodeWidthChanged(object? sender, RangeBaseValueChangedEventArgs e)
        {
            var label = _window.FindControl<TextBlock>("FlowchartNodeWidthLabel");
            if (label != null && sender is Slider slider)
            {
                label.Text = $"{(int)slider.Value} px";
            }

            if (!_isInitializing() && sender is Slider s)
            {
                _settings.FlowchartNodeWidth = (int)s.Value;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Flowchart node width: {(int)s.Value}");
            }
        }

    }
}
