using Radoub.Formats.Common;
using Radoub.Formats.Gff;
using Radoub.Formats.Search;
using Radoub.Formats.Search.Rename;
using Xunit;

namespace Radoub.Formats.Tests.Search.Rename;

public class ResRefReferenceScannerTests
{
    [Fact]
    public void Scan_UtcConversationField_FindsReference()
    {
        var gff = TestGffBuilder.MakeUtc(conversation: "louis_roumain");
        var scanner = new ResRefReferenceScanner();

        var refs = scanner.Scan(gff, ResourceTypes.Utc, oldResRef: "louis_roumain", filePath: "/m/test.utc");

        Assert.Single(refs);
        Assert.Equal("Conversation", refs[0].Field?.Name);
        Assert.Equal("louis_roumain", refs[0].OldValue);
        Assert.Equal(ResRefScopeTier.TypedGffField, refs[0].ScopeTier);
    }

    [Theory]
    [InlineData("Louis_Roumain")]
    [InlineData("LOUIS_ROUMAIN")]
    [InlineData("louis_roumain")]
    public void Scan_FindsCaseInsensitiveMatches(string actualValueInGff)
    {
        var gff = TestGffBuilder.MakeUtc(conversation: actualValueInGff);
        var scanner = new ResRefReferenceScanner();

        var refs = scanner.Scan(gff, ResourceTypes.Utc, oldResRef: "louis_roumain", filePath: "/m/test.utc");

        Assert.Single(refs);
        Assert.Equal(actualValueInGff, refs[0].OldValue);  // preserves original case in OldValue
    }

    [Fact]
    public void Scan_TypedField_DoesNotMatchSubstrings()
    {
        var gff = TestGffBuilder.MakeUtc(conversation: "louis_roumain_extra");
        var scanner = new ResRefReferenceScanner();

        var refs = scanner.Scan(gff, ResourceTypes.Utc, oldResRef: "louis_roumain", filePath: "/m/test.utc");

        Assert.Empty(refs);
    }

    // UTI files have no script-event fields per BioWare spec — items use
    // ItemProperty slots, not script events. No scanner test needed for UTI
    // beyond TemplateResRef (which is a self-identifier, not a reference TO
    // something else, and is therefore not in the scanner's scope).

    [Theory]
    [InlineData("OnEnter")]
    [InlineData("OnExit")]
    [InlineData("OnHeartbeat")]
    [InlineData("OnUserDefined")]
    public void Scan_AreScriptField_FindsReference(string fieldName)
    {
        var gff = TestGffBuilder.MakeAreWithScriptField(fieldName, "test_script");
        var scanner = new ResRefReferenceScanner();

        var refs = scanner.Scan(gff, ResourceTypes.Are, oldResRef: "test_script", filePath: "/m/area.are");

        Assert.Single(refs);
        Assert.Equal(fieldName, refs[0].Field?.Name);
    }

    [Fact]
    public void Scan_UtdConversation_FindsReference()
    {
        var gff = TestGffBuilder.MakeUtd(conversation: "door_dialog");
        var scanner = new ResRefReferenceScanner();

        var refs = scanner.Scan(gff, ResourceTypes.Utd, oldResRef: "door_dialog", filePath: "/m/door.utd");

        Assert.Single(refs);
        Assert.Equal("Conversation", refs[0].Field?.Name);
    }

    [Theory]
    [InlineData("OnOpen")]
    [InlineData("OnLock")]
    [InlineData("OnUnlock")]
    public void Scan_UtdScriptField_FindsReference(string fieldName)
    {
        var gff = TestGffBuilder.MakeUtdWithScriptField(fieldName, "door_script");
        var scanner = new ResRefReferenceScanner();

        var refs = scanner.Scan(gff, ResourceTypes.Utd, oldResRef: "door_script", filePath: "/m/door.utd");

        Assert.Single(refs);
        Assert.Equal(fieldName, refs[0].Field?.Name);
    }

    [Fact]
    public void Scan_GitCreatureListInstances_FindsAllReferences()
    {
        var gff = TestGffBuilder.MakeGitWithList("Creature List", "TemplateResRef",
            "louis", "alice", "louis", "bob");
        var scanner = new ResRefReferenceScanner();

        var refs = scanner.Scan(gff, ResourceTypes.Git, oldResRef: "louis", filePath: "/m/area.git");

        Assert.Equal(2, refs.Count);
        Assert.All(refs, r => Assert.Equal("louis", r.OldValue.ToLowerInvariant()));
        Assert.All(refs, r => Assert.Contains("Creature List", r.Location));
    }

    [Theory]
    [InlineData("Creature List",  "TemplateResRef")]
    [InlineData("Door List",      "TemplateResRef")]
    [InlineData("Placeable List", "TemplateResRef")]
    [InlineData("StoreList",      "ResRef")]
    [InlineData("WaypointList",   "TemplateResRef")]
    [InlineData("Encounter List", "TemplateResRef")]
    [InlineData("TriggerList",    "TemplateResRef")]
    [InlineData("SoundList",      "TemplateResRef")]
    public void Scan_GitAllInstanceLists_FindsReference(string listName, string resRefField)
    {
        var gff = TestGffBuilder.MakeGitWithList(listName, resRefField, "louis");
        var scanner = new ResRefReferenceScanner();

        var refs = scanner.Scan(gff, ResourceTypes.Git, oldResRef: "louis", filePath: "/m/area.git");

        Assert.Single(refs);
        Assert.Contains(listName, refs[0].Location);
    }

    [Theory]
    [InlineData("utc")]  // standard UTC creature blueprint
    [InlineData("bic")]  // BIC player character — same structure
    public void Scan_UtcEquipmentSlots_FindsReference(string ext)
    {
        var resourceType = ext == "utc" ? ResourceTypes.Utc : ResourceTypes.Bic;
        var gff = TestGffBuilder.MakeUtc(equipResRefs: new[] { "louis_sword", "alice_shield", "louis_sword" });
        var scanner = new ResRefReferenceScanner();

        var refs = scanner.Scan(gff, resourceType, oldResRef: "louis_sword", filePath: $"/m/test.{ext}");

        Assert.Equal(2, refs.Count);
        Assert.All(refs, r => Assert.Contains("Equip_ItemList", r.Location));
    }

    [Fact]
    public void Scan_UtcInventoryItems_FindsReference()
    {
        var gff = TestGffBuilder.MakeUtc(inventoryResRefs: new[] { "louis_potion", "key1", "louis_potion" });
        var scanner = new ResRefReferenceScanner();

        var refs = scanner.Scan(gff, ResourceTypes.Utc, oldResRef: "louis_potion", filePath: "/m/test.utc");

        Assert.Equal(2, refs.Count);
        Assert.All(refs, r => Assert.Contains("ItemList", r.Location));
    }

    [Fact]
    public void Scan_UtpInventory_FindsReference()
    {
        var gff = TestGffBuilder.MakeUtpWithInventory("trap_part", "key2", "trap_part");
        var scanner = new ResRefReferenceScanner();

        var refs = scanner.Scan(gff, ResourceTypes.Utp, oldResRef: "trap_part", filePath: "/m/chest.utp");

        Assert.Equal(2, refs.Count);
        Assert.All(refs, r => Assert.Contains("ItemList", r.Location));
    }

    [Fact]
    public void Scan_UtmStorePanelItems_FindsReference()
    {
        var gff = TestGffBuilder.MakeUtmWithItems("Weapons", "louis_sword", "key3", "louis_sword");
        var scanner = new ResRefReferenceScanner();

        var refs = scanner.Scan(gff, ResourceTypes.Utm, oldResRef: "louis_sword", filePath: "/m/store.utm");

        Assert.Equal(2, refs.Count);
        Assert.All(refs, r => Assert.Contains("Weapons", r.Location));
        Assert.All(refs, r => Assert.Contains("InventoryRes", r.Location));
    }

    [Fact]
    public void Scan_DlgEntrySound_FindsReference()
    {
        var gff = TestGffBuilder.MakeDlgWithSound(entryIndex: 2, sound: "louis_voice");
        var scanner = new ResRefReferenceScanner();

        var refs = scanner.Scan(gff, ResourceTypes.Dlg, oldResRef: "louis_voice", filePath: "/m/x.dlg");

        Assert.Single(refs);
        Assert.Contains("Entry 2", refs[0].Location);
    }

    [Fact]
    public void Scan_DlgActionParam_FindsSubstringMatch()
    {
        // Param value contains "louis" as substring (typical: "target_resref=louis")
        var gff = TestGffBuilder.MakeDlgWithActionParam(0, "target_resref", "louis");
        var scanner = new ResRefReferenceScanner();

        var refs = scanner.Scan(gff, ResourceTypes.Dlg, oldResRef: "louis", filePath: "/m/x.dlg");

        Assert.Contains(refs, r => r.ScopeTier == ResRefScopeTier.DlgScriptParam);
    }

    [Fact]
    public void Scan_DlgActionParam_DoesNotMatchKey()
    {
        // Match in KEY (not value) — should NOT be returned per spec
        var gff = TestGffBuilder.MakeDlgWithActionParam(0, "louis_target", "alice");
        var scanner = new ResRefReferenceScanner();

        var refs = scanner.Scan(gff, ResourceTypes.Dlg, oldResRef: "louis", filePath: "/m/x.dlg");

        Assert.DoesNotContain(refs, r => r.ScopeTier == ResRefScopeTier.DlgScriptParam);
    }

    [Fact]
    public void Scan_DlgConditionParam_FindsSubstringMatch()
    {
        var gff = TestGffBuilder.MakeDlgWithConditionParam(0, "check_resref", "louis");
        var scanner = new ResRefReferenceScanner();

        var refs = scanner.Scan(gff, ResourceTypes.Dlg, oldResRef: "louis", filePath: "/m/x.dlg");

        Assert.Contains(refs, r => r.ScopeTier == ResRefScopeTier.DlgScriptParam
            && r.Location.Contains("ConditionParams"));
    }

    public static IEnumerable<object[]> IfoScalarFieldData() => new[]
    {
        new object[] { "Mod_Entry_Area", (Func<string, GffFile>)((string s) => TestGffBuilder.MakeIfo(entryArea: s)) },
        new object[] { "Mod_DefaultBic", (Func<string, GffFile>)((string s) => TestGffBuilder.MakeIfo(defaultBic: s)) },
        new object[] { "Mod_StartMovie", (Func<string, GffFile>)((string s) => TestGffBuilder.MakeIfo(startMovie: s)) },
        new object[] { "Mod_CustomTlk",  (Func<string, GffFile>)((string s) => TestGffBuilder.MakeIfo(customTlk: s)) },
        new object[] { "Mod_OnHeartbeat", (Func<string, GffFile>)((string s) => TestGffBuilder.MakeIfo(onHeartbeat: s)) }
    };

    [Theory]
    [MemberData(nameof(IfoScalarFieldData))]
    public void Scan_IfoScalarField_FindsReference(string registeredGffPath, Func<string, GffFile> builder)
    {
        var gff = builder("test_target");
        var scanner = new ResRefReferenceScanner();

        var refs = scanner.Scan(gff, ResourceTypes.Ifo, oldResRef: "test_target", filePath: "/m/module.ifo");

        Assert.Single(refs);
        // The registered Field.Name often differs from the GFF path — match on GffPath for robustness
        Assert.True(
            refs[0].Field?.GffPath == registeredGffPath || refs[0].Field?.Name == registeredGffPath,
            $"Expected field GffPath or Name '{registeredGffPath}', got Name='{refs[0].Field?.Name}' GffPath='{refs[0].Field?.GffPath}'");
    }

    [Fact]
    public void Scan_IfoModHakList_FindsReference()
    {
        var gff = TestGffBuilder.MakeIfoWithHakList("base_hak", "louis_hak", "extra_hak");
        var scanner = new ResRefReferenceScanner();

        var refs = scanner.Scan(gff, ResourceTypes.Ifo, oldResRef: "louis_hak", filePath: "/m/module.ifo");

        Assert.Single(refs);
        Assert.Contains("Mod_HakList", refs[0].Location);
    }

    public static IEnumerable<object[]> CoverageMatrixData() => new[]
    {
        // resourceType, builder factory, oldResRef
        new object[] { ResourceTypes.Utc, (Func<GffFile>)(() => TestGffBuilder.MakeUtc(conversation: "rr")), "rr" },
        new object[] { ResourceTypes.Bic, (Func<GffFile>)(() => TestGffBuilder.MakeUtc(conversation: "rr")), "rr" },  // BIC == UTC
        // UTI omitted: no script-event fields per BioWare spec; items use ItemProperty slots
        new object[] { ResourceTypes.Utm, (Func<GffFile>)(() => TestGffBuilder.MakeUtmWithItems("Weapons", "rr")), "rr" },
        new object[] { ResourceTypes.Utp, (Func<GffFile>)(() => TestGffBuilder.MakeUtpWithInventory("rr")), "rr" },
        new object[] { ResourceTypes.Utd, (Func<GffFile>)(() => TestGffBuilder.MakeUtd(conversation: "rr")), "rr" },
        new object[] { ResourceTypes.Dlg, (Func<GffFile>)(() => TestGffBuilder.MakeDlgWithSound(0, "rr")), "rr" },
        new object[] { ResourceTypes.Git, (Func<GffFile>)(() => TestGffBuilder.MakeGitWithList("Creature List", "TemplateResRef", "rr")), "rr" },
        new object[] { ResourceTypes.Are, (Func<GffFile>)(() => TestGffBuilder.MakeAreWithScriptField("OnEnter", "rr")), "rr" },
        new object[] { ResourceTypes.Ifo, (Func<GffFile>)(() => TestGffBuilder.MakeIfo(entryArea: "rr")), "rr" }
    };

    [Theory]
    [MemberData(nameof(CoverageMatrixData))]
    public void Scan_CoversAllSpecTier1FileTypes(ushort resourceType, Func<GffFile> builder, string oldResRef)
    {
        var gff = builder();
        var scanner = new ResRefReferenceScanner();

        var refs = scanner.Scan(gff, resourceType, oldResRef, filePath: $"/m/test.{resourceType}");

        Assert.NotEmpty(refs);
    }
}
