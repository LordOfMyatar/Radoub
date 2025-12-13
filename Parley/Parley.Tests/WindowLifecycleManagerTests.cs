using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using DialogEditor.Services;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// Unit tests for WindowLifecycleManager (Issue #343).
    /// Tests window lifecycle management, creation, and cleanup patterns.
    /// </summary>
    public class WindowLifecycleManagerTests
    {
        /// <summary>
        /// Creates a simple test window for testing.
        /// </summary>
        private Window CreateTestWindow()
        {
            return new Window { Title = "Test Window" };
        }

        [AvaloniaFact]
        public void GetOrCreate_NewWindow_CreatesWindow()
        {
            // Arrange
            var manager = new WindowLifecycleManager();

            // Act
            var window = manager.GetOrCreate("Test", CreateTestWindow);

            // Assert
            Assert.NotNull(window);
            Assert.Equal("Test Window", window.Title);
        }

        [AvaloniaFact]
        public void GetOrCreate_ExistingVisibleWindow_ReturnsSameWindow()
        {
            // Arrange
            var manager = new WindowLifecycleManager();
            var firstWindow = manager.GetOrCreate("Test", CreateTestWindow);
            firstWindow.Show();

            // Act
            var secondWindow = manager.GetOrCreate("Test", CreateTestWindow);

            // Assert
            Assert.Same(firstWindow, secondWindow);

            // Cleanup
            firstWindow.Close();
        }

        [AvaloniaFact]
        public void Get_NonExistingWindow_ReturnsNull()
        {
            // Arrange
            var manager = new WindowLifecycleManager();

            // Act
            var window = manager.Get<Window>("NonExistent");

            // Assert
            Assert.Null(window);
        }

        [AvaloniaFact]
        public void IsOpen_VisibleWindow_ReturnsTrue()
        {
            // Arrange
            var manager = new WindowLifecycleManager();
            var window = manager.GetOrCreate("Test", CreateTestWindow);
            window.Show();

            // Act
            var isOpen = manager.IsOpen("Test");

            // Assert
            Assert.True(isOpen);

            // Cleanup
            window.Close();
        }

        [AvaloniaFact]
        public void IsOpen_NonExistingWindow_ReturnsFalse()
        {
            // Arrange
            var manager = new WindowLifecycleManager();

            // Act
            var isOpen = manager.IsOpen("NonExistent");

            // Assert
            Assert.False(isOpen);
        }

        [AvaloniaFact]
        public void Close_ExistingWindow_ClosesAndReturnsTrue()
        {
            // Arrange
            var manager = new WindowLifecycleManager();
            var window = manager.GetOrCreate("Test", CreateTestWindow);
            window.Show();

            // Act
            var result = manager.Close("Test");

            // Assert
            Assert.True(result);
            Assert.False(manager.IsOpen("Test"));
        }

        [AvaloniaFact]
        public void Close_NonExistingWindow_ReturnsFalse()
        {
            // Arrange
            var manager = new WindowLifecycleManager();

            // Act
            var result = manager.Close("NonExistent");

            // Assert
            Assert.False(result);
        }

        [AvaloniaFact]
        public void WithWindow_ExistingVisibleWindow_ExecutesAction()
        {
            // Arrange
            var manager = new WindowLifecycleManager();
            var window = manager.GetOrCreate("Test", CreateTestWindow);
            window.Show();
            var executed = false;

            // Act
            var result = manager.WithWindow<Window>("Test", w =>
            {
                executed = true;
                Assert.Same(window, w);
            });

            // Assert
            Assert.True(result);
            Assert.True(executed);

            // Cleanup
            window.Close();
        }

        [AvaloniaFact]
        public void WithWindow_NonExistingWindow_DoesNotExecuteAction()
        {
            // Arrange
            var manager = new WindowLifecycleManager();
            var executed = false;

            // Act
            var result = manager.WithWindow<Window>("NonExistent", w => executed = true);

            // Assert
            Assert.False(result);
            Assert.False(executed);
        }

        [AvaloniaFact]
        public void ShowOrActivate_NewWindow_ShowsWindow()
        {
            // Arrange
            var manager = new WindowLifecycleManager();

            // Act
            var window = manager.ShowOrActivate("Test", CreateTestWindow);

            // Assert
            Assert.NotNull(window);
            Assert.True(window.IsVisible);

            // Cleanup
            window.Close();
        }

        [AvaloniaFact]
        public void ShowOrActivate_ExistingWindow_ActivatesWindow()
        {
            // Arrange
            var manager = new WindowLifecycleManager();
            var firstWindow = manager.ShowOrActivate("Test", CreateTestWindow);

            // Act
            var secondWindow = manager.ShowOrActivate("Test", CreateTestWindow);

            // Assert
            Assert.Same(firstWindow, secondWindow);

            // Cleanup
            firstWindow.Close();
        }

        [AvaloniaFact]
        public void CloseAll_ClosesAllManagedWindows()
        {
            // Arrange
            var manager = new WindowLifecycleManager();
            var window1 = manager.GetOrCreate("Test1", CreateTestWindow);
            var window2 = manager.GetOrCreate("Test2", CreateTestWindow);
            window1.Show();
            window2.Show();

            // Act
            manager.CloseAll();

            // Assert
            Assert.False(manager.IsOpen("Test1"));
            Assert.False(manager.IsOpen("Test2"));
        }

        [AvaloniaFact]
        public void OnClosed_CallbackInvoked_WhenWindowCloses()
        {
            // Arrange
            var manager = new WindowLifecycleManager();
            var callbackInvoked = false;

            var window = manager.GetOrCreate(
                "Test",
                CreateTestWindow,
                onClosed: w => callbackInvoked = true);
            window.Show();

            // Act
            window.Close();

            // Assert
            Assert.True(callbackInvoked);
        }

        [AvaloniaFact]
        public void WindowKeys_ContainsExpectedKeys()
        {
            // Assert - Verify the well-known keys exist
            Assert.Equal("Settings", WindowKeys.Settings);
            Assert.Equal("Flowchart", WindowKeys.Flowchart);
            Assert.Equal("SoundBrowser", WindowKeys.SoundBrowser);
            Assert.Equal("ScriptBrowser", WindowKeys.ScriptBrowser);
            Assert.Equal("ParameterBrowser", WindowKeys.ParameterBrowser);
        }

        [AvaloniaFact]
        public void GetOrCreate_AfterWindowClosed_CreatesNewWindow()
        {
            // Arrange
            var manager = new WindowLifecycleManager();
            var firstWindow = manager.GetOrCreate("Test", CreateTestWindow);
            firstWindow.Show();
            firstWindow.Close();

            // Act
            var secondWindow = manager.GetOrCreate("Test", CreateTestWindow);

            // Assert
            Assert.NotSame(firstWindow, secondWindow);

            // Cleanup
            secondWindow.Close();
        }
    }
}
