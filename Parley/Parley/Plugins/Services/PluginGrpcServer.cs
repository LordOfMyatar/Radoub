using System;
using System.Threading.Tasks;
using DialogEditor.Plugins.Proto;
using DialogEditor.Services;
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

                // TODO: Implement actual audio playback
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

                // TODO: Implement actual audio stop

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

                // Show notification window on UI thread
                Dispatcher.UIThread.Post(() =>
                {
                    var window = new Window
                    {
                        Title = request.Title,
                        Width = 400,
                        Height = 150,
                        Content = new TextBlock
                        {
                            Text = request.Message,
                            Margin = new Avalonia.Thickness(20),
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap
                        }
                    };
                    window.Show();
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

        public override async Task<ShowDialogResponse> ShowDialog(ShowDialogRequest request, ServerCallContext context)
        {
            try
            {
                UnifiedLogger.LogPlugin(LogLevel.INFO, $"Plugin dialog: {request.Title} - {request.Message}");

                // TODO: Implement actual dialog with buttons
                // For now, just show message and return 0

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    var window = new Window
                    {
                        Title = request.Title,
                        Width = 400,
                        Height = 200,
                        Content = new TextBlock
                        {
                            Text = request.Message,
                            Margin = new Avalonia.Thickness(20),
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap
                        }
                    };
                    window.Show();
                });

                return new ShowDialogResponse
                {
                    ButtonIndex = 0
                };
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Show dialog failed: {ex.Message}");
                return new ShowDialogResponse
                {
                    ButtonIndex = -1
                };
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
                // TODO: Get actual current dialog from MainWindow
                // For now, return empty response

                return Task.FromResult(new GetCurrentDialogResponse
                {
                    DialogId = "",
                    DialogName = ""
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
                // TODO: Get actual selected node from MainWindow
                // For now, return empty response

                return Task.FromResult(new GetSelectedNodeResponse
                {
                    NodeId = "",
                    NodeText = ""
                });
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Get selected node failed: {ex.Message}");
                return Task.FromResult(new GetSelectedNodeResponse());
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
                // TODO: Implement actual file dialog

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
                // TODO: Implement actual file dialog

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
                // TODO: Implement sandboxed file reading

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
                // TODO: Implement sandboxed file writing

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
