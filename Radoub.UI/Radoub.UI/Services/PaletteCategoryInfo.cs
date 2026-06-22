namespace Radoub.UI.Services;

/// <summary>
/// A palette category plus how many cached items fall under it. Returned by
/// ISharedPaletteCacheService.GetCategoryNames (names-first, no item payload).
/// Id is int (not byte) so the Uncategorized sentinel (-1) cannot collide with a
/// real byte category id (0 is the real "Miscellaneous" category). (#987)
/// </summary>
public class PaletteCategoryInfo
{
    public const int UncategorizedId = -1;

    public int Id { get; init; }
    public required string Name { get; init; }
    public string? ParentPath { get; init; }
    public int ItemCount { get; init; }

    public bool IsUncategorized => Id == UncategorizedId;
}
