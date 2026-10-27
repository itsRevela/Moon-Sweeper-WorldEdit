using System;
using System.Numerics;

namespace MoonWorldEdit.Core;

/// <summary>
/// Poses relative to an anchor: what /copy records and /paste places. Relative coordinates are in the anchor's frame,
/// with Y its up, so /rotate turns the whole layout about the anchor's up.
/// </summary>
public static class Layout
{
    public static Pose Relative(Pose anchor, Pose piece)
    {
        var inverse = Quaternion.Inverse(anchor.Rotation);
        return new Pose(Vector3.Transform(piece.Position - anchor.Position, inverse),
            Quaternion.Normalize(inverse * piece.Rotation));
    }

    public static Pose Place(Pose anchor, Pose relative) =>
        new(anchor.Position + Vector3.Transform(relative.Position, anchor.Rotation),
            Quaternion.Normalize(anchor.Rotation * relative.Rotation));

    /// <summary>Turns a relative pose about the anchor's up (Unity's sense: positive is clockwise seen from above).</summary>
    public static Pose Rotate(Pose relative, float degrees)
    {
        if (!float.IsFinite(degrees))
            throw new ArgumentOutOfRangeException(nameof(degrees), degrees, "The angle must be a finite number of degrees.");
        var turn = Quaternion.CreateFromAxisAngle(Vector3.UnitY, degrees * MathF.PI / 180f);
        return new Pose(Vector3.Transform(relative.Position, turn), Quaternion.Normalize(turn * relative.Rotation));
    }
}
