using System;
using System.Threading.Tasks;
using Grpc.Core;
using DialogEditor.Plugins.Proto;
using DialogEditor.Services;

namespace DialogEditor.Plugins.Services
{
    /// <summary>
    /// Dialog service implementation for plugins
    /// </summary>
    public class PluginDialogService : DialogService.DialogServiceBase
    {
        private readonly PermissionChecker _permissions;

        public PluginDialogService(PermissionChecker permissions)
        {
            _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
        }

        public override Task<GetCurrentDialogResponse> GetCurrentDialog(GetCurrentDialogRequest request, ServerCallContext context)
        {
            try
            {
                // Check permission
                _permissions.RequirePermission("dialog.read");

                // TODO: Implement actual dialog access when MainViewModel integration is available
                // For now, return placeholder data

                UnifiedLogger.LogPlugin(LogLevel.INFO, "Plugin requested current dialog");

                return Task.FromResult(new GetCurrentDialogResponse
                {
                    DialogId = "placeholder",
                    DialogName = "No dialog loaded"
                });
            }
            catch (PermissionDeniedException ex)
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
            }
        }

        public override Task<GetSelectedNodeResponse> GetSelectedNode(GetSelectedNodeRequest request, ServerCallContext context)
        {
            try
            {
                // Check permission
                _permissions.RequirePermission("dialog.read");

                // TODO: Implement actual node selection access when MainViewModel integration is available
                // For now, return placeholder data

                UnifiedLogger.LogPlugin(LogLevel.INFO, "Plugin requested selected node");

                return Task.FromResult(new GetSelectedNodeResponse
                {
                    NodeId = "none",
                    NodeText = "No node selected"
                });
            }
            catch (PermissionDeniedException ex)
            {
                throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
            }
        }
    }
}
