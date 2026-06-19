using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Radoub.Formats.Mdl;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Regression tests for the shared-MdlReader data race (#2510). ModelService holds a single
/// MdlReader and #1485 added a background thread that calls LoadModel -> _mdlReader.Parse,
/// racing the main render's parse. Concurrent parses corrupted the shared reader's per-parse
/// state, producing a truncated node tree (dire tiger: 24 meshes -> 19, dropping skin meshes).
/// </summary>
public class MdlReaderConcurrencyTests
{
    private static byte[] LoadFixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Mdl", "c_allip_d.mdl");
        return File.ReadAllBytes(path);
    }

    [Fact]
    public void Parse_SameBytesSingleThread_IsDeterministic()
    {
        // Baseline: the parser must be deterministic on its own (it is — this guards the premise).
        var bytes = LoadFixture();
        var reader = new MdlReader();
        int expected = reader.Parse(bytes).GetMeshNodes().Count();

        for (int i = 0; i < 20; i++)
        {
            int actual = new MdlReader().Parse(bytes).GetMeshNodes().Count();
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void Parse_SharedReaderAcrossThreads_YieldsIdenticalMeshCounts()
    {
        // Reproduces #2510: one shared MdlReader (as ModelService holds it) parsed concurrently
        // from many threads must never produce a truncated model. Pre-fix this fails — some
        // parses drop nodes when their traversal state is clobbered by an interleaving parse.
        var bytes = LoadFixture();
        int expected = new MdlReader().Parse(bytes).GetMeshNodes().Count();
        Assert.True(expected > 0, "fixture should have mesh nodes");

        var shared = new MdlReader();
        var results = new ConcurrentBag<int>();
        const int threads = 16;
        const int iterationsPerThread = 25;

        Parallel.For(0, threads, _ =>
        {
            for (int i = 0; i < iterationsPerThread; i++)
            {
                var model = shared.Parse(bytes);
                results.Add(model.GetMeshNodes().Count());
            }
        });

        Assert.Equal(threads * iterationsPerThread, results.Count);
        Assert.All(results, count => Assert.Equal(expected, count));
    }
}
