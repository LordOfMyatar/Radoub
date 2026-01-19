using Radoub.Formats.Common;
using Radoub.Formats.Gff;
using Radoub.UI.Services;
using Xunit;

namespace Radoub.UI.Tests;

/// <summary>
/// Tests for TlkService.
/// Issue #971 - Part of Epic #959 (UI Uniformity).
/// </summary>
public class TlkServiceTests : IDisposable
{
    private readonly TlkService _service;

    public TlkServiceTests()
    {
        _service = new TlkService();
    }

    public void Dispose()
    {
        _service.Dispose();
    }

    #region Initial State

    [Fact]
    public void NewService_HasNoTlkLoaded()
    {
        Assert.False(_service.IsPrimaryLoaded);
        Assert.False(_service.IsCustomLoaded);
        Assert.Equal(0, _service.PrimaryEntryCount);
        Assert.Equal(0, _service.CustomEntryCount);
    }

    [Fact]
    public void NewService_DefaultsToEnglish()
    {
        Assert.Equal(Language.English, _service.CurrentLanguage);
    }

    [Fact]
    public void NewService_DefaultsToMaleGender()
    {
        Assert.Equal(Gender.Male, _service.CurrentGender);
    }

    [Fact]
    public void NewService_HasEmptyAvailableLanguages()
    {
        Assert.Empty(_service.AvailableLanguages);
    }

    #endregion

    #region Loading

    [Fact]
    public void LoadPrimaryTlk_NonExistentFile_ReturnsFalse()
    {
        var result = _service.LoadPrimaryTlk("nonexistent.tlk");
        Assert.False(result);
        Assert.False(_service.IsPrimaryLoaded);
    }

    [Fact]
    public void LoadPrimaryTlk_NullPath_ReturnsFalse()
    {
        var result = _service.LoadPrimaryTlk(null!);
        Assert.False(result);
    }

    [Fact]
    public void LoadPrimaryTlk_EmptyPath_ReturnsFalse()
    {
        var result = _service.LoadPrimaryTlk("");
        Assert.False(result);
    }

    [Fact]
    public void LoadCustomTlk_NonExistentFile_ReturnsFalse()
    {
        var result = _service.LoadCustomTlk("nonexistent.tlk");
        Assert.False(result);
        Assert.False(_service.IsCustomLoaded);
    }

    [Fact]
    public void ClearTlk_DoesNotThrow()
    {
        var exception = Record.Exception(() => _service.ClearTlk());
        Assert.Null(exception);
    }

    #endregion

    #region String Resolution Without TLK

    [Fact]
    public void GetString_NoTlkLoaded_ReturnsNull()
    {
        Assert.Null(_service.GetString(100));
    }

    [Fact]
    public void GetString_InvalidStrRef_ReturnsNull()
    {
        Assert.Null(_service.GetString(LanguageHelper.InvalidStrRef));
    }

    [Fact]
    public void GetString_WithSource_NoTlkLoaded_ReturnsNull()
    {
        Assert.Null(_service.GetString(100, TlkSource.Primary));
        Assert.Null(_service.GetString(100, TlkSource.Custom));
        Assert.Null(_service.GetString(100, TlkSource.Any));
    }

    #endregion

    #region CExoLocString Resolution

    [Fact]
    public void ResolveLocString_EmptyLocString_ReturnsEmpty()
    {
        var locString = new CExoLocString();
        var result = _service.ResolveLocString(locString);
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ResolveLocString_WithEmbeddedEnglish_ReturnsEnglish()
    {
        var locString = new CExoLocString();
        locString.SetString(LanguageHelper.ToCombinedId(Language.English, Gender.Male), "Hello World");

        var result = _service.ResolveLocString(locString);
        Assert.Equal("Hello World", result);
    }

    [Fact]
    public void ResolveLocString_WithPreferredLanguage_ReturnsPreferred()
    {
        var locString = new CExoLocString();
        locString.SetString(LanguageHelper.ToCombinedId(Language.English, Gender.Male), "Hello");
        locString.SetString(LanguageHelper.ToCombinedId(Language.French, Gender.Male), "Bonjour");

        var result = _service.ResolveLocString(locString, Language.French);
        Assert.Equal("Bonjour", result);
    }

    [Fact]
    public void ResolveLocString_FallsBackToEnglish()
    {
        var locString = new CExoLocString();
        locString.SetString(LanguageHelper.ToCombinedId(Language.English, Gender.Male), "Hello");

        // Request German, should fall back to English
        var result = _service.ResolveLocString(locString, Language.German);
        Assert.Equal("Hello", result);
    }

    [Fact]
    public void ResolveLocString_ReturnsAnyEmbeddedIfNoMatch()
    {
        var locString = new CExoLocString();
        locString.SetString(LanguageHelper.ToCombinedId(Language.Korean, Gender.Male), "안녕하세요");

        // Request English, Korean is only embedded, should return it as fallback
        var result = _service.ResolveLocString(locString);
        Assert.Equal("안녕하세요", result);
    }

    [Fact]
    public void ResolveLocString_PrefersFemaleGenderWhenSet()
    {
        var locString = new CExoLocString();
        locString.SetString(LanguageHelper.ToCombinedId(Language.English, Gender.Male), "He said");
        locString.SetString(LanguageHelper.ToCombinedId(Language.English, Gender.Female), "She said");

        _service.CurrentGender = Gender.Female;
        var result = _service.ResolveLocString(locString);
        Assert.Equal("She said", result);
    }

    #endregion

    #region Language/Gender Properties

    [Fact]
    public void CurrentLanguage_CanBeSet()
    {
        _service.CurrentLanguage = Language.French;
        Assert.Equal(Language.French, _service.CurrentLanguage);
    }

    [Fact]
    public void CurrentGender_CanBeSet()
    {
        _service.CurrentGender = Gender.Female;
        Assert.Equal(Gender.Female, _service.CurrentGender);
    }

    #endregion

    #region GetTlkPath

    [Fact]
    public void GetTlkPath_NullPath_ReturnsNull()
    {
        var result = _service.GetTlkPath(null!, Language.English);
        Assert.Null(result);
    }

    [Fact]
    public void GetTlkPath_EmptyPath_ReturnsNull()
    {
        var result = _service.GetTlkPath("", Language.English);
        Assert.Null(result);
    }

    [Fact]
    public void GetTlkPath_NonExistentPath_ReturnsNull()
    {
        var result = _service.GetTlkPath("/nonexistent/path", Language.English);
        Assert.Null(result);
    }

    #endregion

    #region DetectAvailableLanguages

    [Fact]
    public void DetectAvailableLanguages_NullPath_ReturnsEmpty()
    {
        var result = _service.DetectAvailableLanguages(null!);
        Assert.Empty(result);
    }

    [Fact]
    public void DetectAvailableLanguages_NonExistentPath_ReturnsEmpty()
    {
        var result = _service.DetectAvailableLanguages("/nonexistent/path");
        Assert.Empty(result);
    }

    #endregion

    #region Dispose

    [Fact]
    public void Dispose_ClearsTlk()
    {
        _service.Dispose();
        Assert.False(_service.IsPrimaryLoaded);
        Assert.False(_service.IsCustomLoaded);
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var exception = Record.Exception(() =>
        {
            _service.Dispose();
            _service.Dispose();
        });
        Assert.Null(exception);
    }

    [Fact]
    public void GetString_AfterDispose_ThrowsObjectDisposed()
    {
        _service.Dispose();
        Assert.Throws<ObjectDisposedException>(() => _service.GetString(100));
    }

    #endregion
}
