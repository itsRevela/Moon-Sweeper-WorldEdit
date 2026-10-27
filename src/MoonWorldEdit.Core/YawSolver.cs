using System;
using System.Numerics;

namespace MoonWorldEdit.Core;

/// <summary>
/// Machines save only a yaw, which the game turns into a rotation with the ground's tilt (MoonWorld.DepotPose). To
/// paste a machine facing a given way, ask the game for its forward at yaw 0 and yaw 90, then solve for the yaw that
/// gives the wanted forward. This works whichever sense and order the game composes its rotations in.
/// </summary>
public static class YawSolver
{
    /// <returns>Degrees in [0, 360).</returns>
    public static float Solve(Vector3 forwardAt0, Vector3 forwardAt90, Vector3 wanted, Vector3 up)
    {
        var quarter = SignedAngle(forwardAt0, forwardAt90, up);
        if (MathF.Abs(quarter) < 1f)
            throw new ArgumentException("The forwards at yaw 0 and 90 don't differ about the up axis.", nameof(forwardAt90));
        var yaw = SignedAngle(forwardAt0, wanted, up) * 90f / quarter;
        yaw %= 360f;
        return yaw < 0f ? yaw + 360f : yaw;
    }

    /// <summary>The angle from <paramref name="from"/> to <paramref name="to"/> about <paramref name="axis"/>, in degrees.</summary>
    private static float SignedAngle(Vector3 from, Vector3 to, Vector3 axis) =>
        MathF.Atan2(Vector3.Dot(Vector3.Cross(from, to), axis), Vector3.Dot(from, to)) * 180f / MathF.PI;
}
