using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Radoub.Formats.Itp;
using Radoub.UI.Services.Palette;

namespace Radoub.UI.ViewModels;

/// <summary>What a <see cref="PaletteNodeViewModel"/> represents in the tree.</summary>
public enum PaletteNodeKind { Category, Branch, Blueprint, Uncategorized }

/// <summary>
/// Observable adapter over one palette tree node (or the virtual Uncategorized bucket) for Avalonia
/// TreeView binding (#2477, M3). The M1 <see cref="ItpFile"/> tree is plain non-observable
/// <c>List</c>s; this mirrors it into <see cref="ObservableCollection{T}"/> so binding and
/// selection/expansion state work, without making the shared format model observable. The
/// Uncategorized node has a null <see cref="Model"/> (no backing <see cref="PaletteNode"/>) and is
/// never serialized.
/// </summary>
public partial class PaletteNodeViewModel : ObservableObject
{
    public PaletteNodeKind Kind { get; }

    /// <summary>The backing model node, or null for the virtual Uncategorized bucket.</summary>
    public PaletteNode? Model { get; }

    /// <summary>
    /// Parent node in the tree, or null for a top-level node. Set during <see cref="BuildForest"/>;
    /// used by <see cref="FullPath"/> to chain ancestor names for the disambiguation tooltip (#2488).
    /// </summary>
    public PaletteNodeViewModel? Parent { get; private set; }

    /// <summary>
    /// Root-to-here name chain, joined with " › " (e.g. "Weapons › Custom 1 › a_blade"). Shown as a
    /// tooltip so repeated names (CEP "Custom 1") are unambiguous on hover (#2488). A top-level node's
    /// full path is just its own name.
    /// </summary>
    public string FullPath => Parent is null ? Name : $"{Parent.FullPath} › {Name}";

    public ObservableCollection<PaletteNodeViewModel> Children { get; } = new();

    /// <summary>Add a child and set its <see cref="Parent"/> back-reference.</summary>
    private void AddChild(PaletteNodeViewModel child)
    {
        child.Parent = this;
        Children.Add(child);
    }

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private bool _isExpanded;
    [ObservableProperty] private bool _isSelected;

    private PaletteNodeViewModel(PaletteNodeKind kind, PaletteNode? model, string name)
    {
        Kind = kind;
        Model = model;
        _name = name;
    }

    /// <summary>
    /// Build the top-level adapter forest for the editor: every real <see cref="ItpFile.MainNodes"/>
    /// entry (categories with their blueprint leaves inline, recursively) followed by the virtual
    /// Uncategorized node listing <see cref="PaletteEditorViewModel.GetUncategorized"/>.
    /// </summary>
    /// <param name="strRefResolver">
    /// Optional TLK lookup for category names stored as a StrRef (standard categories carry their
    /// name as a TLK reference, not a literal). Returns null when unresolved; the display then falls
    /// back to a <c>[StrRef N]</c> placeholder. Null disables resolution entirely.
    /// </param>
    public static ObservableCollection<PaletteNodeViewModel> BuildForest(
        PaletteEditorViewModel vm, Func<uint, string?>? strRefResolver = null)
    {
        // Placement is by the blueprint's own PaletteID (authoritative), not the tree's stale
        // Blueprints lists: group every pool blueprint under the category it currently points at.
        var byCategoryId = new Dictionary<byte, List<string>>();
        var uncategorizedRefs = new List<string>();
        foreach (var resRef in vm.Pool.ResRefs)
        {
            var placement = vm.Classify(resRef);
            if (placement.Home is { } home)
                (byCategoryId.TryGetValue(home.Id, out var list) ? list : byCategoryId[home.Id] = new()).Add(resRef);
            else
                uncategorizedRefs.Add(resRef);
        }

        var forest = new ObservableCollection<PaletteNodeViewModel>();
        foreach (var node in vm.Palette.MainNodes)
            forest.Add(BuildNode(node, byCategoryId, strRefResolver));

        var uncategorized = new PaletteNodeViewModel(PaletteNodeKind.Uncategorized, null, "Uncategorized");
        foreach (var resRef in uncategorizedRefs.OrderBy(r => r, StringComparer.OrdinalIgnoreCase))
            uncategorized.AddChild(new PaletteNodeViewModel(PaletteNodeKind.Blueprint, null, resRef));
        forest.Add(uncategorized);
        return forest;
    }

    private static PaletteNodeViewModel BuildNode(
        PaletteNode node, Dictionary<byte, List<string>> byCategoryId, Func<uint, string?>? strRefResolver)
    {
        switch (node)
        {
            case PaletteCategoryNode cat:
            {
                var vmNode = new PaletteNodeViewModel(PaletteNodeKind.Category, cat, DisplayName(cat, strRefResolver));
                // Blueprint leaves come from the pool grouped by PaletteID, not cat.Blueprints.
                if (byCategoryId.TryGetValue(cat.Id, out var refs))
                    foreach (var resRef in refs.OrderBy(r => r, StringComparer.OrdinalIgnoreCase))
                        vmNode.AddChild(new PaletteNodeViewModel(PaletteNodeKind.Blueprint, null, resRef));
                foreach (var child in cat.Children)
                    vmNode.AddChild(BuildNode(child, byCategoryId, strRefResolver));
                return vmNode;
            }
            case PaletteBranchNode br:
            {
                var vmNode = new PaletteNodeViewModel(PaletteNodeKind.Branch, br, DisplayName(br, strRefResolver));
                foreach (var child in br.Children)
                    vmNode.AddChild(BuildNode(child, byCategoryId, strRefResolver));
                return vmNode;
            }
            default:
                return new PaletteNodeViewModel(PaletteNodeKind.Blueprint, node,
                    (node as PaletteBlueprintNode)?.ResRef ?? string.Empty);
        }
    }

    // Category display: the literal Name if present, else the TLK-resolved StrRef text, else a
    // [StrRef N] placeholder. Standard categories store their name as a StrRef, so the resolver is
    // what turns the tree from "[StrRef 5432]" into "Armor".
    private static string DisplayName(PaletteNode node, Func<uint, string?>? strRefResolver)
    {
        if (!string.IsNullOrEmpty(node.Name)) return node.Name!;
        if (node.StrRef is uint s)
        {
            var resolved = strRefResolver?.Invoke(s);
            return !string.IsNullOrEmpty(resolved) ? resolved! : $"[StrRef {s}]";
        }
        return "(unnamed)";
    }
}
