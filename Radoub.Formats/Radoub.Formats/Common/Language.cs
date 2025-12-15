namespace Radoub.Formats.Common;

/// <summary>
/// Aurora Engine language identifiers.
/// Reference: BioWare Aurora LocalizedStrings Format documentation.
/// </summary>
public enum Language
{
    English = 0,
    French = 1,
    German = 2,
    Italian = 3,
    Spanish = 4,
    Polish = 5,
    // Gap in IDs (6-127 reserved)
    Korean = 128,
    ChineseTraditional = 129,
    ChineseSimplified = 130,
    Japanese = 131
}

/// <summary>
/// Gender variants for localized strings.
/// Some languages have different text for masculine/feminine forms.
/// </summary>
public enum Gender
{
    Male = 0,       // Also used as neutral/default
    Female = 1
}

/// <summary>
/// Helper methods for language and gender handling.
/// </summary>
public static class LanguageHelper
{
    /// <summary>
    /// Invalid StrRef value (0xFFFFFFFF) indicating no TLK reference.
    /// </summary>
    public const uint InvalidStrRef = 0xFFFFFFFF;

    /// <summary>
    /// Threshold for custom TLK StrRefs. Values >= this use alternate TLK.
    /// </summary>
    public const uint CustomTlkThreshold = 0x01000000; // 16777216

    /// <summary>
    /// Combine language and gender into a single ID for CExoLocString storage.
    /// Formula: (LanguageID * 2) + Gender
    /// </summary>
    public static uint ToCombinedId(Language language, Gender gender = Gender.Male)
    {
        return ((uint)language * 2) + (uint)gender;
    }

    /// <summary>
    /// Extract language from a combined ID.
    /// </summary>
    public static Language GetLanguage(uint combinedId)
    {
        return (Language)(combinedId / 2);
    }

    /// <summary>
    /// Extract gender from a combined ID.
    /// </summary>
    public static Gender GetGender(uint combinedId)
    {
        return (Gender)(combinedId % 2);
    }

    /// <summary>
    /// Parse combined ID into language and gender components.
    /// </summary>
    public static (Language language, Gender gender) ParseCombinedId(uint combinedId)
    {
        return (GetLanguage(combinedId), GetGender(combinedId));
    }

    /// <summary>
    /// Get display name for a language.
    /// </summary>
    public static string GetDisplayName(Language language)
    {
        return language switch
        {
            Language.English => "English",
            Language.French => "French",
            Language.German => "German",
            Language.Italian => "Italian",
            Language.Spanish => "Spanish",
            Language.Polish => "Polish",
            Language.Korean => "Korean",
            Language.ChineseTraditional => "Chinese (Traditional)",
            Language.ChineseSimplified => "Chinese (Simplified)",
            Language.Japanese => "Japanese",
            _ => $"Unknown ({(int)language})"
        };
    }

    /// <summary>
    /// Get the two-letter language code used in NWN:EE folder structure.
    /// </summary>
    public static string GetLanguageCode(Language language)
    {
        return language switch
        {
            Language.English => "en",
            Language.French => "fr",
            Language.German => "de",
            Language.Italian => "it",
            Language.Spanish => "es",
            Language.Polish => "pl",
            Language.Korean => "ko",
            Language.ChineseTraditional => "zh-TW",
            Language.ChineseSimplified => "zh-CN",
            Language.Japanese => "ja",
            _ => "en"
        };
    }

    /// <summary>
    /// Parse a language code to Language enum.
    /// </summary>
    public static Language? FromLanguageCode(string code)
    {
        return code?.ToLowerInvariant() switch
        {
            "en" => Language.English,
            "fr" => Language.French,
            "de" => Language.German,
            "it" => Language.Italian,
            "es" => Language.Spanish,
            "pl" => Language.Polish,
            "ko" => Language.Korean,
            "zh-tw" => Language.ChineseTraditional,
            "zh-cn" => Language.ChineseSimplified,
            "ja" => Language.Japanese,
            _ => null
        };
    }

    /// <summary>
    /// Check if a StrRef is valid (not 0xFFFFFFFF).
    /// </summary>
    public static bool IsValidStrRef(uint strRef)
    {
        return strRef != InvalidStrRef;
    }

    /// <summary>
    /// Check if a StrRef refers to the custom/alternate TLK.
    /// </summary>
    public static bool IsCustomTlkStrRef(uint strRef)
    {
        return strRef >= CustomTlkThreshold && strRef != InvalidStrRef;
    }

    /// <summary>
    /// Get the actual index into a TLK file, removing the custom TLK flag if present.
    /// </summary>
    public static uint GetTlkIndex(uint strRef)
    {
        if (strRef == InvalidStrRef)
            return InvalidStrRef;

        if (strRef >= CustomTlkThreshold)
            return strRef - CustomTlkThreshold;

        return strRef;
    }

    /// <summary>
    /// Languages to try as fallbacks when preferred language is unavailable.
    /// Per BioWare spec: English, French, German, Italian, Spanish.
    /// </summary>
    public static readonly Language[] FallbackOrder = new[]
    {
        Language.English,
        Language.French,
        Language.German,
        Language.Italian,
        Language.Spanish
    };
}
