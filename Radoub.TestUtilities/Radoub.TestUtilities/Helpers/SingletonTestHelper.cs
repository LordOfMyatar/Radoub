using System.Reflection;

namespace Radoub.TestUtilities.Helpers;

/// <summary>
/// Provides test-only utilities for resetting singleton instances.
/// Uses reflection to reset private static fields without polluting production APIs.
///
/// This replaces the ResetForTesting() / ConfigureForTesting() anti-pattern
/// that was previously embedded in production SettingsService classes.
///
/// Usage:
///   SingletonTestHelper.ResetSingleton&lt;SettingsService&gt;();
///   SingletonTestHelper.ResetSingleton&lt;SettingsService&gt;("_instance", "_testSettingsDirectory");
/// </summary>
public static class SingletonTestHelper
{
    /// <summary>
    /// Reset a singleton by clearing its static _instance field.
    /// </summary>
    /// <typeparam name="T">The singleton type</typeparam>
    public static void ResetSingleton<T>() where T : class
    {
        ResetSingleton<T>("_instance");
    }

    /// <summary>
    /// Reset a singleton by clearing one or more static fields.
    /// Acquires the class's _lock object if present for thread safety.
    /// </summary>
    /// <typeparam name="T">The singleton type</typeparam>
    /// <param name="fieldNames">Names of static fields to set to null</param>
    public static void ResetSingleton<T>(params string[] fieldNames) where T : class
    {
        var type = typeof(T);
        var flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        // Try to acquire the lock if the class has one
        var lockField = type.GetField("_lock", flags);
        var lockObj = lockField?.GetValue(null);

        if (lockObj != null)
        {
            lock (lockObj)
            {
                ClearFields(type, flags, fieldNames);
            }
        }
        else
        {
            ClearFields(type, flags, fieldNames);
        }
    }

    /// <summary>
    /// Reset a static class singleton (like Manifest's TlkService).
    /// Disposes the instance if it implements IDisposable.
    /// </summary>
    /// <param name="type">The static class type</param>
    /// <param name="fieldNames">Names of static fields to clear</param>
    public static void ResetStaticSingleton(Type type, params string[] fieldNames)
    {
        var flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

        var lockField = type.GetField("_lock", flags);
        var lockObj = lockField?.GetValue(null);

        if (lockObj != null)
        {
            lock (lockObj)
            {
                DisposeAndClearFields(type, flags, fieldNames);
            }
        }
        else
        {
            DisposeAndClearFields(type, flags, fieldNames);
        }
    }

    /// <summary>
    /// Configure a singleton's settings directory by setting the environment variable
    /// that BaseToolSettingsService checks during construction.
    /// </summary>
    /// <param name="envVarName">Environment variable name (e.g., "FENCE_SETTINGS_DIR")</param>
    /// <param name="directory">The test directory path, or null to clear</param>
    public static void ConfigureSettingsDirectory(string envVarName, string? directory)
    {
        Environment.SetEnvironmentVariable(envVarName, directory);
    }

    private static void ClearFields(Type type, BindingFlags flags, string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            var field = type.GetField(fieldName, flags);
            if (field != null && !field.IsInitOnly)
            {
                field.SetValue(null, null);
            }
        }
    }

    private static void DisposeAndClearFields(Type type, BindingFlags flags, string[] fieldNames)
    {
        foreach (var fieldName in fieldNames)
        {
            var field = type.GetField(fieldName, flags);
            if (field != null && !field.IsInitOnly)
            {
                var value = field.GetValue(null);
                if (value is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                field.SetValue(null, null);
            }
        }
    }
}
