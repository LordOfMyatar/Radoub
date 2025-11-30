using Avalonia.Controls;
using Avalonia.Threading;
using DialogEditor.Plugins.Services;
using DialogEditor.Services;
using System;
using System.Text.Json;
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
        private bool _javascriptBridgeInitialized;
        private string? _lastJsSyncedNodeId;  // Prevent Flow→Tree→Flow loops (#234)

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

            // Register JavaScript bridge early - must be done before content loads (#234)
            // WebViewControl requires objects registered before browser context initialization
            InitializeJavascriptBridge();

            // Subscribe to BeforeNavigate for custom URL scheme interception (#234)
            // This is a fallback for JS → C# communication if RegisterJavascriptObject doesn't work
            PanelWebView.BeforeNavigate += OnBeforeNavigate;

            // Subscribe to content updates
            PluginUIService.PanelContentUpdated += OnPanelContentUpdated;
            PluginUIService.PanelClosed += OnPanelClosed;

            // Subscribe to node selection changes for Tree→Flow sync (#234)
            DialogContextService.Instance.NodeSelectionChanged += OnNodeSelectionChanged;

            Closed += OnWindowClosed;
            Opened += OnWindowOpened;
        }

        private void OnWindowOpened(object? sender, EventArgs e)
        {
            _isInitialized = true;
            UnifiedLogger.LogPlugin(LogLevel.INFO, $"Plugin panel window opened: {_panelId}");
        }

        /// <summary>
        /// Initialize JavaScript bridge for WebView ↔ C# communication (Epic 40 Phase 3 / #234).
        /// Registers a C# object that JavaScript can call to notify of node clicks.
        /// </summary>
        private void InitializeJavascriptBridge()
        {
            if (_javascriptBridgeInitialized)
                return;

            try
            {
                // Register the bridge object for JavaScript to call
                // The WebView will expose this as window.parleyBridge
                PanelWebView.RegisterJavascriptObject("parleyBridge", new JavascriptBridge(this));
                _javascriptBridgeInitialized = true;
                UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"JavaScript bridge initialized for panel: {_panelId}");
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.WARN, $"Failed to initialize JavaScript bridge: {ex.Message}");
            }
        }

        /// <summary>
        /// Handle node selection from JavaScript (Epic 40 Phase 3 / #234).
        /// Called by the JavaScript bridge when user clicks a node in the flowchart.
        /// </summary>
        internal void OnJavascriptNodeSelected(string nodeId)
        {
            UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"JavaScript node selected: {nodeId}");

            // Track this ID to prevent Flow→Tree→Flow feedback loop
            // When tree selection changes to this ID, we won't re-call JS selectNodeById
            _lastJsSyncedNodeId = nodeId;

            // Request Parley to select this node
            Dispatcher.UIThread.Post(() =>
            {
                DialogContextService.Instance.RequestNodeSelection(nodeId);
            });
        }

        private void OnWindowClosed(object? sender, EventArgs e)
        {
            // Unsubscribe from events
            PluginUIService.PanelContentUpdated -= OnPanelContentUpdated;
            PluginUIService.PanelClosed -= OnPanelClosed;
            PanelWebView.BeforeNavigate -= OnBeforeNavigate;
            DialogContextService.Instance.NodeSelectionChanged -= OnNodeSelectionChanged;

            // Mark panel window as closed so plugin can detect (#235)
            // Don't unregister completely - keep panel info for potential reopen
            PluginUIService.MarkPanelWindowClosed(_panelId);

            UnifiedLogger.LogPlugin(LogLevel.INFO, $"Plugin panel window closed: {_panelId}");
        }

        /// <summary>
        /// Handle node selection changes from Parley tree view for Tree→Flow sync (#234).
        /// Uses ExecuteScript to call JS selectNodeById() without full page reload.
        /// </summary>
        private void OnNodeSelectionChanged(object? sender, EventArgs e)
        {
            // Only handle selection sync for flowchart panel
            if (!_panelId.Contains("flowchart"))
                return;

            var nodeId = DialogContextService.Instance.SelectedNodeId;
            if (string.IsNullOrEmpty(nodeId))
                return;

            // Prevent Flow→Tree→Flow feedback loop:
            // When user clicks flowchart, JS sends nodeId → tree selects it → fires this event
            // Without this guard, we'd call JS selectNodeById again, causing redundant updates
            if (nodeId == _lastJsSyncedNodeId)
                return;

            _lastJsSyncedNodeId = nodeId;

            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    // Execute JS to select the node without reloading the page
                    // This preserves any existing JS state (highlights, zoom, etc.)
                    var escapedNodeId = nodeId.Replace("'", "\\'");
                    PanelWebView.ExecuteScript($"if(typeof selectNodeById === 'function') selectNodeById('{escapedNodeId}');");
                    UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"Tree→Flow sync: selected {nodeId}");
                }
                catch (Exception ex)
                {
                    UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"Tree→Flow sync failed: {ex.Message}");
                }
            });
        }

        /// <summary>
        /// Intercept navigation to custom URL schemes for JS → C# communication (#234).
        /// Handles parley:// scheme URLs to avoid relying solely on RegisterJavascriptObject.
        /// </summary>
        private void OnBeforeNavigate(WebViewControl.Request request)
        {
            var url = request.Url;

            // Check for our custom scheme: parley://selectnode/{nodeId}
            if (url.StartsWith("parley://selectnode/", StringComparison.OrdinalIgnoreCase))
            {
                // Cancel the navigation - we're handling this ourselves
                request.Cancel();

                // Extract the node ID from the URL
                var nodeId = url.Substring("parley://selectnode/".Length);
                nodeId = Uri.UnescapeDataString(nodeId); // Decode any URL encoding

                UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"BeforeNavigate intercepted: selectnode/{nodeId}");

                // Handle the node selection on the UI thread
                Dispatcher.UIThread.Post(() =>
                {
                    OnJavascriptNodeSelected(nodeId);
                });
            }
            // Manual refresh request (#235)
            else if (url.Equals("parley://refresh", StringComparison.OrdinalIgnoreCase))
            {
                request.Cancel();
                UnifiedLogger.LogPlugin(LogLevel.DEBUG, "BeforeNavigate intercepted: refresh");

                // Trigger dialog change event to force flowchart re-render
                Dispatcher.UIThread.Post(() =>
                {
                    DialogContextService.Instance.NotifyDialogChanged();
                });
            }
            // Auto-refresh toggle (#235)
            else if (url.StartsWith("parley://autorefresh/", StringComparison.OrdinalIgnoreCase))
            {
                request.Cancel();
                var state = url.Substring("parley://autorefresh/".Length);
                UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"BeforeNavigate intercepted: autorefresh/{state}");

                // Notify plugin of auto-refresh preference change via broadcast
                Dispatcher.UIThread.Post(() =>
                {
                    PluginUIService.BroadcastPluginSetting(_panelId, "auto_refresh", state == "on" ? "true" : "false");
                });
            }
            // Sync selection toggle (#235)
            else if (url.StartsWith("parley://synctoggle/", StringComparison.OrdinalIgnoreCase))
            {
                request.Cancel();
                var state = url.Substring("parley://synctoggle/".Length);
                UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"BeforeNavigate intercepted: synctoggle/{state}");

                // Notify plugin of sync preference change via broadcast
                Dispatcher.UIThread.Post(() =>
                {
                    PluginUIService.BroadcastPluginSetting(_panelId, "sync_selection", state == "on" ? "true" : "false");
                });
            }
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

    /// <summary>
    /// JavaScript bridge object for WebView ↔ C# communication (Epic 40 Phase 3 / #234).
    /// Methods on this class can be called from JavaScript as window.parleyBridge.methodName().
    /// </summary>
    public class JavascriptBridge
    {
        private readonly PluginPanelWindow _window;

        public JavascriptBridge(PluginPanelWindow window)
        {
            _window = window;
        }

        /// <summary>
        /// Called from JavaScript when a node is clicked in the flowchart.
        /// JavaScript: window.parleyBridge.notifyNodeSelected("entry_0")
        /// </summary>
        public void NotifyNodeSelected(string nodeId)
        {
            _window.OnJavascriptNodeSelected(nodeId);
        }
    }
}
