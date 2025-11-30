using System;
using System.Linq;
using System.Threading.Tasks;
using DialogEditor.Plugins.Proto;
using DialogEditor.Services;
using DialogEditor.Utils;
using Avalonia.Controls;
using Avalonia.Threading;
using Grpc.Core;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DialogEditor.Plugins.Services
{
    /// <summary>
    /// gRPC server that hosts all plugin services
    /// </summary>
    public class PluginGrpcServer : IDisposable
    {
        private readonly IHost _host;
        private readonly int _port;
        private bool _isDisposed;

        public int Port => _port;
        public bool IsRunning => _host != null;

        public PluginGrpcServer()
        {
            // Find available port
            _port = FindAvailablePort();

            // Create Kestrel host with gRPC services
            _host = Host.CreateDefaultBuilder()
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.ConfigureKestrel(options =>
                    {
                        options.ListenLocalhost(_port, listenOptions =>
                        {
                            listenOptions.Protocols = HttpProtocols.Http2;
                        });
                    });

                    webBuilder.ConfigureServices(services =>
                    {
                        services.AddGrpc();
                    });

                    webBuilder.Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGrpcService<AudioServiceImpl>();
                            endpoints.MapGrpcService<UIServiceImpl>();
                            endpoints.MapGrpcService<DialogServiceImpl>();
                            endpoints.MapGrpcService<FileServiceImpl>();
                        });
                    });
                })
                .Build();
        }

        public void Start()
        {
            _host.StartAsync();
            UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"gRPC server started on port {_port}");
        }

        public async Task StopAsync()
        {
            if (_host != null)
            {
                await _host.StopAsync();
                UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"gRPC server stopped on port {_port}");
            }
        }

        private static int FindAvailablePort()
        {
            // Use ephemeral port range (49152-65535)
            var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
            listener.Start();
            int port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            StopAsync().Wait();
            _host?.Dispose();
            _isDisposed = true;
        }
    }

    /// <summary>
    /// Audio service implementation
    /// </summary>
    internal class AudioServiceImpl : Proto.AudioService.AudioServiceBase
    {
        public override Task<PlayAudioResponse> PlayAudio(PlayAudioRequest request, ServerCallContext context)
        {
            try
            {
                UnifiedLogger.LogPlugin(LogLevel.INFO, $"Plugin requested audio playback: {request.FilePath}");

                // See #102 - Wire AudioService to plugin gRPC API
                // For now, just log the request

                return Task.FromResult(new PlayAudioResponse
                {
                    Success = true
                });
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Audio playback failed: {ex.Message}");
                return Task.FromResult(new PlayAudioResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        public override Task<StopAudioResponse> StopAudio(StopAudioRequest request, ServerCallContext context)
        {
            try
            {
                UnifiedLogger.LogPlugin(LogLevel.INFO, "Plugin requested audio stop");

                // See #102 - Wire AudioService to plugin gRPC API

                return Task.FromResult(new StopAudioResponse
                {
                    Success = true
                });
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Audio stop failed: {ex.Message}");
                return Task.FromResult(new StopAudioResponse
                {
                    Success = false
                });
            }
        }
    }

    /// <summary>
    /// UI service implementation
    /// </summary>
    internal class UIServiceImpl : Proto.UIService.UIServiceBase
    {
        public override Task<ShowNotificationResponse> ShowNotification(ShowNotificationRequest request, ServerCallContext context)
        {
            try
            {
                UnifiedLogger.LogPlugin(LogLevel.INFO, $"Plugin notification: {request.Title} - {request.Message}");

                // Show toast notification that auto-closes after 3 seconds
                Dispatcher.UIThread.Post(async () =>
                {
                    var window = new Window
                    {
                        Title = request.Title,
                        Width = 350,
                        Height = 100,
                        WindowStartupLocation = WindowStartupLocation.Manual,
                        SystemDecorations = SystemDecorations.BorderOnly,
                        CanResize = false,
                        ShowInTaskbar = false,
                        Topmost = true,
                        Content = new TextBlock
                        {
                            Text = $"{request.Title}: {request.Message}",
                            Margin = new Avalonia.Thickness(15),
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                        }
                    };

                    // Position in bottom-right corner
                    var screen = window.Screens.Primary;
                    if (screen != null)
                    {
                        var workArea = screen.WorkingArea;
                        window.Position = new Avalonia.PixelPoint(
                            workArea.Right - (int)window.Width - 20,
                            workArea.Bottom - (int)window.Height - 20);
                    }

                    window.Show();

                    // Auto-close after 3 seconds
                    await Task.Delay(3000);
                    if (window.IsVisible)
                    {
                        window.Close();
                    }
                });

                return Task.FromResult(new ShowNotificationResponse
                {
                    Success = true
                });
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Show notification failed: {ex.Message}");
                return Task.FromResult(new ShowNotificationResponse
                {
                    Success = false
                });
            }
        }

        public override Task<ShowDialogResponse> ShowDialog(ShowDialogRequest request, ServerCallContext context)
        {
            try
            {
                // TODO: Implement proper dialog with buttons (Issue #105)
                // For now, just log the request - don't spawn a window that hangs around
                UnifiedLogger.LogPlugin(LogLevel.INFO, $"Plugin dialog (stub): {request.Title} - {request.Message}");

                return Task.FromResult(new ShowDialogResponse
                {
                    ButtonIndex = 0
                });
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Show dialog failed: {ex.Message}");
                return Task.FromResult(new ShowDialogResponse
                {
                    ButtonIndex = -1
                });
            }
        }

        public override Task<RegisterPanelResponse> RegisterPanel(RegisterPanelRequest request, ServerCallContext context)
        {
            try
            {
                var fullPanelId = $"plugin:{request.PanelId}";

                UnifiedLogger.LogPlugin(LogLevel.INFO,
                    $"Panel registered: {fullPanelId} - {request.Title} ({request.Position}, {request.RenderMode})");

                // Raise event for UI layer to create actual panel
                PluginUIService.RaisePanelRegistered(new PanelInfo
                {
                    PluginId = "plugin",
                    PanelId = request.PanelId,
                    FullPanelId = fullPanelId,
                    Title = request.Title,
                    Position = request.Position ?? "right",
                    RenderMode = request.RenderMode ?? "webview",
                    InitialWidth = request.InitialWidth > 0 ? request.InitialWidth : 400,
                    InitialHeight = request.InitialHeight > 0 ? request.InitialHeight : 300,
                    CanFloat = request.CanFloat,
                    CanClose = request.CanClose
                });

                return Task.FromResult(new RegisterPanelResponse
                {
                    Success = true,
                    ActualPanelId = fullPanelId
                });
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

        public override Task<UpdatePanelContentResponse> UpdatePanelContent(UpdatePanelContentRequest request, ServerCallContext context)
        {
            try
            {
                var fullPanelId = $"plugin:{request.PanelId}";

                UnifiedLogger.LogPlugin(LogLevel.DEBUG,
                    $"Panel content update: {fullPanelId} - {request.ContentType} ({request.Content?.Length ?? 0} bytes)");

                // Raise event for UI layer to update panel content
                PluginUIService.RaisePanelContentUpdated(fullPanelId, request.ContentType, request.Content ?? "");

                return Task.FromResult(new UpdatePanelContentResponse
                {
                    Success = true
                });
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

        public override Task<IsPanelOpenResponse> IsPanelOpen(IsPanelOpenRequest request, ServerCallContext context)
        {
            try
            {
                var fullPanelId = $"plugin:{request.PanelId}";
                var isOpen = PluginUIService.IsPanelOpen(fullPanelId);

                return Task.FromResult(new IsPanelOpenResponse { IsOpen = isOpen });
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"IsPanelOpen check failed: {ex.Message}");
                return Task.FromResult(new IsPanelOpenResponse { IsOpen = false });
            }
        }

        public override Task<GetThemeResponse> GetTheme(GetThemeRequest request, ServerCallContext context)
        {
            try
            {
                var themeId = SettingsService.Instance.CurrentThemeId;
                var isDark = themeId.Contains("dark", StringComparison.OrdinalIgnoreCase);

                // Extract theme name from ID (e.g., "org.parley.theme.dark" -> "dark")
                var themeName = themeId.Split('.').LastOrDefault() ?? "unknown";

                UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"GetTheme: {themeId} (isDark={isDark})");

                return Task.FromResult(new GetThemeResponse
                {
                    ThemeId = themeId,
                    ThemeName = themeName,
                    IsDark = isDark
                });
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"GetTheme failed: {ex.Message}");
                return Task.FromResult(new GetThemeResponse
                {
                    ThemeId = "org.parley.theme.light",
                    ThemeName = "light",
                    IsDark = false
                });
            }
        }

        public override Task<GetSpeakerColorsResponse> GetSpeakerColors(GetSpeakerColorsRequest request, ServerCallContext context)
        {
            try
            {
                var response = new GetSpeakerColorsResponse
                {
                    PcColor = SpeakerVisualHelper.GetSpeakerColor("", isPC: true),
                    OwnerColor = SpeakerVisualHelper.GetSpeakerColor("", isPC: false)
                };

                // Get colors for all unique speakers in the current dialog
                var dialogContext = DialogContextService.Instance;
                if (dialogContext.CurrentDialog != null)
                {
                    var speakers = dialogContext.CurrentDialog.Entries
                        .Select(e => e.Speaker)
                        .Where(s => !string.IsNullOrEmpty(s))
                        .Distinct();

                    foreach (var speaker in speakers)
                    {
                        if (!string.IsNullOrEmpty(speaker))
                        {
                            response.SpeakerColors[speaker] = SpeakerVisualHelper.GetSpeakerColor(speaker, isPC: false);
                        }
                    }
                }

                UnifiedLogger.LogPlugin(LogLevel.DEBUG,
                    $"GetSpeakerColors: PC={response.PcColor}, Owner={response.OwnerColor}, {response.SpeakerColors.Count} named speakers");

                return Task.FromResult(response);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"GetSpeakerColors failed: {ex.Message}");
                return Task.FromResult(new GetSpeakerColorsResponse
                {
                    PcColor = SpeakerVisualHelper.ColorPalette.Blue,
                    OwnerColor = SpeakerVisualHelper.ColorPalette.Orange
                });
            }
        }
    }

    /// <summary>
    /// Dialog service implementation
    /// </summary>
    internal class DialogServiceImpl : Proto.DialogService.DialogServiceBase
    {
        public override Task<GetCurrentDialogResponse> GetCurrentDialog(GetCurrentDialogRequest request, ServerCallContext context)
        {
            try
            {
                var dialogContext = DialogContextService.Instance;
                var dialog = dialogContext.CurrentDialog;

                if (dialog == null)
                {
                    return Task.FromResult(new GetCurrentDialogResponse
                    {
                        DialogId = "",
                        DialogName = ""
                    });
                }

                return Task.FromResult(new GetCurrentDialogResponse
                {
                    DialogId = dialogContext.CurrentFileName ?? "untitled",
                    DialogName = System.IO.Path.GetFileNameWithoutExtension(dialogContext.CurrentFileName ?? "Untitled")
                });
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Get current dialog failed: {ex.Message}");
                return Task.FromResult(new GetCurrentDialogResponse());
            }
        }

        public override Task<GetSelectedNodeResponse> GetSelectedNode(GetSelectedNodeRequest request, ServerCallContext context)
        {
            try
            {
                var dialogContext = DialogContextService.Instance;
                var nodeId = dialogContext.SelectedNodeId;

                return Task.FromResult(new GetSelectedNodeResponse
                {
                    NodeId = nodeId ?? "",
                    NodeText = "" // TODO: Look up actual node text
                });
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Get selected node failed: {ex.Message}");
                return Task.FromResult(new GetSelectedNodeResponse());
            }
        }

        /// <summary>
        /// Select a node programmatically from a plugin (Epic 40 Phase 3 / #234).
        /// </summary>
        public override Task<SelectNodeResponse> SelectNode(SelectNodeRequest request, ServerCallContext context)
        {
            try
            {
                var dialogContext = DialogContextService.Instance;
                var nodeId = request.NodeId;

                UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"SelectNode request: {nodeId}");

                if (string.IsNullOrEmpty(nodeId))
                {
                    return Task.FromResult(new SelectNodeResponse
                    {
                        Success = false,
                        ErrorMessage = "Node ID is required"
                    });
                }

                // Request node selection - this raises an event for the View layer
                bool success = dialogContext.RequestNodeSelection(nodeId);

                if (success)
                {
                    UnifiedLogger.LogPlugin(LogLevel.DEBUG, $"SelectNode: Requested selection of {nodeId}");
                }
                else
                {
                    UnifiedLogger.LogPlugin(LogLevel.WARN, $"SelectNode: Invalid node ID {nodeId}");
                }

                return Task.FromResult(new SelectNodeResponse
                {
                    Success = success,
                    ErrorMessage = success ? "" : $"Invalid node ID: {nodeId}"
                });
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"SelectNode failed: {ex.Message}");
                return Task.FromResult(new SelectNodeResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        public override Task<GetDialogStructureResponse> GetDialogStructure(GetDialogStructureRequest request, ServerCallContext context)
        {
            try
            {
                var dialogContext = DialogContextService.Instance;
                var dialog = dialogContext.CurrentDialog;

                if (dialog == null)
                {
                    return Task.FromResult(new GetDialogStructureResponse
                    {
                        Success = false,
                        ErrorMessage = "No dialog loaded"
                    });
                }

                var (nodes, links) = dialogContext.GetDialogStructure();

                var response = new GetDialogStructureResponse
                {
                    Success = true,
                    DialogId = dialogContext.CurrentFileName ?? "untitled",
                    DialogName = System.IO.Path.GetFileNameWithoutExtension(dialogContext.CurrentFileName ?? "Untitled")
                };

                foreach (var node in nodes)
                {
                    response.Nodes.Add(new DialogNodeProto
                    {
                        Id = node.Id,
                        Type = node.Type,
                        Text = node.Text,
                        Speaker = node.Speaker,
                        IsLink = node.IsLink,
                        LinkTarget = node.LinkTarget,
                        HasCondition = node.HasCondition,
                        HasAction = node.HasAction,
                        ConditionScript = node.ConditionScript,
                        ActionScript = node.ActionScript
                    });
                }

                foreach (var link in links)
                {
                    response.Links.Add(new DialogLinkProto
                    {
                        Source = link.Source,
                        Target = link.Target,
                        HasCondition = link.HasCondition,
                        ConditionScript = link.ConditionScript
                    });
                }

                // Reduce log spam - only log on errors (#235)

                return Task.FromResult(response);
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Get dialog structure failed: {ex.Message}");
                return Task.FromResult(new GetDialogStructureResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }
    }

    /// <summary>
    /// File service implementation
    /// </summary>
    internal class FileServiceImpl : Proto.FileService.FileServiceBase
    {
        public override Task<OpenFileDialogResponse> OpenFileDialog(OpenFileDialogRequest request, ServerCallContext context)
        {
            try
            {
                // See #104 - Implement sandboxed file I/O for plugins

                return Task.FromResult(new OpenFileDialogResponse
                {
                    FilePath = "",
                    Cancelled = true
                });
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Open file dialog failed: {ex.Message}");
                return Task.FromResult(new OpenFileDialogResponse
                {
                    Cancelled = true
                });
            }
        }

        public override Task<SaveFileDialogResponse> SaveFileDialog(SaveFileDialogRequest request, ServerCallContext context)
        {
            try
            {
                // See #104 - Implement sandboxed file I/O for plugins

                return Task.FromResult(new SaveFileDialogResponse
                {
                    FilePath = "",
                    Cancelled = true
                });
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Save file dialog failed: {ex.Message}");
                return Task.FromResult(new SaveFileDialogResponse
                {
                    Cancelled = true
                });
            }
        }

        public override Task<ReadFileResponse> ReadFile(ReadFileRequest request, ServerCallContext context)
        {
            try
            {
                // See #104 - Implement sandboxed file I/O for plugins

                UnifiedLogger.LogPlugin(LogLevel.WARN, $"Plugin attempted to read file: {request.FilePath}");

                return Task.FromResult(new ReadFileResponse
                {
                    Success = false,
                    ErrorMessage = "Not implemented"
                });
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Read file failed: {ex.Message}");
                return Task.FromResult(new ReadFileResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }

        public override Task<WriteFileResponse> WriteFile(WriteFileRequest request, ServerCallContext context)
        {
            try
            {
                // See #104 - Implement sandboxed file I/O for plugins

                UnifiedLogger.LogPlugin(LogLevel.WARN, $"Plugin attempted to write file: {request.FilePath}");

                return Task.FromResult(new WriteFileResponse
                {
                    Success = false,
                    ErrorMessage = "Not implemented"
                });
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Write file failed: {ex.Message}");
                return Task.FromResult(new WriteFileResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                });
            }
        }
    }
}
