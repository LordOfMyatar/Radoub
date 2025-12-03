using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using DialogEditor.Plugins.Services;
using DialogEditor.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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

        // Chunked export tracking (#238, #239)
        private readonly Dictionary<string, StringBuilder> _exportChunks = new();
        private readonly Dictionary<string, int> _expectedChunkCounts = new();

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

                // Signal force refresh to plugin via setting broadcast
                // The plugin polls for this setting and triggers a full re-render
                Dispatcher.UIThread.Post(() =>
                {
                    PluginUIService.BroadcastPluginSetting(_panelId, "force_refresh", DateTime.UtcNow.Ticks.ToString());
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
            // Single-request export (#238, #239)
            else if (url.StartsWith("parley://export/", StringComparison.OrdinalIgnoreCase))
            {
                request.Cancel();
                // Format: parley://export/{format}/{base64data}
                var path = url.Substring("parley://export/".Length);
                var slashIndex = path.IndexOf('/');
                if (slashIndex > 0)
                {
                    var format = path.Substring(0, slashIndex);
                    var base64Data = path.Substring(slashIndex + 1);
                    UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"Export intercepted: {format} ({base64Data.Length} chars)");

                    Dispatcher.UIThread.Post(async () =>
                    {
                        await HandleExportAsync(format, base64Data);
                    });
                }
            }
            // Chunked export - receive a chunk (#238, #239)
            else if (url.StartsWith("parley://export-chunk/", StringComparison.OrdinalIgnoreCase))
            {
                request.Cancel();
                // Format: parley://export-chunk/{format}/{exportId}/{chunkIndex}/{totalChunks}/{data}
                var path = url.Substring("parley://export-chunk/".Length);
                var parts = path.Split('/', 5);
                if (parts.Length == 5)
                {
                    var format = parts[0];
                    var exportId = parts[1];
                    var chunkIndex = int.Parse(parts[2]);
                    var totalChunks = int.Parse(parts[3]);
                    var chunkData = parts[4];

                    var key = $"{format}_{exportId}";
                    if (!_exportChunks.ContainsKey(key))
                    {
                        _exportChunks[key] = new StringBuilder();
                        _expectedChunkCounts[key] = totalChunks;
                        UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"Export chunk started: {key}, expecting {totalChunks} chunks");
                    }

                    _exportChunks[key].Append(chunkData);
                    UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"Export chunk {chunkIndex + 1}/{totalChunks} received for {key}");
                }
            }
            // Chunked export - finalize (#238, #239)
            else if (url.StartsWith("parley://export-done/", StringComparison.OrdinalIgnoreCase))
            {
                request.Cancel();
                // Format: parley://export-done/{format}/{exportId}
                var path = url.Substring("parley://export-done/".Length);
                var slashIndex = path.IndexOf('/');
                if (slashIndex > 0)
                {
                    var format = path.Substring(0, slashIndex);
                    var exportId = path.Substring(slashIndex + 1);
                    var key = $"{format}_{exportId}";

                    if (_exportChunks.TryGetValue(key, out var chunks))
                    {
                        var base64Data = chunks.ToString();
                        _exportChunks.Remove(key);
                        _expectedChunkCounts.Remove(key);

                        UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"Export finalized: {format}, {base64Data.Length} chars total");

                        Dispatcher.UIThread.Post(async () =>
                        {
                            await HandleExportAsync(format, base64Data);
                        });
                    }
                }
            }
        }

        /// <summary>
        /// Handle export by showing save dialog and writing file (#238, #239)
        /// </summary>
        private async System.Threading.Tasks.Task HandleExportAsync(string format, string base64Data)
        {
            try
            {
                // Decode base64
                byte[] fileBytes;
                if (format.Equals("svg", StringComparison.OrdinalIgnoreCase))
                {
                    // SVG is text, decode UTF-8
                    var svgString = Encoding.UTF8.GetString(Convert.FromBase64String(base64Data));
                    fileBytes = Encoding.UTF8.GetBytes(svgString);
                }
                else
                {
                    // PNG is binary
                    fileBytes = Convert.FromBase64String(base64Data);
                }

                // Build filename: module_folder-dialog_name.ext
                var modulePath = SettingsService.Instance.CurrentModulePath;
                var moduleFolderName = !string.IsNullOrEmpty(modulePath)
                    ? Path.GetFileName(modulePath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
                    : null;

                var dialogName = DialogContextService.Instance.CurrentFileName ?? "flowchart";
                // Remove .dlg extension if present
                if (dialogName.EndsWith(".dlg", StringComparison.OrdinalIgnoreCase))
                    dialogName = dialogName.Substring(0, dialogName.Length - 4);

                // Combine module folder and dialog name
                string baseFileName;
                if (!string.IsNullOrEmpty(moduleFolderName))
                    baseFileName = $"{moduleFolderName}-{dialogName}";
                else
                    baseFileName = dialogName;

                // Remove any invalid filename characters
                baseFileName = string.Join("_", baseFileName.Split(Path.GetInvalidFileNameChars()));

                var extension = format.ToLower();
                var defaultFileName = $"{baseFileName}.{extension}";

                // Show save file dialog
                var storage = GetTopLevel(this)?.StorageProvider;
                if (storage == null)
                {
                    UnifiedLogger.LogPlugin(LogLevel.ERROR, "Storage provider not available");
                    return;
                }

                var fileType = format.Equals("svg", StringComparison.OrdinalIgnoreCase)
                    ? new FilePickerFileType("SVG Files") { Patterns = new[] { "*.svg" } }
                    : new FilePickerFileType("PNG Images") { Patterns = new[] { "*.png" } };

                var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = $"Export Flowchart as {format.ToUpper()}",
                    SuggestedFileName = defaultFileName,
                    FileTypeChoices = new[] { fileType },
                    DefaultExtension = extension
                });

                if (file != null)
                {
                    await using var stream = await file.OpenWriteAsync();
                    await stream.WriteAsync(fileBytes);
                    UnifiedLogger.LogPlugin(LogLevel.INFO, $"Flowchart exported to: {file.Name}");
                }
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Export failed: {ex.Message}");
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
