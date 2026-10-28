"""One-off patch: /paste-relative and a shared Place step (applied 2026-10-27)."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def edit(path, pairs):
    p = ROOT / path
    s = p.read_text(encoding="utf-8")
    for old, new in pairs:
        assert s.count(old) == 1, (path, old[:60])
        s = s.replace(old, new)
    p.write_text(s, encoding="utf-8")


edit("src/MoonWorldEdit.Plugin/GameWorld.cs", [(
    """    /// <summary>What the player is looking at""",
    """    /// <summary>The player's pose: feet position, facing laid flat on the ground (radial up), as /paste-relative uses.</summary>
    public Pose? PlayerPose()
    {
        var walker = Builder.walker;
        if (walker == null)
            return null;
        var position = walker.transform.position;
        var facing = walker.viewCamera != null ? walker.viewCamera.transform.forward : walker.transform.forward;
        var frame = SurfaceFrame.At(ToNum(position), Centre, ToNum(facing));
        return new Pose(ToNum(position), frame.Rotation);
    }

    /// <summary>What the player is looking at""")])

edit("src/MoonWorldEdit.Plugin/WorldEditor.cs", [
    ("""            case CommandKind.Paste:
                PasteHere(game!);
                break;""", """            case CommandKind.Paste:
                PasteHere(game!);
                break;
            case CommandKind.PasteRelative:
                PasteRelative(game!);
                break;"""),
    ("""        Save(Copy.From(w.Chosen, w.ChosenBelts, anchor));""",
     """        Save(Copy.From(w.Chosen, w.ChosenBelts, anchor, game.PlayerPose()));"""),
    ("""        var anchorPiece = copy.Pieces.OrderBy(p => p.Relative.Position.LengthSquared()).FirstOrDefault();
        var how = "at the aim";
        var anchor = anchorPiece != null
            ? game.SnapPose(anchorPiece.Kind, aim, target, out how)
            : new Pose(GameWorld.ToNum(aim), System.Numerics.Quaternion.Identity);

        game.PrepareCrewSync();""", """        var anchorPiece = copy.Pieces.OrderBy(p => p.Relative.Position.LengthSquared()).FirstOrDefault();
        var how = "at the aim";
        var anchor = anchorPiece != null
            ? game.SnapPose(anchorPiece.Kind, aim, target, out how)
            : new Pose(GameWorld.ToNum(aim), System.Numerics.Quaternion.Identity);
        Place(game, copy, anchor, how);
    }

    // Requested: the copy lands where it was relative to the player when copied, turned with their facing. Exact, no
    // snapping: where the player stands and looks decides it.
    [HideFromIl2Cpp]
    private void PasteRelative(GameWorld game)
    {
        var copy = Load();
        if (copy == null)
            return;
        if (game.PlayerPose() is not { } player)
        {
            Show("Can't find your position right now.");
            return;
        }
        if (copy.AnchorFor(player) is not { } anchor)
        {
            Show("This copy was made before /paste-relative existed: /copy it again.");
            return;
        }
        if (!game.HasRoom(copy.Pieces.Count, copy.Belts.Count))
        {
            Show("The world is at the game's limit of structures or belts.");
            return;
        }
        Place(game, copy, anchor, "relative to you");
    }

    [HideFromIl2Cpp]
    private void Place(GameWorld game, Copy copy, Pose anchor, string how)
    {
        game.PrepareCrewSync();"""),
    ("""        Show($"Pasted {placed.Count} pieces and {belts.Count} belts ({how}){(refused > 0 ? $"; the game refused to show {refused}" : "")}. /undo removes them.");""",
     """        var distance = game.PlayerPose() is { } me ? $", {System.Numerics.Vector3.Distance(me.Position, anchor.Position):0} m from you" : "";
        Show($"Pasted {placed.Count} pieces and {belts.Count} belts ({how}{distance})" +
            $"{(refused > 0 ? $"; the game refused to show {refused}" : "")}. /undo removes them.");"""),
])
print("patched")
