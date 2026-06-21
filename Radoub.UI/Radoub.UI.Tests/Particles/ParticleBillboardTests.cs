using System.Numerics;
using Radoub.UI.Particles;

namespace Radoub.UI.Tests.Particles;

public class ParticleBillboardTests
{
    private static readonly Vector3 CamRight = Vector3.UnitX;
    private static readonly Vector3 CamUp = Vector3.UnitY;

    [Fact]
    public void Billboard_UsesCameraAxes()
    {
        // Normal/Billboard emitters always face the camera: the quad basis is the camera basis,
        // regardless of the emitter's orientation. (#2434)
        var rot = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 3f); // arbitrary node tilt
        var (right, up) = ParticleBillboard.QuadBasis(
            ParticleRenderMode.Billboard, CamRight, CamUp, rot);

        Assert.Equal(CamRight, right);
        Assert.Equal(CamUp, up);
    }

    [Fact]
    public void BillboardLocalZ_UsesNodeLocalAxes_NotCamera()
    {
        // "Billboard to Local Z": particles face the way they came out, locked to the emitter's
        // local frame — NOT the camera. With identity node rotation, local X/Y are world X/Y;
        // here they happen to match the camera basis, so rotate the node to prove it ignores camera. (#2434)
        // 90° about world X maps node local +Y -> world +Z and local +Z -> world -Y. The quad's
        // up axis should follow the node (world +Z), not the camera up (world +Y).
        var rot = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI / 2f);
        var (right, up) = ParticleBillboard.QuadBasis(
            ParticleRenderMode.BillboardLocalZ, CamRight, CamUp, rot);

        // Local +X is unaffected by an X-axis rotation.
        Assert.True(Vector3.Distance(right, Vector3.UnitX) < 1e-5f, $"right={right}");
        // Local +Y rotates to world +Z.
        Assert.True(Vector3.Distance(up, Vector3.UnitZ) < 1e-5f, $"up={up}");
        // Crucially it is NOT the camera up.
        Assert.False(Vector3.Distance(up, CamUp) < 1e-5f, "local-Z quad must not use camera up");
    }

    [Fact]
    public void BillboardLocalZ_IdentityRotation_IsWorldXY()
    {
        var (right, up) = ParticleBillboard.QuadBasis(
            ParticleRenderMode.BillboardLocalZ, CamRight, CamUp, Quaternion.Identity);
        Assert.True(Vector3.Distance(right, Vector3.UnitX) < 1e-5f);
        Assert.True(Vector3.Distance(up, Vector3.UnitY) < 1e-5f);
    }

    // ---- Remaining orientation modes (#2544 item 4 / #2450) ----

    [Fact]
    public void BillboardWorldZ_LiesInWorldGroundPlane_IgnoresCamera()
    {
        // "Billboard to World Z": the quad lies parallel to the world ground plane — its up axis is
        // world +Z-perpendicular (i.e. the quad normal is world +Z). A camera tilt must not change it.
        var tiltedCamUp = Vector3.Normalize(new Vector3(0f, 1f, 1f));
        var (right, up) = ParticleBillboard.QuadBasis(
            ParticleRenderMode.BillboardWorldZ, CamRight, tiltedCamUp, Quaternion.Identity);

        // Both axes lie in the world XY plane (normal is world +Z): Z components ~0.
        Assert.True(MathF.Abs(right.Z) < 1e-5f, $"right.Z={right.Z}");
        Assert.True(MathF.Abs(up.Z) < 1e-5f, $"up.Z={up.Z}");
        // Axes are orthonormal.
        Assert.True(MathF.Abs(Vector3.Dot(right, up)) < 1e-5f, "right·up should be ~0");
        Assert.True(MathF.Abs(right.Length() - 1f) < 1e-5f);
        Assert.True(MathF.Abs(up.Length() - 1f) < 1e-5f);
    }

    [Fact]
    public void AlignedWorldZ_UpLockedToWorldZ()
    {
        // "Aligned to World Z": quad up is locked to world +Z; right is perpendicular (camera-derived).
        var (right, up) = ParticleBillboard.QuadBasis(
            ParticleRenderMode.AlignedWorldZ, CamRight, CamUp, Quaternion.Identity);

        Assert.True(Vector3.Distance(up, Vector3.UnitZ) < 1e-5f, $"up={up}");
        Assert.True(MathF.Abs(Vector3.Dot(right, up)) < 1e-5f, "right·up should be ~0");
        Assert.True(MathF.Abs(right.Length() - 1f) < 1e-5f);
    }

    [Fact]
    public void VelocityAligned_UpFollowsParticleVelocity()
    {
        // "Aligned to particle direction": the quad up axis is the particle's velocity direction.
        var velocity = new Vector3(0f, 0f, 5f); // moving straight up
        var (right, up) = ParticleBillboard.QuadBasis(
            ParticleRenderMode.VelocityAligned, CamRight, CamUp, Quaternion.Identity, velocity);

        Assert.True(Vector3.Distance(up, Vector3.UnitZ) < 1e-5f, $"up={up}");
        Assert.True(MathF.Abs(Vector3.Dot(right, up)) < 1e-5f, "right·up should be ~0");
        Assert.True(MathF.Abs(up.Length() - 1f) < 1e-5f);
    }

    [Fact]
    public void Stretched_UpAlongVelocity_ScaledBySpeed()
    {
        // Motion-blur stretch: the quad up axis points along velocity and its length grows with
        // speed, so faster particles render longer streaks.
        var slow = new Vector3(0f, 0f, 1f);
        var fast = new Vector3(0f, 0f, 4f);
        var (_, upSlow) = ParticleBillboard.QuadBasis(
            ParticleRenderMode.Stretched, CamRight, CamUp, Quaternion.Identity, slow);
        var (_, upFast) = ParticleBillboard.QuadBasis(
            ParticleRenderMode.Stretched, CamRight, CamUp, Quaternion.Identity, fast);

        // Direction is along +Z for both; faster particle's up axis is longer.
        Assert.True(upSlow.Z > 0f && upFast.Z > 0f);
        Assert.True(upFast.Length() > upSlow.Length(),
            $"expected fast stretch ({upFast.Length()}) > slow ({upSlow.Length()})");
    }

    [Fact]
    public void VelocityAligned_ZeroVelocity_FallsBackToCamera()
    {
        // A stationary particle has no direction to align to; fall back to the camera basis
        // rather than producing a degenerate (zero-length) axis.
        var (right, up) = ParticleBillboard.QuadBasis(
            ParticleRenderMode.VelocityAligned, CamRight, CamUp, Quaternion.Identity, Vector3.Zero);
        Assert.Equal(CamRight, right);
        Assert.Equal(CamUp, up);
    }

    [Theory]
    [InlineData(ParticleRenderMode.VelocityAligned, true)]
    [InlineData(ParticleRenderMode.Stretched, true)]
    [InlineData(ParticleRenderMode.Billboard, false)]
    [InlineData(ParticleRenderMode.BillboardLocalZ, false)]
    [InlineData(ParticleRenderMode.BillboardWorldZ, false)]
    [InlineData(ParticleRenderMode.AlignedWorldZ, false)]
    public void IsVelocityDependent_FlagsOnlyVelocityModes(ParticleRenderMode mode, bool expected)
    {
        // The render path resolves the quad basis per particle only for velocity-dependent modes;
        // all others can resolve it once per emitter. (#2544)
        Assert.Equal(expected, ParticleBillboard.IsVelocityDependent(mode));
    }

    [Fact]
    public void VelocityAligned_DifferentVelocities_ProduceDifferentBases()
    {
        // Two particles from the same emitter moving in different directions must orient
        // differently — proving the basis is genuinely per-particle, not per-emitter.
        var (_, upUp) = ParticleBillboard.QuadBasis(
            ParticleRenderMode.VelocityAligned, CamRight, CamUp, Quaternion.Identity, new Vector3(0, 0, 3));
        var (_, upSide) = ParticleBillboard.QuadBasis(
            ParticleRenderMode.VelocityAligned, CamRight, CamUp, Quaternion.Identity, new Vector3(3, 0, 0));

        Assert.True(Vector3.Distance(upUp, Vector3.UnitZ) < 1e-5f);
        Assert.True(Vector3.Distance(upSide, Vector3.UnitX) < 1e-5f);
        Assert.True(Vector3.Distance(upUp, upSide) > 0.5f, "per-particle bases must differ");
    }
}
