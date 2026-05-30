using System.Collections.Generic;

namespace RadoubLauncher.Services;

/// <summary>
/// Builds process argument lists for <see cref="System.Diagnostics.ProcessStartInfo.ArgumentList"/>.
///
/// Each list element is ONE argument with NO surrounding quotes — the runtime escapes
/// each element correctly for the target OS. Replaces the old
/// <c>$"--file \"{path}\""</c> string-concat pattern (#2248), which corrupted parsing
/// when a path contained a literal double-quote (legal on Linux/macOS).
///
/// Pure + static so the arg construction is unit-testable without spawning a process.
/// </summary>
public static class ProcessArgumentBuilder
{
    /// <summary>Args to open a file in a Radoub tool: <c>--file &lt;path&gt;</c>.</summary>
    public static IReadOnlyList<string> FileOpenArgs(string filePath)
        => new[] { "--file", filePath };

    /// <summary>Args to pass a single bare path (e.g. external editor): <c>&lt;path&gt;</c>.</summary>
    public static IReadOnlyList<string> SingleFileArg(string filePath)
        => new[] { filePath };

    /// <summary>
    /// NWScript compiler args: <c>-c &lt;scripts...&gt; [-y -j N] [--root &lt;gamePath&gt;]</c>.
    /// </summary>
    /// <param name="scriptPaths">Scripts to compile (at least one expected).</param>
    /// <param name="gamePath">NWN install root for includes; omitted if null/empty.</param>
    /// <param name="continueOnError">Add <c>-y</c> (batch mode keeps going past errors).</param>
    /// <param name="threadCount">When &gt; 0 and continueOnError, adds <c>-j &lt;n&gt;</c>.</param>
    public static IReadOnlyList<string> CompileArgs(
        IReadOnlyList<string> scriptPaths,
        string? gamePath,
        bool continueOnError = false,
        int threadCount = 0)
    {
        var args = new List<string> { "-c" };
        foreach (var path in scriptPaths)
        {
            args.Add(path);
        }

        if (continueOnError)
        {
            // -j requires an explicit thread count; omitting it makes the compiler
            // parse the next flag as the integer.
            args.Add("-y");
            if (threadCount > 0)
            {
                args.Add("-j");
                args.Add(threadCount.ToString());
            }
        }

        if (!string.IsNullOrEmpty(gamePath))
        {
            args.Add("--root");
            args.Add(gamePath);
        }

        return args;
    }
}
