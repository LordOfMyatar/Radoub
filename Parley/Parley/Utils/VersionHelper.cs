using System;
using System.Reflection;

namespace DialogEditor.Utils
{
    /// <summary>
    /// Provides version information for the application using GitInfo and assembly metadata.
    /// </summary>
    public static class VersionHelper
    {
        private static readonly Assembly _assembly = Assembly.GetExecutingAssembly();

        /// <summary>
        /// Gets the semantic version (e.g., "0.1.0-alpha")
        /// Uses AssemblyInformationalVersion which GitVersion sets correctly
        /// </summary>
        public static string Version
        {
            get
            {
                // Try InformationalVersion first (set by GitVersion)
                var informationalVersion = _assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                if (!string.IsNullOrEmpty(informationalVersion))
                {
                    // InformationalVersion may include commit hash (e.g., "0.1.0-alpha+123")
                    // Strip everything after '+' to get clean version
                    var plusIndex = informationalVersion.IndexOf('+');
                    return plusIndex >= 0 ? informationalVersion.Substring(0, plusIndex) : informationalVersion;
                }

                // Fallback to AssemblyVersion if InformationalVersion not set
                var version = _assembly.GetName().Version;
                return version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "Unknown";
            }
        }

        /// <summary>
        /// Gets the Git commit hash (short form, e.g., "a1b2c3d")
        /// Uses git command as fallback since GitInfo package not generating code
        /// </summary>
        public static string Commit
        {
            get
            {
                try
                {
                    // Try GitInfo first
                    var commitField = Type.GetType("ThisAssembly.Git, Parley")?
                        .GetField("Commit", BindingFlags.Public | BindingFlags.Static);

                    if (commitField != null)
                    {
                        var commit = commitField.GetValue(null)?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(commit))
                        {
                            // Return short hash (first 7 chars)
                            return commit.Length > 7 ? commit.Substring(0, 7) : commit;
                        }
                    }

                    // Fallback: Try to get from git command
                    var process = new System.Diagnostics.Process
                    {
                        StartInfo = new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = "git",
                            Arguments = "rev-parse --short=7 HEAD",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        }
                    };

                    process.Start();
                    var output = process.StandardOutput.ReadToEnd().Trim();
                    process.WaitForExit();

                    if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                    {
                        return output;
                    }
                }
                catch
                {
                    // Fallback if git not available
                }

                return "dev";
            }
        }

        /// <summary>
        /// Gets the Git branch name (e.g., "develop", "main")
        /// Provided by GitInfo package via ThisAssembly.Git.Branch
        /// </summary>
        public static string Branch
        {
            get
            {
                try
                {
                    var branchField = Type.GetType("ThisAssembly.Git, Parley")?
                        .GetField("Branch", BindingFlags.Public | BindingFlags.Static);

                    if (branchField != null)
                    {
                        return branchField.GetValue(null)?.ToString() ?? "Unknown";
                    }
                }
                catch
                {
                    // Fallback if GitInfo not available
                }

                return "Unknown";
            }
        }

        /// <summary>
        /// Gets the build date from the assembly's compilation timestamp
        /// </summary>
        public static string BuildDate
        {
            get
            {
                try
                {
                    var buildDateField = Type.GetType("ThisAssembly.Git, Parley")?
                        .GetField("CommitDate", BindingFlags.Public | BindingFlags.Static);

                    if (buildDateField != null)
                    {
                        var dateStr = buildDateField.GetValue(null)?.ToString();
                        if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, out var date))
                        {
                            return date.ToString("yyyy-MM-dd");
                        }
                    }
                }
                catch
                {
                    // Fallback
                }

                return DateTime.Now.ToString("yyyy-MM-dd");
            }
        }

        /// <summary>
        /// Gets the full version string with commit and date
        /// Format: "1.0.0 (Build a1b2c3d, 2025-10-15)"
        /// </summary>
        public static string FullVersion => $"{Version} (Build {Commit}, {BuildDate})";

        /// <summary>
        /// Gets a compact version string for status bars
        /// Format: "v1.0.0-a1b2c3d"
        /// </summary>
        public static string CompactVersion => $"v{Version}-{Commit}";

        /// <summary>
        /// Gets the product name from assembly metadata
        /// </summary>
        public static string ProductName
        {
            get
            {
                var product = _assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;
                return product ?? "Dialog Editor for Neverwinter Nights";
            }
        }

        /// <summary>
        /// Gets the copyright information
        /// </summary>
        public static string Copyright
        {
            get
            {
                var copyright = _assembly.GetCustomAttribute<AssemblyCopyrightAttribute>()?.Copyright;
                return copyright ?? "Copyright © 2025";
            }
        }

        /// <summary>
        /// Gets detailed version information for logging
        /// </summary>
        public static string GetDetailedVersionInfo()
        {
            return $@"
{ProductName}
Version: {Version}
Build: {Commit} ({Branch})
Date: {BuildDate}
Platform: {Environment.OSVersion.Platform}
.NET: {Environment.Version}
{Copyright}
".Trim();
        }
    }
}
