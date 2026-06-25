using Radoub.Formats.Itp;
using Radoub.Formats.Resolver;
using Radoub.Formats.Services;
using Radoub.Formats.Ssf;
using Radoub.Formats.TwoDA;

namespace Radoub.Formats.Tests;

/// <summary>
/// Minimal IGameDataService test double backed by in-memory 2DA tables. Only the
/// 2DA primitives the rules accessors call (Get2DA / Get2DAValue / Has2DA) are
/// implemented; the default-interface rules methods (GetXpForLevel, etc.) build on
/// those. Every other member throws so misuse is obvious.
/// </summary>
public sealed class FakeGameDataService : IGameDataService
{
    private readonly Dictionary<string, TwoDAFile> _tables = new(StringComparer.OrdinalIgnoreCase);

    public FakeGameDataService Add(string name, TwoDAFile table)
    {
        _tables[name] = table;
        return this;
    }

    public TwoDAFile? Get2DA(string name) => _tables.TryGetValue(name, out var t) ? t : null;
    public string? Get2DAValue(string twoDAName, int rowIndex, string columnName)
        => Get2DA(twoDAName)?.GetValue(rowIndex, columnName);
    public bool Has2DA(string name) => _tables.ContainsKey(name);
    public void ClearCache() { }

    // Unused members
    public string? GetString(uint strRef) => throw new NotImplementedException();
    public string? GetString(string? strRefStr) => throw new NotImplementedException();
    public bool HasCustomTlk => false;
    public void SetCustomTlk(string? path) => throw new NotImplementedException();
    public byte[]? FindResource(string resRef, ushort resourceType) => throw new NotImplementedException();
    public byte[]? FindBaseResource(string resRef, ushort resourceType) => throw new NotImplementedException();
    public ResourceResult? FindResourceWithSource(string resRef, ushort resourceType) => throw new NotImplementedException();
    public IEnumerable<GameResourceInfo> ListResources(ushort resourceType) => throw new NotImplementedException();
    public SsfFile? GetSoundset(int soundsetId) => throw new NotImplementedException();
    public SsfFile? GetSoundsetByResRef(string resRef) => throw new NotImplementedException();
    public string? GetSoundsetResRef(int soundsetId) => throw new NotImplementedException();
    public IEnumerable<PaletteCategory> GetPaletteCategories(ushort resourceType) => throw new NotImplementedException();
    public string? GetPaletteCategoryName(ushort resourceType, byte categoryId) => throw new NotImplementedException();
    public bool IsConfigured => true;
    public void ReloadConfiguration() => throw new NotImplementedException();
    public void ConfigureModuleHaks(string moduleDirectory) => throw new NotImplementedException();
    public void Dispose() { }

    /// <summary>Build a single-column/​multi-row 2DA table for test setup.</summary>
    public static TwoDAFile MakeTable(string[] columns, params string?[][] rows)
    {
        var t = new TwoDAFile();
        t.Columns.AddRange(columns);
        for (int i = 0; i < rows.Length; i++)
        {
            var r = new TwoDARow { Label = i.ToString() };
            r.Values.AddRange(rows[i]);
            t.Rows.Add(r);
        }
        return t;
    }
}

/// <summary>
/// Convenience builders for the BIC-conversion rules seam (#2481).
/// </summary>
public static class BicRulesFake
{
    public static IGameDataService Empty() => new FakeGameDataService();

    public static IGameDataService WithClassHitDie(int classId, int hitDie)
    {
        // classes.2da with a HitDie column; pad rows up to the requested class id.
        var rows = new string?[classId + 1][];
        for (int i = 0; i <= classId; i++)
            rows[i] = new[] { $"Class{i}", i == classId ? hitDie.ToString() : "0" };
        return new FakeGameDataService().Add("classes",
            FakeGameDataService.MakeTable(new[] { "Label", "HitDie" }, rows));
    }

    public static IGameDataService WithSkillCount(int count)
    {
        var rows = new string?[count][];
        for (int i = 0; i < count; i++) rows[i] = new[] { $"Skill{i}" };
        return new FakeGameDataService().Add("skills",
            FakeGameDataService.MakeTable(new[] { "Label" }, rows));
    }

    public static IGameDataService WithExpTable(params string[] xpPerLevel)
    {
        var rows = new string?[xpPerLevel.Length][];
        for (int i = 0; i < xpPerLevel.Length; i++) rows[i] = new[] { xpPerLevel[i] };
        return new FakeGameDataService().Add("exptable",
            FakeGameDataService.MakeTable(new[] { "XP" }, rows));
    }
}
