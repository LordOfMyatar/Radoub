using Avalonia.Controls;
using Avalonia.Input;
using Radoub.Formats.Tokens;

namespace Radoub.UI.Services;

public static class TokenContextMenu
{
    // Categories to include in the non-conversation context menu
    private static readonly string[] IncludedCategories =
    {
        "Name", "Gender (Capitalized)", "Gender (Lowercase)", "Character"
    };

    // Display names for the sub-submenus (merge both gender categories into one)
    private static readonly Dictionary<string, string> CategoryDisplayNames = new()
    {
        ["Name"] = "Name",
        ["Gender (Capitalized)"] = "Gender",
        ["Gender (Lowercase)"] = "Gender",
        ["Character"] = "Character"
    };

    /// <summary>
    /// Build the standard token menu items (Name, Gender, Character sub-submenus).
    /// Each token click invokes the onInsert callback with the token string.
    /// </summary>
    public static List<Control> BuildStandardTokenMenuItems(Action<string> onInsert)
    {
        var items = new List<Control>();
        var addedCategories = new HashSet<string>();

        foreach (var category in IncludedCategories)
        {
            if (!TokenDefinitions.TokensByCategory.TryGetValue(category, out var tokens))
                continue;

            var displayName = CategoryDisplayNames[category];

            // Merge gender categories into one submenu
            MenuItem categoryMenu;
            if (addedCategories.Contains(displayName))
            {
                categoryMenu = items.OfType<MenuItem>().First(m => m.Header?.ToString() == displayName);
                categoryMenu.Items.Add(new Separator());
            }
            else
            {
                categoryMenu = new MenuItem { Header = displayName };
                items.Add(categoryMenu);
                addedCategories.Add(displayName);
            }

            foreach (var token in tokens)
            {
                var tokenText = $"<{token}>";
                var menuItem = new MenuItem { Header = tokenText };
                menuItem.Click += (_, _) => onInsert(tokenText);
                categoryMenu.Items.Add(menuItem);
            }
        }

        return items;
    }

    /// <summary>
    /// Build the full "Insert Token" submenu with standard tokens, quick slots, and "All Tokens...".
    /// </summary>
    public static MenuItem BuildInsertTokenMenu(
        TextBox targetTextBox,
        Action openTokenWindow,
        QuickTokenService? quickTokenService = null)
    {
        var insertMenu = new MenuItem { Header = "Insert Token" };

        // Standard token sub-submenus
        var standardItems = BuildStandardTokenMenuItems(token =>
            TokenInsertionHelper.InsertToken(targetTextBox, token));

        foreach (var item in standardItems)
            insertMenu.Items.Add(item);

        // Quick slots
        insertMenu.Items.Add(new Separator());
        var slots = quickTokenService?.Load() ?? new[]
        {
            new QuickTokenSlot(1, null, null),
            new QuickTokenSlot(2, null, null),
            new QuickTokenSlot(3, null, null)
        };

        foreach (var slot in slots)
        {
            var slotItem = new MenuItem();
            if (slot.Token != null)
            {
                slotItem.Header = $"\u2605 {slot.Label ?? slot.Token}";
                var tokenCopy = slot.Token;
                slotItem.Click += (_, _) => TokenInsertionHelper.InsertToken(targetTextBox, tokenCopy);
            }
            else
            {
                slotItem.Header = "\u2605 (Set in All Tokens...)";
                slotItem.Click += (_, _) => openTokenWindow();
            }
            insertMenu.Items.Add(slotItem);
        }

        // "All Tokens..." entry
        insertMenu.Items.Add(new Separator());
        var allTokensItem = new MenuItem
        {
            Header = "All Tokens...",
            InputGesture = new KeyGesture(Key.T, KeyModifiers.Control)
        };
        allTokensItem.Click += (_, _) => openTokenWindow();
        insertMenu.Items.Add(allTokensItem);

        return insertMenu;
    }

    /// <summary>
    /// Append token menu items to an existing ContextMenu (for SpellCheckTextBox).
    /// </summary>
    public static void AppendTokenMenu(
        ContextMenu menu,
        TextBox targetTextBox,
        Action openTokenWindow,
        QuickTokenService? quickTokenService = null)
    {
        menu.Items.Add(BuildInsertTokenMenu(targetTextBox, openTokenWindow, quickTokenService));
    }

    /// <summary>
    /// Attach a token-enabled context menu to a plain TextBox.
    /// </summary>
    public static void Attach(
        TextBox textBox,
        Action openTokenWindow,
        QuickTokenService? quickTokenService = null)
    {
        textBox.ContextMenu = new ContextMenu();

        textBox.AddHandler(InputElement.PointerPressedEvent, (_, e) =>
        {
            if (e.GetCurrentPoint(textBox).Properties.IsRightButtonPressed)
            {
                var menu = new ContextMenu();

                // Standard edit items
                var cutItem = new MenuItem { Header = "Cut" };
                cutItem.Click += (_, _) => textBox.Cut();
                cutItem.IsEnabled = textBox.SelectedText?.Length > 0;
                menu.Items.Add(cutItem);

                var copyItem = new MenuItem { Header = "Copy" };
                copyItem.Click += (_, _) => textBox.Copy();
                copyItem.IsEnabled = textBox.SelectedText?.Length > 0;
                menu.Items.Add(copyItem);

                var pasteItem = new MenuItem { Header = "Paste" };
                pasteItem.Click += (_, _) => textBox.Paste();
                menu.Items.Add(pasteItem);

                // Token menu
                menu.Items.Add(new Separator());
                menu.Items.Add(BuildInsertTokenMenu(textBox, openTokenWindow, quickTokenService));

                textBox.ContextMenu = menu;
            }
        }, handledEventsToo: true);
    }
}
