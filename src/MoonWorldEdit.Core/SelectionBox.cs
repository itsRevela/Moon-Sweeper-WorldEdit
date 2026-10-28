using System;
using System.Collections.Generic;
using System.Numerics;

namespace MoonWorldEdit.Core;

/// <summary>
/// The wand's selection: the box between corners A and B in a surface frame, stretched up and down by a margin so
/// pieces standing on the ground between two clicked points are inside.
/// </summary>
public sealed class SelectionBox
{
    private const float Slack = 0.01f; // metres: a point exactly on a face counts as inside

    private SelectionBox(SurfaceFrame frame, Vector3 min, Vector3 max)
    {
        Frame = frame;
        Min = min;
        Max = max;
    }

    public SurfaceFrame Frame { get; }
    public Vector3 Min { get; }
    public Vector3 Max { get; }

    public static SelectionBox Between(Vector3 a, Vector3 b, SurfaceFrame frame, float verticalMargin)
    {
        if (!float.IsFinite(verticalMargin) || verticalMargin < 0f)
            throw new ArgumentOutOfRangeException(nameof(verticalMargin), verticalMargin, "The margin must be zero or more.");
        var la = frame.ToLocal(a);
        var lb = frame.ToLocal(b);
        var margin = new Vector3(0f, verticalMargin, 0f);
        return new SelectionBox(frame, Vector3.Min(la, lb) - margin, Vector3.Max(la, lb) + margin);
    }

    /// <summary>
    /// Whether a shape, given by its world-space corners (e.g. its bounding box), overlaps the box by at least
    /// <paramref name="minDepth"/> metres on every axis, so neighbours that merely touch an edge stay out.
    /// </summary>
    public bool Overlaps(IEnumerable<Vector3> worldCorners, float minDepth)
    {
        if (worldCorners == null)
            throw new ArgumentNullException(nameof(worldCorners));
        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        var any = false;
        foreach (var corner in worldCorners)
        {
            var l = Frame.ToLocal(corner);
            min = Vector3.Min(min, l);
            max = Vector3.Max(max, l);
            any = true;
        }
        if (!any)
            throw new ArgumentException("A shape needs at least one corner.", nameof(worldCorners));
        // A thin piece (a floor tile) only needs to be half inside on its thin axis.
        var needed = Vector3.Min(new Vector3(minDepth), (max - min) * 0.5f);
        var depth = Vector3.Min(max, Max) - Vector3.Max(min, Min);
        return depth.X >= needed.X && depth.Y >= needed.Y && depth.Z >= needed.Z;
    }

    /// <summary>
    /// Whether a point is inside the box's outline on the ground, from <paramref name="below"/> metres under the box
    /// to its top: belts lie at the base of what the player clicked on top of.
    /// </summary>
    public bool ContainsFootprint(Vector3 world, float below)
    {
        if (!float.IsFinite(below) || below < 0f)
            throw new ArgumentOutOfRangeException(nameof(below), below, "The depth below must be zero or more.");
        var l = Frame.ToLocal(world);
        return l.X >= Min.X - Slack && l.X <= Max.X + Slack
            && l.Z >= Min.Z - Slack && l.Z <= Max.Z + Slack
            && l.Y >= Min.Y - below - Slack && l.Y <= Max.Y + Slack;
    }

    public bool Contains(Vector3 world)
    {
        var l = Frame.ToLocal(world);
        return l.X >= Min.X - Slack && l.X <= Max.X + Slack
            && l.Y >= Min.Y - Slack && l.Y <= Max.Y + Slack
            && l.Z >= Min.Z - Slack && l.Z <= Max.Z + Slack;
    }
}
