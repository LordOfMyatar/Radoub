using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using DialogEditor.Plugins.Proto;
using DialogEditor.Plugins.Security;
using DialogEditor.Services;
using Radoub.Formats.Logging;

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
        /// Tracks which panel windows are currently open (#235).
        /// Key: fullPanelId, Value: true if window is open
        /// </summary>
        private static readonly ConcurrentDictionary<string, bool> _panelWindowOpen = new();

        /// <summary>
        /// Stores panel-specific settings from UI (#235).
        /// Key: fullPanelId:settingName, Value: setting value
        /// Used for sync_selection, auto_refresh, etc.
        /// </summary>
        private static readonly ConcurrentDictionary<string, string> _panelSettings = new();

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

        /// <summary>
        /// Event raised when a plugin setting changes from UI (#235).
        /// Plugins can subscribe to receive settings like sync_selection, auto_refresh.
        /// </summary>
        public static event EventHandler<PluginSettingChangedEventArgs>? PluginSettingChanged;

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
                // Mark window as open when registered (#235)
                _panelWindowOpen[fullPanelId] = true;

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

                UnifiedLogger.LogPlugin(LogLevel.WARN, $"ClosePanel called for: {fullPanelId} - THIS REMOVES REGISTRATION!");

                // Remove from registry
                if (_registeredPanels.TryRemove(fullPanelId, out _))
                {
                    UnifiedLogger.LogPlugin(LogLevel.INFO, $"Panel closed and unregistered: {fullPanelId}");

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
        /// Check if a panel window is currently open (#235).
        /// Used by plugins to determine if they should continue polling.
        /// Returns false when user closes the window (even if panel is still registered).
        /// </summary>
        public override Task<IsPanelOpenResponse> IsPanelOpen(IsPanelOpenRequest request, ServerCallContext context)
        {
            try
            {
                var pluginId = _security.PluginId;
                var fullPanelId = $"{pluginId}:{request.PanelId}";

                // Check window state, not just registration (#235)
                // Panel stays registered for potential reopen, but window may be closed
                var isOpen = _panelWindowOpen.TryGetValue(fullPanelId, out var windowOpen) && windowOpen;

                return Task.FromResult(new IsPanelOpenResponse { IsOpen = isOpen });
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"IsPanelOpen check failed: {ex.Message}");
                return Task.FromResult(new IsPanelOpenResponse { IsOpen = false });
            }
        }

        /// <summary>
        /// Get a panel setting value (#235).
        /// Used by plugins to retrieve UI toggle states like sync_selection.
        /// </summary>
        public override Task<GetPanelSettingResponse> GetPanelSetting(GetPanelSettingRequest request, ServerCallContext context)
        {
            try
            {
                var pluginId = _security.PluginId;
                var fullPanelId = $"{pluginId}:{request.PanelId}";

                var value = GetPanelSetting(fullPanelId, request.SettingName);

                return Task.FromResult(new GetPanelSettingResponse
                {
                    Found = value != null,
                    Value = value ?? ""
                });
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"GetPanelSetting failed: {ex.Message}");
                return Task.FromResult(new GetPanelSettingResponse { Found = false, Value = "" });
            }
        }

        /// <summary>
        /// Static helper to raise PanelRegistered event (used by UIServiceImpl).
        /// </summary>
        public static void RaisePanelRegistered(PanelInfo panelInfo)
        {
            _registeredPanels.AddOrUpdate(panelInfo.FullPanelId, panelInfo, (_, __) => panelInfo);
            _panelWindowOpen[panelInfo.FullPanelId] = true;  // Mark window as open (#235)
            PanelRegistered?.Invoke(null, new PanelRegisteredEventArgs(panelInfo));
        }

        /// <summary>
        /// Static helper to raise PanelContentUpdated event (used by UIServiceImpl).
        /// </summary>
        public static void RaisePanelContentUpdated(string fullPanelId, string contentType, string content)
        {
            PanelContentUpdated?.Invoke(null, new PanelContentUpdatedEventArgs(fullPanelId, contentType, content));
        }

        /// <summary>
        /// Get all registered panels for a plugin.
        /// </summary>
        public static IEnumerable<PanelInfo> GetPanelsForPlugin(string pluginId)
        {
            return _registeredPanels.Values.Where(p => p.PluginId == pluginId);
        }

        /// <summary>
        /// Get all registered panels across all plugins.
        /// </summary>
        public static IEnumerable<PanelInfo> GetAllRegisteredPanels()
        {
            var count = _registeredPanels.Count;
            UnifiedLogger.LogPlugin(LogLevel.DEBUG,
                $"GetAllRegisteredPanels: {count} panels in registry: [{string.Join(", ", _registeredPanels.Keys)}]");
            return _registeredPanels.Values;
        }

        /// <summary>
        /// Check if a panel window is currently open (#235).
        /// Used by plugins to determine if they should continue polling.
        /// Returns false if window was closed by user (even if panel is still registered).
        /// </summary>
        public static bool IsPanelOpen(string fullPanelId)
        {
            // Check if window is currently open, not just registered
            var isOpen = _panelWindowOpen.TryGetValue(fullPanelId, out var windowOpen) && windowOpen;
            UnifiedLogger.LogPlugin(LogLevel.DEBUG,
                $"IsPanelOpen({fullPanelId}): windowOpen={windowOpen}, result={isOpen}, registered={_registeredPanels.ContainsKey(fullPanelId)}");
            return isOpen;
        }

        /// <summary>
        /// Mark a panel window as closed when user closes the window (#235).
        /// Keeps panel registration for potential reopen, but signals plugin to exit.
        /// </summary>
        public static void MarkPanelWindowClosed(string fullPanelId)
        {
            _panelWindowOpen[fullPanelId] = false;
            UnifiedLogger.LogPlugin(LogLevel.INFO, $"Panel window marked closed: {fullPanelId}");
        }

        /// <summary>
        /// Mark a panel window as open when it's created/reopened (#235).
        /// </summary>
        public static void MarkPanelWindowOpen(string fullPanelId)
        {
            _panelWindowOpen[fullPanelId] = true;
            UnifiedLogger.LogPlugin(LogLevel.INFO, $"Panel window marked open: {fullPanelId}");
        }

        /// <summary>
        /// Broadcast a setting change from UI to plugin (#235).
        /// Used for toggle states like sync_selection, auto_refresh that need to persist across re-renders.
        /// Also stores the setting for later retrieval by plugins via GetPanelSetting.
        /// </summary>
        public static void BroadcastPluginSetting(string fullPanelId, string settingName, string settingValue)
        {
            // Store setting for persistence across re-renders
            var key = $"{fullPanelId}:{settingName}";
            _panelSettings[key] = settingValue;
            UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"Stored setting: {key} = {settingValue}");

            // Raise event for any listeners
            PluginSettingChanged?.Invoke(null, new PluginSettingChangedEventArgs(fullPanelId, settingName, settingValue));
        }

        /// <summary>
        /// Get a stored panel setting (#235).
        /// Returns null if setting not found.
        /// </summary>
        public static string? GetPanelSetting(string fullPanelId, string settingName)
        {
            var key = $"{fullPanelId}:{settingName}";
            return _panelSettings.TryGetValue(key, out var value) ? value : null;
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
                    _panelWindowOpen.TryRemove(key, out _);  // Also remove from window tracking (#235)
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

    /// <summary>
    /// Event args for plugin setting changes from UI (#235).
    /// </summary>
    public class PluginSettingChangedEventArgs : EventArgs
    {
        public string FullPanelId { get; }
        public string SettingName { get; }
        public string SettingValue { get; }

        public PluginSettingChangedEventArgs(string fullPanelId, string settingName, string settingValue)
        {
            FullPanelId = fullPanelId;
            SettingName = settingName;
            SettingValue = settingValue;
        }
    }
}
