using System;
using System.IO;

namespace TestingTools.TestFiles
{
    /// <summary>
    /// Helper class for resolving test file paths relative to workspace root
    /// </summary>
    public static class TestPathHelper
    {
        /// <summary>
        /// Gets the workspace root directory (LNS_DLG folder)
        /// </summary>
        public static string GetWorkspaceRoot()
        {
            // Start from current directory and walk up until we find LNS_DLG.sln
            string currentDir = AppContext.BaseDirectory;

            while (currentDir != null)
            {
                if (File.Exists(Path.Combine(currentDir, "LNS_DLG.sln")))
                {
                    return currentDir;
                }

                var parent = Directory.GetParent(currentDir);
                currentDir = parent?.FullName;
            }

            throw new DirectoryNotFoundException("Could not find workspace root (LNS_DLG.sln not found in parent directories)");
        }

        /// <summary>
        /// Gets the TestFiles directory path
        /// </summary>
        public static string GetTestFilesDir()
        {
            return Path.Combine(GetWorkspaceRoot(), "TestingTools", "TestFiles");
        }

        /// <summary>
        /// Gets the full path to a test file
        /// </summary>
        public static string GetTestFilePath(string filename)
        {
            return Path.Combine(GetTestFilesDir(), filename);
        }
    }
}
