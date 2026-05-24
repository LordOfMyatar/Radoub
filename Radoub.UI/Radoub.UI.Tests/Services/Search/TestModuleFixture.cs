using Radoub.Formats.Gff;
using Radoub.Formats.Tests.Search.Rename;  // linked-in TestGffBuilder

namespace Radoub.UI.Tests.Services.Search;

/// <summary>
/// Builds a minimal real-on-disk module for orchestrator round-trip testing.
/// Module contains:
///   - louis_roumain.utc (creature with Conversation = "louis_dlg")
///   - louis_dlg.dlg     (dialog with no internal references — leaf)
///   - area01.git        (GIT instance pointing to "louis_roumain" creature)
///   - area01.are        (area with no script references — minimal)
///   - script1.nss       (nss source containing "louis_roumain" as a quoted string)
/// </summary>
public static class TestModuleFixture
{
    public static string CreateMinimalModule(string parentDir)
    {
        var moduleDir = Path.Combine(parentDir, $"module-{Guid.NewGuid():N}");
        Directory.CreateDirectory(moduleDir);

        // 1. louis_roumain.utc — references louis_dlg
        var utc = MakeUtc(conversation: "louis_dlg");
        File.WriteAllBytes(Path.Combine(moduleDir, "louis_roumain.utc"), GffWriter.Write(utc));

        // 2. louis_dlg.dlg — minimal valid DLG (no internal refs)
        var dlg = MakeMinimalDlg();
        File.WriteAllBytes(Path.Combine(moduleDir, "louis_dlg.dlg"), GffWriter.Write(dlg));

        // 3. area01.git — references louis_roumain via Creature List > TemplateResRef
        var git = MakeGitWithCreature("louis_roumain");
        File.WriteAllBytes(Path.Combine(moduleDir, "area01.git"), GffWriter.Write(git));

        // 4. area01.are — minimal valid ARE
        var are = MakeMinimalAre();
        File.WriteAllBytes(Path.Combine(moduleDir, "area01.are"), GffWriter.Write(are));

        // 5. script1.nss — text source with "louis_roumain" quoted
        File.WriteAllText(Path.Combine(moduleDir, "script1.nss"),
            "void main() {\n    object o = GetObjectByTag(\"louis_roumain\");\n}\n");

        return moduleDir;
    }

    /// <summary>
    /// Compute a SHA256 hash over the directory's files (filename + content concatenated,
    /// in alphabetical order). Used to detect unintended drift across rename/restore cycles.
    /// </summary>
    public static string ComputeDirectoryHash(string dir)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        var combined = new MemoryStream();
        foreach (var file in Directory.EnumerateFiles(dir).OrderBy(p => p, StringComparer.Ordinal))
        {
            var bytes = File.ReadAllBytes(file);
            var name = System.Text.Encoding.UTF8.GetBytes(Path.GetFileName(file));
            combined.Write(name, 0, name.Length);
            combined.WriteByte(0);
            combined.Write(bytes, 0, bytes.Length);
        }
        combined.Position = 0;
        return Convert.ToHexStringLower(sha.ComputeHash(combined));
    }

    // --- Inline builders (kept here to avoid cross-test-project references) ---

    private static GffFile MakeUtc(string conversation)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        GffFieldBuilder.AddCResRefField(root, "Conversation", conversation);
        return new GffFile { FileType = "UTC ", FileVersion = "V3.2", RootStruct = root };
    }

    private static GffFile MakeMinimalDlg()
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        return new GffFile { FileType = "DLG ", FileVersion = "V3.2", RootStruct = root };
    }

    private static GffFile MakeMinimalAre()
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        return new GffFile { FileType = "ARE ", FileVersion = "V3.2", RootStruct = root };
    }

    private static GffFile MakeGitWithCreature(string templateResRef)
    {
        var root = new GffStruct { Type = 0xFFFFFFFF };
        var instance = new GffStruct { Type = 0 };
        GffFieldBuilder.AddCResRefField(instance, "TemplateResRef", templateResRef);
        GffFieldBuilder.AddListField(root, "Creature List", new[] { instance });
        return new GffFile { FileType = "GIT ", FileVersion = "V3.2", RootStruct = root };
    }

    /// <summary>
    /// Build a richer fixture that exercises every ResRef scope tier the orchestrator
    /// supports: TypedGffField (GIT, UTM panel), DlgScriptParam, NssQuotedString,
    /// NssBareSubstring. Used by the orchestrator end-to-end test.
    /// </summary>
    public static string CreateRichModule(string parentDir)
    {
        var moduleDir = Path.Combine(parentDir, $"rich-{Guid.NewGuid():N}");
        Directory.CreateDirectory(moduleDir);

        // The renamed-file blueprint
        File.WriteAllBytes(Path.Combine(moduleDir, "louis_roumain.utc"),
            GffWriter.Write(TestGffBuilder.MakeUtc(conversation: "louis_dlg")));

        // DLG with an ActionParam carrying "louis_sword" (DlgScriptParam tier target)
        File.WriteAllBytes(Path.Combine(moduleDir, "louis_dlg.dlg"),
            GffWriter.Write(TestGffBuilder.MakeDlgWithActionParam(
                entryIndex: 0, key: "weapon_resref", value: "louis_sword")));

        // GIT instance points to louis_roumain (TypedGffField tier via GIT)
        File.WriteAllBytes(Path.Combine(moduleDir, "area01.git"),
            GffWriter.Write(TestGffBuilder.MakeGitWithList(
                "Creature List", "TemplateResRef", "louis_roumain")));

        // Minimal ARE
        var areRoot = new GffStruct { Type = 0xFFFFFFFF };
        File.WriteAllBytes(Path.Combine(moduleDir, "area01.are"),
            GffWriter.Write(new GffFile { FileType = "ARE ", FileVersion = "V3.2", RootStruct = areRoot }));

        // UTM with Weapons panel carrying louis_sword (TypedGffField via UTM panel branch)
        File.WriteAllBytes(Path.Combine(moduleDir, "store01.utm"),
            GffWriter.Write(TestGffBuilder.MakeUtmWithItems("Weapons", "louis_sword", "alice_shield")));

        // NSS source with both quoted (high-confidence) and bare-substring (low-confidence) refs
        File.WriteAllText(Path.Combine(moduleDir, "script1.nss"),
            "// Reminder: louis_roumain has the merchant key\n" +
            "void main() { object o = GetObjectByTag(\"louis_roumain\"); }\n");

        return moduleDir;
    }
}
