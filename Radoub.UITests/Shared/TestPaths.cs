namespace Radoub.UITests.Shared;

/// <summary>
/// Centralized path management for UI tests.
/// Resolves paths relative to the test project location.
/// </summary>
public static class TestPaths
{
    /// <summary>
    /// Gets the Radoub repository root directory.
    /// </summary>
    public static string RepoRoot
    {
        get
        {
            // Navigate from bin/Debug/net9.0 up to Radoub.UITests, then up to Radoub root
            var testDir = AppDomain.CurrentDomain.BaseDirectory;
            return Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", ".."));
        }
    }

    /// <summary>
    /// Gets the path to a built Parley executable.
    /// </summary>
    /// <param name="configuration">Build configuration (Debug or Release)</param>
    public static string GetParleyExePath(string configuration = "Debug")
    {
        return Path.Combine(RepoRoot, "Parley", "Parley", "bin", configuration, "net9.0", "Parley.exe");
    }

    /// <summary>
    /// Gets path to Parley test files directory.
    /// </summary>
    public static string ParleyTestFiles => Path.Combine(RepoRoot, "Parley", "TestingTools", "TestFiles");
}
