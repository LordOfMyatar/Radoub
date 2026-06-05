using System.Collections.Generic;
using System.Linq;
using RadoubLauncher.Services;
using Xunit;

namespace Trebuchet.Tests;

/// <summary>
/// Tests for ProcessArgumentBuilder.ChunkScriptPaths — splits a script list so
/// each compiler invocation's command line stays under the OS limit (#2343).
///
/// On large modules (~1100 stale scripts) the old single-invocation path passed
/// every absolute path as one command line (~88 KB), exceeding the Windows
/// 32,767-char CreateProcess cap and throwing Win32 error 206
/// ("filename or extension is too long") before the compiler even started.
/// </summary>
public class ScriptPathChunkerTests
{
    [Fact]
    public void ChunkScriptPaths_AllPathsPreservedExactlyOnce_InOrder()
    {
        var paths = Enumerable.Range(0, 50).Select(i => $@"C:\m\script_{i:00}.nss").ToList();

        var chunks = ProcessArgumentBuilder.ChunkScriptPaths(paths, fixedArgsLength: 100, maxCommandLine: 200);

        var flattened = chunks.SelectMany(c => c).ToList();
        Assert.Equal(paths, flattened); // same items, same order, no loss, no dupes
    }

    [Fact]
    public void ChunkScriptPaths_EachChunkUnderLimit_AccountingForFixedArgs()
    {
        var paths = Enumerable.Range(0, 200).Select(i => $@"C:\modules\anphillia\anph_script_{i:000}.nss").ToList();
        const int fixedArgs = 80;   // "-c -y -j 16 --root C:\Games\NWN" etc.
        const int maxCmd = 512;

        var chunks = ProcessArgumentBuilder.ChunkScriptPaths(paths, fixedArgs, maxCmd);

        foreach (var chunk in chunks)
        {
            // Serialized length = each path + a separating space.
            var serialized = chunk.Sum(p => p.Length + 1);
            Assert.True(fixedArgs + serialized <= maxCmd,
                $"Chunk command-line length {fixedArgs + serialized} exceeds limit {maxCmd}");
            Assert.NotEmpty(chunk);
        }
    }

    [Fact]
    public void ChunkScriptPaths_SinglePathLongerThanLimit_StillGetsOwnChunk()
    {
        // A path that alone exceeds the budget can't be split — it must still be
        // emitted in its own chunk rather than dropped or causing an infinite loop.
        var huge = @"C:\m\" + new string('x', 500) + ".nss";
        var paths = new List<string> { @"C:\m\a.nss", huge, @"C:\m\b.nss" };

        var chunks = ProcessArgumentBuilder.ChunkScriptPaths(paths, fixedArgsLength: 50, maxCommandLine: 200);

        Assert.Contains(chunks, c => c.Count == 1 && c[0] == huge);
        Assert.Equal(paths, chunks.SelectMany(c => c).ToList());
    }

    [Fact]
    public void ChunkScriptPaths_SmallListFitsInOneChunk()
    {
        var paths = new List<string> { @"C:\m\a.nss", @"C:\m\b.nss" };

        var chunks = ProcessArgumentBuilder.ChunkScriptPaths(paths, fixedArgsLength: 50, maxCommandLine: 32000);

        Assert.Single(chunks);
        Assert.Equal(paths, chunks[0]);
    }

    [Fact]
    public void ChunkScriptPaths_EmptyList_ReturnsNoChunks()
    {
        var chunks = ProcessArgumentBuilder.ChunkScriptPaths(new List<string>(), fixedArgsLength: 50, maxCommandLine: 200);

        Assert.Empty(chunks);
    }
}
