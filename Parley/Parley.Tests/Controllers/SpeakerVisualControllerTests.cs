using Avalonia.Headless.XUnit;
using DialogEditor.Services;
using DialogEditor.Utils;
using Parley.Tests.Mocks;
using Parley.Views.Helpers;
using Xunit;

namespace Parley.Tests.Controllers
{
    /// <summary>
    /// Unit tests for SpeakerVisualController.
    /// Tests constructor validation and populating-properties guard behavior.
    /// ComboBox initialization and UI event handling require headless tests.
    /// </summary>
    public class SpeakerVisualControllerTests
    {
        private readonly MockSettingsService _mockSettings;

        public SpeakerVisualControllerTests()
        {
            _mockSettings = new MockSettingsService();
        }

        #region Constructor Validation

        [AvaloniaFact]
        public void Constructor_NullWindow_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>("window", () =>
                new SpeakerVisualController(null!, _mockSettings, () => false));
        }

        [AvaloniaFact]
        public void Constructor_NullSettings_ThrowsArgumentNullException()
        {
            var window = new Avalonia.Controls.Window();
            Assert.Throws<ArgumentNullException>("settings", () =>
                new SpeakerVisualController(window, null!, () => false));
        }

        [AvaloniaFact]
        public void Constructor_NullIsPopulatingProperties_ThrowsArgumentNullException()
        {
            var window = new Avalonia.Controls.Window();
            Assert.Throws<ArgumentNullException>("isPopulatingProperties", () =>
                new SpeakerVisualController(window, _mockSettings, null!));
        }

        [AvaloniaFact]
        public void Constructor_ValidArgs_CreatesInstance()
        {
            var controller = CreateController();
            Assert.NotNull(controller);
        }

        #endregion

        #region OnSpeakerShapeChanged Guards

        [AvaloniaFact]
        public void OnSpeakerShapeChanged_WhenPopulatingProperties_DoesNothing()
        {
            var controller = CreateController(isPopulating: true);

            // Should return early without modifying settings
            controller.OnSpeakerShapeChanged(null, null!);

            // No preferences should have been set
            Assert.Empty(_mockSettings.NpcSpeakerPreferences);
        }

        [AvaloniaFact]
        public void OnSpeakerShapeChanged_NullSender_DoesNotThrow()
        {
            var controller = CreateController(isPopulating: false);

            // null sender means comboBox is null, early return
            controller.OnSpeakerShapeChanged(null, null!);

            Assert.Empty(_mockSettings.NpcSpeakerPreferences);
        }

        #endregion

        #region OnSpeakerColorChanged Guards

        [AvaloniaFact]
        public void OnSpeakerColorChanged_WhenPopulatingProperties_DoesNothing()
        {
            var controller = CreateController(isPopulating: true);

            controller.OnSpeakerColorChanged(null, null!);

            Assert.Empty(_mockSettings.NpcSpeakerPreferences);
        }

        [AvaloniaFact]
        public void OnSpeakerColorChanged_NullSender_DoesNotThrow()
        {
            var controller = CreateController(isPopulating: false);

            controller.OnSpeakerColorChanged(null, null!);

            Assert.Empty(_mockSettings.NpcSpeakerPreferences);
        }

        #endregion

        #region Settings Integration

        [AvaloniaFact]
        public void MockSettings_SetSpeakerPreference_StoresShape()
        {
            _mockSettings.SetSpeakerPreference("npc_tag", null, SpeakerVisualHelper.SpeakerShape.Diamond);

            var (color, shape) = _mockSettings.GetSpeakerPreference("npc_tag");
            Assert.Null(color);
            Assert.Equal(SpeakerVisualHelper.SpeakerShape.Diamond, shape);
        }

        [AvaloniaFact]
        public void MockSettings_SetSpeakerPreference_StoresColor()
        {
            _mockSettings.SetSpeakerPreference("npc_tag", SpeakerVisualHelper.ColorPalette.Teal, null);

            var (color, shape) = _mockSettings.GetSpeakerPreference("npc_tag");
            Assert.Equal(SpeakerVisualHelper.ColorPalette.Teal, color);
            Assert.Null(shape);
        }

        [AvaloniaFact]
        public void MockSettings_GetSpeakerPreference_UnknownTag_ReturnsNulls()
        {
            var (color, shape) = _mockSettings.GetSpeakerPreference("unknown");
            Assert.Null(color);
            Assert.Null(shape);
        }

        #endregion

        #region Helper

        private SpeakerVisualController CreateController(bool isPopulating = false)
        {
            var window = new Avalonia.Controls.Window();
            return new SpeakerVisualController(window, _mockSettings, () => isPopulating);
        }

        #endregion
    }
}
