namespace DialogEditor.Services
{
    /// <summary>
    /// Well-known window keys for Parley's MainWindow-managed windows.
    /// Consumed with <see cref="Radoub.UI.Services.WindowLifecycleManager"/> (promoted in #2391).
    /// These are Parley-specific and intentionally stay in the tool, not the shared library.
    /// </summary>
    public static class WindowKeys
    {
        public const string Settings = "Settings";
        public const string Flowchart = "Flowchart";
        public const string SoundBrowser = "SoundBrowser";
        public const string ScriptBrowser = "ScriptBrowser";
        public const string ParameterBrowser = "ParameterBrowser";
        public const string ConversationSimulator = "ConversationSimulator";
    }
}
