using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using DialogEditor.Plugins.Proto;
using DialogEditor.Plugins.Security;
using DialogEditor.Services;

namespace DialogEditor.Plugins.Services
{
    /// <summary>
    /// UI service implementation for plugins.
    /// Provides notification, dialog, and panel management APIs.
    /// </summary>
    public class PluginUIService : UIService.UIServiceBase
    {
        private readonly PluginSecurityContext _security;

        /// <summary>
        /// Registered panels by plugin.
        /// Key: plugin_id:panel_id, Value: panel info
        /// </summary>
        private static readonly ConcurrentDictionary<string, PanelInfo> _registeredPanels = new();

        /// <summary>
        /// Event raised when a panel is registered.
        /// UI layer should subscribe to this to create actual panel windows.
        /// </summary>
        public static event EventHandler<PanelRegisteredEventArgs>? PanelRegistered;

        /// <summary>
        /// Event raised when panel content is updated.
        /// </summary>
        public static event EventHandler<PanelContentUpdatedEventArgs>? PanelContentUpdated;

        /// <summary>
        /// Event raised when a panel should be closed.
        /// </summary>
        public static event EventHandler<PanelClosedEventArgs>? PanelClosed;

        public PluginUIService(PluginSecurityContext security)
        {
            _security = security ?? throw new ArgumentNullException(nameof(security));
        }

        public override Task<ShowNotificationResponse> ShowNotification(ShowNotificationRequest request, ServerCallContext context)
        {
            try
            {
                // Check security (permission + rate limit)
                _security.CheckSecurity("ui.show_notification", "ShowNotification");

                // Log notification (UI implementation would show actual notification)
                UnifiedLogger.LogPlugin(LogLevel.INFO, $"Plugin notification: [{request.Title}] {request.Message}");

                // See #105 - Complete plugin UI notification and dialog APIs
                // For now, just log it

                return Task.FromResult(new ShowNotificationResponse
                {
                    Success = true
                });
            }
            catch (PermissionDeniedException ex)
            {
                // Permission denial already logged by CheckSecurity
                throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
            }
            catch (RateLimitExceededException ex)
            {
                throw new RpcException(new Status(StatusCode.ResourceExhausted, ex.Message));
            }
        }

        public override Task<ShowDialogResponse> ShowDialog(ShowDialogRequest request, ServerCallContext context)
        {
            try
            {
                // Check security (permission + rate limit)
                _security.CheckSecurity("ui.show_dialog", "ShowDialog");

                // Log dialog request
                UnifiedLogger.LogPlugin(LogLevel.INFO, $"Plugin dialog: [{request.Title}] {request.Message}");

                // See #105 - Complete plugin UI notification and dialog APIs
                // For now, default to button index 0

                return Task.FromResult(new ShowDialogResponse
                {
                    ButtonIndex = 0
                });
            }
            catch (PermissionDeniedException ex)
            {
                // Permission denial already logged by CheckSecurity
                throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
            }
            catch (RateLimitExceededException ex)
            {
                throw new RpcException(new Status(StatusCode.ResourceExhausted, ex.Message));
            }
        }

        /// <summary>
        /// Register a new panel for the plugin.
        /// Epic 3 / Issue #92, #224
        /// </summary>
        public override Task<RegisterPanelResponse> RegisterPanel(RegisterPanelRequest request, ServerCallContext context)
        {
            try
            {
                // Check security
                _security.CheckSecurity("ui.create_panel", "RegisterPanel");

                var pluginId = _security.PluginId;
                var fullPanelId = $"{pluginId}:{request.PanelId}";

                // Validate request
                if (string.IsNullOrWhiteSpace(request.PanelId))
                {
                    return Task.FromResult(new RegisterPanelResponse
                    {
                        Success = false,
                        ErrorMessage = "Panel ID is required"
                    });
                }

                if (string.IsNullOrWhiteSpace(request.Title))
                {
                    return Task.FromResult(new RegisterPanelResponse
                    {
                        Success = false,
                        ErrorMessage = "Panel title is required"
                    });
                }

                // Validate position
                var validPositions = new[] { "left", "right", "bottom", "float" };
                var position = string.IsNullOrWhiteSpace(request.Position) ? "right" : request.Position.ToLower();
                if (Array.IndexOf(validPositions, position) < 0)
                {
                    return Task.FromResult(new RegisterPanelResponse
                    {
                        Success = false,
                        ErrorMessage = $"Invalid position. Must be one of: {string.Join(", ", validPositions)}"
                    });
                }

                // Validate render mode
                var validModes = new[] { "webview", "native" };
                var renderMode = string.IsNullOrWhiteSpace(request.RenderMode) ? "webview" : request.RenderMode.ToLower();
                if (Array.IndexOf(validModes, renderMode) < 0)
                {
                    return Task.FromResult(new RegisterPanelResponse
                    {
                        Success = false,
                        ErrorMessage = $"Invalid render mode. Must be one of: {string.Join(", ", validModes)}"
                    });
                }

                // Create panel info
                var panelInfo = new PanelInfo
                {
                    PluginId = pluginId,
                    PanelId = request.PanelId,
                    FullPanelId = fullPanelId,
                    Title = request.Title,
                    Position = position,
                    RenderMode = renderMode,
                    InitialWidth = request.InitialWidth > 0 ? request.InitialWidth : 400,
                    InitialHeight = request.InitialHeight > 0 ? request.InitialHeight : 300,
                    CanFloat = request.CanFloat,
                    CanClose = request.CanClose
                };

                // Register panel (or update if already exists)
                _registeredPanels.AddOrUpdate(fullPanelId, panelInfo, (_, __) => panelInfo);

                UnifiedLogger.LogPlugin(LogLevel.INFO,
                    $"Panel registered: {fullPanelId} - {request.Title} ({position}, {renderMode})");

                // Raise event for UI layer to create actual panel
                PanelRegistered?.Invoke(this, new PanelRegisteredEventArgs(panelInfo));

                return Task.FromResult(new RegisterPanelResponse
                {
                    Success = true,
                    ActualPanelId = fullPanelId
                });
            }
            catch (PermissionDeniedException ex)
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
            }
            catch (RateLimitExceededException ex)
            {
                throw new RpcException(new Status(StatusCode.ResourceExhausted, ex.Message));
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Failed to register panel: {ex.Message}");
                return Task.FromResult(new RegisterPanelResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        /// <summary>
        /// Update content of a registered panel.
        /// Epic 3 / Issue #92, #226
        /// </summary>
        public override Task<UpdatePanelContentResponse> UpdatePanelContent(UpdatePanelContentRequest request, ServerCallContext context)
        {
            try
            {
                // Check security
                _security.CheckSecurity("ui.create_panel", "UpdatePanelContent");

                var pluginId = _security.PluginId;
                var fullPanelId = $"{pluginId}:{request.PanelId}";

                // Check panel exists
                if (!_registeredPanels.TryGetValue(fullPanelId, out var panelInfo))
                {
                    return Task.FromResult(new UpdatePanelContentResponse
                    {
                        Success = false,
                        ErrorMessage = $"Panel not found: {request.PanelId}"
                    });
                }

                // Validate content type
                var validTypes = new[] { "html", "url", "json" };
                var contentType = string.IsNullOrWhiteSpace(request.ContentType) ? "html" : request.ContentType.ToLower();
                if (Array.IndexOf(validTypes, contentType) < 0)
                {
                    return Task.FromResult(new UpdatePanelContentResponse
                    {
                        Success = false,
                        ErrorMessage = $"Invalid content type. Must be one of: {string.Join(", ", validTypes)}"
                    });
                }

                UnifiedLogger.LogPlugin(LogLevel.DEBUG,
                    $"Panel content update: {fullPanelId} ({contentType}, {request.Content?.Length ?? 0} chars)");

                // Raise event for UI layer to update panel
                PanelContentUpdated?.Invoke(this, new PanelContentUpdatedEventArgs(
                    fullPanelId, contentType, request.Content ?? ""));

                return Task.FromResult(new UpdatePanelContentResponse
                {
                    Success = true
                });
            }
            catch (PermissionDeniedException ex)
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
            }
            catch (RateLimitExceededException ex)
            {
                throw new RpcException(new Status(StatusCode.ResourceExhausted, ex.Message));
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Failed to update panel content: {ex.Message}");
                return Task.FromResult(new UpdatePanelContentResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        /// <summary>
        /// Close a registered panel.
        /// Epic 3 / Issue #92
        /// </summary>
        public override Task<ClosePanelResponse> ClosePanel(ClosePanelRequest request, ServerCallContext context)
        {
            try
            {
                // Check security
                _security.CheckSecurity("ui.create_panel", "ClosePanel");

                var pluginId = _security.PluginId;
                var fullPanelId = $"{pluginId}:{request.PanelId}";

                // Remove from registry
                if (_registeredPanels.TryRemove(fullPanelId, out _))
                {
                    UnifiedLogger.LogPlugin(LogLevel.INFO, $"Panel closed: {fullPanelId}");

                    // Raise event for UI layer to close panel
                    PanelClosed?.Invoke(this, new PanelClosedEventArgs(fullPanelId));

                    return Task.FromResult(new ClosePanelResponse { Success = true });
                }
                else
                {
                    UnifiedLogger.LogPlugin(LogLevel.WARN, $"Panel not found for close: {fullPanelId}");
                    return Task.FromResult(new ClosePanelResponse { Success = false });
                }
            }
            catch (PermissionDeniedException ex)
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
            }
            catch (RateLimitExceededException ex)
            {
                throw new RpcException(new Status(StatusCode.ResourceExhausted, ex.Message));
            }
        }

        /// <summary>
        /// Get all registered panels for a plugin.
        /// </summary>
        public static IEnumerable<PanelInfo> GetPanelsForPlugin(string pluginId)
        {
            return _registeredPanels.Values.Where(p => p.PluginId == pluginId);
        }

        /// <summary>
        /// Clean up all panels for a plugin (called on plugin shutdown).
        /// </summary>
        public static void CleanupPluginPanels(string pluginId)
        {
            var keysToRemove = _registeredPanels.Keys
                .Where(k => k.StartsWith($"{pluginId}:"))
                .ToList();

            foreach (var key in keysToRemove)
            {
                if (_registeredPanels.TryRemove(key, out _))
                {
                    PanelClosed?.Invoke(null, new PanelClosedEventArgs(key));
                }
            }

            if (keysToRemove.Count > 0)
            {
                UnifiedLogger.LogPlugin(LogLevel.INFO,
                    $"Cleaned up {keysToRemove.Count} panels for plugin: {pluginId}");
            }
        }
    }

    /// <summary>
    /// Information about a registered plugin panel.
    /// </summary>
    public class PanelInfo
    {
        public string PluginId { get; set; } = "";
        public string PanelId { get; set; } = "";
        public string FullPanelId { get; set; } = "";
        public string Title { get; set; } = "";
        public string Position { get; set; } = "right";
        public string RenderMode { get; set; } = "webview";
        public int InitialWidth { get; set; } = 400;
        public int InitialHeight { get; set; } = 300;
        public bool CanFloat { get; set; } = true;
        public bool CanClose { get; set; } = true;
    }

    /// <summary>
    /// Event args for panel registration.
    /// </summary>
    public class PanelRegisteredEventArgs : EventArgs
    {
        public PanelInfo PanelInfo { get; }

        public PanelRegisteredEventArgs(PanelInfo panelInfo)
        {
            PanelInfo = panelInfo;
        }
    }

    /// <summary>
    /// Event args for panel content updates.
    /// </summary>
    public class PanelContentUpdatedEventArgs : EventArgs
    {
        public string FullPanelId { get; }
        public string ContentType { get; }
        public string Content { get; }

        public PanelContentUpdatedEventArgs(string fullPanelId, string contentType, string content)
        {
            FullPanelId = fullPanelId;
            ContentType = contentType;
            Content = content;
        }
    }

    /// <summary>
    /// Event args for panel close.
    /// </summary>
    public class PanelClosedEventArgs : EventArgs
    {
        public string FullPanelId { get; }

        public PanelClosedEventArgs(string fullPanelId)
        {
            FullPanelId = fullPanelId;
        }
    }
}
