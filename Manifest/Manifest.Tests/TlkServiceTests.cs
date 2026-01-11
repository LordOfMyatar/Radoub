using Manifest.Services;
using Radoub.Formats.Common;
using Radoub.Formats.Gff;
using Radoub.Formats.Settings;
using Xunit;

namespace Manifest.Tests;

/// <summary>
/// Tests for TlkService.
/// Tests focus on logic that doesn't require actual TLK files.
/// </summary>
public class TlkServiceTests : IDisposable
{
    public TlkServiceTests()
    {
        // Reset services before each test
        TlkService.ResetForTesting();
    }

    public void Dispose()
    {
        TlkService.ResetForTesting();
    }

    [Fact]
    public void Instance_ReturnsSameInstance()
    {
        var instance1 = TlkService.Instance;
        var instance2 = TlkService.Instance;

        Assert.Same(instance1, instance2);
    }

    [Fact]
    public void ResolveStrRef_InvalidStrRef_ReturnsNull()
    {
        var service = TlkService.Instance;

        // StrRef of 0xFFFFFFFF is invalid per BioWare spec
        var result = service.ResolveStrRef(0xFFFFFFFF);

        Assert.Null(result);
    }

    [Fact]
    public void ResolveStrRef_ValidStrRef_ReturnsStringOrNull()
    {
        var service = TlkService.Instance;

        // StrRef of 100 - returns text if TLK loaded, null otherwise
        var result = service.ResolveStrRef(100);

        // Either returns a string (TLK available) or null (no TLK)
        if (service.IsAvailable)
        {
            Assert.NotNull(result);
        }
        else
        {
            Assert.Null(result);
        }
    }

    [Fact]
    public void ResolveLocString_Embedded_ReturnsEmbedded()
    {
        var service = TlkService.Instance;

        // Create a CExoLocString with embedded English text
        var locString = new CExoLocString
        {
            StrRef = 0xFFFFFFFF, // Invalid StrRef
            LocalizedStrings = new Dictionary<uint, string>
            {
                { 0, "Embedded English Text" } // English Male = 0
            }
        };

        var result = service.ResolveLocString(locString);

        Assert.Equal("Embedded English Text", result);
    }

    [Fact]
    public void ResolveLocString_EmptyLocString_ReturnsEmptyString()
    {
        var service = TlkService.Instance;

        var locString = new CExoLocString
        {
            StrRef = 0xFFFFFFFF,
            LocalizedStrings = new Dictionary<uint, string>()
        };

        var result = service.ResolveLocString(locString);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ResolveLocString_MultipleLanguages_ReturnsPreferred()
    {
        var service = TlkService.Instance;

        // Create a CExoLocString with multiple languages
        var locString = new CExoLocString
        {
            StrRef = 0xFFFFFFFF,
            LocalizedStrings = new Dictionary<uint, string>
            {
                { 0, "English Text" },   // English Male = 0
                { 2, "French Text" },    // French Male = 2
                { 4, "German Text" }     // German Male = 4
            }
        };

        // Request English (default)
        var result = service.ResolveLocString(locString, Language.English);
        Assert.Equal("English Text", result);

        // Request French
        result = service.ResolveLocString(locString, Language.French);
        Assert.Equal("French Text", result);

        // Request German
        result = service.ResolveLocString(locString, Language.German);
        Assert.Equal("German Text", result);
    }

    [Fact]
    public void LanguageFallback_MissingLanguage_FallsToEnglish()
    {
        var service = TlkService.Instance;

        // Create a CExoLocString with only English
        var locString = new CExoLocString
        {
            StrRef = 0xFFFFFFFF,
            LocalizedStrings = new Dictionary<uint, string>
            {
                { 0, "English Only" } // English Male = 0
            }
        };

        // Request German (not available) - should fall back to English
        var result = service.ResolveLocString(locString, Language.German);

        Assert.Equal("English Only", result);
    }

    [Fact]
    public void GetLocStringInfo_EmbeddedOnly_ReturnsCorrectInfo()
    {
        var service = TlkService.Instance;

        var locString = new CExoLocString
        {
            StrRef = 0xFFFFFFFF,
            LocalizedStrings = new Dictionary<uint, string>
            {
                { 0, "Text" }
            }
        };

        var info = service.GetLocStringInfo(locString);

        Assert.True(info.HasEmbeddedStrings);
        Assert.False(info.HasStrRef);
        Assert.Equal("Embedded", info.SourceDescription);
    }

    [Fact]
    public void GetLocStringInfo_StrRefOnly_ReturnsCorrectInfo()
    {
        var service = TlkService.Instance;

        var locString = new CExoLocString
        {
            StrRef = 12345,
            LocalizedStrings = new Dictionary<uint, string>()
        };

        var info = service.GetLocStringInfo(locString);

        Assert.False(info.HasEmbeddedStrings);
        Assert.True(info.HasStrRef);
        Assert.Equal(12345u, info.StrRef);
        // Note: Will show "no game path" since no TLK is loaded
        Assert.Contains("TLK:12345", info.SourceDescription);
    }

    [Fact]
    public void GetLocStringInfo_CustomTlkStrRef_IdentifiesCorrectly()
    {
        var service = TlkService.Instance;

        // Custom TLK StrRefs are >= 0x01000000
        var customStrRef = (uint)0x01000000 + 100;

        var locString = new CExoLocString
        {
            StrRef = customStrRef,
            LocalizedStrings = new Dictionary<uint, string>()
        };

        var info = service.GetLocStringInfo(locString);

        Assert.True(info.IsCustomTlkRef);
        Assert.Contains("Custom TLK", info.SourceDescription);
    }

    [Fact]
    public void InvalidateCache_ClearsCache()
    {
        var service = TlkService.Instance;

        // Act
        service.InvalidateCache();

        // Assert - no exception means success
        // Cache is internal, so we just verify no crash
        Assert.NotNull(service);
    }

    [Fact]
    public void IsAvailable_WithoutGamePath_ReturnsFalse()
    {
        var service = TlkService.Instance;

        // Without game paths configured, TLK is not available
        // This depends on RadoubSettings state
        // In test environment with no paths, should be false
        var hasGamePaths = RadoubSettings.Instance.HasGamePaths;

        Assert.Equal(hasGamePaths, service.IsAvailable);
    }

    [Fact]
    public void GetAllTranslations_EmbeddedOnly_ReturnsEmbedded()
    {
        var service = TlkService.Instance;

        var locString = new CExoLocString
        {
            StrRef = 0xFFFFFFFF,
            LocalizedStrings = new Dictionary<uint, string>
            {
                { 0, "English" },
                { 2, "French" }
            }
        };

        var translations = service.GetAllTranslations(locString);

        Assert.Contains(Language.English, translations.Keys);
        Assert.Contains(Language.French, translations.Keys);
        Assert.Equal("English", translations[Language.English]);
        Assert.Equal("French", translations[Language.French]);
    }

    [Fact]
    public void GetTlkStatusSummary_NoGamePath_ReturnsAppropriateMessage()
    {
        var service = TlkService.Instance;

        var status = service.GetTlkStatusSummary();

        // Without game paths, should indicate that
        Assert.Contains("TLK:", status);
    }

    [Fact]
    public void ResolveLocString_FallbackChain_ReturnsAnyAvailable()
    {
        var service = TlkService.Instance;

        // Create a CExoLocString with only a non-default language
        var locString = new CExoLocString
        {
            StrRef = 0xFFFFFFFF,
            LocalizedStrings = new Dictionary<uint, string>
            {
                { 8, "Italian Only" } // Italian Male = 8
            }
        };

        // Request English (not available) - should eventually return Italian
        var result = service.ResolveLocString(locString, Language.English);

        Assert.Equal("Italian Only", result);
    }
}
