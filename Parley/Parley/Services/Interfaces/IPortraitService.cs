using Avalonia.Media.Imaging;

namespace DialogEditor.Services
{
    /// <summary>
    /// Interface for loading NWN portrait images.
    /// Supports loading from loose files and base game data.
    /// #1230: Phase 3 - Service interface extraction for dependency injection.
    /// </summary>
    public interface IPortraitService
    {
        /// <summary>
        /// Sets the game data path for loading portraits from base game.
        /// </summary>
        void SetGameDataPath(string? path);

        /// <summary>
        /// Sets the module override path for loading custom portraits.
        /// </summary>
        void SetModuleOverridePath(string? path);

        /// <summary>
        /// Loads a portrait image by its base ResRef.
        /// NWN portraits use naming: [baseResRef]s.tga (small), [baseResRef]m.tga (medium), [baseResRef]l.tga (large).
        /// </summary>
        /// <param name="baseResRef">Base portrait ResRef from portraits.2da (e.g., "po_elara").</param>
        /// <param name="size">Size suffix: 's' (small/thumbnail), 'm' (medium), 'l' (large). Default 's'.</param>
        /// <returns>Bitmap if found, null otherwise.</returns>
        Bitmap? LoadPortrait(string? baseResRef, char size = 's');

        /// <summary>
        /// Clears the portrait cache.
        /// </summary>
        void ClearCache();
    }
}
