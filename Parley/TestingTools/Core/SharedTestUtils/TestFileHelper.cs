using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SharedTestUtils
{
    public class TestFileHelper
    {
        private static TestFilesConfig? _config;

        public static TestFilesConfig GetConfig()
        {
            if (_config == null)
            {
                var configPath = Path.Combine(GetTestingToolsRoot(), "TestFiles.json");
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    _config = JsonSerializer.Deserialize<TestFilesConfig>(json);
                }
                else
                {
                    throw new FileNotFoundException($"TestFiles.json not found at: {configPath}");
                }
            }
            return _config!;
        }

        public static string GetTestFilePath(string testFileKey)
        {
            var config = GetConfig();
            if (config.TestFiles.TryGetValue(testFileKey, out var testFile))
            {
                return Path.Combine(ExpandHomeDirectory(config.TestFilesDirectory), testFile.Filename);
            }
            throw new ArgumentException($"Test file key '{testFileKey}' not found in TestFiles.json");
        }

        public static List<string> GetTestSuite(string suiteName)
        {
            var config = GetConfig();
            if (config.TestSuites.TryGetValue(suiteName, out var suite))
            {
                return suite.Select(GetTestFilePath).ToList();
            }
            throw new ArgumentException($"Test suite '{suiteName}' not found in TestFiles.json");
        }

        public static List<string> GetAllTestFiles()
        {
            var config = GetConfig();
            var baseDir = ExpandHomeDirectory(config.TestFilesDirectory);
            return config.TestFiles.Values
                .Select(tf => Path.Combine(baseDir, tf.Filename))
                .ToList();
        }

        public static void PrintAvailableFiles()
        {
            var config = GetConfig();
            Console.WriteLine("Available Test Files:");
            Console.WriteLine("====================");
            foreach (var kvp in config.TestFiles)
            {
                var tf = kvp.Value;
                Console.WriteLine($"  {kvp.Key,-20} - {tf.Description} ({tf.Complexity})");
            }
            Console.WriteLine();
            Console.WriteLine("Available Test Suites:");
            Console.WriteLine("=====================");
            foreach (var kvp in config.TestSuites)
            {
                Console.WriteLine($"  {kvp.Key,-20} - {string.Join(", ", kvp.Value)}");
            }
        }

        private static string ExpandHomeDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
                return path;

            // Handle "C:~\" notation - replace with user profile directory
            if (path.StartsWith("C:~", StringComparison.OrdinalIgnoreCase))
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return path.Replace("C:~", userProfile);
            }

            // Handle standard "~/" notation (Unix-style)
            if (path.StartsWith("~/") || path.StartsWith("~\\"))
            {
                var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                return path.Replace("~", userProfile);
            }

            return path;
        }

        private static string GetTestingToolsRoot()
        {
            // Navigate up from bin/Debug/net9.0-windows7.0 to workspace root, then to TestingTools
            var currentDir = AppContext.BaseDirectory;
            while (!string.IsNullOrEmpty(currentDir))
            {
                var testingToolsPath = Path.Combine(currentDir, "TestingTools");
                if (Directory.Exists(testingToolsPath) && File.Exists(Path.Combine(testingToolsPath, "TestFiles.json")))
                {
                    return testingToolsPath;
                }
                currentDir = Directory.GetParent(currentDir)?.FullName;
            }

            // Fallback: try relative path from current directory
            var fallbackPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "..", ".."));
            var fallbackTestingTools = Path.Combine(fallbackPath, "TestingTools");
            if (Directory.Exists(fallbackTestingTools) && File.Exists(Path.Combine(fallbackTestingTools, "TestFiles.json")))
            {
                return fallbackTestingTools;
            }

            throw new DirectoryNotFoundException($"Could not find TestingTools directory. Searched from: {AppContext.BaseDirectory}");
        }
    }

    public class TestFilesConfig
    {
        public string testFilesDirectory { get; set; } = "";
        public Dictionary<string, TestFileInfo> testFiles { get; set; } = new();
        public Dictionary<string, List<string>> testSuites { get; set; } = new();

        // Properties with correct casing for code use
        public string TestFilesDirectory => testFilesDirectory;
        public Dictionary<string, TestFileInfo> TestFiles => testFiles;
        public Dictionary<string, List<string>> TestSuites => testSuites;
    }

    public class TestFileInfo
    {
        public string filename { get; set; } = "";
        public string description { get; set; } = "";
        public string complexity { get; set; } = "";
        public List<string> features { get; set; } = new();

        // Properties with correct casing for code use
        public string Filename => filename;
        public string Description => description;
        public string Complexity => complexity;
        public List<string> Features => features;
    }
}
