using Radoub.Formats.Gff;
using Xunit;

namespace Radoub.TestUtilities.Assertions;

/// <summary>
/// Assertion helpers for comparing GFF structures.
/// </summary>
public static class GffAssertions
{
    /// <summary>
    /// Assert that two CExoLocStrings are equal.
    /// </summary>
    public static void AssertEqual(CExoLocString expected, CExoLocString actual, string context = "")
    {
        var prefix = string.IsNullOrEmpty(context) ? "" : $"{context}: ";

        Assert.Equal(expected.StrRef, actual.StrRef);

        foreach (var kvp in expected.LocalizedStrings)
        {
            Assert.True(actual.LocalizedStrings.ContainsKey(kvp.Key),
                $"{prefix}Missing language {kvp.Key}");
            Assert.Equal(kvp.Value, actual.LocalizedStrings[kvp.Key]);
        }

        foreach (var kvp in actual.LocalizedStrings)
        {
            Assert.True(expected.LocalizedStrings.ContainsKey(kvp.Key),
                $"{prefix}Unexpected language {kvp.Key}");
        }
    }

    /// <summary>
    /// Assert that a localized string contains expected text for English.
    /// </summary>
    public static void AssertEnglishText(CExoLocString locString, string expectedText)
    {
        Assert.True(locString.LocalizedStrings.ContainsKey(0),
            "Localized string missing English (language 0)");
        Assert.Equal(expectedText, locString.LocalizedStrings[0]);
    }

    /// <summary>
    /// Assert that a localized string has a TLK reference.
    /// </summary>
    public static void AssertHasTlkReference(CExoLocString locString, uint? expectedStrRef = null)
    {
        Assert.NotEqual(0xFFFFFFFF, locString.StrRef);

        if (expectedStrRef.HasValue)
        {
            Assert.Equal(expectedStrRef.Value, locString.StrRef);
        }
    }

    /// <summary>
    /// Assert that a localized string has no TLK reference (uses inline text only).
    /// </summary>
    public static void AssertNoTlkReference(CExoLocString locString)
    {
        Assert.Equal(0xFFFFFFFF, locString.StrRef);
    }
}
