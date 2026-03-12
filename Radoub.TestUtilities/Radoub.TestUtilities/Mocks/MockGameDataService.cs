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

    private readonly Dictionary<(string resRef, ushort resourceType), byte[]> _resources = new(
        new ResourceKeyComparer());

    private readonly List<GameResourceInfo> _resourceInfos = new();

    public byte[]? FindResource(string resRef, ushort resourceType)
    {
        return _resources.TryGetValue((resRef.ToLowerInvariant(), resourceType), out var data) ? data : null;
    }

    public IEnumerable<GameResourceInfo> ListResources(ushort resourceType)
    {
        return _resourceInfos.Where(r => r.ResourceType == resourceType);
    }

    /// <summary>
    /// Add a resource to the mock for FindResource lookups.
    /// </summary>
    public void SetResource(string resRef, ushort resourceType, byte[] data)
    {
        _resources[(resRef.ToLowerInvariant(), resourceType)] = data;
    }

    /// <summary>
    /// Add a resource info entry for ListResources.
    /// </summary>
    public void AddResourceInfo(string resRef, ushort resourceType, string? sourcePath = null)
    {
        _resourceInfos.Add(new GameResourceInfo
        {
            ResRef = resRef,
            ResourceType = resourceType,
            Source = GameResourceSource.Bif,
            SourcePath = sourcePath
        });
    }

    private class ResourceKeyComparer : IEqualityComparer<(string resRef, ushort resourceType)>
    {
        public bool Equals((string resRef, ushort resourceType) x, (string resRef, ushort resourceType) y)
            => string.Equals(x.resRef, y.resRef, StringComparison.OrdinalIgnoreCase) && x.resourceType == y.resourceType;

        public int GetHashCode((string resRef, ushort resourceType) obj)
            => HashCode.Combine(obj.resRef.ToLowerInvariant(), obj.resourceType);
    }

    #endregion

    #region Soundset Access

    public SsfFile? GetSoundset(int soundsetId) => null;

    public SsfFile? GetSoundsetByResRef(string resRef) => null;

    public string? GetSoundsetResRef(int soundsetId) => null;

    #endregion

    #region Palette Access

    public IEnumerable<PaletteCategory> GetPaletteCategories(ushort resourceType)
    {
        // Return empty by default - tests can override if needed
        return Enumerable.Empty<PaletteCategory>();
    }

    public string? GetPaletteCategoryName(ushort resourceType, byte categoryId)
    {
        // Return null by default - tests can override if needed
        return null;
    }

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
    /// Set a single 2DA value. Creates the 2DA and column if needed.
    /// Convenience method for simple test setups.
    /// </summary>
    public void Set2DAValue(string twoDAName, int rowIndex, string columnName, string value)
    {
        var lowerName = twoDAName.ToLowerInvariant();
        var lowerCol = columnName.ToLowerInvariant();

        if (!_2daFiles.TryGetValue(lowerName, out var file))
        {
            file = new TwoDAFile();
            _2daFiles[lowerName] = file;
        }

        // Ensure column exists
        var colIndex = file.Columns.FindIndex(c => c.Equals(lowerCol, StringComparison.OrdinalIgnoreCase));
        if (colIndex < 0)
        {
            file.Columns.Add(columnName);
            colIndex = file.Columns.Count - 1;
        }

        // Ensure rows exist up to rowIndex
        while (file.Rows.Count <= rowIndex)
        {
            var newRow = new TwoDARow { Label = file.Rows.Count.ToString() };
            // Fill with nulls for existing columns
            for (int i = 0; i < file.Columns.Count; i++)
                newRow.Values.Add(null);
            file.Rows.Add(newRow);
        }

        // Ensure row has enough values
        var row = file.Rows[rowIndex];
        while (row.Values.Count <= colIndex)
            row.Values.Add(null);

        row.Values[colIndex] = value;
    }

    /// <summary>
    /// Set a TLK string. Convenience method for simple test setups.
    /// </summary>
    public void SetTlkString(uint strRef, string value)
    {
        _tlkStrings[strRef] = value;
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

        // classes.2da - common classes (values match NWN classes.2da exactly)
        // AlignRestrict/AlignRstrctType/InvertRestrict:
        //   Default (Invert=0): mask bits are PROHIBITED alignments
        //   Inverted (Invert=1): mask bits are REQUIRED alignments
        //   Barbarian: 0x02 prohibited on LC axis → cannot be Lawful
        //   Bard: 0x02 prohibited on LC axis → cannot be Lawful
        //   Druid: 0x01 required on both axes, inverted → must have Neutral on at least one axis
        //   Monk: 0x05 prohibited on LC axis → cannot be Neutral/Chaotic (must be Lawful)
        //   Paladin: 0x15 prohibited on both axes → cannot be N/C/E (must be Lawful Good)
        var classes = new TwoDAFile();
        classes.Columns.AddRange(new[] { "Label", "Name", "HitDie", "AttackBonusTable",
            "AlignRestrict", "AlignRstrctType", "InvertRestrict", "PlayerClass" });
        AddRow(classes, "0", "Barbarian", "40", "12", "CLS_ATK_1", "0x02", "0x01", "0", "1");
        AddRow(classes, "1", "Bard", "41", "6", "CLS_ATK_2", "0x02", "0x01", "0", "1");
        AddRow(classes, "2", "Cleric", "42", "8", "CLS_ATK_2", "0x00", "0x00", "0", "1");
        AddRow(classes, "3", "Druid", "43", "8", "CLS_ATK_2", "0x01", "0x03", "1", "1");
        AddRow(classes, "4", "Fighter", "44", "10", "CLS_ATK_1", "0x00", "0x01", "0", "1");
        AddRow(classes, "5", "Monk", "45", "8", "CLS_ATK_2", "0x05", "0x01", "0", "1");
        AddRow(classes, "6", "Paladin", "46", "10", "CLS_ATK_1", "0x15", "0x03", "0", "1");
        AddRow(classes, "7", "Ranger", "47", "10", "CLS_ATK_1", "0x00", "0x00", "0", "1");
        AddRow(classes, "8", "Rogue", "48", "6", "CLS_ATK_2", "0x00", "0x00", "0", "1");
        AddRow(classes, "9", "Sorcerer", "49", "4", "CLS_ATK_3", "0x00", "0x00", "0", "1");
        AddRow(classes, "10", "Wizard", "50", "4", "CLS_ATK_3", "0x00", "0x00", "0", "1");
        _2daFiles["classes"] = classes;

        // gender.2da
        var gender = new TwoDAFile();
        gender.Columns.AddRange(new[] { "Label", "Name" });
        AddRow(gender, "0", "Male", "60");
        AddRow(gender, "1", "Female", "61");
        _2daFiles["gender"] = gender;

        // appearance.2da - sample entries
        var appearance = new TwoDAFile();
        appearance.Columns.AddRange(new[] { "LABEL", "STRING_REF", "MODELTYPE", "RACE", "SIZECATEGORY" });
        AddRow(appearance, "0", "A_Badger", "6798", "F", "****", "1");    // Tiny
        AddRow(appearance, "1", "A_Bat", "6799", "F", "****", "1");      // Tiny
        AddRow(appearance, "6", "P_HHF_", "246", "P", "4", "2");        // Halfling female - Small
        AddRow(appearance, "7", "P_HHM_", "247", "P", "4", "3");        // Halfling male - Medium
        _2daFiles["appearance"] = appearance;

        // phenotype.2da - body types
        // Note: real NWN data has row 1 empty, but we pack them for testing
        // since GetAllPhenotypes breaks at first empty row after finding data
        var phenotype = new TwoDAFile();
        phenotype.Columns.AddRange(new[] { "Label", "Name" });
        AddRow(phenotype, "0", "Normal", "6900");
        AddRow(phenotype, "2", "Large", "6901");
        _2daFiles["phenotype"] = phenotype;

        // portraits.2da - portrait entries
        var portraits = new TwoDAFile();
        portraits.Columns.AddRange(new[] { "BaseResRef", "Sex" });
        AddRow(portraits, "0", "hu_m_01_", "0");
        AddRow(portraits, "1", "hu_f_01_", "1");
        AddRow(portraits, "2", "****", "****");  // empty row
        AddRow(portraits, "3", "el_m_01_", "0");
        _2daFiles["portraits"] = portraits;

        // wingmodel.2da - wing types
        var wingmodel = new TwoDAFile();
        wingmodel.Columns.AddRange(new[] { "LABEL" });
        AddRow(wingmodel, "0", "****");
        AddRow(wingmodel, "1", "Angel");
        AddRow(wingmodel, "2", "Demon");
        AddRow(wingmodel, "3", "Butterfly");
        _2daFiles["wingmodel"] = wingmodel;

        // tailmodel.2da - tail types
        var tailmodel = new TwoDAFile();
        tailmodel.Columns.AddRange(new[] { "LABEL" });
        AddRow(tailmodel, "0", "****");
        AddRow(tailmodel, "1", "Lizard");
        AddRow(tailmodel, "2", "Bone");
        _2daFiles["tailmodel"] = tailmodel;

        // soundset.2da - sound sets
        var soundset = new TwoDAFile();
        soundset.Columns.AddRange(new[] { "LABEL", "STRREF" });
        AddRow(soundset, "0", "Male_1", "7000");
        AddRow(soundset, "1", "Female_1", "7001");
        AddRow(soundset, "2", "****", "****");  // empty row
        AddRow(soundset, "3", "Male_2", "7002");
        _2daFiles["soundset"] = soundset;

        // domains.2da - sample cleric domains
        // Columns: Label, Name, Description, Level_1..Level_9, GrantedFeat
        var domains = new TwoDAFile();
        domains.Columns.AddRange(new[] { "Label", "Name", "Description",
            "Level_1", "Level_2", "Level_3", "Level_4", "Level_5",
            "Level_6", "Level_7", "Level_8", "Level_9", "GrantedFeat" });
        // Row 0: Air domain (spell IDs 0-8, granted feat row 0)
        AddRow(domains, "0", "Air", "1000", "1100",
            "0", "1", "2", "3", "4", "5", "6", "7", "8", "0");
        // Row 1: Animal domain (spell IDs 9-17, granted feat row 1)
        AddRow(domains, "1", "Animal", "1001", "1101",
            "9", "10", "11", "12", "13", "14", "15", "16", "17", "1");
        // Row 2: Death domain (7 spells, no level 8-9, no granted feat)
        AddRow(domains, "2", "Death", "1002", "1102",
            "18", "19", "20", "21", "22", "23", "24", "****", "****", "****");
        // Row 3: empty/invalid domain
        AddRow(domains, "3", "****", "****",
            "****", "****", "****", "****", "****", "****", "****", "****", "****", "****");
        _2daFiles["domains"] = domains;

        // spells.2da - domain spell entries (minimal: just Name column for GetSpellName)
        var spells = new TwoDAFile();
        spells.Columns.AddRange(new[] { "Label", "Name" });
        // Air domain spells (rows 0-8)
        AddRow(spells, "0", "Call Lightning", "2000");
        AddRow(spells, "1", "Gust of Wind", "2001");
        AddRow(spells, "2", "Lightning Bolt", "2002");
        AddRow(spells, "3", "Chain Lightning", "2003");
        AddRow(spells, "4", "Whirlwind", "2004");
        AddRow(spells, "5", "Control Winds", "2005");
        AddRow(spells, "6", "Storm of Vengeance", "2006");
        AddRow(spells, "7", "Elemental Swarm", "2007");
        AddRow(spells, "8", "Gate", "2008");
        // Animal domain spells (rows 9-17)
        AddRow(spells, "9", "Cat's Grace", "2009");
        AddRow(spells, "10", "Bull's Strength", "2010");
        AddRow(spells, "11", "True Seeing", "2011");
        AddRow(spells, "12", "Polymorph Self", "2012");
        AddRow(spells, "13", "Summon Creature V", "2013");
        AddRow(spells, "14", "Greater Stoneskin", "2014");
        AddRow(spells, "15", "Creeping Doom", "2015");
        AddRow(spells, "16", "Finger of Death", "2016");
        AddRow(spells, "17", "Shapechange", "2017");
        // Death domain spells (rows 18-24)
        AddRow(spells, "18", "Phantasmal Killer", "2018");
        AddRow(spells, "19", "Enervation", "2019");
        AddRow(spells, "20", "Circle of Death", "2020");
        AddRow(spells, "21", "Destruction", "2021");
        AddRow(spells, "22", "Horrid Wilting", "2022");
        AddRow(spells, "23", "Wail of the Banshee", "2023");
        AddRow(spells, "24", "Implosion", "2024");
        _2daFiles["spells"] = spells;

        // feat.2da - domain-granted feat entries (minimal: just FEAT column for name lookup)
        var feat = new TwoDAFile();
        feat.Columns.AddRange(new[] { "Label", "FEAT" });
        // Row 0: Air domain granted feat
        AddRow(feat, "0", "AirGrantedFeat", "3000");
        // Row 1: Animal domain granted feat
        AddRow(feat, "1", "AnimalGrantedFeat", "3001");
        _2daFiles["feat"] = feat;

        // packages.2da - class packages with domain info for Cleric, Associate for familiar/companion
        var packages = new TwoDAFile();
        packages.Columns.AddRange(new[] { "Label", "Name", "ClassID", "Domain1", "Domain2", "Associate" });
        // Row 0: Cleric default package (ClassID=2, has domains, no familiar)
        AddRow(packages, "0", "Cleric_Default", "100", "2", "0", "1", "****");
        // Row 1: Fighter default package (ClassID=4, no domains, no familiar)
        AddRow(packages, "1", "Fighter_Default", "101", "4", "****", "****", "****");
        // Row 2: Wizard default package (ClassID=10, has familiar)
        AddRow(packages, "2", "Wizard_Default", "102", "10", "****", "****", "0");
        // Row 3: Sorcerer default package (ClassID=9, has familiar)
        AddRow(packages, "3", "Sorcerer_Default", "103", "9", "****", "****", "0");
        // Row 4: Druid default package (ClassID=3, has animal companion)
        AddRow(packages, "4", "Druid_Default", "104", "3", "****", "****", "0");
        _2daFiles["packages"] = packages;

        // hen_familiar.2da - familiar types (matches NWN game data)
        var familiars = new TwoDAFile();
        familiars.Columns.AddRange(new[] { "NAME", "STRREF" });
        AddRow(familiars, "0", "Bat", "4000");
        AddRow(familiars, "1", "Panther", "4001");
        AddRow(familiars, "2", "HellHound", "4002");
        AddRow(familiars, "3", "Imp", "4003");
        AddRow(familiars, "4", "FireMephit", "4004");
        AddRow(familiars, "5", "IceMephit", "4005");
        AddRow(familiars, "6", "Pixie", "4006");
        AddRow(familiars, "7", "Raven", "4007");
        AddRow(familiars, "8", "FaerieDragon", "4008");
        AddRow(familiars, "9", "PseudoDragon", "4009");
        AddRow(familiars, "10", "Eyeball", "4010");
        _2daFiles["hen_familiar"] = familiars;

        // hen_companion.2da - animal companion types (matches NWN game data)
        var companions = new TwoDAFile();
        companions.Columns.AddRange(new[] { "NAME", "STRREF" });
        AddRow(companions, "0", "Badger", "5000");
        AddRow(companions, "1", "Wolf", "5001");
        AddRow(companions, "2", "Bear", "5002");
        AddRow(companions, "3", "Boar", "5003");
        AddRow(companions, "4", "Hawk", "5004");
        AddRow(companions, "5", "Panther", "5005");
        AddRow(companions, "6", "GiantSpider", "5006");
        AddRow(companions, "7", "DireWolf", "5007");
        AddRow(companions, "8", "DireRat", "5008");
        _2daFiles["hen_companion"] = companions;

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
            (61, "Female"),
            // Domain names and descriptions
            (1000, "Air"),
            (1001, "Animal"),
            (1002, "Death"),
            (1100, "The Air domain grants power over wind and lightning."),
            (1101, "The Animal domain grants an affinity with animals."),
            (1102, "The Death domain grants power over death and undeath."),
            // Domain spell names (Air: 2000-2008, Animal: 2009-2017, Death: 2018-2024)
            (2000, "Call Lightning"),
            (2001, "Gust of Wind"),
            (2002, "Lightning Bolt"),
            (2003, "Chain Lightning"),
            (2004, "Whirlwind"),
            (2005, "Control Winds"),
            (2006, "Storm of Vengeance"),
            (2007, "Elemental Swarm"),
            (2008, "Gate"),
            (2009, "Cat's Grace"),
            (2010, "Bull's Strength"),
            (2011, "True Seeing"),
            (2012, "Polymorph Self"),
            (2013, "Summon Creature V"),
            (2014, "Greater Stoneskin"),
            (2015, "Creeping Doom"),
            (2016, "Finger of Death"),
            (2017, "Shapechange"),
            (2018, "Phantasmal Killer"),
            (2019, "Enervation"),
            (2020, "Circle of Death"),
            (2021, "Destruction"),
            (2022, "Horrid Wilting"),
            (2023, "Wail of the Banshee"),
            (2024, "Implosion"),
            // Domain feat names
            (3000, "Elemental Turning"),
            (3001, "Animal Companion"),
            // Package names
            (100, "Cleric Default"),
            (101, "Fighter Default"),
            (102, "Wizard Default"),
            (103, "Sorcerer Default"),
            (104, "Druid Default"),
            // Familiar names (matches NWN game data)
            (4000, "Bat"),
            (4001, "Panther"),
            (4002, "Hell Hound"),
            (4003, "Imp"),
            (4004, "Fire Mephit"),
            (4005, "Ice Mephit"),
            (4006, "Pixie"),
            (4007, "Raven"),
            (4008, "Faerie Dragon"),
            (4009, "Pseudo Dragon"),
            (4010, "Eyeball"),
            // Animal companion names (matches NWN game data)
            (5000, "Badger"),
            (5001, "Wolf"),
            (5002, "Brown Bear"),
            (5003, "Boar"),
            (5004, "Hawk"),
            (5005, "Panther"),
            (5006, "Giant Spider"),
            (5007, "Dire Wolf"),
            (5008, "Dire Rat"),
            // Appearance names
            (6798, "Badger"),
            (6799, "Bat"),
            // Phenotype names
            (6900, "Normal"),
            (6901, "Large"),
            // Sound set names
            (7000, "Male Voice 1"),
            (7001, "Female Voice 1"),
            (7002, "Male Voice 2")
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
