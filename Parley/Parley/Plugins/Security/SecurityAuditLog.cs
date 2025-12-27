using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using DialogEditor.Services;
using Radoub.Formats.Logging;

namespace DialogEditor.Plugins.Security
{
    /// <summary>
    /// Security audit log for tracking plugin security events
    /// </summary>
    public class SecurityAuditLog
    {
        private readonly ConcurrentQueue<SecurityEvent> _events = new();
        private readonly int _maxEvents;

        public SecurityAuditLog(int maxEvents = 1000)
        {
            _maxEvents = maxEvents;
        }

        /// <summary>
        /// Log a permission denial
        /// </summary>
        public void LogPermissionDenied(string pluginId, string permission, string operation)
        {
            LogEvent(new SecurityEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = SecurityEventType.PermissionDenied,
                PluginId = pluginId,
                Details = $"Permission '{permission}' denied for operation '{operation}'"
            });

            UnifiedLogger.LogPlugin(LogLevel.WARN,
                $"SECURITY: Permission denied - Plugin: {pluginId}, Permission: {permission}, Operation: {operation}");
        }

        /// <summary>
        /// Log a rate limit violation
        /// </summary>
        public void LogRateLimitViolation(string pluginId, string operation, int callCount, int limit)
        {
            LogEvent(new SecurityEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = SecurityEventType.RateLimitViolation,
                PluginId = pluginId,
                Details = $"Rate limit exceeded for '{operation}': {callCount}/{limit}"
            });

            UnifiedLogger.LogPlugin(LogLevel.WARN,
                $"SECURITY: Rate limit violation - Plugin: {pluginId}, Operation: {operation}, Count: {callCount}/{limit}");
        }

        /// <summary>
        /// Log a sandbox violation (attempted access outside sandbox)
        /// </summary>
        public void LogSandboxViolation(string pluginId, string attemptedPath)
        {
            LogEvent(new SecurityEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = SecurityEventType.SandboxViolation,
                PluginId = pluginId,
                Details = $"Attempted access outside sandbox: {attemptedPath}"
            });

            UnifiedLogger.LogPlugin(LogLevel.ERROR,
                $"SECURITY: Sandbox violation - Plugin: {pluginId}, Path: {UnifiedLogger.SanitizePath(attemptedPath)}");
        }

        /// <summary>
        /// Log a plugin timeout
        /// </summary>
        public void LogTimeout(string pluginId, string operation, TimeSpan duration)
        {
            LogEvent(new SecurityEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = SecurityEventType.Timeout,
                PluginId = pluginId,
                Details = $"Operation '{operation}' timed out after {duration.TotalSeconds:F1}s"
            });

            UnifiedLogger.LogPlugin(LogLevel.WARN,
                $"SECURITY: Timeout - Plugin: {pluginId}, Operation: {operation}, Duration: {duration.TotalSeconds:F1}s");
        }

        /// <summary>
        /// Log a plugin crash
        /// </summary>
        public void LogCrash(string pluginId, string reason)
        {
            LogEvent(new SecurityEvent
            {
                Timestamp = DateTime.UtcNow,
                EventType = SecurityEventType.Crash,
                PluginId = pluginId,
                Details = $"Plugin crashed: {reason}"
            });

            UnifiedLogger.LogPlugin(LogLevel.ERROR,
                $"SECURITY: Plugin crash - Plugin: {pluginId}, Reason: {reason}");
        }

        /// <summary>
        /// Get recent events for a plugin
        /// </summary>
        public List<SecurityEvent> GetEvents(string? pluginId = null, int maxCount = 100)
        {
            var events = _events.ToList();

            if (!string.IsNullOrEmpty(pluginId))
            {
                events = events.Where(e => e.PluginId == pluginId).ToList();
            }

            return events.OrderByDescending(e => e.Timestamp).Take(maxCount).ToList();
        }

        /// <summary>
        /// Get event count by type for a plugin
        /// </summary>
        public Dictionary<SecurityEventType, int> GetEventCounts(string pluginId)
        {
            var events = _events.Where(e => e.PluginId == pluginId);
            return events.GroupBy(e => e.EventType)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        /// <summary>
        /// Clear old events
        /// </summary>
        public void ClearOldEvents(TimeSpan age)
        {
            var cutoff = DateTime.UtcNow - age;
            while (_events.TryPeek(out var evt) && evt.Timestamp < cutoff)
            {
                _events.TryDequeue(out _);
            }
        }

        private void LogEvent(SecurityEvent evt)
        {
            _events.Enqueue(evt);

            // Trim if exceeds max
            while (_events.Count > _maxEvents)
            {
                _events.TryDequeue(out _);
            }
        }
    }

    public class SecurityEvent
    {
        public DateTime Timestamp { get; set; }
        public SecurityEventType EventType { get; set; }
        public string PluginId { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
    }

    public enum SecurityEventType
    {
        PermissionDenied,
        RateLimitViolation,
        SandboxViolation,
        Timeout,
        Crash
    }
}
