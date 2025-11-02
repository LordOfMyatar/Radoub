using System;
using DialogEditor.Services;

namespace DialogEditor.Utils
{
    public static class DebugLogger
    {
        private static dynamic? _mainWindow;

        public static void Initialize(object mainWindow)
        {
            Console.WriteLine($"★★★ [DebugLogger.Initialize] CALLED with mainWindow type: {mainWindow?.GetType().Name ?? "null"}");
            _mainWindow = mainWindow;

            // Initialize UnifiedLogger callback for UI integration
            UnifiedLogger.SetDebugConsoleCallback(message =>
            {
                try
                {
                    Console.WriteLine($"★★★ [DebugLogger CALLBACK] Received: {message?.Substring(0, Math.Min(50, message?.Length ?? 0)) ?? "null"}...");
                    Console.WriteLine($"★★★ [DebugLogger CALLBACK] _mainWindow is null: {_mainWindow == null}");

                    if (_mainWindow != null)
                    {
                        Console.WriteLine($"★★★ [DebugLogger CALLBACK] About to call AddDebugMessage");
                        _mainWindow.AddDebugMessage(message);
                        Console.WriteLine($"★★★ [DebugLogger CALLBACK] AddDebugMessage completed");
                    }

                    Console.WriteLine($"[Debug] {message}"); // Keep console output too
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"★★★ [DebugLogger] ERROR in callback: {ex.Message}");
                    Console.WriteLine($"★★★ [DebugLogger] Stack: {ex.StackTrace}");
                }
            });

            Console.WriteLine("★★★ [DebugLogger.Initialize] Callback registered successfully");
        }
        
        public static void Log(string message)
        {
            try
            {
                // Use UnifiedLogger for session-based organization
                UnifiedLogger.LogApplication(LogLevel.DEBUG, message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG LOG ERROR] {ex.Message}");
            }
        }
        
        public static void LogError(string message, Exception? ex = null)
        {
            var fullMessage = ex != null ? $"{message} - {ex.Message}" : message;
            
            // Use UnifiedLogger for session-based organization
            UnifiedLogger.LogApplication(LogLevel.ERROR, fullMessage);
        }
        
        public static void LogInfo(string message)
        {
            // Use UnifiedLogger for session-based organization
            UnifiedLogger.LogApplication(LogLevel.INFO, message);
        }
        
        public static void LogWarning(string message)
        {
            // Use UnifiedLogger for session-based organization
            UnifiedLogger.LogApplication(LogLevel.WARN, message);
        }
        
        public static void ClearDebugOutput()
        {
            try
            {
                _mainWindow?.ClearDebugOutput();
                Console.WriteLine("[Debug] Clear debug output requested");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG LOG ERROR] Failed to clear debug output: {ex.Message}");
            }
        }
    }
}