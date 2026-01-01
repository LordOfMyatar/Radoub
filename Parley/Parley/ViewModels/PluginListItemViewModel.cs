namespace DialogEditor.ViewModels
{
    /// <summary>
    /// View model for plugin list item in SettingsWindow.
    /// Extracted from SettingsWindow.axaml.cs for better organization.
    /// </summary>
    public class PluginListItemViewModel
    {
        public string PluginId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Version { get; set; } = "";
        public string Author { get; set; } = "";
        public string Description { get; set; } = "";
        public string TrustLevel { get; set; } = "";
        public string Permissions { get; set; } = "";
        public bool IsEnabled { get; set; } = true;

        public string TrustBadge => TrustLevel.ToUpperInvariant() switch
        {
            "OFFICIAL" => "[OFFICIAL]",
            "VERIFIED" => "[VERIFIED]",
            _ => "[UNVERIFIED]"
        };

        public string DisplayText => $"{Name} v{Version} by {Author} {TrustBadge}";
    }
}
