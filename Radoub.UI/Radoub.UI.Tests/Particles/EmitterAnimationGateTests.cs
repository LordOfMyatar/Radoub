using Radoub.Formats.Mdl;
using Radoub.UI.Particles;

namespace Radoub.UI.Tests.Particles;

/// <summary>
/// At-rest emitter gating (#2544 / #2439). NWN placeable destruction effects (wood debris, fire)
/// are emitters keyed to a "die" animation: their birthrate is 0 in the "default" (at-rest) pose
/// and only fires during destruction. The preview shows the placeable at rest, so those emitters
/// must be silent — the model-led signal is the emitter's birthrate in the default animation, not
/// the static header peak. Continuous emitters (brazier flame) keep a non-zero default birthrate
/// and keep rendering. Found via UAT on the plc_a02 bookshelf fixture.
/// </summary>
public class EmitterAnimationGateTests
{
    private static MdlEmitterNode Emitter(string name, float staticBirthRate) =>
        new() { Name = name, BirthRate = staticBirthRate, Update = "Explosion" };

    // Builds a model with a geometry root holding the given emitters (static header birthrates),
    // plus a "default" animation whose copy of each emitter carries the supplied at-rest birthrate.
    private static MdlModel ModelWith(
        (string name, float staticBr, float defaultBr)[] emitters,
        bool hasDestructionAnim)
    {
        var model = new MdlModel { Name = "test" };
        var geomRoot = new MdlNode { Name = "root" };
        foreach (var e in emitters)
            geomRoot.Children.Add(Emitter(e.name, e.staticBr));
        model.GeometryRoot = geomRoot;

        if (hasDestructionAnim)
            model.Animations.Add(new MdlAnimation { Name = "die", Length = 0.33f });

        // "default" animation tree: emitter copies carry the at-rest (keyed) birthrate.
        var defaultRoot = new MdlNode { Name = "root" };
        foreach (var e in emitters)
            defaultRoot.Children.Add(Emitter(e.name, e.defaultBr));
        model.Animations.Add(new MdlAnimation { Name = "default", Length = 0f, GeometryRoot = defaultRoot });

        return model;
    }

    [Fact]
    public void DestructionEmitter_SilentAtRest()
    {
        // plc_a02 bookshelf: static header birthrate 10, but default-anim birthrate 0 → silent.
        var model = ModelWith(new[] { ("fire!08", 10f, 0f) }, hasDestructionAnim: true);
        Assert.False(EmitterAnimationGate.ShouldRenderAtRest(model, "fire!08"));
    }

    [Fact]
    public void ContinuousEmitter_RendersAtRest()
    {
        // brazier flame: default-anim birthrate 20 (non-zero) → keeps streaming even though the
        // model also has a die animation (mixed model, like plc_i05).
        var model = ModelWith(new[] { ("fire!06", 20f, 20f) }, hasDestructionAnim: true);
        Assert.True(EmitterAnimationGate.ShouldRenderAtRest(model, "fire!06"));
    }

    [Fact]
    public void MixedModel_GatesEachEmitterIndependently()
    {
        // plc_i05: same model, a streaming flame (default 20) and silent debris (default 0).
        var model = ModelWith(new[]
        {
            ("fire!06", 20f, 20f),     // continuous flame
            ("ChunkyWood87", 3f, 0f),  // destruction debris
        }, hasDestructionAnim: true);

        Assert.True(EmitterAnimationGate.ShouldRenderAtRest(model, "fire!06"));
        Assert.False(EmitterAnimationGate.ShouldRenderAtRest(model, "ChunkyWood87"));
    }

    [Fact]
    public void NoDefaultAnim_RendersFromStaticHeader()
    {
        // c_fairy: a creature with no "default" animation gating its emitters — nothing turns them
        // off, so they render as before (#2395 regression guard).
        var model = new MdlModel { Name = "c_fairy" };
        var geomRoot = new MdlNode { Name = "root" };
        geomRoot.Children.Add(Emitter("fairyDust", 70f));
        model.GeometryRoot = geomRoot;
        // No "default" animation at all.

        Assert.True(EmitterAnimationGate.ShouldRenderAtRest(model, "fairyDust"));
    }

    // ---- State-aware gating for placeable preview states (#2556) ----

    // Brazier-like model: header flame 20, default 20, on 20, off 0 (the real plc_i05 shape).
    private static MdlModel BrazierModel()
    {
        var model = new MdlModel { Name = "plc_i05" };
        var root = new MdlNode { Name = "root" };
        root.Children.Add(Emitter("fire!06", 20f));
        model.GeometryRoot = root;

        void AddStateAnim(string name, float flameBr)
        {
            var r = new MdlNode { Name = "root" };
            r.Children.Add(Emitter("fire!06", flameBr));
            model.Animations.Add(new MdlAnimation { Name = name, Length = 0.033f, GeometryRoot = r });
        }
        AddStateAnim("default", 20f);
        AddStateAnim("on", 20f);
        AddStateAnim("off", 0f);
        return model;
    }

    [Fact]
    public void DeactivatedState_TurnsFlameOff()
    {
        // Selecting Deactivated (off animation) keys fire!06 birthrate to 0 → silent.
        Assert.False(EmitterAnimationGate.ShouldRenderForState(BrazierModel(), "fire!06", "off"));
    }

    [Fact]
    public void ActivatedState_KeepsFlameOn()
    {
        Assert.True(EmitterAnimationGate.ShouldRenderForState(BrazierModel(), "fire!06", "on"));
    }

    [Fact]
    public void DefaultState_FallsBackToDefaultAnim()
    {
        // Null/"default" state behaves exactly like ShouldRenderAtRest.
        Assert.True(EmitterAnimationGate.ShouldRenderForState(BrazierModel(), "fire!06", null));
        Assert.True(EmitterAnimationGate.ShouldRenderForState(BrazierModel(), "fire!06", "default"));
    }

    [Fact]
    public void StateAnimMissingEmitter_FallsBackToDefault()
    {
        // A state whose animation doesn't carry this emitter falls through to the default-anim rule.
        var model = BrazierModel();
        // "open" has no emitter copy → fall back to default (20) → render.
        model.Animations.Add(new MdlAnimation { Name = "open", Length = 0.033f, GeometryRoot = new MdlNode { Name = "root" } });
        Assert.True(EmitterAnimationGate.ShouldRenderForState(model, "fire!06", "open"));
    }
}
