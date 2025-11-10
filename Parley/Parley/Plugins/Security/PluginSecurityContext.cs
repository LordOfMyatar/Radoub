using System;
using DialogEditor.Services;

namespace DialogEditor.Plugins.Security
{
    /// <summary>
    /// Security context for a plugin, combining permission checking, rate limiting, and audit logging
    /// </summary>
    public class PluginSecurityContext
    {
        private readonly string _pluginId;
        private readonly PermissionChecker _permissions;
        private readonly RateLimiter _rateLimiter;
        private readonly SecurityAuditLog _auditLog;

        public string PluginId => _pluginId;

        public PluginSecurityContext(
            string pluginId,
            PermissionChecker permissions,
            RateLimiter rateLimiter,
            SecurityAuditLog auditLog)
        {
            _pluginId = pluginId ?? throw new ArgumentNullException(nameof(pluginId));
            _permissions = permissions ?? throw new ArgumentNullException(nameof(permissions));
            _rateLimiter = rateLimiter ?? throw new ArgumentNullException(nameof(rateLimiter));
            _auditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));
        }

        /// <summary>
        /// Check permission, rate limit, and log security events
        /// </summary>
        public void CheckSecurity(string permission, string operation)
        {
            // Check permission first
            try
            {
                _permissions.RequirePermission(permission);
            }
            catch (PermissionDeniedException)
            {
                _auditLog.LogPermissionDenied(_pluginId, permission, operation);
                throw;
            }

            // Check rate limit
            if (!_rateLimiter.AllowCall(_pluginId, operation))
            {
                var callCount = _rateLimiter.GetCallCount(_pluginId, operation);
                _auditLog.LogRateLimitViolation(_pluginId, operation, callCount, 1000);

                throw new RateLimitExceededException(
                    $"Rate limit exceeded for plugin '{_pluginId}' on operation '{operation}'");
            }
        }

        /// <summary>
        /// Log a sandbox violation
        /// </summary>
        public void LogSandboxViolation(string attemptedPath)
        {
            _auditLog.LogSandboxViolation(_pluginId, attemptedPath);
        }

        /// <summary>
        /// Log a timeout
        /// </summary>
        public void LogTimeout(string operation, TimeSpan duration)
        {
            _auditLog.LogTimeout(_pluginId, operation, duration);
        }
    }

    /// <summary>
    /// Exception thrown when rate limit is exceeded
    /// </summary>
    public class RateLimitExceededException : Exception
    {
        public RateLimitExceededException(string message) : base(message)
        {
        }

        public RateLimitExceededException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
