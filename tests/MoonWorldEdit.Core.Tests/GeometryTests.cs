using System.Numerics;
using MoonWorldEdit.Core;

namespace MoonWorldEdit.Core.Tests;

// The Moon is a sphere centred on the world origin (about 2,000 m across the lounge tables), so "up" is radial and
// selections, copies and pastes work in a local frame on its surface.
public sealed class GeometryTests
{
    private const float Radius = 2000f;
    private static readonly Vector3 Centre = Vector3.Zero;
    private static readonly Vector3 Surface = Vector3.Normalize(new Vector3(13.5f, 1993f, -16f)) * Radius;

    private static void Near(Vector3 expected, Vector3 actual, float tolerance = 1e-3f) =>
        Assert.True(Vector3.Distance(expected, actual) < tolerance, $"expected {expected}, got {actual}");

    private static void SameRotation(Quaternion expected, Quaternion actual) =>
        Assert.True(MathF.Abs(Quaternion.Dot(expected, actual)) > 0.99999f, $"expected {expected}, got {actual}");

    [Fact]
    public void Frame_UpPointsAwayFromTheMoonsCentre()
    {
        var frame = SurfaceFrame.At(Surface, Centre, Vector3.UnitZ);

        Near(Vector3.Normalize(Surface), frame.Up);
    }

    [Fact]
    public void Frame_AxesAreOrthonormal()
    {
        var f = SurfaceFrame.At(Surface, Centre, new Vector3(0.3f, 0.2f, 1f));

        Assert.Equal(0f, Vector3.Dot(f.Right, f.Up), 4);
        Assert.Equal(0f, Vector3.Dot(f.Up, f.Forward), 4);
        Assert.Equal(0f, Vector3.Dot(f.Forward, f.Right), 4);
        Assert.Equal(1f, f.Right.Length() * f.Up.Length() * f.Forward.Length(), 4);
    }

    [Fact]
    public void Frame_ForwardIsTheHintFlattenedOntoTheGround()
    {
        var hint = new Vector3(0.1f, 5f, 1f); // mostly up, a little forward
        var f = SurfaceFrame.At(Surface, Centre, hint);

        var flattened = Vector3.Normalize(hint - Vector3.Dot(hint, f.Up) * f.Up);
        Near(flattened, f.Forward);
    }

    [Fact]
    public void Frame_HintStraightUp_StillGivesAValidFrame()
    {
        var f = SurfaceFrame.At(Surface, Centre, Vector3.Normalize(Surface));

        Assert.Equal(1f, f.Forward.Length(), 4);
        Assert.Equal(0f, Vector3.Dot(f.Forward, f.Up), 4);
    }

    [Fact]
    public void Frame_LocalAndWorldRoundTrip()
    {
        var f = SurfaceFrame.At(Surface, Centre, Vector3.UnitX);
        var world = Surface + new Vector3(3.2f, -1.5f, 7.75f);

        Near(world, f.ToWorld(f.ToLocal(world)));
    }

    [Fact]
    public void Frame_LocalAxesMapOntoTheFrame()
    {
        var f = SurfaceFrame.At(Surface, Centre, Vector3.UnitX);

        Near(f.Origin + 2 * f.Right + 3 * f.Up + 4 * f.Forward, f.ToWorld(new Vector3(2, 3, 4)));
    }

    private static (Vector3 A, Vector3 B, SurfaceFrame Frame) Corners()
    {
        var frame = SurfaceFrame.At(Surface, Centre, Vector3.UnitX);
        return (frame.ToWorld(new Vector3(-4, 0, -6)), frame.ToWorld(new Vector3(5, 0.5f, 3)), frame);
    }

    [Fact]
    public void Box_ContainsPointsBetweenTheCorners()
    {
        var (a, b, frame) = Corners();
        var box = SelectionBox.Between(a, b, frame, verticalMargin: 1f);

        Assert.True(box.Contains(frame.ToWorld(new Vector3(0, 0.2f, 0))));
    }

    [Theory]
    [InlineData(6f, 0f, 0f)] // past B sideways
    [InlineData(0f, 0f, -7f)] // before A
    [InlineData(0f, 3f, 0f)] // above the margin
    [InlineData(0f, -2f, 0f)] // below the margin
    public void Box_ExcludesPointsOutside(float x, float y, float z)
    {
        var (a, b, frame) = Corners();
        var box = SelectionBox.Between(a, b, frame, verticalMargin: 1f);

        Assert.False(box.Contains(frame.ToWorld(new Vector3(x, y, z))));
    }

    [Fact]
    public void Box_CornerOrderDoesNotMatter()
    {
        var (a, b, frame) = Corners();
        var probe = frame.ToWorld(new Vector3(1, 0.3f, 1));

        Assert.Equal(SelectionBox.Between(a, b, frame, 1f).Contains(probe), SelectionBox.Between(b, a, frame, 1f).Contains(probe));
    }

    [Fact]
    public void Box_SameCornerTwice_ContainsOnlyThatSpot()
    {
        var frame = SurfaceFrame.At(Surface, Centre, Vector3.UnitX);
        var box = SelectionBox.Between(Surface, Surface, frame, verticalMargin: 0.5f);

        Assert.True(box.Contains(Surface));
        Assert.False(box.Contains(frame.ToWorld(new Vector3(0.6f, 0, 0))));
    }

    [Fact]
    public void Box_NegativeMargin_Throws()
    {
        var frame = SurfaceFrame.At(Surface, Centre, Vector3.UnitX);

        Assert.Throws<ArgumentOutOfRangeException>(() => SelectionBox.Between(Surface, Surface, frame, -1f));
    }

    private static Pose Anchor() =>
        new(Surface, Quaternion.CreateFromAxisAngle(Vector3.Normalize(Surface), 0.4f));

    [Fact]
    public void Relative_RoundTripsAtTheSameAnchor()
    {
        var anchor = Anchor();
        var piece = new Pose(Surface + new Vector3(2, 1, -3), Quaternion.CreateFromYawPitchRoll(0.3f, 0.1f, -0.2f));

        var placed = Layout.Place(anchor, Layout.Relative(anchor, piece));

        Near(piece.Position, placed.Position);
        SameRotation(piece.Rotation, placed.Rotation);
    }

    [Fact]
    public void Relative_MovingTheAnchorMovesThePieceRigidly()
    {
        var anchor = Anchor();
        var piece = new Pose(Surface + new Vector3(2, 1, -3), Quaternion.Identity);
        var shift = new Vector3(10, 0, 4);

        var placed = Layout.Place(anchor with { Position = anchor.Position + shift }, Layout.Relative(anchor, piece));

        Near(piece.Position + shift, placed.Position);
    }

    [Fact]
    public void Rotate_TurnsTheLayoutAboutTheAnchorsUp()
    {
        var relative = new Pose(new Vector3(1, 0, 0), Quaternion.Identity);

        var turned = Layout.Rotate(relative, 90f);

        var expected = Vector3.Transform(new Vector3(1, 0, 0), Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2));
        Near(expected, turned.Position);
    }

    [Fact]
    public void Rotate_TurnsEachPieceToo()
    {
        var turned = Layout.Rotate(new Pose(Vector3.Zero, Quaternion.Identity), 90f);

        SameRotation(Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2), turned.Rotation);
    }

    [Fact]
    public void Rotate_FullTurn_ChangesNothing()
    {
        var relative = new Pose(new Vector3(2, 0.5f, -1), Quaternion.CreateFromYawPitchRoll(0.7f, 0, 0));

        var turned = Layout.Rotate(Layout.Rotate(Layout.Rotate(Layout.Rotate(relative, 90), 90), 90), 90);

        Near(relative.Position, turned.Position);
        SameRotation(relative.Rotation, turned.Rotation);
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void Rotate_NonFiniteAngle_Throws(float degrees)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Layout.Rotate(new Pose(Vector3.Zero, Quaternion.Identity), degrees));
    }
}
