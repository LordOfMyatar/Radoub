namespace DialogEditor.Services
{
    /// <summary>
    /// One-time migration helpers for moving Parley-local path settings onto the shared
    /// RadoubSettings keys (#2357). Mirrors Trebuchet's migration rule (#2295): adopt the
    /// legacy value only when the shared value is still empty, so a value already set
    /// elsewhere is never overwritten.
    /// </summary>
    public static class SettingsPathMigration
    {
        /// <summary>
        /// Returns the value the shared setting should hold after migration:
        /// the legacy value if (and only if) the shared value is currently empty,
        /// otherwise the existing shared value. Null is treated as empty.
        /// </summary>
        public static string AdoptLegacyIfSharedEmpty(string? legacyValue, string? sharedValue)
        {
            var shared = sharedValue ?? "";
            if (!string.IsNullOrEmpty(shared))
                return shared;

            return legacyValue ?? "";
        }
    }
}
