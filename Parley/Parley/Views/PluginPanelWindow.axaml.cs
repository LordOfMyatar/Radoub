using Avalonia.Controls;
using Avalonia.Threading;
using DialogEditor.Plugins.Services;
using DialogEditor.Services;
using System;
using WebViewControl;

namespace DialogEditor.Views
{
    /// <summary>
    /// Floating window for plugin panels with WebView support.
    /// Uses WebViewControl-Avalonia (CefGlue/Chromium) for HTML/URL content.
    /// Epic 3 / Issue #225
    /// </summary>
    public partial class PluginPanelWindow : Window
    {
        private readonly string _panelId;
        private bool _isInitialized;

        public string PanelId => _panelId;

        /// <summary>
        /// Parameterless constructor required by Avalonia XAML loader.
        /// Use the PanelInfo constructor for actual use.
        /// </summary>
        public PluginPanelWindow() : this(new PanelInfo
        {
            FullPanelId = "preview",
            Title = "Preview Panel",
            InitialWidth = 600,
            InitialHeight = 400
        })
        {
        }

        public PluginPanelWindow(PanelInfo panelInfo)
        {
            InitializeComponent();

            _panelId = panelInfo.FullPanelId;
            Title = panelInfo.Title;
            Width = panelInfo.InitialWidth;
            Height = panelInfo.InitialHeight;

            // Subscribe to content updates
            PluginUIService.PanelContentUpdated += OnPanelContentUpdated;
            PluginUIService.PanelClosed += OnPanelClosed;

            Closed += OnWindowClosed;
            Opened += OnWindowOpened;
        }

        private void OnWindowOpened(object? sender, EventArgs e)
        {
            _isInitialized = true;
            UnifiedLogger.LogPlugin(LogLevel.INFO, $"Plugin panel window opened: {_panelId}");
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            // Unsubscribe from events
            PluginUIService.PanelContentUpdated -= OnPanelContentUpdated;
            PluginUIService.PanelClosed -= OnPanelClosed;

            UnifiedLogger.LogPlugin(LogLevel.INFO, $"Plugin panel window closed: {_panelId}");
        }

        private void OnPanelContentUpdated(object? sender, PanelContentUpdatedEventArgs e)
        {
            if (e.FullPanelId != _panelId)
                return;

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    UpdateContent(e.ContentType, e.Content);
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogPlugin(LogLevel.ERROR,
                        $"Failed to update panel content: {ex.Message}");
                }
            });
        }

        private void OnPanelClosed(object? sender, PanelClosedEventArgs e)
        {
            if (e.FullPanelId != _panelId)
                return;

            Dispatcher.UIThread.Post(() =>
            {
                Close();
            });
        }

        /// <summary>
        /// Update the panel content using WebView for HTML/URL or TextBlock for other types.
        /// </summary>
        /// <param name="contentType">Type of content: "html", "url", or "json"</param>
        /// <param name="content">The content to display</param>
        public void UpdateContent(string contentType, string content)
        {
            if (!_isInitialized)
            {
                UnifiedLogger.LogPlugin(LogLevel.WARN,
                    "Cannot update content - window not initialized");
                return;
            }

            try
            {
                // Hide placeholder
                PlaceholderBorder.IsVisible = false;

                switch (contentType.ToLower())
                {
                    case "html":
                        // Use WebView for HTML rendering
                        ContentBorder.IsVisible = false;
                        PanelWebView.IsVisible = true;
                        PanelWebView.LoadHtml(content);
                        break;

                    case "url":
                        // Use WebView to navigate to URL
                        ContentBorder.IsVisible = false;
                        PanelWebView.IsVisible = true;
                        PanelWebView.LoadUrl(content);
                        break;

                    case "json":
                        // Use TextBlock for JSON display
                        PanelWebView.IsVisible = false;
                        ContentBorder.IsVisible = true;
                        ContentTextBlock.Text = content;
                        break;

                    default:
                        // Use TextBlock for unknown content types
                        PanelWebView.IsVisible = false;
                        ContentBorder.IsVisible = true;
                        ContentTextBlock.Text = $"[{contentType}]\n\n{content}";
                        break;
                }

                UnifiedLogger.LogPlugin(LogLevel.DEBUG,
                    $"Panel content updated: {_panelId} ({contentType})");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR,
                    $"Error updating panel content: {ex.Message}");
            }
        }
    }
}
