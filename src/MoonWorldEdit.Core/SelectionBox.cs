using System;
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

    public bool Contains(Vector3 world)
    {
        var l = Frame.ToLocal(world);
        return l.X >= Min.X - Slack && l.X <= Max.X + Slack
            && l.Y >= Min.Y - Slack && l.Y <= Max.Y + Slack
            && l.Z >= Min.Z - Slack && l.Z <= Max.Z + Slack;
    }
}
