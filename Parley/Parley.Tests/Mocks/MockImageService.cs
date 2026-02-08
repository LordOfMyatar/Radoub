using Radoub.Formats.Services;

namespace Parley.Tests.Mocks
{
    /// <summary>
    /// Mock IImageService for unit and integration testing.
    /// Returns null for all image lookups by default.
    /// </summary>
    public class MockImageService : IImageService
    {
        private readonly Dictionary<string, ImageData> _portraits = new();

        public ImageData? DecodeImage(byte[] data, string format) => null;

        public ImageData? LoadImage(string resRef, ushort resourceType) => null;

        public ImageData? GetItemIcon(int baseItemType, int modelNumber = 0) => null;

        public ImageData? GetPortrait(string resRef)
        {
            return _portraits.TryGetValue(resRef, out var data) ? data : null;
        }

        public ImageData? GetSpellIcon(int spellId) => null;

        public ImageData? GetFeatIcon(int featId) => null;

        public ImageData? GetSkillIcon(int skillId) => null;

        public ImageData? GetClassIcon(int classId) => null;

        public void ClearCache() => _portraits.Clear();

        /// <summary>
        /// Register a portrait for testing.
        /// </summary>
        public void SetupPortrait(string resRef, ImageData data)
        {
            _portraits[resRef] = data;
        }
    }
}
