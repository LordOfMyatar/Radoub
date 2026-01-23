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
    /// Gets the path to a built Quartermaster executable.
    /// </summary>
    /// <param name="configuration">Build configuration (Debug or Release)</param>
    public static string GetQuartermasterExePath(string configuration = "Debug")
    {
        return Path.Combine(RepoRoot, "Quartermaster", "Quartermaster", "bin", configuration, "net9.0", "Quartermaster.exe");
    }

    /// <summary>
    /// Gets path to Quartermaster test files directory.
    /// </summary>
    public static string QuartermasterTestFiles => Path.Combine(RepoRoot, "Quartermaster", "Quartermaster.Tests", "TestData");

    /// <summary>
    /// Gets a specific Quartermaster test file path.
    /// </summary>
    public static string GetQuartermasterTestFile(string filename) => Path.Combine(QuartermasterTestFiles, filename);

    /// <summary>
    /// Gets the path to a built Fence executable.
    /// </summary>
    /// <param name="configuration">Build configuration (Debug or Release)</param>
    public static string GetFenceExePath(string configuration = "Debug")
    {
        return Path.Combine(RepoRoot, "Fence", "Fence", "bin", configuration, "net9.0", "Fence.exe");
    }

    /// <summary>
    /// Gets the path to a built Trebuchet executable.
    /// </summary>
    /// <param name="configuration">Build configuration (Debug or Release)</param>
    public static string GetTrebuchetExePath(string configuration = "Debug")
    {
        return Path.Combine(RepoRoot, "Trebuchet", "Trebuchet", "bin", configuration, "net9.0", "Trebuchet.exe");
    }

    #region Integration Test Data (Spoofed NWN Environment)

    /// <summary>
    /// Gets the root TestData directory containing spoofed NWN environment.
    /// </summary>
    public static string TestDataRoot => Path.Combine(RepoRoot, "Radoub.IntegrationTests", "TestData");

    /// <summary>
    /// Gets the spoofed NWN game root directory (simulates NWN installation).
    /// Contains: hak/ folder with test HAK files.
    /// </summary>
    public static string TestGameRoot => Path.Combine(TestDataRoot, "GameRoot");

    /// <summary>
    /// Gets the test HAK directory within the spoofed game root.
    /// </summary>
    public static string TestHakDirectory => Path.Combine(TestGameRoot, "hak");

    /// <summary>
    /// Gets a specific test HAK file path.
    /// </summary>
    public static string GetTestHakFile(string filename) => Path.Combine(TestHakDirectory, filename);

    /// <summary>
    /// Gets the test module directory (unpacked module with dlg, utc, scripts, etc.).
    /// </summary>
    public static string TestModuleDirectory => Path.Combine(TestDataRoot, "TestModule");

    /// <summary>
    /// Gets a specific file from the test module.
    /// </summary>
    public static string GetTestModuleFile(string filename) => Path.Combine(TestModuleDirectory, filename);

    #endregion

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
