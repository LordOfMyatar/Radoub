using System;
using System.Collections.ObjectModel;
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

    public ObservableCollection<PaletteNodeViewModel> Children { get; } = new();

    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private bool _isDrifted;
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
        var forest = new ObservableCollection<PaletteNodeViewModel>();
        foreach (var node in vm.Palette.MainNodes)
            forest.Add(BuildNode(node, vm, strRefResolver));

        var uncategorized = new PaletteNodeViewModel(PaletteNodeKind.Uncategorized, null, "Uncategorized");
        foreach (var resRef in vm.GetUncategorized())
            uncategorized.Children.Add(new PaletteNodeViewModel(PaletteNodeKind.Blueprint, null, resRef));
        forest.Add(uncategorized);
        return forest;
    }

    private static PaletteNodeViewModel BuildNode(
        PaletteNode node, PaletteEditorViewModel vm, Func<uint, string?>? strRefResolver)
    {
        switch (node)
        {
            case PaletteCategoryNode cat:
            {
                var vmNode = new PaletteNodeViewModel(PaletteNodeKind.Category, cat, DisplayName(cat, strRefResolver));
                foreach (var bp in cat.Blueprints)
                {
                    var leaf = new PaletteNodeViewModel(PaletteNodeKind.Blueprint, bp, bp.ResRef)
                    {
                        IsDrifted = vm.Classify(bp.ResRef).Kind == PalettePlacementKind.Drifted,
                    };
                    vmNode.Children.Add(leaf);
                }
                foreach (var child in cat.Children)
                    vmNode.Children.Add(BuildNode(child, vm, strRefResolver));
                return vmNode;
            }
            case PaletteBranchNode br:
            {
                var vmNode = new PaletteNodeViewModel(PaletteNodeKind.Branch, br, DisplayName(br, strRefResolver));
                foreach (var child in br.Children)
                    vmNode.Children.Add(BuildNode(child, vm, strRefResolver));
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
