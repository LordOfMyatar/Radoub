using System;
using System.Collections.Concurrent;
using System.Threading;
using DialogEditor.Services;
using Radoub.Formats.Logging;

namespace DialogEditor.Plugins.Security
{
    /// <summary>
    /// Rate limiter to prevent plugin abuse
    /// </summary>
    public class RateLimiter
    {
        private readonly int _maxCallsPerWindow;
        private readonly TimeSpan _window;
        private readonly ConcurrentDictionary<string, CallWindow> _windows = new();

        public RateLimiter(int maxCallsPerWindow = 1000, TimeSpan? window = null)
        {
            _maxCallsPerWindow = maxCallsPerWindow;
            _window = window ?? TimeSpan.FromMinutes(1);
        }

        /// <summary>
        /// Check if a call is allowed for a plugin
        /// </summary>
        public bool AllowCall(string pluginId, string operation)
        {
            var key = $"{pluginId}:{operation}";
            var now = DateTime.UtcNow;

            var window = _windows.GetOrAdd(key, _ => new CallWindow(now));

            lock (window)
            {
                // Reset window if expired
                if (now - window.WindowStart > _window)
                {
                    window.WindowStart = now;
                    window.CallCount = 0;
                }

                // Check limit
                if (window.CallCount >= _maxCallsPerWindow)
                {
                    UnifiedLogger.LogPlugin(LogLevel.WARN,
                        $"Rate limit exceeded for {pluginId} on {operation}: {window.CallCount}/{_maxCallsPerWindow} calls");
                    return false;
                }

                window.CallCount++;
                return true;
            }
        }

        /// <summary>
        /// Get current call count for a plugin operation
        /// </summary>
        public int GetCallCount(string pluginId, string operation)
        {
            var key = $"{pluginId}:{operation}";
            if (_windows.TryGetValue(key, out var window))
            {
                lock (window)
                {
                    var now = DateTime.UtcNow;
                    if (now - window.WindowStart > _window)
                    {
                        return 0;
                    }
                    return window.CallCount;
                }
            }
            return 0;
        }

        /// <summary>
        /// Reset rate limit for a plugin
        /// </summary>
        public void Reset(string pluginId)
        {
            foreach (var key in _windows.Keys)
            {
                if (key.StartsWith($"{pluginId}:"))
                {
                    _windows.TryRemove(key, out _);
                }
            }
        }

        private class CallWindow
        {
            public DateTime WindowStart { get; set; }
            public int CallCount { get; set; }

            public CallWindow(DateTime start)
            {
                WindowStart = start;
                CallCount = 0;
            }
        }
    }
}
