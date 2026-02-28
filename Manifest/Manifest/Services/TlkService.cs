using Radoub.UI.Services;

namespace Manifest.Services;

/// <summary>
/// Singleton wrapper around the shared Radoub.UI.Services.TlkService.
/// Provides backwards-compatible static Instance access for Manifest.
/// Issue #1286 - Consolidated from separate Manifest implementation.
/// </summary>
public static class TlkService
{
    private static Radoub.UI.Services.TlkService? _instance;
    private static readonly object _lock = new();

    /// <summary>
    /// Singleton instance of the shared TlkService, with settings integration enabled.
    /// </summary>
    public static Radoub.UI.Services.TlkService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    if (_instance == null)
                    {
                        _instance = new Radoub.UI.Services.TlkService();
                        _instance.EnableSettingsIntegration();
                    }
                }
            }
            return _instance;
        }
    }

}
