using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using DialogEditor.Models;
using DialogEditor.Services;
using Radoub.Formats.Logging;
using DialogEditor.Utils;
using DialogEditor.ViewModels;

namespace DialogEditor.Views
{
    /// <summary>
    /// Conversation Simulator window - walk through dialog branches and track coverage.
    /// Issue #478 - Conversation Simulator Sprint 1
    /// </summary>
    public partial class ConversationSimulatorWindow : Window
    {
        private ConversationSimulatorViewModel? _viewModel;

        public ConversationSimulatorWindow()
        {
            InitializeComponent();
            ApplyPcColors();
        }

        public ConversationSimulatorWindow(Dialog dialog, string filePath) : this()
        {
            _viewModel = new ConversationSimulatorViewModel(dialog, filePath);
            DataContext = _viewModel;

            _viewModel.RequestClose += (s, e) => Close();
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

            // Start the conversation
            _viewModel.StartConversation();

            // Apply initial colors (PropertyChanged won't fire for initial values)
            UpdateSpeakerColor();
            UpdateChoicesIndicator();

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"ConversationSimulator: Opened for {UnifiedLogger.SanitizePath(filePath)}");
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ConversationSimulatorViewModel.NpcSpeakerColor))
            {
                UpdateSpeakerColor();
            }
            else if (e.PropertyName == nameof(ConversationSimulatorViewModel.IsShowingPcChoices))
            {
                UpdateChoicesIndicator();
            }
        }

        private void UpdateSpeakerColor()
        {
            if (_viewModel == null) return;

            var speakerLabel = this.FindControl<TextBlock>("SpeakerLabel");
            if (speakerLabel != null)
            {
                var color = Color.Parse(_viewModel.NpcSpeakerColor);
                speakerLabel.Foreground = new SolidColorBrush(color);
            }
        }

        /// <summary>
        /// Update the choices indicator to show PC (blue circle) or NPC (orange square) style.
        /// </summary>
        private void UpdateChoicesIndicator()
        {
            if (_viewModel == null) return;

            var choicesShape = this.FindControl<Ellipse>("PcCircle");
            var chooseLabel = this.FindControl<TextBlock>("ChooseResponseLabel");

            if (_viewModel.IsShowingPcChoices)
            {
                // PC choices: blue circle, "Choose response:"
                var pcColorHex = SpeakerVisualHelper.GetSpeakerColor("", isPC: true);
                var pcColor = Color.Parse(pcColorHex);
                var pcBrush = new SolidColorBrush(pcColor);
                var strokeBrush = new SolidColorBrush(Color.FromArgb(255,
                    (byte)(pcColor.R * 0.8), (byte)(pcColor.G * 0.8), (byte)(pcColor.B * 0.8)));

                if (choicesShape != null)
                {
                    choicesShape.Fill = pcBrush;
                    choicesShape.Stroke = strokeBrush;
                }
                if (chooseLabel != null)
                {
                    chooseLabel.Text = "Choose response:";
                    chooseLabel.Foreground = pcBrush;
                }
            }
            else
            {
                // NPC choices (root entry selection): use NPC color, "Select start:"
                var npcColorHex = SpeakerVisualHelper.GetSpeakerColor("", isPC: false);
                var npcColor = Color.Parse(npcColorHex);
                var npcBrush = new SolidColorBrush(npcColor);
                var strokeBrush = new SolidColorBrush(Color.FromArgb(255,
                    (byte)(npcColor.R * 0.8), (byte)(npcColor.G * 0.8), (byte)(npcColor.B * 0.8)));

                if (choicesShape != null)
                {
                    choicesShape.Fill = npcBrush;
                    choicesShape.Stroke = strokeBrush;
                }
                if (chooseLabel != null)
                {
                    chooseLabel.Text = "Select start:";
                    chooseLabel.Foreground = npcBrush;
                }
            }
        }

        private void ApplyPcColors()
        {
            // Initial setup - will be overridden by UpdateChoicesIndicator based on state
            var pcColorHex = SpeakerVisualHelper.GetSpeakerColor("", isPC: true);
            var pcColor = Color.Parse(pcColorHex);
            var pcBrush = new SolidColorBrush(pcColor);

            // Darker stroke for the circle
            var strokeColor = Color.FromArgb(255,
                (byte)(pcColor.R * 0.8),
                (byte)(pcColor.G * 0.8),
                (byte)(pcColor.B * 0.8));
            var strokeBrush = new SolidColorBrush(strokeColor);

            // Apply to PC indicator elements
            var pcCircle = this.FindControl<Ellipse>("PcCircle");
            if (pcCircle != null)
            {
                pcCircle.Fill = pcBrush;
                pcCircle.Stroke = strokeBrush;
            }

            var chooseLabel = this.FindControl<TextBlock>("ChooseResponseLabel");
            if (chooseLabel != null)
            {
                chooseLabel.Foreground = pcBrush;
            }
        }

        private void OnReplyClicked(object? sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radioButton && radioButton.Tag is int index)
            {
                _viewModel?.SelectReply(index);
            }
        }

        private void OnSkipClick(object? sender, RoutedEventArgs e)
        {
            _viewModel?.Skip();
        }

        private void OnRestartClick(object? sender, RoutedEventArgs e)
        {
            _viewModel?.StartConversation();
        }

        private void OnExitClick(object? sender, RoutedEventArgs e)
        {
            _viewModel?.Exit();
            Close();
        }

        private void OnClearCoverageClick(object? sender, RoutedEventArgs e)
        {
            _viewModel?.ClearCoverage();
        }

        private void OnSpeakClick(object? sender, RoutedEventArgs e)
        {
            _viewModel?.Speak();
        }

        private void OnStopClick(object? sender, RoutedEventArgs e)
        {
            _viewModel?.StopSpeaking();
        }
    }
}
