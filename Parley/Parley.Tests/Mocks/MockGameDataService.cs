using Radoub.Formats.Itp;
using Radoub.Formats.Services;
using Radoub.Formats.Ssf;
using Radoub.Formats.TwoDA;

namespace Parley.Tests.Mocks
{
    /// <summary>
    /// Mock IGameDataService for unit testing.
    /// Allows setup of 2DA data, TLK strings, and resources without game files.
    /// </summary>
    public class MockGameDataService : IGameDataService
    {
        private readonly Dictionary<string, TwoDAFile> _2daFiles = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<uint, string> _tlkStrings = new();
        private readonly Dictionary<string, byte[]> _resources = new();
        private readonly Dictionary<string, SsfFile> _soundsets = new();
        private readonly List<PaletteCategory> _paletteCategories = new();
        private string? _customTlkPath;

        public bool IsConfigured { get; set; } = true;
        public bool HasCustomTlk => _customTlkPath != null;

        #region 2DA Access

        public TwoDAFile? Get2DA(string name)
        {
            return _2daFiles.TryGetValue(name, out var file) ? file : null;
        }

        public string? Get2DAValue(string twoDAName, int rowIndex, string columnName)
        {
            var file = Get2DA(twoDAName);
            return file?.GetValue(rowIndex, columnName);
        }

        public bool Has2DA(string name) => _2daFiles.ContainsKey(name);

        public void ClearCache()
        {
            _2daFiles.Clear();
            _tlkStrings.Clear();
            _resources.Clear();
            _soundsets.Clear();
        }

        #endregion

        #region TLK String Resolution

        public string? GetString(uint strRef)
        {
            return _tlkStrings.TryGetValue(strRef, out var str) ? str : null;
        }

        public string? GetString(string? strRefStr)
        {
            if (string.IsNullOrEmpty(strRefStr) || strRefStr == "****")
                return null;
            if (uint.TryParse(strRefStr, out var strRef))
                return GetString(strRef);
            return null;
        }

        public void SetCustomTlk(string? path)
        {
            _customTlkPath = path;
        }

        #endregion

        #region Resource Access

        public byte[]? FindResource(string resRef, ushort resourceType)
        {
            var key = $"{resRef}:{resourceType}";
            return _resources.TryGetValue(key, out var data) ? data : null;
        }

        public IEnumerable<GameResourceInfo> ListResources(ushort resourceType)
        {
            return _resources
                .Where(kv => kv.Key.EndsWith($":{resourceType}"))
                .Select(kv => new GameResourceInfo
                {
                    ResRef = kv.Key.Split(':')[0],
                    ResourceType = resourceType,
                    Source = GameResourceSource.Override
                });
        }

        #endregion

        #region Soundset Access

        public SsfFile? GetSoundset(int soundsetId)
        {
            var resRef = GetSoundsetResRef(soundsetId);
            return resRef != null ? GetSoundsetByResRef(resRef) : null;
        }

        public SsfFile? GetSoundsetByResRef(string resRef)
        {
            return _soundsets.TryGetValue(resRef, out var file) ? file : null;
        }

        public string? GetSoundsetResRef(int soundsetId)
        {
            return Get2DAValue("soundset", soundsetId, "RESREF");
        }

        #endregion

        #region Palette Access

        public IEnumerable<PaletteCategory> GetPaletteCategories(ushort resourceType)
        {
            return _paletteCategories;
        }

        public string? GetPaletteCategoryName(ushort resourceType, byte categoryId)
        {
            return _paletteCategories.FirstOrDefault(c => c.Id == categoryId)?.Name;
        }

        #endregion

        #region Configuration

        public void ReloadConfiguration() { }

        #endregion

        public void Dispose() { }

        #region Test Setup Methods

        /// <summary>
        /// Register a 2DA file for testing.
        /// </summary>
        public void Setup2DA(string name, TwoDAFile file)
        {
            _2daFiles[name] = file;
        }

        /// <summary>
        /// Register a TLK string for testing.
        /// </summary>
        public void SetupTlkString(uint strRef, string value)
        {
            _tlkStrings[strRef] = value;
        }

        /// <summary>
        /// Register a resource for testing.
        /// </summary>
        public void SetupResource(string resRef, ushort resourceType, byte[] data)
        {
            _resources[$"{resRef}:{resourceType}"] = data;
        }

        /// <summary>
        /// Register a soundset for testing.
        /// </summary>
        public void SetupSoundset(string resRef, SsfFile file)
        {
            _soundsets[resRef] = file;
        }

        /// <summary>
        /// Add a palette category for testing.
        /// </summary>
        public void AddPaletteCategory(PaletteCategory category)
        {
            _paletteCategories.Add(category);
        }

        #endregion
    }
}
