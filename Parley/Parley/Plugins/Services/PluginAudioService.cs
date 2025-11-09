using System;
using System.IO;
using System.Threading.Tasks;
using Grpc.Core;
using DialogEditor.Plugins.Proto;
using DialogEditor.Services;

namespace DialogEditor.Plugins.Services
{
    /// <summary>
    /// Audio service implementation for plugins
    /// </summary>
    public class PluginAudioService : Proto.AudioService.AudioServiceBase
    {
        private readonly PermissionChecker _permissions;
        private readonly global::DialogEditor.Services.AudioService _audioService;

        public PluginAudioService(PermissionChecker permissions, global::DialogEditor.Services.AudioService audioService)
        {
            _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
        }

        public override async Task<PlayAudioResponse> PlayAudio(PlayAudioRequest request, ServerCallContext context)
        {
            try
            {
                // Check permission
                _permissions.RequirePermission("audio.play");

                // Validate file path
                if (string.IsNullOrWhiteSpace(request.FilePath))
                {
                    return new PlayAudioResponse
                    {
                        Success = false,
                        ErrorMessage = "File path is required"
                    };
                }

                if (!File.Exists(request.FilePath))
                {
                    return new PlayAudioResponse
                    {
                        Success = false,
                        ErrorMessage = $"File not found: {request.FilePath}"
                    };
                }

                // Play audio
                await Task.Run(() => _audioService.Play(request.FilePath));

                UnifiedLogger.LogPlugin(LogLevel.INFO, $"Plugin played audio: {request.FilePath}");

                return new PlayAudioResponse
                {
                    Success = true
                };
            }
            catch (PermissionDeniedException ex)
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogPlugin(LogLevel.ERROR, $"Error playing audio: {ex.Message}");
                return new PlayAudioResponse
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public override Task<StopAudioResponse> StopAudio(StopAudioRequest request, ServerCallContext context)
        {
            try
            {
                // Check permission
                _permissions.RequirePermission("audio.play");

                _audioService.Stop();

                UnifiedLogger.LogPlugin(LogLevel.INFO, "Plugin stopped audio");

                return Task.FromResult(new StopAudioResponse
                {
                    Success = true
                });
            }
            catch (PermissionDeniedException ex)
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
            }
        }
    }
}
