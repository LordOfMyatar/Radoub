using Radoub.UI.Views;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Characterization tests for the shared New-blueprint dialog validation (#2517).
/// Locks in the Name/Tag/ResRef rules that were previously duplicated in Fence's NewStoreWindow
/// and Reliquary's NewPlaceableWindow.
/// </summary>
public class NewBlueprintValidationTests
{
    [Fact]
    public void Validate_AllValid_ReturnsNull()
    {
        Assert.Null(NewBlueprintValidation.Validate("General Store", "GENERAL_STORE", "general_store"));
    }

    [Fact]
    public void Validate_EmptyName_ReturnsNameError()
    {
        var error = NewBlueprintValidation.Validate("", "TAG", "resref");
        Assert.Equal("Name is required.", error);
    }

    [Fact]
    public void Validate_WhitespaceName_ReturnsNameError()
    {
        var error = NewBlueprintValidation.Validate("   ", "TAG", "resref");
        Assert.Equal("Name is required.", error);
    }

    [Fact]
    public void Validate_NullName_ReturnsNameError()
    {
        var error = NewBlueprintValidation.Validate(null, "TAG", "resref");
        Assert.Equal("Name is required.", error);
    }

    [Fact]
    public void Validate_EmptyTag_ReturnsTagError()
    {
        // Empty Tag is an error (not skipped) — matches the original windows.
        var error = NewBlueprintValidation.Validate("Name", "", "resref");
        Assert.Equal("Tag must be 1-32 characters (A-Z, 0-9, underscore).", error);
    }

    [Fact]
    public void Validate_EmptyResRef_ReturnsResRefRequiredError()
    {
        var error = NewBlueprintValidation.Validate("Name", "TAG", "");
        Assert.Equal("ResRef is required.", error);
    }

    [Fact]
    public void Validate_InvalidResRefCharacters_ReturnsResRefFormatError()
    {
        // Uppercase / illegal characters fail IsValidResRef.
        var error = NewBlueprintValidation.Validate("Name", "TAG", "Bad ResRef!");
        Assert.Equal("ResRef must be 1-16 lowercase alphanumeric/underscore characters.", error);
    }

    [Fact]
    public void Validate_NameTrimmedBeforeCheck()
    {
        // A name that is only valid after trimming still passes the name gate.
        var error = NewBlueprintValidation.Validate("  Name  ", "TAG", "resref");
        Assert.Null(error);
    }
}
