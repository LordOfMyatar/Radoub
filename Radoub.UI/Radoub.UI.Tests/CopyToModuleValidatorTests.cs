using System.IO;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for CopyToModuleValidator — validation logic used by the
/// Copy-to-Module dialog to guard ResRef and Tag inputs before
/// writing a copied archive resource into the module directory.
/// </summary>
public class CopyToModuleValidatorTests : IDisposable
{
    private readonly string _tempDir;

    public CopyToModuleValidatorTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "CopyToModuleTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Validate_EmptyResRef_ReturnsEmptyResRefState()
    {
        var result = CopyToModuleValidator.Validate(
            resRef: "",
            tag: null,
            originalResRef: "nw_store_01",
            moduleDirectory: _tempDir,
            extension: ".utm",
            showTagAndName: true);

        Assert.Equal(CopyToModuleValidationState.EmptyResRef, result.State);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_WhitespaceResRef_ReturnsEmptyResRefState()
    {
        var result = CopyToModuleValidator.Validate(
            resRef: "   ",
            tag: null,
            originalResRef: "nw_store_01",
            moduleDirectory: _tempDir,
            extension: ".utm",
            showTagAndName: true);

        Assert.Equal(CopyToModuleValidationState.EmptyResRef, result.State);
    }

    [Fact]
    public void Validate_ResRefTooLong_ReturnsInvalidResRefState()
    {
        var result = CopyToModuleValidator.Validate(
            resRef: "this_is_way_too_long_for_aurora",
            tag: null,
            originalResRef: "nw_store_01",
            moduleDirectory: _tempDir,
            extension: ".utm",
            showTagAndName: true);

        Assert.Equal(CopyToModuleValidationState.InvalidResRef, result.State);
    }

    [Fact]
    public void Validate_ResRefInvalidChars_ReturnsInvalidResRefState()
    {
        var result = CopyToModuleValidator.Validate(
            resRef: "bad-name",
            tag: null,
            originalResRef: "nw_store_01",
            moduleDirectory: _tempDir,
            extension: ".utm",
            showTagAndName: true);

        Assert.Equal(CopyToModuleValidationState.InvalidResRef, result.State);
    }

    [Fact]
    public void Validate_DuplicateFileInModuleDir_ReturnsDuplicateFileState()
    {
        var existingResRef = "existing_store";
        File.WriteAllBytes(Path.Combine(_tempDir, existingResRef + ".utm"), new byte[] { 0 });

        var result = CopyToModuleValidator.Validate(
            resRef: existingResRef,
            tag: null,
            originalResRef: "nw_store_01",
            moduleDirectory: _tempDir,
            extension: ".utm",
            showTagAndName: true);

        Assert.Equal(CopyToModuleValidationState.DuplicateFile, result.State);
    }

    [Fact]
    public void Validate_TagTooLong_ReturnsTagTooLongState()
    {
        var longTag = new string('x', CopyToModuleValidator.MaxTagLength + 1);

        var result = CopyToModuleValidator.Validate(
            resRef: "good_name",
            tag: longTag,
            originalResRef: "nw_store_01",
            moduleDirectory: _tempDir,
            extension: ".utm",
            showTagAndName: true);

        Assert.Equal(CopyToModuleValidationState.TagTooLong, result.State);
    }

    [Fact]
    public void Validate_TagTooLongIgnoredWhenShowTagAndNameFalse()
    {
        var longTag = new string('x', CopyToModuleValidator.MaxTagLength + 1);

        var result = CopyToModuleValidator.Validate(
            resRef: "good_name",
            tag: longTag,
            originalResRef: "nw_store_01",
            moduleDirectory: _tempDir,
            extension: ".dlg",
            showTagAndName: false);

        Assert.Equal(CopyToModuleValidationState.Valid, result.State);
    }

    [Fact]
    public void Validate_TagAtMaxLength_IsValid()
    {
        var tagAtLimit = new string('x', CopyToModuleValidator.MaxTagLength);

        var result = CopyToModuleValidator.Validate(
            resRef: "good_name",
            tag: tagAtLimit,
            originalResRef: "nw_store_01",
            moduleDirectory: _tempDir,
            extension: ".utm",
            showTagAndName: true);

        Assert.Equal(CopyToModuleValidationState.Valid, result.State);
    }

    [Fact]
    public void Validate_UnchangedResRef_ReturnsUnchangedState()
    {
        var result = CopyToModuleValidator.Validate(
            resRef: "nw_store_01",
            tag: "TestTag",
            originalResRef: "nw_store_01",
            moduleDirectory: _tempDir,
            extension: ".utm",
            showTagAndName: true);

        Assert.Equal(CopyToModuleValidationState.Unchanged, result.State);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validate_UnchangedResRef_CaseInsensitive()
    {
        var result = CopyToModuleValidator.Validate(
            resRef: "NW_STORE_01",  // uppercase form should still fail Aurora validation first
            tag: "TestTag",
            originalResRef: "nw_store_01",
            moduleDirectory: _tempDir,
            extension: ".utm",
            showTagAndName: true);

        // Uppercase fails filename validation before the unchanged check.
        Assert.Equal(CopyToModuleValidationState.InvalidResRef, result.State);
    }

    [Fact]
    public void Validate_ValidInput_ReturnsValid()
    {
        var result = CopyToModuleValidator.Validate(
            resRef: "my_new_store",
            tag: "MyStore",
            originalResRef: "nw_store_01",
            moduleDirectory: _tempDir,
            extension: ".utm",
            showTagAndName: true);

        Assert.Equal(CopyToModuleValidationState.Valid, result.State);
        Assert.True(result.IsValid);
        Assert.Null(result.Message);
    }

    [Fact]
    public void Validate_EmptyModuleDirectory_SkipsDuplicateCheck()
    {
        // Tests that pass an empty module dir (for pure field validation)
        // should not error out on missing directory.
        var result = CopyToModuleValidator.Validate(
            resRef: "my_new_store",
            tag: null,
            originalResRef: "nw_store_01",
            moduleDirectory: "",
            extension: ".utm",
            showTagAndName: true);

        Assert.Equal(CopyToModuleValidationState.Valid, result.State);
    }

    [Fact]
    public void Validate_NullTag_IsValidWhenShowTagAndNameTrue()
    {
        // Null tag is how the dialog represents "not entered yet" — shouldn't fail validation.
        var result = CopyToModuleValidator.Validate(
            resRef: "my_new_store",
            tag: null,
            originalResRef: "nw_store_01",
            moduleDirectory: _tempDir,
            extension: ".utm",
            showTagAndName: true);

        Assert.Equal(CopyToModuleValidationState.Valid, result.State);
    }

    [Fact]
    public void CopyToModuleResult_AcceptsNullTagAndName()
    {
        // Parley-style result: ResRef only.
        var result = new CopyToModuleResult("my_dialog", NewTag: null, NewName: null);

        Assert.Equal("my_dialog", result.NewResRef);
        Assert.Null(result.NewTag);
        Assert.Null(result.NewName);
    }
}
