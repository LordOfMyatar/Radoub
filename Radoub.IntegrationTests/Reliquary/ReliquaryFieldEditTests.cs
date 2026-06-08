using FlaUI.Core.AutomationElements;
using Radoub.IntegrationTests.Shared;
using Xunit;

namespace Radoub.IntegrationTests.Reliquary;

/// <summary>
/// Deeper Reliquary interaction tests (#2304) — beyond the launch/close + single-toggle
/// smoke set. Covers IdentityCombat field presence, a Tag edit that persists across a
/// save→reload (re-launch) cycle, and the Conversation field rendering (moved to Identity, #2425).
/// </summary>
[Collection("ReliquarySequential")]
public class ReliquaryFieldEditTests : ReliquaryTestBase
{
    private static string CopyFixtureToTemp(string fixture, string tempName)
    {
        var source = TestPaths.GetReliquaryTestFile(fixture);
        var tempDir = TestPaths.CreateTempTestDirectory();
        var tempFile = Path.Combine(tempDir, tempName);
        File.Copy(source, tempFile);
        return tempFile;
    }

    private string? GetTextBoxText(string automationId)
    {
        var element = FindElement(automationId);
        if (element == null) return null;
        if (element.Patterns.Value.IsSupported) return element.Patterns.Value.Pattern.Value;
        return element.AsTextBox()?.Text;
    }

    [Fact]
    [Trait("Category", "IdentityCombat")]
    public void Reliquary_IdentityCombatPanel_CoreFieldsRender()
    {
        var tempFile = CopyFixtureToTemp("chest1.utp", "test_placeable.utp");
        try
        {
            StartApplication($"--file \"{tempFile}\"");
            Assert.True(WaitForTitleContains("test_placeable", FileOperationTimeout), "Placeable should load");

            Assert.NotNull(FindElement("Reliquary_Field_Name", maxRetries: 10));
            Assert.NotNull(FindElement("Reliquary_Field_Tag", maxRetries: 10));
            Assert.NotNull(FindElement("Reliquary_Field_ResRef", maxRetries: 10));
            Assert.NotNull(FindElement("Reliquary_Combo_Appearance", maxRetries: 10));
        }
        finally
        {
            StopApplication();
            TestPaths.CleanupTempDirectory(Path.GetDirectoryName(tempFile)!);
        }
    }

    [Fact]
    [Trait("Category", "Behavior")]
    public void Reliquary_IdentityPanel_ConversationFieldRenders()
    {
        var tempFile = CopyFixtureToTemp("chest1.utp", "test_placeable.utp");
        try
        {
            StartApplication($"--file \"{tempFile}\"");
            Assert.True(WaitForTitleContains("test_placeable", FileOperationTimeout), "Placeable should load");

            Assert.NotNull(FindElement("Reliquary_Field_Conversation", maxRetries: 10));
            Assert.NotNull(FindElement("Reliquary_Button_EditConversation", maxRetries: 10));
        }
        finally
        {
            StopApplication();
            TestPaths.CleanupTempDirectory(Path.GetDirectoryName(tempFile)!);
        }
    }

    [Fact]
    [Trait("Category", "Persistence")]
    public void Reliquary_TagEdit_PersistsAcrossSaveAndReload()
    {
        var tempFile = CopyFixtureToTemp("chest1.utp", "test_placeable.utp");
        try
        {
            StartApplication($"--file \"{tempFile}\"");
            Assert.True(WaitForTitleContains("test_placeable", FileOperationTimeout), "Placeable should load");

            var tag = FindElement("Reliquary_Field_Tag");
            Assert.NotNull(tag);
            EnsureFocused();
            // Append a marker to the existing tag, then save.
            tag!.AsTextBox()!.Enter("Z9");
            Assert.True(WaitForTitleContains("*", TimeSpan.FromSeconds(3)), "Title should show dirty marker after edit");
            SendCtrlS();
            Assert.True(WaitForTitleNotContains("*", FileOperationTimeout), "Ctrl+S should clear the dirty marker");

            var savedTag = GetTextBoxText("Reliquary_Field_Tag");
            StopApplication();

            // Re-launch and confirm the edited tag was persisted to disk.
            StartApplication($"--file \"{tempFile}\"");
            Assert.True(WaitForTitleContains("test_placeable", FileOperationTimeout), "Placeable should reload");
            var reloadedTag = GetTextBoxText("Reliquary_Field_Tag");

            Assert.Equal(savedTag, reloadedTag);
            Assert.Contains("Z9", reloadedTag ?? "");
        }
        finally
        {
            StopApplication();
            TestPaths.CleanupTempDirectory(Path.GetDirectoryName(tempFile)!);
        }
    }
}
