using System.Numerics;

namespace MoonWorldEdit.Core;

/// <summary>A position and orientation, in world space or relative to an anchor.</summary>
public readonly record struct Pose(Vector3 Position, Quaternion Rotation);
