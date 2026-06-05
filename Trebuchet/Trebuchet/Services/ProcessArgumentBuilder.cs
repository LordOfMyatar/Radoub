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

    /// <summary>
    /// Split <paramref name="scriptPaths"/> into chunks so each compiler invocation's
    /// command line stays under <paramref name="maxCommandLine"/> characters (#2343).
    ///
    /// Windows caps a process command line at 32,767 chars; passing ~1100 absolute
    /// paths as one <c>-c</c> invocation overflowed it and threw Win32 error 206
    /// before the compiler started. Each path contributes its length plus one space
    /// separator; <paramref name="fixedArgsLength"/> reserves room for the constant
    /// flags (<c>-c -y -j N --root &lt;path&gt;</c>) shared by every chunk.
    ///
    /// A single path longer than the remaining budget still gets its own chunk —
    /// it can't be split, and dropping it would silently skip a script.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> ChunkScriptPaths(
        IReadOnlyList<string> scriptPaths, int fixedArgsLength, int maxCommandLine)
    {
        var chunks = new List<IReadOnlyList<string>>();
        if (scriptPaths.Count == 0)
            return chunks;

        var budget = maxCommandLine - fixedArgsLength;
        if (budget < 1) budget = 1;

        var current = new List<string>();
        var currentLen = 0;

        foreach (var path in scriptPaths)
        {
            var cost = path.Length + 1; // path + separating space

            // Start a new chunk when adding this path would overflow the budget,
            // unless the current chunk is empty (a lone oversize path must still
            // be emitted rather than looping forever).
            if (current.Count > 0 && currentLen + cost > budget)
            {
                chunks.Add(current);
                current = new List<string>();
                currentLen = 0;
            }

            current.Add(path);
            currentLen += cost;
        }

        if (current.Count > 0)
            chunks.Add(current);

        return chunks;
    }
}
