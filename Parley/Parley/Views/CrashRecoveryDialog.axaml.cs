using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DialogEditor.Services;

namespace DialogEditor.Views
{
    public partial class CrashRecoveryDialog : Window
    {
        public bool StartInSafeMode { get; private set; } = true;
        public bool DisableOnlyCrashedPlugins { get; private set; } = false;

        // Parameterless constructor for XAML/Avalonia runtime
        public CrashRecoveryDialog() : this(new List<string>())
        {
        }

        public CrashRecoveryDialog(List<string> pluginsLoadedDuringCrash)
        {
            InitializeComponent();
            LoadPluginList(pluginsLoadedDuringCrash);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        private void LoadPluginList(List<string> plugins)
        {
            var pluginListTextBlock = this.FindControl<TextBlock>("PluginListTextBlock");
            if (pluginListTextBlock != null)
            {
                if (plugins.Count > 0)
                {
                    pluginListTextBlock.Text = string.Join("\n", plugins.Select(p => $"• {p}"));
                }
                else
                {
                    pluginListTextBlock.Text = "• No plugins were loaded";
                }
            }
        }

        private void OnContinueClick(object? sender, RoutedEventArgs e)
        {
            var safeModeRadio = this.FindControl<RadioButton>("SafeModeRadio");
            var disableSpecificCheckBox = this.FindControl<CheckBox>("DisableSpecificCheckBox");

            StartInSafeMode = safeModeRadio?.IsChecked == true;
            DisableOnlyCrashedPlugins = disableSpecificCheckBox?.IsChecked == true;

            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"Crash recovery: SafeMode={StartInSafeMode}, DisableSpecific={DisableOnlyCrashedPlugins}");

            Close();
        }
    }
}
