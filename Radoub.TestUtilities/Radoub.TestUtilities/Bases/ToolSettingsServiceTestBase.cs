using Radoub.TestUtilities.Helpers;
using Xunit;

namespace Radoub.TestUtilities.Bases;

/// <summary>
/// Shared test base for tools whose SettingsService derives from
/// <c>BaseToolSettingsService&lt;TSettings&gt;</c> (Quartermaster, Fence, Manifest,
/// Trebuchet, Relique, Reliquary). Covers the singleton + window-clamp + recent-files +
/// log-retention contract that the base class owns and every tool inherited verbatim (#2464).
///
/// Parley does NOT use this base (DI-based settings, different isolation) and is excluded.
///
/// Each tool's test class derives from this, supplies the env var, dir prefix, and a few
/// accessors, and keeps ONLY its tool-specific tests (panel widths, validation level, etc.).
///
/// The base stays UI-framework-free: the subclass supplies every member accessor as an
/// abstract method, so Radoub.TestUtilities does not need a reference to Radoub.UI.
/// </summary>
/// <typeparam name="TService">The concrete tool SettingsService (exposes a static singleton).</typeparam>
public abstract class ToolSettingsServiceTestBase<TService> : IDisposable
    where TService : class
{
    protected readonly string TestSettingsDir;

    /// <summary>Environment variable the tool's settings dir override reads (e.g. "FENCE_SETTINGS_DIR").</summary>
    protected abstract string SettingsEnvironmentVariable { get; }

    /// <summary>Temp-directory name prefix for this tool's isolated settings (e.g. "Fence").</summary>
    protected abstract string ToolDirPrefix { get; }

    /// <summary>Return the tool's singleton instance (e.g. <c>SettingsService.Instance</c>).</summary>
    protected abstract TService GetInstance();

    /// <summary>Reset the tool's singleton so the next <see cref="GetInstance"/> reloads from disk.</summary>
    protected abstract void ResetSingleton();

    // --- Settings surface (public on BaseToolSettingsService; provided by the subclass
    //     so the base test stays compile-safe and UI-framework-free, no reflection). ---
    protected abstract double GetWindowWidth(TService service);
    protected abstract void SetWindowWidth(TService service, double value);
    protected abstract double GetWindowHeight(TService service);
    protected abstract void SetWindowHeight(TService service, double value);
    protected abstract IReadOnlyList<string> GetRecentFiles(TService service);
    protected abstract void AddRecentFile(TService service, string path);
    protected abstract int GetMaxRecentFiles(TService service);
    protected abstract void SetMaxRecentFiles(TService service, int value);
    protected abstract int GetLogRetentionSessions(TService service);
    protected abstract void SetLogRetentionSessions(TService service, int value);

    /// <summary>File extension a recent-file fixture should use (e.g. ".utc").</summary>
    protected virtual string RecentFileExtension => ".dat";

    protected ToolSettingsServiceTestBase()
    {
        TestSettingsDir = Path.Combine(Path.GetTempPath(), $"{ToolDirPrefix}_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(TestSettingsDir);

        ResetSingleton();
        SingletonTestHelper.ConfigureSettingsDirectory(SettingsEnvironmentVariable, TestSettingsDir);
    }

    public void Dispose()
    {
        ResetSingleton();
        SingletonTestHelper.ConfigureSettingsDirectory(SettingsEnvironmentVariable, null);

        try
        {
            if (Directory.Exists(TestSettingsDir))
                Directory.Delete(TestSettingsDir, recursive: true);
        }
        catch
        {
            // Ignore cleanup failures
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Instance_ReturnsSameInstance()
    {
        Assert.Same(GetInstance(), GetInstance());
    }

    [Fact]
    public void WindowWidth_EnforcesMinimum()
    {
        var service = GetInstance();
        SetWindowWidth(service, 1);
        Assert.True(GetWindowWidth(service) >= 600);
    }

    [Fact]
    public void WindowHeight_EnforcesMinimum()
    {
        var service = GetInstance();
        SetWindowHeight(service, 1);
        Assert.True(GetWindowHeight(service) >= 400);
    }

    [Fact]
    public void MaxRecentFiles_EnforcesRange()
    {
        var service = GetInstance();

        SetMaxRecentFiles(service, 0);
        Assert.True(GetMaxRecentFiles(service) >= 1);

        SetMaxRecentFiles(service, 100);
        Assert.True(GetMaxRecentFiles(service) <= 20);
    }

    [Fact]
    public void LogRetentionSessions_EnforcesRange()
    {
        var service = GetInstance();

        SetLogRetentionSessions(service, 0);
        Assert.True(GetLogRetentionSessions(service) >= 1);

        SetLogRetentionSessions(service, 100);
        Assert.True(GetLogRetentionSessions(service) <= 10);
    }

    [Fact]
    public void RecentFiles_ReturnsListCopy()
    {
        var service = GetInstance();
        Assert.NotSame(GetRecentFiles(service), GetRecentFiles(service));
    }

    [Fact]
    public void AddRecentFile_AddsExistingFileToList()
    {
        var service = GetInstance();
        var tempFile = Path.Combine(TestSettingsDir, $"recent{RecentFileExtension}");
        File.WriteAllText(tempFile, "test");

        AddRecentFile(service, tempFile);

        Assert.Contains(tempFile, GetRecentFiles(service));
    }

    [Fact]
    public void WindowWidth_PersistsAcrossReload()
    {
        var service = GetInstance();
        SetWindowWidth(service, 1000);

        ResetSingleton();

        Assert.Equal(1000, GetWindowWidth(GetInstance()));
    }
}
