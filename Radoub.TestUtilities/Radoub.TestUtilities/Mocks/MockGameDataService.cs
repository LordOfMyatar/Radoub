using Radoub.Formats.Services;
using Radoub.Formats.Ssf;
using Radoub.Formats.TwoDA;

namespace Radoub.TestUtilities.Mocks;

/// <summary>
/// Mock implementation of IGameDataService for unit testing.
/// Provides configurable 2DA data and TLK strings without requiring game files.
/// </summary>
public class MockGameDataService : IGameDataService
{
    private readonly Dictionary<string, TwoDAFile> _2daFiles = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<uint, string> _tlkStrings = new();
    private bool _isConfigured = true;

    /// <summary>
    /// Creates a new MockGameDataService with optional sample data.
    /// </summary>
    /// <param name="includeSampleData">If true, populates with common 2DA data for testing.</param>
    public MockGameDataService(bool includeSampleData = true)
    {
        if (includeSampleData)
        {
            PopulateSampleData();
        }
    }

    #region 2DA Access

    public TwoDAFile? Get2DA(string name)
    {
        return _2daFiles.TryGetValue(name, out var file) ? file : null;
    }

    public string? Get2DAValue(string twoDAName, int rowIndex, string columnName)
    {
        var file = Get2DA(twoDAName);
        return file?.GetValue(rowIndex, columnName);
    }

    public bool Has2DA(string name) => _2daFiles.ContainsKey(name);

    public void ClearCache() => _2daFiles.Clear();

    #endregion

    #region TLK String Resolution

    public string? GetString(uint strRef)
    {
        return _tlkStrings.TryGetValue(strRef, out var value) ? value : null;
    }

    public string? GetString(string? strRefStr)
    {
        if (string.IsNullOrEmpty(strRefStr) || strRefStr == "****")
            return null;

        if (uint.TryParse(strRefStr, out var strRef))
            return GetString(strRef);

        return null;
    }

    public bool HasCustomTlk => false;

    public void SetCustomTlk(string? path) { }

    #endregion

    #region Resource Access

    public byte[]? FindResource(string resRef, ushort resourceType) => null;

    public IEnumerable<GameResourceInfo> ListResources(ushort resourceType)
    {
        return Enumerable.Empty<GameResourceInfo>();
    }

    #endregion

    #region Soundset Access

    public SsfFile? GetSoundset(int soundsetId) => null;

    public SsfFile? GetSoundsetByResRef(string resRef) => null;

    public string? GetSoundsetResRef(int soundsetId) => null;

    #endregion

    #region Configuration

    public bool IsConfigured
    {
        get => _isConfigured;
        set => _isConfigured = value;
    }

    public void ReloadConfiguration() { }

    public void Dispose() { }

    #endregion

    #region Test Data Setup

    /// <summary>
    /// Add a 2DA file to the mock.
    /// </summary>
    public MockGameDataService With2DA(string name, TwoDAFile file)
    {
        _2daFiles[name] = file;
        return this;
    }

    /// <summary>
    /// Add a TLK string to the mock.
    /// </summary>
    public MockGameDataService WithString(uint strRef, string value)
    {
        _tlkStrings[strRef] = value;
        return this;
    }

    /// <summary>
    /// Add multiple TLK strings.
    /// </summary>
    public MockGameDataService WithStrings(params (uint strRef, string value)[] strings)
    {
        foreach (var (strRef, value) in strings)
        {
            _tlkStrings[strRef] = value;
        }
        return this;
    }

    /// <summary>
    /// Mark the service as not configured (for testing error paths).
    /// </summary>
    public MockGameDataService AsUnconfigured()
    {
        _isConfigured = false;
        return this;
    }

    #endregion

    #region Sample Data

    private void PopulateSampleData()
    {
        // racialtypes.2da - common races
        var racialTypes = new TwoDAFile();
        racialTypes.Columns.AddRange(new[] { "Label", "Name", "ConverName", "ConverNameLower" });
        AddRow(racialTypes, "0", "Dwarf", "5", "5", "5");
        AddRow(racialTypes, "1", "Elf", "6", "6", "6");
        AddRow(racialTypes, "2", "Gnome", "7", "7", "7");
        AddRow(racialTypes, "3", "Halfling", "8", "8", "8");
        AddRow(racialTypes, "4", "HalfElf", "9", "9", "9");
        AddRow(racialTypes, "5", "HalfOrc", "10", "10", "10");
        AddRow(racialTypes, "6", "Human", "11", "11", "11");
        _2daFiles["racialtypes"] = racialTypes;

        // classes.2da - common classes
        var classes = new TwoDAFile();
        classes.Columns.AddRange(new[] { "Label", "Name", "HitDie", "AttackBonusTable" });
        AddRow(classes, "0", "Barbarian", "40", "12", "CLS_ATK_1");
        AddRow(classes, "1", "Bard", "41", "6", "CLS_ATK_2");
        AddRow(classes, "2", "Cleric", "42", "8", "CLS_ATK_2");
        AddRow(classes, "3", "Druid", "43", "8", "CLS_ATK_2");
        AddRow(classes, "4", "Fighter", "44", "10", "CLS_ATK_1");
        AddRow(classes, "5", "Monk", "45", "8", "CLS_ATK_2");
        AddRow(classes, "6", "Paladin", "46", "10", "CLS_ATK_1");
        AddRow(classes, "7", "Ranger", "47", "10", "CLS_ATK_1");
        AddRow(classes, "8", "Rogue", "48", "6", "CLS_ATK_2");
        AddRow(classes, "9", "Sorcerer", "49", "4", "CLS_ATK_3");
        AddRow(classes, "10", "Wizard", "50", "4", "CLS_ATK_3");
        _2daFiles["classes"] = classes;

        // gender.2da
        var gender = new TwoDAFile();
        gender.Columns.AddRange(new[] { "Label", "Name" });
        AddRow(gender, "0", "Male", "60");
        AddRow(gender, "1", "Female", "61");
        _2daFiles["gender"] = gender;

        // appearance.2da - sample entries
        var appearance = new TwoDAFile();
        appearance.Columns.AddRange(new[] { "LABEL", "STRING_REF", "MODELTYPE", "RACE" });
        AddRow(appearance, "0", "A_Badger", "6798", "F", "****");
        AddRow(appearance, "1", "A_Bat", "6799", "F", "****");
        AddRow(appearance, "6", "P_HHF_", "246", "P", "4");  // Halfling female
        AddRow(appearance, "7", "P_HHM_", "247", "P", "4");  // Halfling male
        _2daFiles["appearance"] = appearance;

        // Common TLK strings
        WithStrings(
            (5, "Dwarf"),
            (6, "Elf"),
            (7, "Gnome"),
            (8, "Halfling"),
            (9, "Half-Elf"),
            (10, "Half-Orc"),
            (11, "Human"),
            (40, "Barbarian"),
            (41, "Bard"),
            (42, "Cleric"),
            (43, "Druid"),
            (44, "Fighter"),
            (45, "Monk"),
            (46, "Paladin"),
            (47, "Ranger"),
            (48, "Rogue"),
            (49, "Sorcerer"),
            (50, "Wizard"),
            (60, "Male"),
            (61, "Female")
        );
    }

    private static void AddRow(TwoDAFile file, string label, params string?[] values)
    {
        file.Rows.Add(new TwoDARow
        {
            Label = label,
            Values = values.ToList()
        });
    }

    #endregion
}
