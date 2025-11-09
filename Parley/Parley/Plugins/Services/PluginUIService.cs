using System;
using System.Threading.Tasks;
using Grpc.Core;
using DialogEditor.Plugins.Proto;
using DialogEditor.Services;

namespace DialogEditor.Plugins.Services
{
    /// <summary>
    /// UI service implementation for plugins
    /// </summary>
    public class PluginUIService : UIService.UIServiceBase
    {
        private readonly PermissionChecker _permissions;

        public PluginUIService(PermissionChecker permissions)
        {
            _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        }

        public override Task<ShowNotificationResponse> ShowNotification(ShowNotificationRequest request, ServerCallContext context)
        {
            try
            {
                // Check permission
                _permissions.RequirePermission("ui.show_notification");

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
                throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
            }
        }

        public override Task<ShowDialogResponse> ShowDialog(ShowDialogRequest request, ServerCallContext context)
        {
            try
            {
                // Check permission
                _permissions.RequirePermission("ui.show_dialog");

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
                throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
            }
        }
    }
}
