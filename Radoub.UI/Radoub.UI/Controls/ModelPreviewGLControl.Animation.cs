using System;
using System.Collections.Generic;
using Radoub.UI.Services;
using Radoub.Formats.Logging;
using Radoub.Formats.Mdl;

namespace Radoub.UI.Controls;

/// <summary>
/// ModelPreviewGLControl partial: skeletal animation playback (playhead, timer-driven advance) and
/// the per-frame pose sampling used by both the mesh and particle paths. Split from the monolithic
/// control (#2127); no behavior change. Shared animation state lives in the main partial.
/// </summary>
public partial class ModelPreviewGLControl
{
    // ----- Animation playback (#2124) -----

    /// <summary>
    /// Currently active animation, or null if none selected.
    /// </summary>
    public MdlAnimation? ActiveAnimation => _activeAnimation;

    /// <summary>
    /// Current playhead time in seconds (0..Animation.Length).
    /// </summary>
    public float AnimationTime
    {
        get => _animTime;
        set
        {
            _animTime = value;
            _cachedPose = null;
            _needsMeshUpdate = true;
            RequestNextFrameRendering();
        }
    }

    /// <summary>
    /// True when animation is auto-advancing. Toggle with Play/Pause.
    /// </summary>
    public bool IsAnimationPlaying => _animPlaying;

    /// <summary>
    /// Playback speed multiplier (1.0 = real-time).
    /// </summary>
    public float AnimationSpeed
    {
        get => _animSpeed;
        set => _animSpeed = Math.Clamp(value, 0.1f, 5f);
    }

    /// <summary>
    /// Select an animation by reference (null to clear). Resets playhead to 0.
    /// </summary>
    public void SetActiveAnimation(MdlAnimation? animation)
    {
        _activeAnimation = animation;
        _animTime = 0f;
        _cachedPose = null;
        _needsMeshUpdate = true;
        RequestNextFrameRendering();
    }

    public void PlayAnimation()
    {
        if (_activeAnimation == null) return;
        _animPlaying = true;
        _animLastTick = DateTime.UtcNow;
        EnsureAnimTimer();
    }

    public void PauseAnimation()
    {
        _animPlaying = false;
    }

    private void EnsureAnimTimer()
    {
        if (_animTimer != null) return;
        _animTimer = new Avalonia.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(33), // ~30 fps
        };
        _animTimer.Tick += OnAnimTick;
        _animTimer.Start();
    }

    private void OnAnimTick(object? sender, EventArgs e)
    {
        // Particle simulation runs BEFORE the skeletal-animation early-return (#2395)
        // so emitters animate even when no animation is selected/playing (the common
        // case for a static blueprint preview). Never let a sim exception escape into
        // the timer/render loop (CLAUDE.md) — log WARN and continue.
        if (_particleSystems.Count > 0)
        {
            try
            {
                UpdateParticleSystems();
            }
            catch (Exception ex)
            {
                UnifiedLogger.LogApplication(LogLevel.WARN,
                    $"[Particle] tick update failed (non-fatal): {ex.GetType().Name}: {ex.Message}");
            }
        }

        if (!_animPlaying || _activeAnimation == null) return;

        var now = DateTime.UtcNow;
        var dt = (float)(now - _animLastTick).TotalSeconds * _animSpeed;
        _animLastTick = now;

        float length = _activeAnimation.Length;
        if (length <= 0)
        {
            _animTime = 0f;
        }
        else
        {
            _animTime += dt;
            if (_animTime >= length)
                _animTime -= length * MathF.Floor(_animTime / length); // loop
        }

        _cachedPose = null;
        _needsMeshUpdate = true;
        RequestNextFrameRendering();
    }

    /// <summary>
    /// Build a node-name → sampled-pose dictionary from the active animation
    /// at the current <see cref="AnimationTime"/>. Cached until playhead moves
    /// or the animation changes.
    /// </summary>
    private Dictionary<string, ModelViewController.NodePose>? GetCurrentPose()
    {
        if (_activeAnimation?.GeometryRoot == null) return null;
        if (_cachedPose != null) return _cachedPose;

        var pose = new Dictionary<string, ModelViewController.NodePose>(StringComparer.OrdinalIgnoreCase);
        BuildPoseRecursive(_activeAnimation.GeometryRoot, _animTime, pose);
        _cachedPose = pose;
        return pose;
    }

    private static void BuildPoseRecursive(MdlNode animNode, float t,
        Dictionary<string, ModelViewController.NodePose> pose)
    {
        bool hasPos = animNode.PositionTimes.Length > 1;
        bool hasOri = animNode.OrientationTimes.Length > 1;
        bool hasScl = animNode.ScaleTimes.Length > 1;

        if (hasPos || hasOri || hasScl)
        {
            var p = new ModelViewController.NodePose(
                hasPos, hasPos ? MdlAnimationEvaluator.EvaluatePosition(animNode, t) : animNode.Position,
                hasOri, hasOri ? MdlAnimationEvaluator.EvaluateOrientation(animNode, t) : animNode.Orientation,
                hasScl, hasScl ? MdlAnimationEvaluator.EvaluateScale(animNode, t) : animNode.Scale);
            if (!string.IsNullOrEmpty(animNode.Name))
                pose[animNode.Name] = p;
        }

        foreach (var child in animNode.Children)
            BuildPoseRecursive(child, t, pose);
    }
}
