using ItemEditor.Services;
using ItemEditor.ViewModels;
using Radoub.Formats.Common;
using Radoub.Formats.Mdl;
using Radoub.Formats.Services;
using Radoub.Formats.TwoDA;
using Radoub.Formats.Uti;
using Radoub.TestUtilities.Mocks;
using Radoub.UI.Services;
using System.Numerics;
using Xunit;

namespace ItemEditor.Tests.Services;

/// <summary>
/// Tests the wiring layer between ItemViewModel + ItemModelResolver + MdlPartComposer
/// + the live preview renderer. Uses a FakePreviewRenderer to verify what gets pushed
/// to the renderer in response to view-model property changes.
///
/// Per the plan (NonPublic/Plans/2026-04-30-1996-1908-plan.md, PR3b section), behavior to verify:
///  - ModelType 0/1/2 (Simple/Layered/Composite) → renderer.Model set, no SetArmorColors
///    (Layered uses Cloth1/2 colors via SetArmorColors, see plan)
///  - ModelType 3 (Armor) → renderer.Model set + SetArmorColors with all six colors
///  - PropertyChanged for non-tracked fields → no preview update
///  - Rapid PropertyChanged bursts collapse to a single reload after debounce
///  - HasModel=false → renderer cleared, placeholder visible
///  - Unbind() detaches the PropertyChanged handler
/// </summary>
public class ItemPreviewControllerTests
{
    // ----- Fakes -----

    private sealed class FakePreviewRenderer : IItemPreviewRenderer
    {
        public MdlModel? CurrentModel { get; private set; }
        public bool PlaceholderVisible { get; private set; }
        public int LoadCount { get; private set; }
        public int ClearCount { get; private set; }
        public (int metal1, int metal2, int cloth1, int cloth2, int leather1, int leather2)? LastArmorColors { get; private set; }
        public int ArmorColorsCallCount { get; private set; }
        public Quaternion LastModelRootOrientation { get; private set; } = Quaternion.Identity;

        public void SetModel(MdlModel model)
        {
            CurrentModel = model;
            LastModelRootOrientation = model.GeometryRoot?.Orientation ?? Quaternion.Identity;
            PlaceholderVisible = false;
            LoadCount++;
        }

        public void Clear()
        {
            CurrentModel = null;
            PlaceholderVisible = true;
            ClearCount++;
        }

        public void SetArmorColors(int metal1, int metal2, int cloth1, int cloth2, int leather1, int leather2)
        {
            LastArmorColors = (metal1, metal2, cloth1, cloth2, leather1, leather2);
            ArmorColorsCallCount++;
        }
    }

    // ----- Setup helpers -----

    private static MockGameDataService BuildGameData()
    {
        var game = new MockGameDataService(includeSampleData: false);

        var baseItems = new TwoDAFile();
        baseItems.Columns.AddRange(new[] { "Label", "ItemClass", "ModelType", "DefaultModel", "WeaponWield" });

        // Row 0: Simple weapon (longsword) — WeaponWield=**** (default melee held weapon)
        baseItems.Rows.Add(new TwoDARow { Label = "0", Values = new() { "BASE_ITEM_LONGSWORD", "w_swrd", "0", "w_swrd_001", "****" } });
        // Row 1: Layered (cloak) — WeaponWield=1 (nonweapon)
        baseItems.Rows.Add(new TwoDARow { Label = "1", Values = new() { "BASE_ITEM_CLOAK", "cloak", "1", "cloak_001", "1" } });
        // Row 2: Composite weapon (twobladed) — WeaponWield=8 (two-bladed)
        baseItems.Rows.Add(new TwoDARow { Label = "2", Values = new() { "BASE_ITEM_TWOBLADEDSWORD", "wdbsw", "2", "wdbsw_b_001", "8" } });
        // Row 3: Armor — WeaponWield=1 (nonweapon)
        baseItems.Rows.Add(new TwoDARow { Label = "3", Values = new() { "BASE_ITEM_ARMOR", "armor", "3", "****", "1" } });
        // Row 4: Helmet — ModelType=0, WeaponWield=1 (nonweapon — should NOT rotate even though ModelType matches simple weapon)
        baseItems.Rows.Add(new TwoDARow { Label = "4", Values = new() { "BASE_ITEM_HELMET", "helm", "0", "helm_001", "1" } });
        // Row 5: Bow — WeaponWield=5 (held; should rotate)
        baseItems.Rows.Add(new TwoDARow { Label = "5", Values = new() { "BASE_ITEM_LONGBOW", "w_bow", "0", "w_bow_001", "5" } });

        game.With2DA("baseitems", baseItems);
        return game;
    }

    private static MdlModel MakePartModel(string name)
    {
        var root = new MdlNode { Name = name + "_root", Orientation = Quaternion.Identity, Scale = 1.0f };
        var mesh = new MdlTrimeshNode
        {
            Name = name + "_mesh",
            Position = Vector3.Zero,
            Orientation = Quaternion.Identity,
            Scale = 1.0f,
            Vertices = new[] { new Vector3(1, 0, 0) },
            Faces = System.Array.Empty<MdlFace>(),
            Parent = root,
        };
        root.Children.Add(mesh);
        return new MdlModel { Name = name, GeometryRoot = root, IsBinary = true };
    }

    private static (FakePreviewRenderer renderer, ItemPreviewController controller, MockGameDataService game)
        BuildController(System.Action<MockGameDataService>? setupResources = null)
    {
        var game = BuildGameData();

        // Register a placeholder MDL byte for any ResRef — the loader func returns synthetic MdlModels
        setupResources?.Invoke(game);

        var baseItemSvc = new BaseItemTypeService(game);
        var resolver = new ItemModelResolver(baseItemSvc, game);
        var composer = new MdlPartComposer(game, (resRef, _) => MakePartModel(resRef));
        var renderer = new FakePreviewRenderer();

        // Test mode: pump debounce manually via FlushDebounce()
        var controller = new ItemPreviewController(resolver, composer, renderer, baseItemSvc, debounceManually: true);
        return (renderer, controller, game);
    }

    // ----- Tests -----

    [Fact]
    public void Bind_NullViewModel_ClearsPreview()
    {
        var (renderer, controller, _) = BuildController();

        controller.BindViewModel(null);
        controller.FlushDebounce();

        Assert.True(renderer.PlaceholderVisible);
        Assert.Null(renderer.CurrentModel);
    }

    [Fact]
    public void Bind_SimpleWeapon_LoadsSingleModelAndDoesNotApplyColors()
    {
        var (renderer, controller, game) = BuildController(g =>
            g.SetResource("w_swrd_005", ResourceTypes.Mdl, new byte[] { 1 }));
        var vm = new ItemViewModel(new UtiFile { BaseItem = 0, ModelPart1 = 5 });

        controller.BindViewModel(vm);
        controller.FlushDebounce();

        Assert.NotNull(renderer.CurrentModel);
        Assert.False(renderer.PlaceholderVisible);
        Assert.Equal(0, renderer.ArmorColorsCallCount);
    }

    [Fact]
    public void Bind_LayeredItem_LoadsModelAndAppliesClothColorsOnly()
    {
        var (renderer, controller, game) = BuildController(g =>
            g.SetResource("cloak_003", ResourceTypes.Mdl, new byte[] { 1 }));
        var vm = new ItemViewModel(new UtiFile
        {
            BaseItem = 1,
            ModelPart1 = 3,
            Cloth1Color = 7,
            Cloth2Color = 11,
            Metal1Color = 99,   // should be ignored for ModelType 1
            Leather1Color = 99, // should be ignored for ModelType 1
        });

        controller.BindViewModel(vm);
        controller.FlushDebounce();

        Assert.NotNull(renderer.CurrentModel);
        Assert.NotNull(renderer.LastArmorColors);
        // Cloth1/2 set; metal/leather forced to 0 to avoid recoloring non-cloth layers
        Assert.Equal(7, renderer.LastArmorColors!.Value.cloth1);
        Assert.Equal(11, renderer.LastArmorColors.Value.cloth2);
        Assert.Equal(0, renderer.LastArmorColors.Value.metal1);
        Assert.Equal(0, renderer.LastArmorColors.Value.leather1);
    }

    [Fact]
    public void Bind_CompositeWeapon_LoadsThreePartsViaComposeFlatNoColors()
    {
        var (renderer, controller, game) = BuildController(g =>
        {
            g.SetResource("wdbsw_b_011", ResourceTypes.Mdl, new byte[] { 1 });
            g.SetResource("wdbsw_m_021", ResourceTypes.Mdl, new byte[] { 1 });
            g.SetResource("wdbsw_t_031", ResourceTypes.Mdl, new byte[] { 1 });
        });
        var vm = new ItemViewModel(new UtiFile
        {
            BaseItem = 2,
            ModelPart1 = 11,
            ModelPart2 = 21,
            ModelPart3 = 31,
            Cloth1Color = 99, // should be ignored for ModelType 2
        });

        controller.BindViewModel(vm);
        controller.FlushDebounce();

        Assert.NotNull(renderer.CurrentModel);
        Assert.Equal(3, renderer.CurrentModel!.GetMeshNodes().Count());
        Assert.Equal(0, renderer.ArmorColorsCallCount);
    }

    [Fact]
    public void Bind_Armor_LoadsCompositeAndAppliesAllSixColors()
    {
        var (renderer, controller, game) = BuildController(g =>
        {
            g.SetResource("pmh0_chest005", ResourceTypes.Mdl, new byte[] { 1 });
            g.SetResource("pmh0_pelvis002", ResourceTypes.Mdl, new byte[] { 1 });
        });
        var uti = new UtiFile
        {
            BaseItem = 3,
            Cloth1Color = 1,
            Cloth2Color = 2,
            Leather1Color = 3,
            Leather2Color = 4,
            Metal1Color = 5,
            Metal2Color = 6,
        };
        uti.ArmorParts["Torso"] = 5;
        uti.ArmorParts["Pelvis"] = 2;
        var vm = new ItemViewModel(uti);

        controller.BindViewModel(vm);
        controller.FlushDebounce();

        Assert.NotNull(renderer.CurrentModel);
        Assert.NotNull(renderer.LastArmorColors);
        Assert.Equal(5, renderer.LastArmorColors!.Value.metal1);
        Assert.Equal(6, renderer.LastArmorColors.Value.metal2);
        Assert.Equal(1, renderer.LastArmorColors.Value.cloth1);
        Assert.Equal(2, renderer.LastArmorColors.Value.cloth2);
        Assert.Equal(3, renderer.LastArmorColors.Value.leather1);
        Assert.Equal(4, renderer.LastArmorColors.Value.leather2);
    }

    [Fact]
    public void ModelPartChange_TriggersReload()
    {
        var (renderer, controller, game) = BuildController(g =>
        {
            g.SetResource("w_swrd_005", ResourceTypes.Mdl, new byte[] { 1 });
            g.SetResource("w_swrd_010", ResourceTypes.Mdl, new byte[] { 1 });
        });
        var vm = new ItemViewModel(new UtiFile { BaseItem = 0, ModelPart1 = 5 });

        controller.BindViewModel(vm);
        controller.FlushDebounce();
        var initialLoads = renderer.LoadCount;

        vm.ModelPart1 = 10;
        controller.FlushDebounce();

        Assert.Equal(initialLoads + 1, renderer.LoadCount);
    }

    [Fact]
    public void RapidPropertyChanges_CollapseToSingleReload()
    {
        var (renderer, controller, game) = BuildController(g =>
        {
            g.SetResource("pmh0_chest001", ResourceTypes.Mdl, new byte[] { 1 });
        });
        var uti = new UtiFile { BaseItem = 3 };
        uti.ArmorParts["Torso"] = 1;
        var vm = new ItemViewModel(uti);

        controller.BindViewModel(vm);
        controller.FlushDebounce();
        var loadsAfterInitial = renderer.LoadCount;

        // Burst of 5 color changes — debounce should collapse them
        vm.Cloth1Color = 1;
        vm.Cloth2Color = 2;
        vm.Leather1Color = 3;
        vm.Metal1Color = 4;
        vm.Metal2Color = 5;
        controller.FlushDebounce();

        Assert.Equal(loadsAfterInitial + 1, renderer.LoadCount);
    }

    [Fact]
    public void IrrelevantPropertyChange_DoesNotTriggerReload()
    {
        var (renderer, controller, game) = BuildController(g =>
            g.SetResource("w_swrd_005", ResourceTypes.Mdl, new byte[] { 1 }));
        var vm = new ItemViewModel(new UtiFile { BaseItem = 0, ModelPart1 = 5, Tag = "old" });

        controller.BindViewModel(vm);
        controller.FlushDebounce();
        var loadsAfterInitial = renderer.LoadCount;

        vm.Tag = "new"; // Tag is not in the watched property set
        controller.FlushDebounce();

        Assert.Equal(loadsAfterInitial, renderer.LoadCount);
    }

    [Fact]
    public void HasModelFalse_ShowsPlaceholder()
    {
        var (renderer, controller, _) = BuildController();
        // No MDL resources registered → resolver returns HasModel=false
        var vm = new ItemViewModel(new UtiFile { BaseItem = 0, ModelPart1 = 5 });

        controller.BindViewModel(vm);
        controller.FlushDebounce();

        Assert.True(renderer.PlaceholderVisible);
        Assert.Null(renderer.CurrentModel);
    }

    [Fact]
    public void BaseItemOutOfRange_ShowsPlaceholderNoCrash()
    {
        var (renderer, controller, _) = BuildController();
        var vm = new ItemViewModel(new UtiFile { BaseItem = 999 });

        controller.BindViewModel(vm);
        controller.FlushDebounce();

        Assert.True(renderer.PlaceholderVisible);
    }

    [Fact]
    public void Unbind_DetachesPropertyChangedHandler()
    {
        var (renderer, controller, game) = BuildController(g =>
            g.SetResource("w_swrd_005", ResourceTypes.Mdl, new byte[] { 1 }));
        var vm = new ItemViewModel(new UtiFile { BaseItem = 0, ModelPart1 = 5 });

        controller.BindViewModel(vm);
        controller.FlushDebounce();
        var loadsAfterBind = renderer.LoadCount;

        controller.Unbind();
        vm.ModelPart1 = 10; // should NOT trigger a reload now
        controller.FlushDebounce();

        Assert.Equal(loadsAfterBind, renderer.LoadCount);
    }

    // ----- New tests for orientation + ArmorParts watching (#1908 PR3b follow-up) -----

    [Fact]
    public void ArmorPartChange_TriggersReload()
    {
        var (renderer, controller, game) = BuildController(g =>
        {
            g.SetResource("pmh0_chest005", ResourceTypes.Mdl, new byte[] { 1 });
            g.SetResource("pmh0_chest010", ResourceTypes.Mdl, new byte[] { 1 });
        });
        var uti = new UtiFile { BaseItem = 3 };
        uti.ArmorParts["Torso"] = 5;
        var vm = new ItemViewModel(uti);

        controller.BindViewModel(vm);
        controller.FlushDebounce();
        var loadsAfterInitial = renderer.LoadCount;

        // Change Torso part — fires PropertyChanged("ArmorPart_Torso")
        vm.SetArmorPart("Torso", 10);
        controller.FlushDebounce();

        Assert.Equal(loadsAfterInitial + 1, renderer.LoadCount);
    }

    [Fact]
    public void Bind_HeldWeapon_AppliesXRotationToModelRoot()
    {
        var (renderer, controller, _) = BuildController(g =>
            g.SetResource("w_swrd_001", ResourceTypes.Mdl, new byte[] { 1 }));
        // BaseItem=0 → longsword in our test 2DA, WeaponWield=**** → IsHeldWeapon=true
        var vm = new ItemViewModel(new UtiFile { BaseItem = 0, ModelPart1 = 1 });

        controller.BindViewModel(vm);
        controller.FlushDebounce();

        Assert.NotNull(renderer.CurrentModel);
        // Held weapons should be rotated 90° around X so Y-axis-up becomes Z-axis-up.
        // The exact quaternion: (sin(45°), 0, 0, cos(45°)) ≈ (0.707, 0, 0, 0.707).
        const float tol = 0.001f;
        var expected = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2f);
        Assert.Equal(expected.X, renderer.LastModelRootOrientation.X, tol);
        Assert.Equal(expected.Y, renderer.LastModelRootOrientation.Y, tol);
        Assert.Equal(expected.Z, renderer.LastModelRootOrientation.Z, tol);
        Assert.Equal(expected.W, renderer.LastModelRootOrientation.W, tol);
    }

    [Fact]
    public void Bind_CompositeWeapon_AppliesXRotationToModelRoot()
    {
        var (renderer, controller, _) = BuildController(g =>
        {
            g.SetResource("wdbsw_b_011", ResourceTypes.Mdl, new byte[] { 1 });
            g.SetResource("wdbsw_m_011", ResourceTypes.Mdl, new byte[] { 1 });
            g.SetResource("wdbsw_t_011", ResourceTypes.Mdl, new byte[] { 1 });
        });
        // BaseItem=2 → twobladed sword, WeaponWield=8 → IsHeldWeapon=true
        var vm = new ItemViewModel(new UtiFile { BaseItem = 2, ModelPart1 = 11, ModelPart2 = 11, ModelPart3 = 11 });

        controller.BindViewModel(vm);
        controller.FlushDebounce();

        Assert.NotNull(renderer.CurrentModel);
        Assert.NotEqual(Quaternion.Identity, renderer.LastModelRootOrientation);
    }

    [Fact]
    public void Bind_Helmet_DoesNotApplyRotation()
    {
        var (renderer, controller, _) = BuildController(g =>
            g.SetResource("helm_001", ResourceTypes.Mdl, new byte[] { 1 }));
        // BaseItem=4 → helmet, ModelType=0 BUT WeaponWield=1 → IsHeldWeapon=false
        var vm = new ItemViewModel(new UtiFile { BaseItem = 4, ModelPart1 = 1 });

        controller.BindViewModel(vm);
        controller.FlushDebounce();

        Assert.NotNull(renderer.CurrentModel);
        Assert.Equal(Quaternion.Identity, renderer.LastModelRootOrientation);
    }

    [Fact]
    public void Bind_Armor_DoesNotApplyRotation()
    {
        var (renderer, controller, _) = BuildController(g =>
        {
            g.SetResource("pmh0_chest005", ResourceTypes.Mdl, new byte[] { 1 });
        });
        var uti = new UtiFile { BaseItem = 3 };
        uti.ArmorParts["Torso"] = 5;
        var vm = new ItemViewModel(uti);

        controller.BindViewModel(vm);
        controller.FlushDebounce();

        Assert.NotNull(renderer.CurrentModel);
        Assert.Equal(Quaternion.Identity, renderer.LastModelRootOrientation);
    }

    [Fact]
    public void Bind_Bow_AppliesXRotation()
    {
        var (renderer, controller, _) = BuildController(g =>
            g.SetResource("w_bow_001", ResourceTypes.Mdl, new byte[] { 1 }));
        // BaseItem=5 → bow, WeaponWield=5 → IsHeldWeapon=true
        var vm = new ItemViewModel(new UtiFile { BaseItem = 5, ModelPart1 = 1 });

        controller.BindViewModel(vm);
        controller.FlushDebounce();

        Assert.NotNull(renderer.CurrentModel);
        Assert.NotEqual(Quaternion.Identity, renderer.LastModelRootOrientation);
    }
}
