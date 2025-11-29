using Avalonia.Controls;
using Avalonia.Threading;
using DialogEditor.Plugins.Services;
using DialogEditor.Services;
using System;

namespace DialogEditor.Views
{
    /// <summary>
    /// Floating window for plugin panels.
    /// Initially uses TextBlock for content display.
    /// WebView integration will be added in a follow-up when API is clarified.
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
        /// Update the panel content.
        /// Currently displays content as text. WebView rendering to be added.
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
                // Hide placeholder, show content
                PlaceholderBorder.IsVisible = false;
                ContentBorder.IsVisible = true;

                switch (contentType.ToLower())
                {
                    case "html":
                        // For now, display HTML as text
                        // TODO: Integrate WebView for proper HTML rendering (#225)
                        ContentTextBlock.Text = $"[HTML Content Preview]\n\n{StripHtmlTags(content)}";
                        break;

                    case "url":
                        // Display URL info
                        ContentTextBlock.Text = $"[URL Content]\n\nNavigate to: {content}\n\n(WebView integration pending)";
                        break;

                    case "json":
                        // Pretty-print JSON
                        ContentTextBlock.Text = $"[JSON Content]\n\n{content}";
                        break;

                    default:
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

        /// <summary>
        /// Simple HTML tag stripper for preview purposes.
        /// </summary>
        private static string StripHtmlTags(string html)
        {
            if (string.IsNullOrEmpty(html))
                return string.Empty;

            // Simple regex-free approach for basic HTML stripping
            var result = html;

            // Remove style and script blocks
            while (true)
            {
                var styleStart = result.IndexOf("<style", StringComparison.OrdinalIgnoreCase);
                if (styleStart < 0) break;
                var styleEnd = result.IndexOf("</style>", styleStart, StringComparison.OrdinalIgnoreCase);
                if (styleEnd < 0) break;
                result = result.Remove(styleStart, styleEnd - styleStart + 8);
            }

            while (true)
            {
                var scriptStart = result.IndexOf("<script", StringComparison.OrdinalIgnoreCase);
                if (scriptStart < 0) break;
                var scriptEnd = result.IndexOf("</script>", scriptStart, StringComparison.OrdinalIgnoreCase);
                if (scriptEnd < 0) break;
                result = result.Remove(scriptStart, scriptEnd - scriptStart + 9);
            }

            // Remove all tags
            var inTag = false;
            var output = new System.Text.StringBuilder();
            foreach (var c in result)
            {
                if (c == '<')
                {
                    inTag = true;
                    continue;
                }
                if (c == '>')
                {
                    inTag = false;
                    continue;
                }
                if (!inTag)
                {
                    output.Append(c);
                }
            }

            // Decode common HTML entities
            var text = output.ToString();
            text = text.Replace("&nbsp;", " ");
            text = text.Replace("&lt;", "<");
            text = text.Replace("&gt;", ">");
            text = text.Replace("&amp;", "&");
            text = text.Replace("&quot;", "\"");

            // Collapse whitespace
            while (text.Contains("  "))
            {
                text = text.Replace("  ", " ");
            }

            return text.Trim();
        }
    }
}
