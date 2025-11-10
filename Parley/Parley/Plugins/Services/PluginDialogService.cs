using System;
using System.Threading.Tasks;
using Grpc.Core;
using DialogEditor.Plugins.Proto;
using DialogEditor.Plugins.Security;
using DialogEditor.Services;

namespace DialogEditor.Plugins.Services
{
    /// <summary>
    /// Dialog service implementation for plugins
    /// </summary>
    public class PluginDialogService : DialogService.DialogServiceBase
    {
        private readonly PluginSecurityContext _security;

        public PluginDialogService(PluginSecurityContext security)
        {
            _security = security ?? throw new ArgumentNullException(nameof(security));
        }

        public override Task<GetCurrentDialogResponse> GetCurrentDialog(GetCurrentDialogRequest request, ServerCallContext context)
        {
            try
            {
                // Check security (permission + rate limit)
                _security.CheckSecurity("dialog.read", "GetCurrentDialog");

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
                // Permission denial already logged by CheckSecurity
                throw new RpcException(new Status(StatusCode.PermissionDenied, ex.Message));
            }
            catch (RateLimitExceededException ex)
            {
                throw new RpcException(new Status(StatusCode.ResourceExhausted, ex.Message));
            }
        }

        public override Task<GetSelectedNodeResponse> GetSelectedNode(GetSelectedNodeRequest request, ServerCallContext context)
        {
            try
            {
                // Check security (permission + rate limit)
                _security.CheckSecurity("dialog.read", "GetSelectedNode");

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
