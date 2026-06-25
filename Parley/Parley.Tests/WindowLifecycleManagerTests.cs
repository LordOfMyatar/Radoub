using Avalonia.Headless.XUnit;
using DialogEditor.Services;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// Tests for Parley's well-known <see cref="WindowKeys"/>.
    /// The generic WindowLifecycleManager moved to Radoub.UI (#2391); its tests live in
    /// Radoub.UI.Tests. WindowKeys is Parley-specific and stays here.
    /// </summary>
    public class WindowLifecycleManagerTests
    {
        [AvaloniaFact]
        public void WindowKeys_ContainsExpectedKeys()
        {
            // Assert - Verify the well-known keys exist
            Assert.Equal("Settings", WindowKeys.Settings);
            Assert.Equal("Flowchart", WindowKeys.Flowchart);
            Assert.Equal("SoundBrowser", WindowKeys.SoundBrowser);
            Assert.Equal("ScriptBrowser", WindowKeys.ScriptBrowser);
            Assert.Equal("ParameterBrowser", WindowKeys.ParameterBrowser);
            Assert.Equal("ConversationSimulator", WindowKeys.ConversationSimulator);
        }
    }
}
