using ItemEditor.ViewModels;
using Radoub.Formats.Logging;
using Radoub.Formats.Mdl;
using Radoub.Formats.Uti;
using Radoub.UI.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace ItemEditor.Services;

/// <summary>
/// Wires <see cref="ItemViewModel"/> property changes to the 3D preview renderer for
/// the currently-edited item. Handles ModelType-specific load and recolor decisions and
/// debounces rapid bursts (e.g. color spinner drags) so the renderer reloads at most
/// once per quiet window. UI-thread-agnostic: the production caller schedules
/// <see cref="FlushDebounce"/> through a <c>DispatcherTimer</c>, tests pump it manually.
///
/// Per the four-PR plan (NonPublic/Plans/2026-04-30-1996-1908-plan.md, PR3b section):
/// no animations, mannequin-prefixed armor composition, debounce 100 ms.
/// </summary>
public sealed class ItemPreviewController
{
    /// <summary>VM properties that should trigger a preview reload when they change.</summary>
    private static readonly HashSet<string> WatchedProperties = new(StringComparer.Ordinal)
    {
        nameof(ItemViewModel.BaseItem),
        nameof(ItemViewModel.ModelPart1),
        nameof(ItemViewModel.ModelPart2),
        nameof(ItemViewModel.ModelPart3),
        nameof(ItemViewModel.Cloth1Color),
        nameof(ItemViewModel.Cloth2Color),
        nameof(ItemViewModel.Leather1Color),
        nameof(ItemViewModel.Leather2Color),
        nameof(ItemViewModel.Metal1Color),
        nameof(ItemViewModel.Metal2Color),
    };

    private readonly ItemModelResolver _resolver;
    private readonly MdlPartComposer _composer;
    private readonly IItemPreviewRenderer _renderer;
    private readonly string _armorMannequinPrefix;
    private readonly bool _debounceManually;

    private ItemViewModel? _viewModel;
    private bool _pendingUpdate;

    public ItemPreviewController(
        ItemModelResolver resolver,
        MdlPartComposer composer,
        IItemPreviewRenderer renderer,
        string armorMannequinPrefix = "pmh0",
        bool debounceManually = false)
    {
        _resolver = resolver;
        _composer = composer;
        _renderer = renderer;
        _armorMannequinPrefix = armorMannequinPrefix;
        _debounceManually = debounceManually;
    }

    public bool HasPendingUpdate => _pendingUpdate;

    public void BindViewModel(ItemViewModel? vm)
    {
        Unbind();

        _viewModel = vm;
        if (vm == null)
        {
            _renderer.Clear();
            return;
        }

        vm.PropertyChanged += OnViewModelPropertyChanged;
        _pendingUpdate = true;
    }

    public void Unbind()
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel = null;
        }
        _pendingUpdate = false;
    }

    public void FlushDebounce()
    {
        if (!_pendingUpdate) return;
        _pendingUpdate = false;

        var vm = _viewModel;
        if (vm == null)
        {
            _renderer.Clear();
            return;
        }

        ApplyUpdate(vm);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == null || !WatchedProperties.Contains(e.PropertyName))
            return;

        _pendingUpdate = true;
        // Production hook: the code-behind owns a DispatcherTimer that observes
        // _pendingUpdate and calls FlushDebounce after the quiet window. Tests pass
        // debounceManually=true and call FlushDebounce directly.
        _ = _debounceManually;
    }

    private void ApplyUpdate(ItemViewModel vm)
    {
        try
        {
            var uti = vm.Uti;
            var resolution = _resolver.Resolve(uti);

            if (!resolution.HasModel)
            {
                _renderer.Clear();
                return;
            }

            // ModelType is implied by the resolver's flags + result count:
            //   armor (3): HasArmorParts=true → Compose with skeleton+parts
            //   simple (0) / composite (2): HasColorFields=false → ComposeFlat
            //   layered (1): HasColorFields=true && HasArmorParts=false → ComposeFlat + cloth-only colors
            MdlModel? composed = resolution.HasArmorParts
                ? _composer.Compose(_armorMannequinPrefix, BuildArmorParts(resolution.MdlResRefs))
                : _composer.ComposeFlat(resolution.MdlResRefs, $"item_{uti.BaseItem}");

            if (composed == null)
            {
                _renderer.Clear();
                return;
            }

            _renderer.SetModel(composed);

            if (resolution.HasColorFields)
            {
                ApplyColors(resolution.HasArmorParts, vm);
            }
        }
        catch (Exception ex)
        {
            UnifiedLogger.LogApplication(LogLevel.WARN,
                $"ItemPreviewController.ApplyUpdate failed: {ex.GetType().Name}: {ex.Message}");
            _renderer.Clear();
        }
    }

    private void ApplyColors(bool isArmor, ItemViewModel vm)
    {
        if (isArmor)
        {
            // ModelType 3 — all six color slots apply
            _renderer.SetArmorColors(
                metal1: vm.Metal1Color,
                metal2: vm.Metal2Color,
                cloth1: vm.Cloth1Color,
                cloth2: vm.Cloth2Color,
                leather1: vm.Leather1Color,
                leather2: vm.Leather2Color);
        }
        else
        {
            // ModelType 1 (Layered) — Cloth1/2 only. Pass 0 for the others so the
            // renderer does not recolor metal/leather layers that don't exist on layered items.
            _renderer.SetArmorColors(
                metal1: 0,
                metal2: 0,
                cloth1: vm.Cloth1Color,
                cloth2: vm.Cloth2Color,
                leather1: 0,
                leather2: 0);
        }
    }

    /// <summary>
    /// Recover (partType, resRef) tuples from the resolver's flat ResRef list.
    /// The resolver produced ResRefs of the form <c>{prefix}_{partType}{partNumber:D3}</c>
    /// (e.g. <c>pmh0_chest005</c>); peel off the prefix and the trailing 3 digits.
    /// </summary>
    private static List<(string PartType, string ResRef)> BuildArmorParts(IReadOnlyList<string> resolvedResRefs)
    {
        var parts = new List<(string, string)>(resolvedResRefs.Count);
        foreach (var resRef in resolvedResRefs)
        {
            var underscore = resRef.IndexOf('_');
            if (underscore < 0) continue;
            var afterPrefix = resRef.AsSpan(underscore + 1);
            if (afterPrefix.Length < 4) continue; // need at least 1-char partType + 3 digits
            var partType = afterPrefix[..^3].ToString();
            parts.Add((partType, resRef));
        }
        return parts;
    }
}
