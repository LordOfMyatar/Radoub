using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Radoub.UI.Utils;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Repo-level dead-stub lint (#2362, #2231 guard). Walks every tool's
/// <c>MainWindow.axaml</c> and runs it through <see cref="MenuStubAnalyzer"/>.
///
/// This is a characterization test: it pins the <em>current</em> set of disabled
/// menu stubs (Manifest + Fence Undo/Redo, per the #2359 audit). It fails if a new
/// stub is introduced anywhere, OR if a known stub is removed without updating the
/// list below — the latter is the signal that #2231 wired undo for that tool and the
/// expectation should shrink. Either way, a silent regression cannot ship.
/// </summary>
public class MainWindowMenuStubLintTests
{
    /// <summary>
    /// Known, accepted dead stubs as of #2362. Keyed by tool name (the directory under
    /// the repo root). Each value is the set of menu Headers that are currently disabled
    /// stubs. When #2231 wires undo for a tool, remove its entry here.
    /// </summary>
    private static readonly Dictionary<string, string[]> KnownStubs = new()
    {
        // Manifest Undo/Redo wired in #2231 Sprint 3 (#2253) — stubs removed.
        ["Fence"] = new[] { "_Undo", "_Redo" },
    };

    [Fact]
    public void NoTool_Introduces_Unexpected_MenuStubs()
    {
        var repoRoot = FindRepoRoot();
        var mainWindows = Directory
            .GetFiles(repoRoot, "MainWindow.axaml", SearchOption.AllDirectories)
            .Where(p => p.Replace('\\', '/').Contains("/Views/"))
            .Where(p => !p.Replace('\\', '/').Contains("/bin/") &&
                        !p.Replace('\\', '/').Contains("/obj/"))
            .OrderBy(p => p)
            .ToList();

        Assert.NotEmpty(mainWindows); // sanity: we actually found the views

        var unexpected = new List<string>();

        foreach (var path in mainWindows)
        {
            string tool = ToolNameFromPath(path, repoRoot);
            var content = File.ReadAllText(path);
            var found = MenuStubAnalyzer.FindDisabledMenuStubs(content)
                .Select(v => v.Header)
                .OrderBy(h => h)
                .ToArray();

            var expected = (KnownStubs.TryGetValue(tool, out var e) ? e : Array.Empty<string>())
                .OrderBy(h => h)
                .ToArray();

            if (!found.SequenceEqual(expected))
            {
                unexpected.Add(
                    $"{tool} ({path}): expected stubs [{string.Join(", ", expected)}], " +
                    $"found [{string.Join(", ", found)}]");
            }
        }

        Assert.True(
            unexpected.Count == 0,
            "Menu dead-stub lint mismatch (#2362 / #2231 guard):\n" +
            string.Join("\n", unexpected) +
            "\n\nIf you WIRED undo (good — #2231), remove that tool from KnownStubs. " +
            "If you ADDED a disabled menu stub, wire it via a binding instead.");
    }

    private static string ToolNameFromPath(string axamlPath, string repoRoot)
    {
        // <repoRoot>/<Tool>/<Tool>/Views/MainWindow.axaml  →  <Tool>
        var rel = Path.GetRelativePath(repoRoot, axamlPath).Replace('\\', '/');
        return rel.Split('/').First();
    }

    private static string FindRepoRoot()
    {
        // Walk up from the test assembly until we find the repo marker (Radoub.sln).
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Radoub.sln")))
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        throw new InvalidOperationException(
            "Could not locate repo root (Radoub.sln) above " + AppContext.BaseDirectory);
    }
}
