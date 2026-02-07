using Avalonia.Headless.XUnit;
using DialogEditor.Models;
using DialogEditor.Services;
using DialogEditor.ViewModels;
using Parley.Tests.Mocks;
using Parley.Views.Helpers;
using Xunit;

namespace Parley.Tests.Controllers
{
    /// <summary>
    /// Unit tests for FileMenuController.
    /// Tests business logic (filename validation, constructor guards).
    /// UI-dependent methods (OnOpenClick, PopulateRecentFilesMenu) require headless/integration tests.
    /// </summary>
    public class FileMenuControllerTests
    {
        private readonly MockSettingsService _mockSettings;
        private readonly MainViewModel _viewModel;

        public FileMenuControllerTests()
        {
            _mockSettings = new MockSettingsService();
            _viewModel = new MainViewModel();
        }

        #region Constructor Validation

        [AvaloniaFact]
        public void Constructor_NullSettings_ThrowsArgumentNullException()
        {
            var w = new Avalonia.Controls.Window();
            Assert.Throws<ArgumentNullException>("settings", () =>
                new FileMenuController(w, new SafeControlFinder(w), null!,
                    () => _viewModel, () => { }, () => { }, () => { }, () => { }, () => { },
                    () => new ScriptParameterUIManager(_ => null, _ => { }, () => { }, () => false, () => null),
                    () => Task.FromResult(false)));
        }

        [AvaloniaFact]
        public void Constructor_NullGetViewModel_ThrowsArgumentNullException()
        {
            var w = new Avalonia.Controls.Window();
            Assert.Throws<ArgumentNullException>("getViewModel", () =>
                new FileMenuController(w, new SafeControlFinder(w), _mockSettings,
                    null!, () => { }, () => { }, () => { }, () => { }, () => { },
                    () => new ScriptParameterUIManager(_ => null, _ => { }, () => { }, () => false, () => null),
                    () => Task.FromResult(false)));
        }

        [AvaloniaFact]
        public void Constructor_NullSaveCurrentNodeProperties_ThrowsArgumentNullException()
        {
            var w = new Avalonia.Controls.Window();
            Assert.Throws<ArgumentNullException>("saveCurrentNodeProperties", () =>
                new FileMenuController(w, new SafeControlFinder(w), _mockSettings,
                    () => _viewModel, null!, () => { }, () => { }, () => { }, () => { },
                    () => new ScriptParameterUIManager(_ => null, _ => { }, () => { }, () => false, () => null),
                    () => Task.FromResult(false)));
        }

        [AvaloniaFact]
        public void Constructor_NullClearPropertiesPanel_ThrowsArgumentNullException()
        {
            var w = new Avalonia.Controls.Window();
            Assert.Throws<ArgumentNullException>("clearPropertiesPanel", () =>
                new FileMenuController(w, new SafeControlFinder(w), _mockSettings,
                    () => _viewModel, () => { }, null!, () => { }, () => { }, () => { },
                    () => new ScriptParameterUIManager(_ => null, _ => { }, () => { }, () => false, () => null),
                    () => Task.FromResult(false)));
        }

        [AvaloniaFact]
        public void Constructor_NullPopulateRecentFilesMenu_ThrowsArgumentNullException()
        {
            var w = new Avalonia.Controls.Window();
            Assert.Throws<ArgumentNullException>("populateRecentFilesMenu", () =>
                new FileMenuController(w, new SafeControlFinder(w), _mockSettings,
                    () => _viewModel, () => { }, () => { }, null!, () => { }, () => { },
                    () => new ScriptParameterUIManager(_ => null, _ => { }, () => { }, () => false, () => null),
                    () => Task.FromResult(false)));
        }

        [AvaloniaFact]
        public void Constructor_NullUpdateEmbeddedFlowchart_ThrowsArgumentNullException()
        {
            var w = new Avalonia.Controls.Window();
            Assert.Throws<ArgumentNullException>("updateEmbeddedFlowchartAfterLoad", () =>
                new FileMenuController(w, new SafeControlFinder(w), _mockSettings,
                    () => _viewModel, () => { }, () => { }, () => { }, null!, () => { },
                    () => new ScriptParameterUIManager(_ => null, _ => { }, () => { }, () => false, () => null),
                    () => Task.FromResult(false)));
        }

        [AvaloniaFact]
        public void Constructor_NullClearFlowcharts_ThrowsArgumentNullException()
        {
            var w = new Avalonia.Controls.Window();
            Assert.Throws<ArgumentNullException>("clearFlowcharts", () =>
                new FileMenuController(w, new SafeControlFinder(w), _mockSettings,
                    () => _viewModel, () => { }, () => { }, () => { }, () => { }, null!,
                    () => new ScriptParameterUIManager(_ => null, _ => { }, () => { }, () => false, () => null),
                    () => Task.FromResult(false)));
        }

        [AvaloniaFact]
        public void Constructor_NullGetParameterUIManager_ThrowsArgumentNullException()
        {
            var w = new Avalonia.Controls.Window();
            Assert.Throws<ArgumentNullException>("getParameterUIManager", () =>
                new FileMenuController(w, new SafeControlFinder(w), _mockSettings,
                    () => _viewModel, () => { }, () => { }, () => { }, () => { }, () => { },
                    null!, () => Task.FromResult(false)));
        }

        [AvaloniaFact]
        public void Constructor_NullShowSaveAsDialogAsync_ThrowsArgumentNullException()
        {
            var w = new Avalonia.Controls.Window();
            Assert.Throws<ArgumentNullException>("showSaveAsDialogAsync", () =>
                new FileMenuController(w, new SafeControlFinder(w), _mockSettings,
                    () => _viewModel, () => { }, () => { }, () => { }, () => { }, () => { },
                    () => new ScriptParameterUIManager(_ => null, _ => { }, () => { }, () => false, () => null),
                    null!));
        }

        [AvaloniaFact]
        public void Constructor_NullOptionalParams_DoesNotThrow()
        {
            // scanCreaturesForModule and updateDialogBrowserCurrentFile are optional
            var controller = CreateController(
                scanCreaturesForModule: null,
                updateDialogBrowserCurrentFile: null);
            Assert.NotNull(controller);
        }

        #endregion

        #region Filename Validation (#826)

        [AvaloniaTheory]
        [InlineData("test.dlg")]
        [InlineData("short.dlg")]
        [InlineData("a.dlg")]
        [InlineData("1234567890123456.dlg")]   // Exactly 16 chars
        [InlineData("merchant_01.dlg")]         // 11 chars
        [InlineData("x.json")]                  // 1 char
        public async Task ValidateFilenameAsync_ValidLength_ReturnsTrue(string filename)
        {
            var controller = CreateController();
            var filePath = Path.Combine(Path.GetTempPath(), filename);

            var result = await controller.ValidateFilenameAsync(filePath);

            Assert.True(result);
        }

        [AvaloniaTheory]
        [InlineData("12345678901234567.dlg")]  // 17 chars - too long
        [InlineData("Test1_SharedReply.dlg")]  // 17 chars
        [InlineData("abcdefghijklmnopqrst.dlg")] // 20 chars
        public async Task ValidateFilenameAsync_TooLong_RejectsFile(string filename)
        {
            var controller = CreateController();
            var filePath = Path.Combine(Path.GetTempPath(), filename);

            // ValidateFilenameAsync shows a non-modal error dialog when filename is too long.
            // In headless tests, Show(owner) throws because the owner window isn't visible.
            // The exception proves the validation detected the over-length filename.
            await Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await controller.ValidateFilenameAsync(filePath));
        }

        [AvaloniaFact]
        public async Task ValidateFilenameAsync_ExactlyMaxLength_ReturnsTrue()
        {
            var controller = CreateController();
            // 16 characters exactly
            var filePath = Path.Combine(Path.GetTempPath(), "abcdefghijklmnop.dlg");

            var result = await controller.ValidateFilenameAsync(filePath);

            Assert.True(result);
        }

        [AvaloniaFact]
        public async Task ValidateFilenameAsync_EmptyFilename_ReturnsTrue()
        {
            var controller = CreateController();
            // Empty filename without extension is 0 chars, within limit
            var filePath = Path.Combine(Path.GetTempPath(), ".dlg");

            var result = await controller.ValidateFilenameAsync(filePath);

            Assert.True(result);
        }

        [AvaloniaFact]
        public async Task ValidateFilenameAsync_IgnoresExtension()
        {
            var controller = CreateController();
            // "test" is 4 chars, well under 16
            var filePath = Path.Combine(Path.GetTempPath(), "test.verylongextension");

            var result = await controller.ValidateFilenameAsync(filePath);

            Assert.True(result);
        }

        [AvaloniaFact]
        public async Task ValidateFilenameAsync_WithNestedPath_OnlyChecksFilename()
        {
            var controller = CreateController();
            // Deep path but short filename
            var filePath = Path.Combine(
                Path.GetTempPath(),
                "some", "very", "deeply", "nested", "path",
                "test.dlg");

            var result = await controller.ValidateFilenameAsync(filePath);

            Assert.True(result);
        }

        #endregion

        #region OnCloseClick

        [AvaloniaFact]
        public void OnCloseClick_CallsCloseDialogAndClearActions()
        {
            bool propertiesCleared = false;
            bool flowchartsCleared = false;

            var controller = CreateController(
                clearPropertiesPanel: () => propertiesCleared = true,
                clearFlowcharts: () => flowchartsCleared = true);

            controller.OnCloseClick(null, null!);

            Assert.True(propertiesCleared);
            Assert.True(flowchartsCleared);
        }

        #endregion

        #region Helper

        /// <summary>
        /// Creates a FileMenuController with sensible test defaults.
        /// Parameters can be overridden for specific test scenarios.
        /// Window and SafeControlFinder are stubbed since UI tests are separate.
        /// </summary>
        private FileMenuController CreateController(
            ISettingsService? settings = null,
            Func<MainViewModel>? getViewModel = null,
            Action? saveCurrentNodeProperties = null,
            Action? clearPropertiesPanel = null,
            Action? populateRecentFilesMenu = null,
            Action? updateEmbeddedFlowchart = null,
            Action? clearFlowcharts = null,
            Func<ScriptParameterUIManager>? getParameterUIManager = null,
            Func<Task<bool>>? showSaveAsDialogAsync = null,
            Func<string, Task>? scanCreaturesForModule = null,
            Action<string>? updateDialogBrowserCurrentFile = null)
        {
            // Stub window/controls - these controllers need a Window but
            // we test business logic paths that don't touch UI controls.
            // The Window is needed for constructor but filename validation doesn't use it.
            var stubWindow = new Avalonia.Controls.Window();
            var stubControls = new SafeControlFinder(stubWindow);

            return new FileMenuController(
                window: stubWindow,
                controls: stubControls,
                settings: settings ?? _mockSettings,
                getViewModel: getViewModel ?? (() => _viewModel),
                saveCurrentNodeProperties: saveCurrentNodeProperties ?? (() => { }),
                clearPropertiesPanel: clearPropertiesPanel ?? (() => { }),
                populateRecentFilesMenu: populateRecentFilesMenu ?? (() => { }),
                updateEmbeddedFlowchartAfterLoad: updateEmbeddedFlowchart ?? (() => { }),
                clearFlowcharts: clearFlowcharts ?? (() => { }),
                getParameterUIManager: getParameterUIManager ?? (() => new ScriptParameterUIManager(
                    _ => null, _ => { }, () => { }, () => false, () => null)),
                showSaveAsDialogAsync: showSaveAsDialogAsync ?? (() => Task.FromResult(false)),
                scanCreaturesForModule: scanCreaturesForModule,
                updateDialogBrowserCurrentFile: updateDialogBrowserCurrentFile);
        }

        #endregion
    }
}
