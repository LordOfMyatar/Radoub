using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Parley.Views.Helpers;
using Xunit;

namespace Parley.Tests
{
    /// <summary>
    /// Unit tests for SafeControlFinder utility class (Issue #342).
    /// Tests control finding, caching, and null-safe access patterns.
    /// </summary>
    public class SafeControlFinderTests
    {
        /// <summary>
        /// Creates a test panel with named controls for testing.
        /// Uses NameScope to enable FindControl to work in unit tests.
        /// </summary>
        private StackPanel CreateTestPanel()
        {
            var panel = new StackPanel();

            // Create a NameScope for the panel so FindControl works
            var nameScope = new NameScope();
            NameScope.SetNameScope(panel, nameScope);

            var textBox = new TextBox { Name = "TestTextBox", Text = "Initial" };
            var checkBox = new CheckBox { Name = "TestCheckBox", IsChecked = true };
            var button = new Button { Name = "TestButton", Content = "Click" };
            var comboBox = new ComboBox { Name = "TestComboBox" };

            panel.Children.Add(textBox);
            panel.Children.Add(checkBox);
            panel.Children.Add(button);
            panel.Children.Add(comboBox);

            // Register controls in the NameScope
            nameScope.Register("TestTextBox", textBox);
            nameScope.Register("TestCheckBox", checkBox);
            nameScope.Register("TestButton", button);
            nameScope.Register("TestComboBox", comboBox);

            return panel;
        }

        [AvaloniaFact]
        public void Get_ExistingControl_ReturnsControl()
        {
            // Arrange
            var panel = CreateTestPanel();
            var finder = new SafeControlFinder(panel);

            // Act
            var textBox = finder.Get<TextBox>("TestTextBox");

            // Assert
            Assert.NotNull(textBox);
            Assert.Equal("Initial", textBox.Text);
        }

        [AvaloniaFact]
        public void Get_NonExistingControl_ReturnsNull()
        {
            // Arrange
            var panel = CreateTestPanel();
            var finder = new SafeControlFinder(panel);

            // Act
            var result = finder.Get<TextBox>("NonExistent");

            // Assert
            Assert.Null(result);
        }

        [AvaloniaFact]
        public void Get_WrongType_ReturnsNull()
        {
            // Arrange
            var panel = CreateTestPanel();
            var finder = new SafeControlFinder(panel);

            // Act - TestTextBox exists but is not a Button
            var result = finder.Get<Button>("TestTextBox");

            // Assert
            Assert.Null(result);
        }

        [AvaloniaFact]
        public void WithControl_ExistingControl_ExecutesAction()
        {
            // Arrange
            var panel = CreateTestPanel();
            var finder = new SafeControlFinder(panel);
            var executed = false;

            // Act
            var result = finder.WithControl<TextBox>("TestTextBox", tb =>
            {
                tb.Text = "Modified";
                executed = true;
            });

            // Assert
            Assert.True(result);
            Assert.True(executed);
            Assert.Equal("Modified", finder.Get<TextBox>("TestTextBox")?.Text);
        }

        [AvaloniaFact]
        public void WithControl_NonExistingControl_DoesNotExecuteAction()
        {
            // Arrange
            var panel = CreateTestPanel();
            var finder = new SafeControlFinder(panel);
            var executed = false;

            // Act
            var result = finder.WithControl<TextBox>("NonExistent", tb => executed = true);

            // Assert
            Assert.False(result);
            Assert.False(executed);
        }

        [AvaloniaFact]
        public void WithControls_TwoExistingControls_ExecutesAction()
        {
            // Arrange
            var panel = CreateTestPanel();
            var finder = new SafeControlFinder(panel);
            var executed = false;

            // Act
            var result = finder.WithControls<TextBox, CheckBox>(
                "TestTextBox", "TestCheckBox",
                (tb, cb) =>
                {
                    tb.Text = "Updated";
                    cb.IsChecked = false;
                    executed = true;
                });

            // Assert
            Assert.True(result);
            Assert.True(executed);
            Assert.Equal("Updated", finder.Get<TextBox>("TestTextBox")?.Text);
            Assert.False(finder.Get<CheckBox>("TestCheckBox")?.IsChecked);
        }

        [AvaloniaFact]
        public void WithControls_OneMissing_DoesNotExecuteAction()
        {
            // Arrange
            var panel = CreateTestPanel();
            var finder = new SafeControlFinder(panel);
            var executed = false;

            // Act
            var result = finder.WithControls<TextBox, CheckBox>(
                "TestTextBox", "NonExistent",
                (tb, cb) => executed = true);

            // Assert
            Assert.False(result);
            Assert.False(executed);
        }

        [AvaloniaFact]
        public void WithControls_ThreeControls_AllExist_ExecutesAction()
        {
            // Arrange
            var panel = CreateTestPanel();
            var finder = new SafeControlFinder(panel);
            var executed = false;

            // Act
            var result = finder.WithControls<TextBox, CheckBox, Button>(
                "TestTextBox", "TestCheckBox", "TestButton",
                (tb, cb, btn) => executed = true);

            // Assert
            Assert.True(result);
            Assert.True(executed);
        }

        [AvaloniaFact]
        public void WithControls_FourControls_AllExist_ExecutesAction()
        {
            // Arrange
            var panel = CreateTestPanel();
            var finder = new SafeControlFinder(panel);
            var executed = false;

            // Act
            var result = finder.WithControls<TextBox, CheckBox, Button, ComboBox>(
                "TestTextBox", "TestCheckBox", "TestButton", "TestComboBox",
                (tb, cb, btn, combo) => executed = true);

            // Assert
            Assert.True(result);
            Assert.True(executed);
        }

        [AvaloniaFact]
        public void SetText_ExistingTextBox_SetsText()
        {
            // Arrange
            var panel = CreateTestPanel();
            var finder = new SafeControlFinder(panel);

            // Act
            var result = finder.SetText("TestTextBox", "New Value");

            // Assert
            Assert.True(result);
            Assert.Equal("New Value", finder.Get<TextBox>("TestTextBox")?.Text);
        }

        [AvaloniaFact]
        public void SetText_NonExistingTextBox_ReturnsFalse()
        {
            // Arrange
            var panel = CreateTestPanel();
            var finder = new SafeControlFinder(panel);

            // Act
            var result = finder.SetText("NonExistent", "Value");

            // Assert
            Assert.False(result);
        }

        [AvaloniaFact]
        public void SetText_NullValue_SetsEmptyString()
        {
            // Arrange
            var panel = CreateTestPanel();
            var finder = new SafeControlFinder(panel);

            // Act
            var result = finder.SetText("TestTextBox", null);

            // Assert
            Assert.True(result);
            Assert.Equal("", finder.Get<TextBox>("TestTextBox")?.Text);
        }

        [AvaloniaFact]
        public void GetText_ExistingTextBox_ReturnsText()
        {
            // Arrange
            var panel = CreateTestPanel();
            var finder = new SafeControlFinder(panel);

            // Act
            var result = finder.GetText("TestTextBox");

            // Assert
            Assert.Equal("Initial", result);
        }

        [AvaloniaFact]
        public void GetText_NonExistingTextBox_ReturnsNull()
        {
            // Arrange
            var panel = CreateTestPanel();
            var finder = new SafeControlFinder(panel);

            // Act
            var result = finder.GetText("NonExistent");

            // Assert
            Assert.Null(result);
        }

        [AvaloniaFact]
        public void SetChecked_ExistingCheckBox_SetsValue()
        {
            // Arrange
            var panel = CreateTestPanel();
            var finder = new SafeControlFinder(panel);

            // Act
            var result = finder.SetChecked("TestCheckBox", false);

            // Assert
            Assert.True(result);
            Assert.False(finder.Get<CheckBox>("TestCheckBox")?.IsChecked);
        }

        [AvaloniaFact]
        public void GetChecked_ExistingCheckBox_ReturnsValue()
        {
            // Arrange
            var panel = CreateTestPanel();
            var finder = new SafeControlFinder(panel);

            // Act
            var result = finder.GetChecked("TestCheckBox");

            // Assert
            Assert.True(result);
        }

        [AvaloniaFact]
        public void SetEnabled_ExistingControl_SetsValue()
        {
            // Arrange
            var panel = CreateTestPanel();
            var finder = new SafeControlFinder(panel);

            // Act
            var result = finder.SetEnabled("TestButton", false);

            // Assert
            Assert.True(result);
            Assert.False(finder.Get<Button>("TestButton")?.IsEnabled);
        }

        [AvaloniaFact]
        public void SetVisible_ExistingControl_SetsValue()
        {
            // Arrange
            var panel = CreateTestPanel();
            var finder = new SafeControlFinder(panel);

            // Act
            var result = finder.SetVisible("TestButton", false);

            // Assert
            Assert.True(result);
            Assert.False(finder.Get<Button>("TestButton")?.IsVisible);
        }

        [AvaloniaFact]
        public void Caching_Enabled_ReturnsCachedControl()
        {
            // Arrange
            var panel = CreateTestPanel();
            var finder = new SafeControlFinder(panel, enableCaching: true);

            // Act - Get control twice
            var first = finder.Get<TextBox>("TestTextBox");
            var second = finder.Get<TextBox>("TestTextBox");

            // Assert - Both references should be same object (cached)
            Assert.Same(first, second);
        }

        [AvaloniaFact]
        public void ClearCache_RemovesCachedControls()
        {
            // Arrange
            var panel = CreateTestPanel();
            var finder = new SafeControlFinder(panel, enableCaching: true);

            // Prime the cache
            var first = finder.Get<TextBox>("TestTextBox");

            // Act
            finder.ClearCache();

            // Get again after clear
            var second = finder.Get<TextBox>("TestTextBox");

            // Assert - Should still work (finds control again)
            Assert.NotNull(second);
            Assert.Equal(first?.Text, second?.Text);
        }

        [AvaloniaFact]
        public void SetCachingEnabled_DisablesCachingAndClearsCache()
        {
            // Arrange
            var panel = CreateTestPanel();
            var finder = new SafeControlFinder(panel, enableCaching: true);

            // Prime the cache
            _ = finder.Get<TextBox>("TestTextBox");

            // Act - Disable caching
            finder.SetCachingEnabled(false);

            // Assert - Should still find control
            var result = finder.Get<TextBox>("TestTextBox");
            Assert.NotNull(result);
        }

        [AvaloniaFact]
        public void Constructor_NullRoot_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<System.ArgumentNullException>(() => new SafeControlFinder(null!));
        }
    }
}
