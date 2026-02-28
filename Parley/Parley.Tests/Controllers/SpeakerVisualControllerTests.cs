using Avalonia.Headless.XUnit;
using Parley.Tests.Mocks;
using Parley.Views.Helpers;
using Xunit;

namespace Parley.Tests.Controllers
{
    /// <summary>
    /// Unit tests for SpeakerVisualController.
    /// Tests populating-properties guard behavior for shape/color changes.
    /// ComboBox initialization and UI event handling require headless tests.
    /// </summary>
    public class SpeakerVisualControllerTests
    {
        private readonly MockSettingsService _mockSettings;

        public SpeakerVisualControllerTests()
        {
            _mockSettings = new MockSettingsService();
        }

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

        #region Helper

        private SpeakerVisualController CreateController(bool isPopulating = false)
        {
            var window = new Avalonia.Controls.Window();
            return new SpeakerVisualController(window, _mockSettings, () => isPopulating);
        }

        #endregion
    }
}
