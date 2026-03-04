using Quartermaster.Services;
using Radoub.Formats.Utc;
using System.IO;
using Xunit;

namespace Quartermaster.Tests;

public class ScriptTemplateServiceTests : IDisposable
{
    private readonly string _tempDir;

    public ScriptTemplateServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ScriptTemplateTests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void LoadTemplate_ValidIni_ReturnsAllScripts()
    {
        var iniContent = """
            [ResRefs]
            OnBlocked=nw_c2_defaulte
            OnDamaged=nw_c2_default6
            OnDeath=nw_c2_default7
            OnConversation=nw_c2_default4
            OnDisturbed=nw_c2_default8
            OnCombatRoundEnd=nw_c2_default3
            OnHeartbeat=nw_c2_default1
            OnPhysicalAttacked=nw_c2_default5
            OnPerception=nw_c2_default2
            OnRested=nw_c2_defaulta
            OnSpawn=nw_c2_default9
            OnSpellCast=nw_c2_defaultb
            OnUserDefined=nw_c2_defaultd
            """;

        var filePath = Path.Combine(_tempDir, "test.ini");
        File.WriteAllText(filePath, iniContent);

        var result = ScriptTemplateService.LoadTemplate(filePath);

        Assert.Equal(13, result.Count);
        Assert.Equal("nw_c2_defaulte", result[nameof(UtcFile.ScriptOnBlocked)]);
        Assert.Equal("nw_c2_default6", result[nameof(UtcFile.ScriptDamaged)]);
        Assert.Equal("nw_c2_default7", result[nameof(UtcFile.ScriptDeath)]);
        Assert.Equal("nw_c2_default4", result[nameof(UtcFile.ScriptDialogue)]);
        Assert.Equal("nw_c2_default8", result[nameof(UtcFile.ScriptDisturbed)]);
        Assert.Equal("nw_c2_default3", result[nameof(UtcFile.ScriptEndRound)]);
        Assert.Equal("nw_c2_default1", result[nameof(UtcFile.ScriptHeartbeat)]);
        Assert.Equal("nw_c2_default5", result[nameof(UtcFile.ScriptAttacked)]);
        Assert.Equal("nw_c2_default2", result[nameof(UtcFile.ScriptOnNotice)]);
        Assert.Equal("nw_c2_defaulta", result[nameof(UtcFile.ScriptRested)]);
        Assert.Equal("nw_c2_default9", result[nameof(UtcFile.ScriptSpawn)]);
        Assert.Equal("nw_c2_defaultb", result[nameof(UtcFile.ScriptSpellAt)]);
        Assert.Equal("nw_c2_defaultd", result[nameof(UtcFile.ScriptUserDefine)]);
    }

    [Fact]
    public void LoadTemplate_EmptyValues_ReturnsEmptyStrings()
    {
        var iniContent = """
            [ResRefs]
            OnBlocked=
            OnDamaged=
            """;

        var filePath = Path.Combine(_tempDir, "empty.ini");
        File.WriteAllText(filePath, iniContent);

        var result = ScriptTemplateService.LoadTemplate(filePath);

        Assert.Equal(2, result.Count);
        Assert.Equal("", result[nameof(UtcFile.ScriptOnBlocked)]);
        Assert.Equal("", result[nameof(UtcFile.ScriptDamaged)]);
    }

    [Fact]
    public void LoadTemplate_MissingResRefsSection_ReturnsEmpty()
    {
        var iniContent = """
            [SomeOtherSection]
            OnBlocked=nw_c2_defaulte
            """;

        var filePath = Path.Combine(_tempDir, "nosection.ini");
        File.WriteAllText(filePath, iniContent);

        var result = ScriptTemplateService.LoadTemplate(filePath);

        Assert.Empty(result);
    }

    [Fact]
    public void LoadTemplate_UnknownKeys_AreSkipped()
    {
        var iniContent = """
            [ResRefs]
            OnBlocked=nw_c2_defaulte
            OnUnknownEvent=some_script
            """;

        var filePath = Path.Combine(_tempDir, "unknown.ini");
        File.WriteAllText(filePath, iniContent);

        var result = ScriptTemplateService.LoadTemplate(filePath);

        Assert.Single(result);
        Assert.Equal("nw_c2_defaulte", result[nameof(UtcFile.ScriptOnBlocked)]);
    }

    [Fact]
    public void LoadTemplate_CommentsAndBlankLines_AreIgnored()
    {
        var iniContent = """
            ; This is a comment
            # Another comment

            [ResRefs]

            ; Script for blocked event
            OnBlocked=nw_c2_defaulte
            """;

        var filePath = Path.Combine(_tempDir, "comments.ini");
        File.WriteAllText(filePath, iniContent);

        var result = ScriptTemplateService.LoadTemplate(filePath);

        Assert.Single(result);
        Assert.Equal("nw_c2_defaulte", result[nameof(UtcFile.ScriptOnBlocked)]);
    }

    [Fact]
    public void LoadTemplate_CaseInsensitiveKeys()
    {
        var iniContent = """
            [ResRefs]
            onblocked=nw_c2_defaulte
            ONDAMAGED=nw_c2_default6
            """;

        var filePath = Path.Combine(_tempDir, "case.ini");
        File.WriteAllText(filePath, iniContent);

        var result = ScriptTemplateService.LoadTemplate(filePath);

        Assert.Equal(2, result.Count);
        Assert.Equal("nw_c2_defaulte", result[nameof(UtcFile.ScriptOnBlocked)]);
        Assert.Equal("nw_c2_default6", result[nameof(UtcFile.ScriptDamaged)]);
    }

    [Fact]
    public void SaveTemplate_WritesValidIni()
    {
        var scripts = new Dictionary<string, string>
        {
            [nameof(UtcFile.ScriptOnBlocked)] = "nw_c2_defaulte",
            [nameof(UtcFile.ScriptDamaged)] = "nw_c2_default6",
            [nameof(UtcFile.ScriptDeath)] = "nw_c2_default7",
            [nameof(UtcFile.ScriptDialogue)] = "nw_c2_default4",
            [nameof(UtcFile.ScriptDisturbed)] = "nw_c2_default8",
            [nameof(UtcFile.ScriptEndRound)] = "nw_c2_default3",
            [nameof(UtcFile.ScriptHeartbeat)] = "nw_c2_default1",
            [nameof(UtcFile.ScriptAttacked)] = "nw_c2_default5",
            [nameof(UtcFile.ScriptOnNotice)] = "nw_c2_default2",
            [nameof(UtcFile.ScriptRested)] = "nw_c2_defaulta",
            [nameof(UtcFile.ScriptSpawn)] = "nw_c2_default9",
            [nameof(UtcFile.ScriptSpellAt)] = "nw_c2_defaultb",
            [nameof(UtcFile.ScriptUserDefine)] = "nw_c2_defaultd"
        };

        var filePath = Path.Combine(_tempDir, "saved.ini");
        ScriptTemplateService.SaveTemplate(filePath, scripts);

        var content = File.ReadAllText(filePath);

        Assert.Contains("[ResRefs]", content);
        Assert.Contains("OnBlocked=nw_c2_defaulte", content);
        Assert.Contains("OnDamaged=nw_c2_default6", content);
        Assert.Contains("OnHeartbeat=nw_c2_default1", content);
        Assert.Contains("OnSpawn=nw_c2_default9", content);
    }

    [Fact]
    public void SaveTemplate_MissingScripts_WritesEmptyValues()
    {
        var scripts = new Dictionary<string, string>
        {
            [nameof(UtcFile.ScriptSpawn)] = "my_spawn"
        };

        var filePath = Path.Combine(_tempDir, "partial.ini");
        ScriptTemplateService.SaveTemplate(filePath, scripts);

        var content = File.ReadAllText(filePath);

        // All 13 keys should be present
        Assert.Contains("OnSpawn=my_spawn", content);
        Assert.Contains("OnBlocked=", content);
        Assert.Contains("OnDeath=", content);
    }

    [Fact]
    public void RoundTrip_SaveThenLoad_PreservesAllValues()
    {
        var original = new Dictionary<string, string>
        {
            [nameof(UtcFile.ScriptOnBlocked)] = "custom_block",
            [nameof(UtcFile.ScriptDamaged)] = "custom_damage",
            [nameof(UtcFile.ScriptDeath)] = "custom_death",
            [nameof(UtcFile.ScriptDialogue)] = "custom_conv",
            [nameof(UtcFile.ScriptDisturbed)] = "custom_disturb",
            [nameof(UtcFile.ScriptEndRound)] = "custom_round",
            [nameof(UtcFile.ScriptHeartbeat)] = "custom_heart",
            [nameof(UtcFile.ScriptAttacked)] = "custom_attack",
            [nameof(UtcFile.ScriptOnNotice)] = "custom_percep",
            [nameof(UtcFile.ScriptRested)] = "custom_rest",
            [nameof(UtcFile.ScriptSpawn)] = "custom_spawn",
            [nameof(UtcFile.ScriptSpellAt)] = "custom_spell",
            [nameof(UtcFile.ScriptUserDefine)] = "custom_user"
        };

        var filePath = Path.Combine(_tempDir, "roundtrip.ini");

        ScriptTemplateService.SaveTemplate(filePath, original);
        var loaded = ScriptTemplateService.LoadTemplate(filePath);

        Assert.Equal(original.Count, loaded.Count);
        foreach (var (key, value) in original)
        {
            Assert.Equal(value, loaded[key]);
        }
    }

    [Fact]
    public void IniKeyToFieldName_ValidKey_ReturnsFieldName()
    {
        Assert.Equal(nameof(UtcFile.ScriptSpawn), ScriptTemplateService.IniKeyToFieldName("OnSpawn"));
        Assert.Equal(nameof(UtcFile.ScriptDeath), ScriptTemplateService.IniKeyToFieldName("OnDeath"));
        Assert.Equal(nameof(UtcFile.ScriptAttacked), ScriptTemplateService.IniKeyToFieldName("OnPhysicalAttacked"));
    }

    [Fact]
    public void IniKeyToFieldName_InvalidKey_ReturnsNull()
    {
        Assert.Null(ScriptTemplateService.IniKeyToFieldName("OnFakeEvent"));
    }

    [Fact]
    public void FieldNameToIniKey_ValidField_ReturnsIniKey()
    {
        Assert.Equal("OnSpawn", ScriptTemplateService.FieldNameToIniKey(nameof(UtcFile.ScriptSpawn)));
        Assert.Equal("OnDeath", ScriptTemplateService.FieldNameToIniKey(nameof(UtcFile.ScriptDeath)));
        Assert.Equal("OnPhysicalAttacked", ScriptTemplateService.FieldNameToIniKey(nameof(UtcFile.ScriptAttacked)));
    }

    [Fact]
    public void FieldNameToIniKey_InvalidField_ReturnsNull()
    {
        Assert.Null(ScriptTemplateService.FieldNameToIniKey("FakeFieldName"));
    }

    [Fact]
    public void AllThirteenMappings_AreComplete()
    {
        // Verify all 13 script slots have valid bidirectional mappings
        var fieldNames = new[]
        {
            nameof(UtcFile.ScriptOnBlocked),
            nameof(UtcFile.ScriptDamaged),
            nameof(UtcFile.ScriptDeath),
            nameof(UtcFile.ScriptDialogue),
            nameof(UtcFile.ScriptDisturbed),
            nameof(UtcFile.ScriptEndRound),
            nameof(UtcFile.ScriptHeartbeat),
            nameof(UtcFile.ScriptAttacked),
            nameof(UtcFile.ScriptOnNotice),
            nameof(UtcFile.ScriptRested),
            nameof(UtcFile.ScriptSpawn),
            nameof(UtcFile.ScriptSpellAt),
            nameof(UtcFile.ScriptUserDefine)
        };

        foreach (var fieldName in fieldNames)
        {
            var iniKey = ScriptTemplateService.FieldNameToIniKey(fieldName);
            Assert.NotNull(iniKey);

            var roundTripped = ScriptTemplateService.IniKeyToFieldName(iniKey!);
            Assert.Equal(fieldName, roundTripped);
        }
    }

    [Fact]
    public void LoadTemplate_RealNwn1Format_ParsesCorrectly()
    {
        // Actual content from nwn1.ini (Aurora Toolset format)
        var iniContent = """
            [ResRefs]
            OnBlocked=nw_c2_defaulte
            OnDamaged=nw_c2_default6
            OnDeath=nw_c2_default7
            OnConversation=nw_c2_default4
            OnDisturbed=nw_c2_default8
            OnCombatRoundEnd=nw_c2_default3
            OnHeartbeat=nw_c2_default1
            OnPhysicalAttacked=nw_c2_default5
            OnPerception=nw_c2_default2
            OnRested=nw_c2_defaulta
            OnSpawn=nw_c2_default9
            OnSpellCast=nw_c2_defaultb
            OnUserDefined=nw_c2_defaultd
            """;

        var filePath = Path.Combine(_tempDir, "nwn1.ini");
        File.WriteAllText(filePath, iniContent);

        var result = ScriptTemplateService.LoadTemplate(filePath);

        // Verify all OC default scripts loaded
        Assert.Equal(13, result.Count);
        Assert.Equal("nw_c2_default9", result[nameof(UtcFile.ScriptSpawn)]);
        Assert.Equal("nw_c2_default1", result[nameof(UtcFile.ScriptHeartbeat)]);
    }
}
