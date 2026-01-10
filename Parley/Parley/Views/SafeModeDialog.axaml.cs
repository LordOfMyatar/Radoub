using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Radoub.Formats.Logging;

namespace DialogEditor.Views
{
    public partial class SafeModeDialog : Window
    {
        /// <summary>
        /// Whether the user chose to clear scrap data
        /// </summary>
        public bool ClearScrap { get; private set; }

        /// <summary>
        /// Whether the user chose to continue (true) or exit (false)
        /// </summary>
        public bool ShouldContinue { get; private set; }

        public SafeModeDialog()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void OnContinueClick(object? sender, RoutedEventArgs e)
        {
            var clearScrapCheckBox = this.FindControl<CheckBox>("ClearScrapCheckBox");
            ClearScrap = clearScrapCheckBox?.IsChecked == true;
            ShouldContinue = true;

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"SafeMode dialog: Continue, ClearScrap={ClearScrap}");

            Close();
        }

        private void OnExitClick(object? sender, RoutedEventArgs e)
        {
            ShouldContinue = false;
            UnifiedLogger.LogApplication(LogLevel.INFO, "SafeMode dialog: User chose to exit");
            Close();
        }
    }
}
