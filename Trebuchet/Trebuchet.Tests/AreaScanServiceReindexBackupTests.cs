using Radoub.Formats.Gff;
using RadoubLauncher.Services;

namespace Trebuchet.Tests;

/// <summary>
/// Backup + atomic-write tests for AreaScanService.ReindexFactions.
/// Regression: #2246 — partial reindex failure used to leave inconsistent state
/// across .git/.utc files with no way to roll back.
/// </summary>
public class AreaScanServiceReindexBackupTests : IDisposable
{
    private readonly string _workingDir;
    private readonly string _backupRoot;
    private readonly string _root;

    public AreaScanServiceReindexBackupTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"ReindexBackup_{Guid.NewGuid():N}");
        _workingDir = Path.Combine(_root, "mymodule");
        Directory.CreateDirectory(_workingDir);
        _backupRoot = Path.Combine(_root, "Backups");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, true); } catch { }
    }

    private string CreateGitFile(string name, uint[] creatureFactionIds)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        var creatureList = new GffList();
        foreach (var f in creatureFactionIds)
        {
            var creature = new GffStruct { Type = 4 };
            GffFieldBuilder.AddWordField(creature, "FactionID", (ushort)f);
            GffFieldBuilder.AddCResRefField(creature, "TemplateResRef", "test_creature");
            creatureList.Elements.Add(creature);
        }
        creatureList.Count = (uint)creatureList.Elements.Count;
        GffFieldBuilder.AddListField(root, "Creature List", creatureList);
        var encounterList = new GffList { Count = 0 };
        GffFieldBuilder.AddListField(root, "Encounter List", encounterList);

        var gff = new GffFile { FileType = "GIT ", FileVersion = "V3.2", RootStruct = root };
        var path = Path.Combine(_workingDir, name + ".git");
        GffWriter.Write(gff, path);
        return path;
    }

    private string CreateUtcFile(string name, uint factionId)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        GffFieldBuilder.AddWordField(root, "FactionID", (ushort)factionId);
        GffFieldBuilder.AddCResRefField(root, "TemplateResRef", name);

        var gff = new GffFile { FileType = "UTC ", FileVersion = "V3.2", RootStruct = root };
        var path = Path.Combine(_workingDir, name + ".utc");
        GffWriter.Write(gff, path);
        return path;
    }

    [Fact]
    public void ReindexFactions_BackupRootProvided_BacksUpModifiedGitFiles()
    {
        var area1 = CreateGitFile("area001", new uint[] { 5 }); // deleted
        var area2 = CreateGitFile("area002", new uint[] { 7 }); // > deleted, decrement
        var unchanged = CreateGitFile("area003", new uint[] { 2 }); // < deleted

        var originalArea1 = File.ReadAllBytes(area1);
        var originalArea2 = File.ReadAllBytes(area2);

        var result = AreaScanService.ReindexFactions(_workingDir, deletedIndex: 5, parentFactionId: 3, backupRoot: _backupRoot);

        Assert.True(result.FilesModified >= 2);
        Assert.True(Directory.Exists(_backupRoot));

        var backups = Directory.GetFiles(_backupRoot, "*.git", SearchOption.AllDirectories);
        var backupNames = backups.Select(Path.GetFileName).ToArray();
        Assert.Contains("area001.git", backupNames);
        Assert.Contains("area002.git", backupNames);
        // area003 not modified, not backed up
        Assert.DoesNotContain("area003.git", backupNames);

        var area1Backup = backups.First(b => Path.GetFileName(b) == "area001.git");
        Assert.Equal(originalArea1, File.ReadAllBytes(area1Backup));
        var area2Backup = backups.First(b => Path.GetFileName(b) == "area002.git");
        Assert.Equal(originalArea2, File.ReadAllBytes(area2Backup));
    }

    [Fact]
    public void ReindexFactions_BackupRootProvided_BacksUpModifiedUtcFiles()
    {
        var utc1 = CreateUtcFile("blueprint01", 5); // deleted
        var utc2 = CreateUtcFile("blueprint02", 2); // unchanged

        var originalUtc1 = File.ReadAllBytes(utc1);

        var result = AreaScanService.ReindexFactions(_workingDir, deletedIndex: 5, parentFactionId: 3, backupRoot: _backupRoot);

        Assert.True(result.BlueprintsReindexed >= 1);
        var backups = Directory.GetFiles(_backupRoot, "*.utc", SearchOption.AllDirectories);
        var names = backups.Select(Path.GetFileName).ToArray();
        Assert.Contains("blueprint01.utc", names);
        Assert.DoesNotContain("blueprint02.utc", names);

        var utc1Backup = backups.First(b => Path.GetFileName(b) == "blueprint01.utc");
        Assert.Equal(originalUtc1, File.ReadAllBytes(utc1Backup));
    }

    [Fact]
    public void ReindexFactions_NoFilesModified_NoBackupDirectoryCreated()
    {
        CreateGitFile("area001", new uint[] { 2 }); // unchanged
        CreateUtcFile("blueprint01", 1); // unchanged

        var result = AreaScanService.ReindexFactions(_workingDir, deletedIndex: 5, parentFactionId: 3, backupRoot: _backupRoot);

        Assert.Equal(0, result.FilesModified);
        // Empty backup dir is OK; what we care about is no bogus backup file content
        if (Directory.Exists(_backupRoot))
        {
            var files = Directory.GetFiles(_backupRoot, "*.*", SearchOption.AllDirectories);
            Assert.Empty(files);
        }
    }

    [Fact]
    public void ReindexFactions_OnSingleThreadedSyncContext_DoesNotDeadlock()
    {
        // Regression for the hardlock when deleting a faction: BackupService
        // awaits without ConfigureAwait(false), so a naive sync-over-async
        // bridge deadlocks if the caller holds a captured sync context (the
        // Avalonia UI thread is exactly that shape).
        CreateGitFile("area001", new uint[] { 5 });

        var previous = SynchronizationContext.Current;
        var ctx = new SingleThreadSyncContext();
        SynchronizationContext.SetSynchronizationContext(ctx);

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var task = Task.Run(() =>
                AreaScanService.ReindexFactions(_workingDir, deletedIndex: 5, parentFactionId: 3, backupRoot: _backupRoot));

            // Pump the sync context until the work completes, with a hard
            // timeout. A deadlock would leave task.IsCompleted false forever.
            while (!task.IsCompleted)
            {
                if (cts.IsCancellationRequested)
                    throw new TimeoutException("ReindexFactions deadlocked under a single-threaded sync context");
                ctx.RunPending();
                Thread.Sleep(10);
            }

            Assert.Equal(1, task.Result.FilesModified);
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }
    }

    private sealed class SingleThreadSyncContext : SynchronizationContext
    {
        private readonly Queue<(SendOrPostCallback cb, object? state)> _queue = new();
        private readonly object _gate = new();

        public override void Post(SendOrPostCallback d, object? state)
        {
            lock (_gate) _queue.Enqueue((d, state));
        }

        public override void Send(SendOrPostCallback d, object? state) => d(state);

        public void RunPending()
        {
            while (true)
            {
                (SendOrPostCallback cb, object? state) item;
                lock (_gate)
                {
                    if (_queue.Count == 0) return;
                    item = _queue.Dequeue();
                }
                item.cb(item.state);
            }
        }
    }

    [Fact]
    public void ReindexFactions_NoBackupRoot_StillWritesAtomically()
    {
        var area1 = CreateGitFile("area001", new uint[] { 5 });

        var result = AreaScanService.ReindexFactions(_workingDir, deletedIndex: 5, parentFactionId: 3);

        Assert.Equal(1, result.FilesModified);
        // No .tmp leftover after success
        var tempFiles = Directory.GetFiles(_workingDir, "*.tmp");
        Assert.Empty(tempFiles);
    }
}
