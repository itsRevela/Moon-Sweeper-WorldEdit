using System.Numerics;
using MoonWorldEdit.Core;

namespace MoonWorldEdit.Core.Tests;

// /paste-relative (requested): the copy lands where it was relative to the player when it was copied, turned with
// the player's facing.
public sealed class RelativePasteTests
{
    private static readonly Vector3 Surface = Vector3.Normalize(new Vector3(13.5f, 1993f, -16f)) * 2000f;

    private static void Near(Vector3 expected, Vector3 actual) =>
        Assert.True(Vector3.Distance(expected, actual) < 1e-3f, $"expected {expected}, got {actual}");

    [Fact]
    public void FrameRotation_MapsLocalAxesOntoTheFrame()
    {
        var frame = SurfaceFrame.At(Surface, Vector3.Zero, new Vector3(0.3f, 0f, 1f));

        Near(frame.Forward, Vector3.Transform(Vector3.UnitZ, frame.Rotation));
        Near(frame.Up, Vector3.Transform(Vector3.UnitY, frame.Rotation));
    }

    private static Pose Player(Vector3 position, Vector3 facing)
    {
        var frame = SurfaceFrame.At(position, Vector3.Zero, facing);
        return new Pose(position, frame.Rotation);
    }

    private static Copy CopyWithPlayer(Pose anchor, Pose player) =>
        Copy.From(new[] { new WorldPiece(1, 110, anchor, true, 0, 0) }, Array.Empty<WorldBelt>(), anchor, player);

    [Fact]
    public void AnchorFor_SamePlayerPose_GivesTheOriginalAnchor()
    {
        var player = Player(Surface, Vector3.UnitZ);
        var anchor = new Pose(Surface + new Vector3(4, 0, 6), player.Rotation);

        var placed = CopyWithPlayer(anchor, player).AnchorFor(player)!.Value;

        Near(anchor.Position, placed.Position);
    }

    [Fact]
    public void AnchorFor_PlayerTurnedHalfway_PutsTheBuildBehindWhereItWas()
    {
        var player = Player(Surface, Vector3.UnitZ);
        var ahead = SurfaceFrame.At(Surface, Vector3.Zero, Vector3.UnitZ).ToWorld(new Vector3(0, 0, 10)); // 10 m in front
        var copy = CopyWithPlayer(new Pose(ahead, player.Rotation), player);

        var turned = Player(Surface, -Vector3.UnitZ);
        var placed = copy.AnchorFor(turned)!.Value;

        var behind = SurfaceFrame.At(Surface, Vector3.Zero, Vector3.UnitZ).ToWorld(new Vector3(0, 0, -10));
        Near(behind, placed.Position);
    }

    [Fact]
    public void AnchorFor_PlayerMoved_MovesTheBuildWithThem()
    {
        var player = Player(Surface, Vector3.UnitZ);
        var copy = CopyWithPlayer(new Pose(Surface + new Vector3(3, 0, 0), player.Rotation), player);
        var moved = player with { Position = player.Position + new Vector3(0, 0, 25) };

        Near(Surface + new Vector3(3, 0, 25), copy.AnchorFor(moved)!.Value.Position);
    }

    [Fact]
    public void AnchorFor_CopyWithoutPlayer_IsNull()
    {
        var anchor = new Pose(Surface, Quaternion.Identity);
        var copy = Copy.From(new[] { new WorldPiece(1, 110, anchor, true, 0, 0) }, Array.Empty<WorldBelt>(), anchor);

        Assert.Null(copy.AnchorFor(Player(Surface, Vector3.UnitZ)));
    }

    [Fact]
    public void File_KeepsThePlayerPose()
    {
        var player = Player(Surface, Vector3.UnitZ);
        var copy = CopyWithPlayer(new Pose(Surface + new Vector3(4, 0, 6), player.Rotation), player);

        var back = ClipboardFile.Read(ClipboardFile.Write(copy));

        Near(copy.AnchorFor(player)!.Value.Position, back.AnchorFor(player)!.Value.Position);
    }

    [Fact]
    public void File_Version1WithoutPlayer_StillReads()
    {
        var text = "{\"version\":1,\"pieces\":[{\"ref\":1,\"kind\":110,\"x\":0,\"y\":0,\"z\":0,\"qx\":0,\"qy\":0,\"qz\":0,\"qw\":1," +
                   "\"placedPose\":true,\"style\":0,\"support\":0}],\"belts\":[]}";

        Assert.Null(ClipboardFile.Read(text).AnchorFor(Player(Surface, Vector3.UnitZ)));
    }

    [Fact]
    public void Rotate_KeepsWhereTheAnchorIsFromThePlayer()
    {
        var player = Player(Surface, Vector3.UnitZ);
        var copy = CopyWithPlayer(new Pose(Surface + new Vector3(4, 0, 6), player.Rotation), player);

        Near(copy.AnchorFor(player)!.Value.Position, copy.Rotated(90f).AnchorFor(player)!.Value.Position);
    }

    [Fact]
    public void Parse_KnowsPasteRelative()
    {
        Assert.Equal(CommandKind.PasteRelative, Command.Parse("/paste-relative").Kind);
    }
}
