using Avalonia.Media.Imaging;
using DialogEditor.Services;

namespace Parley.Tests.Mocks
{
    /// <summary>
    /// Mock IPortraitService for unit testing.
    /// Returns null for all portraits by default (no filesystem/game data needed).
    /// </summary>
    public class MockPortraitService : IPortraitService
    {
        private string? _gameDataPath;
        private string? _moduleOverridePath;
        private readonly Dictionary<string, Bitmap?> _portraits = new();

        public void SetGameDataPath(string? path)
        {
            _gameDataPath = path;
        }

        public void SetModuleOverridePath(string? path)
        {
            _moduleOverridePath = path;
        }

        public Bitmap? LoadPortrait(string? baseResRef, char size = 's')
        {
            if (baseResRef == null)
                return null;

            var key = $"{baseResRef}{size}";
            return _portraits.TryGetValue(key, out var bitmap) ? bitmap : null;
        }

        public void ClearCache()
        {
            _portraits.Clear();
        }

        /// <summary>
        /// Register a portrait bitmap for testing.
        /// Key format: "{baseResRef}{size}" (e.g., "po_elaras").
        /// </summary>
        public void AddPortrait(string baseResRef, char size, Bitmap bitmap)
        {
            _portraits[$"{baseResRef}{size}"] = bitmap;
        }

        public string? GameDataPath => _gameDataPath;
        public string? ModuleOverridePath => _moduleOverridePath;
    }
}
