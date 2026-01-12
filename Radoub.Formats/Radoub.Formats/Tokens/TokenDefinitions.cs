namespace Radoub.Formats.Tokens;

/// <summary>
/// Definitions for standard Aurora Engine tokens.
/// Reference: Aurora Toolset conversation editor token list.
/// </summary>
public static class TokenDefinitions
{
    /// <summary>
    /// All standard game variable tokens recognized by the Aurora Engine.
    /// These are case-sensitive and include both capitalized and lowercase variants.
    /// </summary>
    public static readonly string[] StandardTokens =
    {
        // Name tokens
        "FirstName",
        "LastName",
        "FullName",
        "PlayerName",

        // Gender tokens (capitalized - start of sentence)
        "Boy/Girl",
        "Brother/Sister",
        "He/She",
        "Him/Her",
        "His/Her",
        "His/Hers",
        "Lad/Lass",
        "Lord/Lady",
        "Male/Female",
        "Man/Woman",
        "Master/Mistress",
        "Mister/Missus",
        "Sir/Madam",

        // Gender tokens (lowercase - mid-sentence)
        "boy/girl",
        "brother/sister",
        "he/she",
        "him/her",
        "his/her",
        "his/hers",
        "lad/lass",
        "lord/lady",
        "male/female",
        "man/woman",
        "master/mistress",
        "mister/missus",
        "sir/madam",

        // Colorful language
        "bitch/bastard",

        // Character info
        "Class",
        "class",
        "Race",
        "race",
        "Subrace",
        "Deity",
        "Level",

        // Alignment tokens
        "Alignment",
        "alignment",
        "Good/Evil",
        "good/evil",
        "Lawful/Chaotic",
        "lawful/chaotic",
        "Law/Chaos",
        "law/chaos",

        // Time tokens
        "Day/Night",
        "day/night",
        "GameMonth",
        "GameTime",
        "GameYear",
        "QuarterDay",
        "quarterday"
    };

    /// <summary>
    /// Standard tokens organized by category for UI display.
    /// </summary>
    public static readonly Dictionary<string, string[]> TokensByCategory = new()
    {
        ["Name"] = new[]
        {
            "FirstName", "LastName", "FullName", "PlayerName"
        },
        ["Gender (Capitalized)"] = new[]
        {
            "Boy/Girl", "Brother/Sister", "He/She", "Him/Her", "His/Her",
            "His/Hers", "Lad/Lass", "Lord/Lady", "Male/Female", "Man/Woman",
            "Master/Mistress", "Mister/Missus", "Sir/Madam"
        },
        ["Gender (Lowercase)"] = new[]
        {
            "boy/girl", "brother/sister", "he/she", "him/her", "his/her",
            "his/hers", "lad/lass", "lord/lady", "male/female", "man/woman",
            "master/mistress", "mister/missus", "sir/madam", "bitch/bastard"
        },
        ["Character"] = new[]
        {
            "Class", "class", "Race", "race", "Subrace", "Deity", "Level"
        },
        ["Alignment"] = new[]
        {
            "Alignment", "alignment", "Good/Evil", "good/evil",
            "Lawful/Chaotic", "lawful/chaotic", "Law/Chaos", "law/chaos"
        },
        ["Time"] = new[]
        {
            "Day/Night", "day/night", "GameMonth", "GameTime", "GameYear",
            "QuarterDay", "quarterday"
        }
    };

    /// <summary>
    /// Hash set for O(1) lookup of standard tokens.
    /// </summary>
    public static readonly HashSet<string> StandardTokenSet =
        new(StandardTokens, StringComparer.Ordinal);

    /// <summary>
    /// Check if a token name is a standard Aurora token.
    /// </summary>
    public static bool IsStandardToken(string tokenName)
    {
        return StandardTokenSet.Contains(tokenName);
    }

    /// <summary>
    /// Check if a token name is a CUSTOM token (CUSTOM0-CUSTOM9 or higher).
    /// </summary>
    public static bool IsCustomToken(string tokenName, out int tokenNumber)
    {
        tokenNumber = -1;
        if (!tokenName.StartsWith("CUSTOM", StringComparison.OrdinalIgnoreCase))
            return false;

        return int.TryParse(tokenName.AsSpan(6), out tokenNumber) && tokenNumber >= 0;
    }

    /// <summary>
    /// Highlight token opening tags.
    /// </summary>
    public static readonly Dictionary<string, HighlightType> HighlightOpenTags = new(StringComparer.OrdinalIgnoreCase)
    {
        ["<StartAction>"] = HighlightType.Action,
        ["<StartCheck>"] = HighlightType.Check,
        ["<StartHighlight>"] = HighlightType.Highlight
    };

    /// <summary>
    /// Closing tag for all highlight tokens.
    /// </summary>
    public const string HighlightCloseTag = "</Start>";

    /// <summary>
    /// Alternative closing tag (some content uses this).
    /// </summary>
    public const string HighlightCloseTagAlt = "<End>";

    /// <summary>
    /// Color token close tag.
    /// </summary>
    public const string ColorCloseTag = "</c>";

    /// <summary>
    /// Example text shown when selecting highlight tokens in the UI.
    /// </summary>
    public static readonly Dictionary<HighlightType, string> HighlightExamples = new()
    {
        [HighlightType.Action] = "[Character waves]",
        [HighlightType.Check] = "[Lore]",
        [HighlightType.Highlight] = "[Important text]"
    };

    /// <summary>
    /// Display colors for highlight tokens (Aurora Engine defaults).
    /// </summary>
    public static readonly Dictionary<HighlightType, string> HighlightColors = new()
    {
        [HighlightType.Action] = "#00FF00",    // Green
        [HighlightType.Check] = "#FF0000",      // Red
        [HighlightType.Highlight] = "#0080FF"   // Blue
    };
}
