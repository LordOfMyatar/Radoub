using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Radoub.UI.Utils;

/// <summary>
/// Pure XAML lint that flags "dead-stub" menu items (#2362, #2231 guard).
///
/// A dead stub is a <c>&lt;MenuItem&gt;</c> inside a top-level <c>&lt;Menu&gt;</c> whose
/// enabled state is hardcoded <c>IsEnabled="False"</c> — the disabled
/// "Not yet implemented" anti-pattern the #2359 audit found on Manifest/Fence Undo/Redo.
/// A genuinely wired menu item drives its enabled state via a binding (or code-behind),
/// never a literal False.
///
/// Deliberately scoped to top-level <c>&lt;Menu&gt;</c> only. Literal
/// <c>IsEnabled="False"</c> is legitimate and common elsewhere — OK buttons disabled
/// until a selection, wizard Back buttons, and <c>&lt;ContextMenu&gt;</c> items paired
/// with an <c>IsVisible</c> binding — so those are not flagged.
/// </summary>
public static class MenuStubAnalyzer
{
    /// <summary>A flagged dead-stub menu item.</summary>
    public readonly record struct MenuStubViolation(string Header, string Reason);

    private const string AvaloniaNs = "https://github.com/avaloniaui";

    /// <summary>
    /// Returns the dead-stub violations in a single AXAML document. Empty when clean.
    /// Malformed XML yields an empty list rather than throwing — a non-parseable view
    /// would fail the build elsewhere, and the lint should not be the thing that crashes.
    /// </summary>
    public static IReadOnlyList<MenuStubViolation> FindDisabledMenuStubs(string axamlContent)
    {
        var violations = new List<MenuStubViolation>();
        if (string.IsNullOrWhiteSpace(axamlContent))
        {
            return violations;
        }

        XDocument doc;
        try
        {
            doc = XDocument.Parse(axamlContent);
        }
        catch (System.Xml.XmlException)
        {
            return violations;
        }

        if (doc.Root is null)
        {
            return violations;
        }

        XName menuName = XName.Get("Menu", AvaloniaNs);
        XName menuItemName = XName.Get("MenuItem", AvaloniaNs);

        // Only top-level <Menu> elements. ContextMenus (and anything nested inside one)
        // are excluded by construction since we never descend from a ContextMenu root.
        foreach (var menu in doc.Descendants(menuName))
        {
            foreach (var item in menu.Descendants(menuItemName))
            {
                if (IsLiteralDisabled(item))
                {
                    string header = (string?)item.Attribute("Header") ?? "(unnamed)";
                    violations.Add(new MenuStubViolation(
                        header,
                        $"Top-level menu item '{header}' is hardcoded IsEnabled=\"False\" " +
                        "(dead stub). Wire its enabled state via a binding or remove it."));
                }
            }
        }

        return violations;
    }

    private static bool IsLiteralDisabled(XElement menuItem)
    {
        string? value = (string?)menuItem.Attribute("IsEnabled");
        if (value is null)
        {
            return false;
        }

        // A binding like "{Binding HasFile}" is wired, not a stub.
        string trimmed = value.Trim();
        if (trimmed.StartsWith('{'))
        {
            return false;
        }

        return string.Equals(trimmed, "False", System.StringComparison.OrdinalIgnoreCase);
    }
}
