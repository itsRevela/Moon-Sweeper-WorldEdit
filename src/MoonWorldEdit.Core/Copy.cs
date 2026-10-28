using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace MoonWorldEdit.Core;

/// <summary>A placed structure as the world has it: id, kind, world pose, style and the id it stands on (0: none).</summary>
/// <param name="PlacedPose">The game keeps a full rotation for structural pieces and only a yaw for machines.</param>
public sealed record WorldPiece(int Id, int Kind, Pose Pose, bool PlacedPose, int Style, int Support);

/// <summary>A belt: its two ends and the directions it leaves and arrives in (world space, or relative in a copy).</summary>
public sealed record WorldBelt(Vector3 Start, Vector3 StartDirection, Vector3 End, Vector3 EndDirection);

/// <summary>A copied piece, relative to the copy's anchor. <see cref="SupportRef"/> is another piece's Ref, or 0.</summary>
public sealed record CopiedPiece(int Ref, int Kind, Pose Relative, bool PlacedPose, int Style, int SupportRef);

/// <summary>What /copy took: pieces and belts relative to an anchor whose Y is up.</summary>
public sealed class Copy
{
    /// <param name="fromPlayer">The anchor relative to the player when copied (for /paste-relative), if known.</param>
    public Copy(IReadOnlyList<CopiedPiece> pieces, IReadOnlyList<WorldBelt> belts, Pose? fromPlayer = null)
    {
        Pieces = pieces ?? throw new ArgumentNullException(nameof(pieces));
        Belts = belts ?? throw new ArgumentNullException(nameof(belts));
        FromPlayer = fromPlayer;
    }

    /// <summary>The anchor relative to the player's pose when the copy was made; null for copies without one.</summary>
    public Pose? FromPlayer { get; }

    /// <summary>
    /// Where /paste-relative puts the anchor for a player at <paramref name="player"/>: the same place relative to them,
    /// turned with their facing. Null when the copy has no player pose.
    /// </summary>
    public Pose? AnchorFor(Pose player) => FromPlayer is { } relative ? Layout.Place(player, relative) : null;

    public IReadOnlyList<CopiedPiece> Pieces { get; }
    public IReadOnlyList<WorldBelt> Belts { get; }
    public bool IsEmpty => Pieces.Count == 0 && Belts.Count == 0;

    /// <param name="player">The player's pose (position, facing frame) when copying, for /paste-relative.</param>
    public static Copy From(IEnumerable<WorldPiece> pieces, IEnumerable<WorldBelt> belts, Pose anchor, Pose? player = null)
    {
        var list = pieces.ToList();
        var ids = list.Select(p => p.Id).ToHashSet();
        var copied = list.Select(p => new CopiedPiece(p.Id, p.Kind, Layout.Relative(anchor, p.Pose), p.PlacedPose, p.Style,
            ids.Contains(p.Support) ? p.Support : 0)).ToList();
        var inverse = Quaternion.Inverse(anchor.Rotation);
        var relativeBelts = belts.Select(b => new WorldBelt(
            Vector3.Transform(b.Start - anchor.Position, inverse), Vector3.Transform(b.StartDirection, inverse),
            Vector3.Transform(b.End - anchor.Position, inverse), Vector3.Transform(b.EndDirection, inverse))).ToList();
        return new Copy(copied, relativeBelts, player is { } p ? Layout.Relative(p, anchor) : null);
    }

    /// <summary>The copy turned about the anchor's up (positive: clockwise seen from above, as in Unity).</summary>
    public Copy Rotated(float degrees)
    {
        var turn = Layout.Rotate(new Pose(Vector3.Zero, Quaternion.Identity), degrees).Rotation;
        return new Copy(
            Pieces.Select(p => p with { Relative = Layout.Rotate(p.Relative, degrees) }).ToList(),
            Belts.Select(b => new WorldBelt(Vector3.Transform(b.Start, turn), Vector3.Transform(b.StartDirection, turn),
                Vector3.Transform(b.End, turn), Vector3.Transform(b.EndDirection, turn))).ToList(),
            FromPlayer); // the anchor stays where it is; the layout turns about it
    }
}

/// <summary>Which pieces and belts the wand's box takes.</summary>
public static class Selection
{
    public const float MinOverlap = 0.2f; // metres a piece must reach into the box (neighbours that touch stay out)
    public const float BeltDepthBelow = 4f; // metres under the box where belts still count

    /// <summary>Pieces whose base point is inside the box (pieces without a known shape).</summary>
    public static IEnumerable<WorldPiece> Pieces(IEnumerable<WorldPiece> pieces, SelectionBox box) =>
        pieces.Where(p => box.Contains(p.Pose.Position));

    /// <summary>Pieces whose shape (world-space corners, by id) overlaps the box; base point when the shape is unknown.</summary>
    public static IEnumerable<WorldPiece> Pieces(IEnumerable<WorldPiece> pieces, SelectionBox box,
        IReadOnlyDictionary<int, Vector3[]> shapes) =>
        pieces.Where(p => shapes.TryGetValue(p.Id, out var corners) && corners.Length > 0
            ? box.Overlaps(corners, MinOverlap)
            : box.Contains(p.Pose.Position));

    /// <summary>Belts with both ends inside the box's outline, down to <see cref="BeltDepthBelow"/> under it.</summary>
    public static IEnumerable<WorldBelt> Belts(IEnumerable<WorldBelt> belts, SelectionBox box) =>
        belts.Where(b => box.ContainsFootprint(b.Start, BeltDepthBelow) && box.ContainsFootprint(b.End, BeltDepthBelow));
}
