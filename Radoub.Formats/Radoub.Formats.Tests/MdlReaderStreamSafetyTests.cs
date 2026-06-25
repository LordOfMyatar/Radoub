using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Radoub.Formats.Mdl;
using Xunit;

namespace Radoub.Formats.Tests;

/// <summary>
/// Tests for MdlReader.Parse(Stream) thread-safety (#2543, follow-up to #2510).
/// The Stream overload used to read the caller's stream twice (IsBinaryMdl seeked
/// and restored position, then the reader re-read sequentially). After #2543 it
/// snapshots the stream into a private buffer once and parses the buffer, so a
/// given stream is read exactly once and the overload no longer depends on an
/// unenforced "never read this stream twice" assumption.
/// </summary>
public class MdlReaderStreamSafetyTests
{
    private static byte[] LoadFixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "TestData", "Mdl", "c_allip_d.mdl");
        return File.ReadAllBytes(path);
    }

    [Fact]
    public void ParseStream_MatchesParseBytes()
    {
        var bytes = LoadFixture();
        int fromBytes = new MdlReader().Parse(bytes).GetMeshNodes().Count();

        using var stream = new MemoryStream(bytes);
        int fromStream = new MdlReader().Parse(stream).GetMeshNodes().Count();

        Assert.Equal(fromBytes, fromStream);
        Assert.True(fromStream > 0, "fixture should have mesh nodes");
    }

    [Fact]
    public void ParseStream_ReadsStreamExactlyOnce_LeavesPositionAtEnd()
    {
        // The pre-#2543 overload restored position after IsBinaryMdl, then read
        // forward — a non-seekable stream would have broken on the second read.
        // After the fix the stream is consumed once (CopyTo), ending at EOF.
        var bytes = LoadFixture();
        using var stream = new MemoryStream(bytes);

        new MdlReader().Parse(stream);

        Assert.Equal(stream.Length, stream.Position);
    }

    [Fact]
    public void ParseStream_NonSeekableStream_Succeeds()
    {
        // A forward-only stream cannot satisfy the old save/restore-position dance.
        // The buffered fix copies it first, so parsing a non-seekable stream works.
        var bytes = LoadFixture();
        using var inner = new MemoryStream(bytes);
        using var forwardOnly = new ForwardOnlyStream(inner);

        int count = new MdlReader().Parse(forwardOnly).GetMeshNodes().Count();

        Assert.True(count > 0);
    }

    [Fact]
    public void ParseStream_NullStream_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new MdlReader().Parse((Stream)null!));
    }

    [Fact]
    public void ParseStream_PerThreadStreams_YieldIdenticalMeshCounts()
    {
        // Supported concurrency contract: each thread owns its own stream over the
        // same bytes. This must always agree with the byte[] path.
        var bytes = LoadFixture();
        int expected = new MdlReader().Parse(bytes).GetMeshNodes().Count();

        var shared = new MdlReader();
        var results = new ConcurrentBag<int>();
        const int threads = 16;
        const int iterationsPerThread = 25;

        Parallel.For(0, threads, _ =>
        {
            for (int i = 0; i < iterationsPerThread; i++)
            {
                using var stream = new MemoryStream(bytes);
                results.Add(shared.Parse(stream).GetMeshNodes().Count());
            }
        });

        Assert.Equal(threads * iterationsPerThread, results.Count);
        Assert.All(results, count => Assert.Equal(expected, count));
    }

    /// <summary>A minimal non-seekable, read-only wrapper to prove the buffered path.</summary>
    private sealed class ForwardOnlyStream : Stream
    {
        private readonly Stream _inner;
        public ForwardOnlyStream(Stream inner) => _inner = inner;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
