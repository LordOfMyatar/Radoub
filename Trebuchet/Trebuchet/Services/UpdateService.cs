using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Radoub.Formats.Logging;

namespace RadoubLauncher.Services;

/// <summary>
/// Service for checking and applying updates from GitHub releases.
/// </summary>
public class UpdateService : INotifyPropertyChanged, IDisposable
{
    private static UpdateService? _instance;
    public static UpdateService Instance => _instance ??= new UpdateService();

    private bool _disposed;

    private const string GitHubOwner = "LordOfMyatar";
    private const string GitHubRepo = "Radoub";
    private const string ReleasesApiUrl = $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";

    private readonly HttpClient _httpClient;
    private bool _isChecking;
    private bool _updateAvailable;
    private string _currentVersion = "";
    private string _latestVersion = "";
    private string _releaseNotes = "";
    private string _downloadUrl = "";
    private string _releaseUrl = "";
    private DateTime _lastChecked = DateTime.MinValue;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool IsChecking
    {
        get => _isChecking;
        private set { _isChecking = value; OnPropertyChanged(); }
    }

    public bool UpdateAvailable
    {
        get => _updateAvailable;
        private set { _updateAvailable = value; OnPropertyChanged(); }
    }

    public string CurrentVersion
    {
        get => _currentVersion;
        private set { _currentVersion = value; OnPropertyChanged(); }
    }

    public string LatestVersion
    {
        get => _latestVersion;
        private set { _latestVersion = value; OnPropertyChanged(); }
    }

    public string ReleaseNotes
    {
        get => _releaseNotes;
        private set { _releaseNotes = value; OnPropertyChanged(); }
    }

    public string DownloadUrl
    {
        get => _downloadUrl;
        private set { _downloadUrl = value; OnPropertyChanged(); }
    }

    public string ReleaseUrl
    {
        get => _releaseUrl;
        private set { _releaseUrl = value; OnPropertyChanged(); }
    }

    public DateTime LastChecked
    {
        get => _lastChecked;
        private set { _lastChecked = value; OnPropertyChanged(); }
    }

    private UpdateService()
    {
        // No client-wide timeout: downloads can legitimately run long. The update *check*
        // applies its own 15s budget via a linked CTS (#2248); the streamed download relies
        // on caller cancellation.
        _httpClient = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Radoub-Trebuchet");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");

        LoadCurrentVersion();
    }

    private void LoadCurrentVersion()
    {
        try
        {
            // Use InformationalVersion for semantic version with suffix (e.g., "0.1.0-alpha")
            var infoVersion = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

            if (!string.IsNullOrEmpty(infoVersion))
            {
                // Strip git hash if present (e.g., "0.1.0-alpha+abc123" -> "0.1.0-alpha")
                var plusIndex = infoVersion.IndexOf('+');
                if (plusIndex > 0)
                    infoVersion = infoVersion[..plusIndex];

                CurrentVersion = infoVersion;
            }
            else
            {
                // Fallback to assembly version
                var version = Assembly.GetExecutingAssembly().GetName().Version;
                if (version != null)
                {
                    CurrentVersion = $"{version.Major}.{version.Minor}.{version.Build}";
                }
                else
                {
                    CurrentVersion = "0.0.0";
                }
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to get current version: {ex.Message}");
            CurrentVersion = "0.0.0";
        }
    }

    /// <summary>
    /// Check for updates from GitHub releases.
    /// </summary>
    public async Task<bool> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        if (IsChecking) return false;

        IsChecking = true;
        UpdateAvailable = false;

        try
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, "Checking for updates...");

            // 15s budget for the check so a flaky network can't hang it (#2248).
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(15));

            var response = await _httpClient.GetAsync(ReleasesApiUrl, timeoutCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, $"Failed to check for updates: {response.StatusCode}");
                return false;
            }

            var release = await response.Content.ReadFromJsonAsync<GitHubRelease>(timeoutCts.Token);
            if (release == null)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN, "Failed to parse release response");
                return false;
            }

            LastChecked = DateTime.Now;

            // Parse version from tag (e.g., "v1.0.0" -> "1.0.0")
            var tagVersion = release.TagName?.TrimStart('v', 'V') ?? "0.0.0";
            LatestVersion = tagVersion;
            ReleaseNotes = release.Body ?? "";
            ReleaseUrl = release.HtmlUrl ?? "";

            // Find appropriate download asset
            DownloadUrl = FindDownloadAsset(release.Assets);

            // Compare versions
            if (IsNewerVersion(tagVersion, CurrentVersion))
            {
                UpdateAvailable = true;
                UnifiedLogger.LogApplication(LogLevel.INFO, $"Update available: {CurrentVersion} -> {LatestVersion}");
                return true;
            }

            UnifiedLogger.LogApplication(LogLevel.INFO, $"No update available (current: {CurrentVersion}, latest: {LatestVersion})");
            return false;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "Update check timed out after 15s");
            return false;
        }
        catch (OperationCanceledException)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Update check cancelled");
            return false;
        }
        catch (HttpRequestException ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, $"Network error checking for updates: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Error checking for updates: {ex.Message}");
            return false;
        }
        finally
        {
            IsChecking = false;
        }
    }

    /// <summary>
    /// Find the appropriate download asset for the current platform.
    /// </summary>
    private string FindDownloadAsset(List<GitHubAsset>? assets)
    {
        if (assets == null || assets.Count == 0)
            return "";

        // Determine platform suffix
        string platformSuffix;
        if (OperatingSystem.IsWindows())
            platformSuffix = "win";
        else if (OperatingSystem.IsMacOS())
            platformSuffix = "osx";
        else if (OperatingSystem.IsLinux())
            platformSuffix = "linux";
        else
            platformSuffix = "";

        // Look for platform-specific asset first
        foreach (var asset in assets)
        {
            var name = asset.Name?.ToLowerInvariant() ?? "";
            if (name.Contains("trebuchet") && name.Contains(platformSuffix) && name.EndsWith(".zip"))
            {
                return asset.BrowserDownloadUrl ?? "";
            }
        }

        // Fall back to any Trebuchet zip
        foreach (var asset in assets)
        {
            var name = asset.Name?.ToLowerInvariant() ?? "";
            if (name.Contains("trebuchet") && name.EndsWith(".zip"))
            {
                return asset.BrowserDownloadUrl ?? "";
            }
        }

        // Fall back to first zip
        foreach (var asset in assets)
        {
            var name = asset.Name?.ToLowerInvariant() ?? "";
            if (name.EndsWith(".zip"))
            {
                return asset.BrowserDownloadUrl ?? "";
            }
        }

        return "";
    }

    /// <summary>
    /// Compare two version strings to determine if the first is newer.
    /// </summary>
    internal static bool IsNewerVersion(string latest, string current)
    {
        // Strip prerelease (-alpha) and build-metadata (+gitHash) suffixes so a segment
        // like "0+abc123" doesn't blow up the numeric compare. Unparseable segments are
        // treated as 0 via TryParse rather than throwing and silently suppressing the
        // update (#2248).
        var latestClean = StripSuffixes(latest);
        var currentClean = StripSuffixes(current);

        var latestParts = latestClean.Split('.');
        var currentParts = currentClean.Split('.');

        for (int i = 0; i < Math.Max(latestParts.Length, currentParts.Length); i++)
        {
            int latestNum = i < latestParts.Length && int.TryParse(latestParts[i], out var ln) ? ln : 0;
            int currentNum = i < currentParts.Length && int.TryParse(currentParts[i], out var cn) ? cn : 0;

            if (latestNum > currentNum) return true;
            if (latestNum < currentNum) return false;
        }

        // If base versions are equal, a released version (no -prerelease suffix) is
        // newer than an alpha/beta.
        bool latestIsPrerelease = latest.Contains('-');
        bool currentIsPrerelease = current.Contains('-');

        if (!latestIsPrerelease && currentIsPrerelease) return true;

        return false;
    }

    private static string StripSuffixes(string version)
    {
        var s = version.Split('-')[0];
        var plus = s.IndexOf('+');
        return plus >= 0 ? s[..plus] : s;
    }

    /// <summary>
    /// Open the release page in the default browser.
    /// </summary>
    public void OpenReleasePage()
    {
        if (string.IsNullOrEmpty(ReleaseUrl)) return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = ReleaseUrl,
                UseShellExecute = true
            })?.Dispose();
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to open release page: {ex.Message}");
        }
    }

    /// <summary>
    /// Download the update to a temporary location.
    /// </summary>
    public async Task<string?> DownloadUpdateAsync(IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(DownloadUrl))
        {
            UnifiedLogger.LogApplication(LogLevel.WARN, "No download URL available");
            return null;
        }

        try
        {
            UnifiedLogger.LogApplication(LogLevel.INFO, $"Downloading update from: {DownloadUrl}");

            using var request = new HttpRequestMessage(HttpMethod.Get, DownloadUrl);
            var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1;
            var tempPath = Path.Combine(Path.GetTempPath(), $"Trebuchet-{LatestVersion}.zip");

            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[8192];
            var totalBytesRead = 0L;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                totalBytesRead += bytesRead;

                if (totalBytes > 0)
                {
                    progress?.Report((double)totalBytesRead / totalBytes);
                }
            }

            UnifiedLogger.LogApplication(LogLevel.INFO, $"Update downloaded to: {UnifiedLogger.SanitizePath(tempPath)}");
            return tempPath;
        }
        catch (OperationCanceledException)
        {
            UnifiedLogger.LogApplication(LogLevel.DEBUG, "Update download cancelled");
            return null;
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.ERROR, $"Failed to download update: {ex.Message}");
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _httpClient.Dispose();

        // Clear the singleton so a later Instance access constructs a fresh service
        // instead of handing back this disposed one (#2248).
        if (ReferenceEquals(_instance, this))
        {
            _instance = null;
        }
    }

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// GitHub release API response model.
/// </summary>
public class GitHubRelease
{
    [JsonPropertyName("tag_name")]
    public string? TagName { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("html_url")]
    public string? HtmlUrl { get; set; }

    [JsonPropertyName("prerelease")]
    public bool Prerelease { get; set; }

    [JsonPropertyName("draft")]
    public bool Draft { get; set; }

    [JsonPropertyName("published_at")]
    public DateTime? PublishedAt { get; set; }

    [JsonPropertyName("assets")]
    public List<GitHubAsset>? Assets { get; set; }
}

/// <summary>
/// GitHub release asset model.
/// </summary>
public class GitHubAsset
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("browser_download_url")]
    public string? BrowserDownloadUrl { get; set; }

    [JsonPropertyName("size")]
    public long Size { get; set; }

    [JsonPropertyName("download_count")]
    public int DownloadCount { get; set; }
}
