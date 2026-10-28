using System.Numerics;
using MoonWorldEdit.Core;

namespace MoonWorldEdit.Core.Tests;

// A piece is selected when its shape overlaps the wand's box, not just its base point: clicking on top of tall
// foundations must still take them (requested: foundations were missed).
public sealed class OverlapTests
{
    private static readonly Vector3 Surface = Vector3.Normalize(new Vector3(13.5f, 1993f, -16f)) * 2000f;
    private static readonly SurfaceFrame Frame = SurfaceFrame.At(Surface, Vector3.Zero, Vector3.UnitX);

    // The eight corners of a box in the frame, from local min to local max.
    private static Vector3[] Corners(Vector3 min, Vector3 max) =>
        (from x in new[] { min.X, max.X }
         from y in new[] { min.Y, max.Y }
         from z in new[] { min.Z, max.Z }
         select Frame.ToWorld(new Vector3(x, y, z))).ToArray();

    // The wand clicked on top of a 3 m foundation's surface: both points at height 3.
    private static SelectionBox TopClicks() =>
        SelectionBox.Between(Frame.ToWorld(new Vector3(-4, 3, -4)), Frame.ToWorld(new Vector3(4, 3, 4)), Frame, 1f);

    [Fact]
    public void Overlaps_TallFoundationClickedOnTop_IsSelected()
    {
        var foundation = Corners(new Vector3(-3, 0, -3), new Vector3(3, 3, 3)); // base well below the clicks

        Assert.True(TopClicks().Overlaps(foundation, 0.2f));
    }

    [Fact]
    public void Overlaps_PieceBesideTheBox_IsNotSelected()
    {
        var neighbour = Corners(new Vector3(5, 0, -3), new Vector3(11, 3, 3));

        Assert.False(TopClicks().Overlaps(neighbour, 0.2f));
    }

    [Fact]
    public void Overlaps_JustTouchingTheEdge_IsNotSelected()
    {
        // Shares the box's edge by 5 cm: less than the minimum depth, so a neighbour isn't swept in.
        var touching = Corners(new Vector3(3.95f, 0, -3), new Vector3(10, 3, 3));

        Assert.False(TopClicks().Overlaps(touching, 0.2f));
    }

    [Fact]
    public void Overlaps_RoofFarAboveTheClicks_IsNotSelected()
    {
        var roof = Corners(new Vector3(-3, 9, -3), new Vector3(3, 10, 3));

        Assert.False(TopClicks().Overlaps(roof, 0.2f));
    }

    [Fact]
    public void Overlaps_ThinFloorTileInside_IsSelected()
    {
        var tile = Corners(new Vector3(-1, 2.95f, -1), new Vector3(1, 3.05f, 1)); // 10 cm thick

        Assert.True(TopClicks().Overlaps(tile, 0.2f));
    }

    [Fact]
    public void Footprint_BeltAtTheBaseOfAFoundationClickedOnTop_IsInside()
    {
        Assert.True(TopClicks().ContainsFootprint(Frame.ToWorld(new Vector3(1, 0, 1)), below: 4f));
    }

    [Fact]
    public void Footprint_PointOutsideTheOutline_IsOutside()
    {
        Assert.False(TopClicks().ContainsFootprint(Frame.ToWorld(new Vector3(6, 3, 1)), below: 4f));
    }

    [Fact]
    public void Footprint_PointFarBelow_IsOutside()
    {
        Assert.False(TopClicks().ContainsFootprint(Frame.ToWorld(new Vector3(1, -3, 1)), below: 4f));
    }

    [Fact]
    public void Overlaps_NoCorners_Throws()
    {
        Assert.Throws<ArgumentException>(() => TopClicks().Overlaps(Array.Empty<Vector3>(), 0.2f));
    }
}
