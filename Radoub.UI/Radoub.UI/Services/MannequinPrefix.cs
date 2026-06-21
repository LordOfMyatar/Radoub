namespace Radoub.UI.Services;

/// <summary>
/// Builds the body-model prefix the armor/clothing preview is composed on. Blueprint
/// editors (Relique) have no creature context, so a fixed human-phenotype-0 mannequin is
/// used and only the gender varies. Gender lives in the prefix (pmh0 male / pfh0 female),
/// mirroring Quartermaster's ModelService.BuildModelPrefix rule (#2407).
/// </summary>
public static class MannequinPrefix
{
    public const string Male = "pmh0";
    public const string Female = "pfh0";

    /// <summary>gender==1 → female (pfh0); anything else → male (pmh0).</summary>
    public static string ForGender(int gender) => gender == 1 ? Female : Male;
}
