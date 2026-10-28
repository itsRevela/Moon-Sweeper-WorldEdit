using System;
using System.Numerics;

namespace MoonWorldEdit.Core;

/// <summary>
/// A local frame on the Moon's surface: <see cref="Up"/> points away from the Moon's centre, and <see cref="Forward"/>
/// and <see cref="Right"/> lie flat on the ground. Local coordinates are (right, up, forward), as in Unity.
/// </summary>
public readonly struct SurfaceFrame
{
    private const float Degenerate = 1e-8f;

    private SurfaceFrame(Vector3 origin, Vector3 right, Vector3 up, Vector3 forward)
    {
        Origin = origin;
        Right = right;
        Up = up;
        Forward = forward;
    }

    public Vector3 Origin { get; }
    public Vector3 Right { get; }
    public Vector3 Up { get; }
    public Vector3 Forward { get; }

    /// <summary>The frame at <paramref name="origin"/>, its forward the hint laid flat on the ground.</summary>
    public static SurfaceFrame At(Vector3 origin, Vector3 moonCentre, Vector3 forwardHint)
    {
        var radial = origin - moonCentre;
        if (!IsFinite(origin) || !IsFinite(forwardHint) || radial.LengthSquared() < Degenerate)
            throw new ArgumentException("A surface frame needs a finite point away from the Moon's centre.", nameof(origin));
        var up = Vector3.Normalize(radial);
        var forward = Flatten(forwardHint, up);
        if (forward.LengthSquared() < Degenerate)
            forward = Flatten(Vector3.UnitZ, up);
        if (forward.LengthSquared() < Degenerate)
            forward = Flatten(Vector3.UnitX, up);
        forward = Vector3.Normalize(forward);
        return new SurfaceFrame(origin, Vector3.Cross(up, forward), up, forward);
    }

    /// <summary>The frame's orientation: turns local (right, up, forward) onto <see cref="Right"/>, <see cref="Up"/>, <see cref="Forward"/>.</summary>
    public Quaternion Rotation => Quaternion.Normalize(Quaternion.CreateFromRotationMatrix(new Matrix4x4(
        Right.X, Right.Y, Right.Z, 0f,
        Up.X, Up.Y, Up.Z, 0f,
        Forward.X, Forward.Y, Forward.Z, 0f,
        0f, 0f, 0f, 1f)));

    public Vector3 ToLocal(Vector3 world)
    {
        var d = world - Origin;
        return new Vector3(Vector3.Dot(d, Right), Vector3.Dot(d, Up), Vector3.Dot(d, Forward));
    }

    public Vector3 ToWorld(Vector3 local) => Origin + local.X * Right + local.Y * Up + local.Z * Forward;

    private static Vector3 Flatten(Vector3 v, Vector3 up) => v - Vector3.Dot(v, up) * up;

    private static bool IsFinite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
}
