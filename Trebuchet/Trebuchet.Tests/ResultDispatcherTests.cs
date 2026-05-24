using System.Collections.Generic;
using Radoub.Formats.Common;
using RadoubLauncher.Services;

namespace Trebuchet.Tests;

/// <summary>
/// Unit tests for ResultDispatcher.Plan — pure decision logic that maps a
/// Marlinspike result row (file path + resource type) to a DispatchAction.
///
/// Covers the fallback chain for #2183: tool launch → configured external editor
/// → OS default handler → final no-file/file-missing terminals.
/// </summary>
public class ResultDispatcherTests
{
    private static readonly IReadOnlyDictionary<ushort, string> DefaultToolMap = new Dictionary<ushort, string>
    {
        [ResourceTypes.Dlg] = "Parley",
        [ResourceTypes.Utc] = "Quartermaster",
        [ResourceTypes.Uti] = "Relique",
    };

    [Fact]
    public void Plan_NullOrEmptyFilePath_ReturnsNoFile()
    {
        var plan = ResultDispatcher.Plan(
            filePath: null,
            resourceType: ResourceTypes.Nss,
            resourceToolMap: DefaultToolMap,
            codeEditorPath: null,
            fileExists: _ => true);

        Assert.Equal(DispatchAction.NoFile, plan.Action);
    }

    [Fact]
    public void Plan_FileDoesNotExist_ReturnsFileMissing()
    {
        var plan = ResultDispatcher.Plan(
            filePath: "/tmp/ghost.nss",
            resourceType: ResourceTypes.Nss,
            resourceToolMap: DefaultToolMap,
            codeEditorPath: null,
            fileExists: _ => false);

        Assert.Equal(DispatchAction.FileMissing, plan.Action);
        Assert.Equal("/tmp/ghost.nss", plan.FilePath);
    }

    [Fact]
    public void Plan_KnownResourceType_DispatchesToTool()
    {
        var plan = ResultDispatcher.Plan(
            filePath: "/m/test.dlg",
            resourceType: ResourceTypes.Dlg,
            resourceToolMap: DefaultToolMap,
            codeEditorPath: null,
            fileExists: _ => true);

        Assert.Equal(DispatchAction.ToolLaunch, plan.Action);
        Assert.Equal("Parley", plan.ToolName);
        Assert.Equal("/m/test.dlg", plan.FilePath);
    }

    [Fact]
    public void Plan_NssWithConfiguredEditor_DispatchesToExternalEditor()
    {
        var plan = ResultDispatcher.Plan(
            filePath: "/m/script.nss",
            resourceType: ResourceTypes.Nss,
            resourceToolMap: DefaultToolMap,
            codeEditorPath: "C:/tools/code.exe",
            fileExists: path => path == "/m/script.nss" || path == "C:/tools/code.exe");

        Assert.Equal(DispatchAction.ExternalEditor, plan.Action);
        Assert.Equal("C:/tools/code.exe", plan.EditorPath);
        Assert.Equal("/m/script.nss", plan.FilePath);
    }

    [Fact]
    public void Plan_NssWithoutEditor_FallsBackToOsDefault()
    {
        var plan = ResultDispatcher.Plan(
            filePath: "/m/script.nss",
            resourceType: ResourceTypes.Nss,
            resourceToolMap: DefaultToolMap,
            codeEditorPath: null,
            fileExists: _ => true);

        Assert.Equal(DispatchAction.OsDefault, plan.Action);
        Assert.Equal("/m/script.nss", plan.FilePath);
    }

    [Fact]
    public void Plan_NssWithEmptyEditorPath_FallsBackToOsDefault()
    {
        var plan = ResultDispatcher.Plan(
            filePath: "/m/script.nss",
            resourceType: ResourceTypes.Nss,
            resourceToolMap: DefaultToolMap,
            codeEditorPath: "",
            fileExists: _ => true);

        Assert.Equal(DispatchAction.OsDefault, plan.Action);
    }

    [Fact]
    public void Plan_EditorPathConfiguredButFileMissing_FallsBackToOsDefault()
    {
        // Editor path set but the configured editor exe no longer exists on disk —
        // don't try to spawn it; fall back to OS default handler.
        var plan = ResultDispatcher.Plan(
            filePath: "/m/script.nss",
            resourceType: ResourceTypes.Nss,
            resourceToolMap: DefaultToolMap,
            codeEditorPath: "C:/tools/missing.exe",
            fileExists: path => path == "/m/script.nss");

        Assert.Equal(DispatchAction.OsDefault, plan.Action);
    }

    [Fact]
    public void Plan_UnknownResourceTypeAnyExtension_UsesEditorFallback()
    {
        // ResourceType 0 (unknown), but file exists — should fall through to
        // ExternalEditor (if configured) or OsDefault. .ncs is a plausible case.
        var plan = ResultDispatcher.Plan(
            filePath: "/m/script.ncs",
            resourceType: 0,
            resourceToolMap: DefaultToolMap,
            codeEditorPath: "C:/tools/code.exe",
            fileExists: _ => true);

        Assert.Equal(DispatchAction.ExternalEditor, plan.Action);
        Assert.Equal("C:/tools/code.exe", plan.EditorPath);
    }

    [Fact]
    public void Plan_ToolLaunchTakesPrecedenceOverEditor()
    {
        // Even if CodeEditorPath is configured, a known resource type goes to its tool.
        var plan = ResultDispatcher.Plan(
            filePath: "/m/test.utc",
            resourceType: ResourceTypes.Utc,
            resourceToolMap: DefaultToolMap,
            codeEditorPath: "C:/tools/code.exe",
            fileExists: _ => true);

        Assert.Equal(DispatchAction.ToolLaunch, plan.Action);
        Assert.Equal("Quartermaster", plan.ToolName);
    }
}
