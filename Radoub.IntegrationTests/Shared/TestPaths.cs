namespace Radoub.IntegrationTests.Shared;

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
            // Navigate from bin/Debug/net9.0 up to Radoub.IntegrationTests, then up to Radoub root
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

    /// <summary>
    /// Gets a specific Parley test dialog file path.
    /// </summary>
    public static string GetTestFile(string filename) => Path.Combine(ParleyTestFiles, filename);

    /// <summary>
    /// Gets the path to a built Manifest executable.
    /// </summary>
    /// <param name="configuration">Build configuration (Debug or Release)</param>
    public static string GetManifestExePath(string configuration = "Debug")
    {
        return Path.Combine(RepoRoot, "Manifest", "Manifest", "bin", configuration, "net9.0", "Manifest.exe");
    }

    /// <summary>
    /// Gets path to Manifest test files directory.
    /// </summary>
    public static string ManifestTestFiles => Path.Combine(RepoRoot, "Manifest", "Manifest.Tests", "TestData");

    /// <summary>
    /// Gets a specific Manifest test file path.
    /// </summary>
    public static string GetManifestTestFile(string filename) => Path.Combine(ManifestTestFiles, filename);

    /// <summary>
    /// Creates a temporary directory for test output.
    /// Call Cleanup() when done.
    /// </summary>
    public static string CreateTempTestDirectory()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "Radoub.IntegrationTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    /// <summary>
    /// Copies a test file to a temp location for modification tests.
    /// </summary>
    public static string CopyTestFileToTemp(string sourceFilename)
    {
        var tempDir = CreateTempTestDirectory();
        var sourcePath = GetTestFile(sourceFilename);
        var destPath = Path.Combine(tempDir, sourceFilename);
        File.Copy(sourcePath, destPath);
        return destPath;
    }

    /// <summary>
    /// Cleans up a temp directory created by CreateTempTestDirectory.
    /// </summary>
    public static void CleanupTempDirectory(string tempDir)
    {
        if (Directory.Exists(tempDir))
        {
            try
            {
                Directory.Delete(tempDir, recursive: true);
            }
            catch
            {
                // Best effort cleanup - might fail if files still locked
            }
        }
    }
}
