using Avalonia.Controls;
using Avalonia.Interactivity;
using DialogEditor.Models;
using DialogEditor.Services;
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
        }

        public ConversationSimulatorWindow(Dialog dialog, string filePath) : this()
        {
            _viewModel = new ConversationSimulatorViewModel(dialog, filePath);
            DataContext = _viewModel;

            _viewModel.RequestClose += (s, e) => Close();

            // Start the conversation
            _viewModel.StartConversation();

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"ConversationSimulator: Opened for {UnifiedLogger.SanitizePath(filePath)}");
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
    }
}
