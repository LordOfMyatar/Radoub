using System;
using System.IO;
using System.Threading.Tasks;
using Grpc.Core;
using DialogEditor.Plugins.Proto;
using DialogEditor.Plugins.Security;
using DialogEditor.Services;
using Radoub.Formats.Logging;

namespace DialogEditor.Plugins.Services
{
    /// <summary>
    /// Audio service implementation for plugins
    /// </summary>
    public class PluginAudioService : Proto.AudioService.AudioServiceBase
    {
        private readonly PluginSecurityContext _security;
        private readonly global::DialogEditor.Services.AudioService _audioService;

        public PluginAudioService(PluginSecurityContext security, global::DialogEditor.Services.AudioService audioService)
        {
            _security = security ?? throw new ArgumentNullException(nameof(security));
            _audioService = audioService ?? throw new ArgumentNullException(nameof(audioService));
        }

        public override async Task<PlayAudioResponse> PlayAudio(PlayAudioRequest request, ServerCallContext context)
        {
            try
            {
                // Check security (permission + rate limit)
                _security.CheckSecurity("audio.play", "PlayAudio");

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
                // Permission denial already logged by CheckSecurity
                throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
            }
            catch (RateLimitExceededException ex)
            {
                throw new RpcException(new Status(StatusCode.ResourceExhausted, ex.Message));
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
                // Check security (permission + rate limit)
                _security.CheckSecurity("audio.play", "StopAudio");

                _audioService.Stop();

                UnifiedLogger.LogPlugin(LogLevel.INFO, "Plugin stopped audio");

                return Task.FromResult(new StopAudioResponse
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
    }
}
