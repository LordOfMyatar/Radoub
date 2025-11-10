using System;
using System.Threading.Tasks;
using Grpc.Core;
using DialogEditor.Plugins.Proto;
using DialogEditor.Plugins.Security;
using DialogEditor.Services;

namespace DialogEditor.Plugins.Services
{
    /// <summary>
    /// UI service implementation for plugins
    /// </summary>
    public class PluginUIService : UIService.UIServiceBase
    {
        private readonly PluginSecurityContext _security;

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

                // TODO: Implement actual UI notification when UI framework is available
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

                // TODO: Implement actual UI dialog when UI framework is available
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
    }
}
