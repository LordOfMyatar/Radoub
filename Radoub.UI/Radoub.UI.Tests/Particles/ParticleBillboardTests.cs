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
}
