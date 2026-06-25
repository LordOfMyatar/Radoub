using System;
using System.Collections.Generic;
using System.Numerics;
using Radoub.UI.Services;
using Radoub.Formats.Logging;
using Radoub.Formats.Mdl;

namespace Radoub.UI.Controls;

/// <summary>
/// ModelPreviewGLControl partial: emitter compilation and the particle-system lifecycle (build,
/// at-rest gating, per-tick simulation, world-transform sampling). The GL upload/draw for particles
/// lives in <c>ModelPreviewGLControl.Particles.cs</c>. Split from the monolithic control (#2127);
/// no behavior change.
/// </summary>
public partial class ModelPreviewGLControl
{
    /// <summary>
    /// (Re)build the per-emitter <see cref="Radoub.UI.Particles.ParticleSystem"/> list for a
    /// newly-assigned model (#2395). Clears any existing systems, then compiles one system per
    /// <see cref="MdlEmitterNode"/>. Each system gets a stable, distinct seed (index+1) so
    /// identical emitters don't move in lockstep. If any emitter exists, resets the particle
    /// clock and ensures the animation timer runs so the sim advances even with no skeletal
    /// animation selected.
    /// </summary>
    private void RebuildParticleSystems(MdlModel? model)
    {
        _particleSystems.Clear();

        if (model == null || !model.HasEmitterNodes())
            return;

        int index = 0;
        foreach (var node in model.EnumerateAllNodes())
        {
            if (node is not MdlEmitterNode emitter) continue;
            try
            {
                var compiled = Radoub.UI.Particles.EmitterCompiler.Compile(emitter);

                // At-rest gate (the #2439 root): placeable destruction effects (wood debris, fire)
                // are emitters keyed to a "die" animation — their birthrate is 0 in the default
                // pose and they only fire when destroyed. The preview shows the placeable at rest,
                // so those emitters must be silent. The model leads: the signal is the emitter's
                // birthrate in the "default" animation, not the static header peak. Continuous
                // emitters (brazier flame, creature dust) keep a non-zero default birthrate and
                // render. Full animation-driven emission (firing during a played "die") is a
                // follow-up. (#2544 / #2439)
                if (!Radoub.UI.Particles.EmitterAnimationGate.ShouldRenderAtRest(model, emitter.Name))
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"[Particle] Emitter '{emitter.Name}' is gated to a non-default animation (silent at rest) — not rendered. (#2544)");
                    continue;
                }

                // Secondary, model-led cull: an emitter authored with no effective output (birthrate
                // ≤ 0 or lifespan ≤ 0) produces nothing — skip building a system for it. (#2544)
                if (emitter.BirthRate <= 0f || emitter.LifeExp <= 0f)
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"[Particle] Emitter '{emitter.Name}' has no effective output (birthRate={emitter.BirthRate}, lifeExp={emitter.LifeExp}) — not rendered. (#2544)");
                    continue;
                }

                // Render modes with no faithful implementation (Beam/Mesh/LinkedChain) still fall
                // back to a camera-facing billboard — log once per model so the limitation stays visible.
                bool unsupportedRender = compiled.RenderMode == Radoub.UI.Particles.ParticleRenderMode.Beam
                    || compiled.RenderMode == Radoub.UI.Particles.ParticleRenderMode.Mesh
                    || compiled.RenderMode == Radoub.UI.Particles.ParticleRenderMode.LinkedChain;
                if (unsupportedRender)
                {
                    UnifiedLogger.LogApplication(LogLevel.INFO,
                        $"[Particle] Emitter '{emitter.Name}' render mode {compiled.RenderMode} not faithfully supported — rendered as a camera-facing billboard (approximate). (#2544)");
                }

                var system = new Radoub.UI.Particles.ParticleSystem(compiled, (uint)(index + 1));
                // Pre-warm to steady-state so the emitter looks already-running on first frame
                // (Aurora pre-warms; otherwise particles fill in slowly from empty). (#2395)
                system.PreWarm(GetEmitterWorldPosition(emitter), GetEmitterWorldRotation(emitter));
                _particleSystems.Add((emitter, compiled, system));
                index++;
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"[Particle] failed to compile emitter '{emitter.Name}' (skipped): {ex.GetType().Name}: {ex.Message}");
            }
        }

        if (_particleSystems.Count > 0)
        {
            _particleLastTick = DateTime.UtcNow;
            EnsureAnimTimer();
            UnifiedLogger.LogApplication(LogLevel.INFO,
                $"[Particle] built {_particleSystems.Count} particle system(s) for model '{model.Name}'");
        }
    }

    /// <summary>
    /// True if the active model has any emitter node that is animated by some animation (i.e. an
    /// ancestor or the emitter itself has position/orientation keyframes). Tools use this to decide
    /// whether to default the preview to a playing idle animation so animated emitters (e.g. the
    /// fairy's flapping wing Dummies) sweep correctly instead of rendering as a static orb. (#2434)
    /// </summary>
    public bool HasAnimatedEmitters()
    {
        if (_model == null || _model.Animations.Count == 0) return false;
        if (!_model.HasEmitterNodes()) return false;

        // Collect the geometry-tree ancestor names of every emitter.
        var emitterChainNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var node in _model.EnumerateAllNodes())
        {
            if (node is not MdlEmitterNode) continue;
            for (var cur = (MdlNode?)node; cur != null; cur = cur.Parent)
                if (!string.IsNullOrEmpty(cur.Name)) emitterChainNames.Add(cur.Name);
        }

        // An emitter is animated if any animation keyframes a node in its chain.
        foreach (var anim in _model.Animations)
        {
            if (anim.GeometryRoot == null) continue;
            if (AnimTreeKeyframesAny(anim.GeometryRoot, emitterChainNames)) return true;
        }
        return false;
    }

    private static bool AnimTreeKeyframesAny(MdlNode animNode, HashSet<string> names)
    {
        bool keyed = animNode.PositionTimes.Length > 1 || animNode.OrientationTimes.Length > 1
            || animNode.ScaleTimes.Length > 1;
        if (keyed && !string.IsNullOrEmpty(animNode.Name) && names.Contains(animNode.Name))
            return true;
        foreach (var child in animNode.Children)
            if (AnimTreeKeyframesAny(child, names)) return true;
        return false;
    }

    /// <summary>
    /// Advance every particle system by the wall-clock delta since the last particle tick (#2395).
    /// ParticleSystem already guards/clamps dt internally, so no extra clamp here. Throttled
    /// DEBUG log (~1s) reports system count + total live particles for the UAT log-smoke check.
    /// </summary>
    private void UpdateParticleSystems()
    {
        var now = DateTime.UtcNow;
        float pdt = (float)(now - _particleLastTick).TotalSeconds;
        _particleLastTick = now;

        // Sample the active animation's pose so emitter nodes follow animated parents (e.g. the
        // fairy's wing Dummies flap during idle — without this the wing emitter sits at its static
        // bind pose and its particles stack into an orb instead of sweeping into a wing blade). Null
        // pose (no animation playing) falls back to the static bind transform. (#2434)
        var pose = GetCurrentPose();
        foreach (var (node, _, system) in _particleSystems)
        {
            var worldPos = GetEmitterWorldPosition(node, pose);
            var worldRot = GetEmitterWorldRotation(node, pose);
            system.Update(pdt, worldPos, worldRot);
        }

        RequestNextFrameRendering();

        // Throttled diagnostic (~1s) so UAT can confirm particles are alive without log spam.
        if ((now - _lastParticleLogTime).TotalSeconds >= 1.0)
        {
            _lastParticleLogTime = now;
            int liveTotal = 0;
            foreach (var (_, _, system) in _particleSystems)
                liveTotal += system.LiveCount;
            UnifiedLogger.LogApplication(LogLevel.DEBUG,
                $"[Particle] systems={_particleSystems.Count} liveTotal={liveTotal}");
        }
    }

    /// <summary>
    /// World-space spawn origin for an emitter node (#2395). Reuses the same node-hierarchy
    /// world-transform plumbing the mesh path uses (<see cref="ModelViewController.GetWorldTransform(MdlNode?)"/>,
    /// which composes Position/Orientation/Scale up the Parent chain), transforming the local
    /// origin to world space. NOTE: the mesh buffer additionally pre-centers vertices by the
    /// model's geometric center (UpdateMeshBuffersCore Pass 2); particle positions are NOT yet
    /// centered to match, so the sim runs in raw model space. This is fine for sim-only wiring —
    /// the next (render) task will reconcile particle coords with the centered mesh frame.
    /// </summary>
    private static Vector3 GetEmitterWorldPosition(MdlEmitterNode node,
        IReadOnlyDictionary<string, ModelViewController.NodePose>? pose = null)
    {
        var world = ModelViewController.GetWorldTransform(node, pose);
        return ModelViewController.TransformPosition(Vector3.Zero, world);
    }

    /// <summary>
    /// World-space rotation for an emitter node (#2395). Mirrors <see cref="GetEmitterWorldPosition"/>:
    /// composes the node's world transform up the Parent chain, then extracts the rotation as a
    /// Quaternion. Decomposes (not raw <c>CreateFromRotationMatrix</c>) so any scale in the
    /// transform doesn't corrupt the rotation; falls back to identity if decomposition fails.
    /// </summary>
    private static Quaternion GetEmitterWorldRotation(MdlEmitterNode node,
        IReadOnlyDictionary<string, ModelViewController.NodePose>? pose = null)
    {
        var world = ModelViewController.GetWorldTransform(node, pose);
        return Matrix4x4.Decompose(world, out _, out var rotation, out _)
            ? rotation
            : Quaternion.Identity;
    }
}
