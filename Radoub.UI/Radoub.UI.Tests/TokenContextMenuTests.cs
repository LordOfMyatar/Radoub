using Avalonia.Controls;
using Xunit;
using Radoub.UI.Services;
using Radoub.Formats.Tokens;

namespace Radoub.UI.Tests;

public class TokenContextMenuTests
{
    [Fact]
    public void BuildTokenSubmenu_HasNameCategory()
    {
        var items = TokenContextMenu.BuildStandardTokenMenuItems(_ => { });
        var nameMenu = items.OfType<MenuItem>().FirstOrDefault(m => m.Header?.ToString() == "Name");
        Assert.NotNull(nameMenu);
        var children = nameMenu.Items.OfType<MenuItem>().Select(m => m.Header?.ToString()).ToList();
        Assert.Contains("<FirstName>", children);
        Assert.Contains("<LastName>", children);
        Assert.Contains("<FullName>", children);
        Assert.Contains("<PlayerName>", children);
    }

    [Fact]
    public void BuildTokenSubmenu_HasGenderCategory()
    {
        var items = TokenContextMenu.BuildStandardTokenMenuItems(_ => { });
        var genderMenu = items.OfType<MenuItem>().FirstOrDefault(m => m.Header?.ToString() == "Gender");
        Assert.NotNull(genderMenu);
        var children = genderMenu.Items.OfType<MenuItem>().Select(m => m.Header?.ToString()).ToList();
        Assert.Contains("<Boy/Girl>", children);
        Assert.Contains("<boy/girl>", children);
        Assert.Contains("<He/She>", children);
        Assert.Contains("<he/she>", children);
    }

    [Fact]
    public void BuildTokenSubmenu_HasCharacterCategory()
    {
        var items = TokenContextMenu.BuildStandardTokenMenuItems(_ => { });
        var charMenu = items.OfType<MenuItem>().FirstOrDefault(m => m.Header?.ToString() == "Character");
        Assert.NotNull(charMenu);
        var children = charMenu.Items.OfType<MenuItem>().Select(m => m.Header?.ToString()).ToList();
        Assert.Contains("<Class>", children);
        Assert.Contains("<Race>", children);
        Assert.Contains("<Subrace>", children);
        Assert.Contains("<Deity>", children);
        Assert.Contains("<Level>", children);
    }

    [Fact]
    public void BuildTokenSubmenu_ExcludesAlignmentAndTime()
    {
        var items = TokenContextMenu.BuildStandardTokenMenuItems(_ => { });
        var headers = items.OfType<MenuItem>().Select(m => m.Header?.ToString()).ToList();
        Assert.DoesNotContain("Alignment", headers);
        Assert.DoesNotContain("Time", headers);
    }

    [Fact]
    public void BuildTokenSubmenu_ClickInvokesCallback()
    {
        string? inserted = null;
        var items = TokenContextMenu.BuildStandardTokenMenuItems(token => inserted = token);
        var nameMenu = items.OfType<MenuItem>().First(m => m.Header?.ToString() == "Name");
        var firstNameItem = nameMenu.Items.OfType<MenuItem>().First(m => m.Header?.ToString() == "<FirstName>");

        // Simulate click
        firstNameItem.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));

        Assert.Equal("<FirstName>", inserted);
    }

    [Fact]
    public void BuildInsertTokenMenu_HasAllTokensEntry()
    {
        var textBox = new TextBox();
        var menu = TokenContextMenu.BuildInsertTokenMenu(textBox, () => { });

        var allTokensItem = menu.Items.OfType<MenuItem>().LastOrDefault();
        Assert.NotNull(allTokensItem);
        Assert.Equal("All Tokens...", allTokensItem.Header?.ToString());
    }

    [Fact]
    public void BuildInsertTokenMenu_HasQuickSlots()
    {
        var textBox = new TextBox();
        var tempDir = Path.Combine(Path.GetTempPath(), $"radoub-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var configPath = Path.Combine(tempDir, "quick-tokens.json");
            var service = new QuickTokenService(configPath);
            var slots = service.Load();
            slots[0] = new QuickTokenSlot(1, "<CUSTOM1001>", "Red");
            service.Save(slots);

            var menu = TokenContextMenu.BuildInsertTokenMenu(textBox, () => { }, service);

            // Find quick slot items (contain star)
            var starItems = menu.Items.OfType<MenuItem>()
                .Where(m => m.Header?.ToString()?.Contains("\u2605") == true).ToList();
            Assert.Equal(3, starItems.Count);
            Assert.Contains("Red", starItems[0].Header?.ToString());
            Assert.Contains("(Set in All Tokens...)", starItems[1].Header?.ToString());
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void BuildInsertTokenMenu_EmptyQuickSlots_ShowSetMessage()
    {
        var textBox = new TextBox();
        var menu = TokenContextMenu.BuildInsertTokenMenu(textBox, () => { });

        var starItems = menu.Items.OfType<MenuItem>()
            .Where(m => m.Header?.ToString()?.Contains("\u2605") == true).ToList();
        Assert.Equal(3, starItems.Count);
        Assert.All(starItems, item =>
            Assert.Contains("(Set in All Tokens...)", item.Header?.ToString()));
    }
}
