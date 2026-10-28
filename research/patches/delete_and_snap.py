"""One-off patch: /delete with undo/redo, and snapping for /paste-relative (applied 2026-10-27)."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
s = ""


def load(path):
    global s
    s = (ROOT / path).read_text(encoding="utf-8")


def save(path):
    (ROOT / path).write_text(s, encoding="utf-8")


def rep(old, new):
    global s
    assert s.count(old) == 1, old[:70]
    s = s.replace(old, new)


# ---- GameWorld: belts with ids, and snapping toward a wanted rotation ------------------------------------------------
load("src/MoonWorldEdit.Plugin/GameWorld.cs")
rep("""    public List<WorldBelt> Belts()
    {
        var origin = World.transform.position;
        var list = new List<WorldBelt>();
        foreach (var entry in World.conveyors)
        {
            var rec = entry.Value;
            list.Add(new WorldBelt(ToNum(rec.start + origin), ToNum(rec.startDirection), ToNum(rec.end + origin), ToNum(rec.endDirection)));
        }
        return list;
    }""", """    public List<(int Id, WorldBelt Belt)> Belts()
    {
        var origin = World.transform.position;
        var list = new List<(int, WorldBelt)>();
        foreach (var entry in World.conveyors)
        {
            var rec = entry.Value;
            list.Add((rec.id, new WorldBelt(ToNum(rec.start + origin), ToNum(rec.startDirection), ToNum(rec.end + origin),
                ToNum(rec.endDirection))));
        }
        return list;
    }""")
rep("""    public Pose SnapPose(int kind, Vector3 aim, int target, out string how)
    {
        var plan = World.PlanStructure(kind, aim, 0f, target, false, 0f, 0);
        if (plan != null && plan.snapped)
        {
            how = string.IsNullOrEmpty(plan.hint) ? "snapped" : plan.hint;
            return new Pose(ToNum(plan.position), ToNum(plan.rotation));
        }""", """    /// <param name="want">For /paste-relative: the pose it would take unsnapped. The snap keeps the turn nearest its
    /// rotation, and with nothing to snap to the piece goes exactly there.</param>
    public Pose SnapPose(int kind, Vector3 aim, int target, out string how, Pose? want = null)
    {
        StructurePlan? best = null;
        var bestMatch = -1f;
        for (var turn = 0; turn < (want == null ? 1 : MaxTurns); turn++)
        {
            var plan = World.PlanStructure(kind, aim, 0f, target, false, 0f, turn);
            if (plan == null || !plan.snapped)
                continue;
            var match = want is { } w ? Math.Abs(Num.Quaternion.Dot(ToNum(plan.rotation), w.Rotation)) : 1f;
            if (match > bestMatch)
            {
                best = plan;
                bestMatch = match;
            }
        }
        if (best != null)
        {
            how = string.IsNullOrEmpty(best.hint) ? "snapped" : best.hint;
            return new Pose(ToNum(best.position), ToNum(best.rotation));
        }
        if (want is { } exact)
        {
            how = "exact (no snap here)";
            return exact;
        }""")
rep("""    private const string FallbackOwner = "moon-base";""", """    private const string FallbackOwner = "moon-base";
    private const int MaxTurns = 6; // grid turns tried when snapping toward a rotation (hexes have 6, squares 4)""")
save("src/MoonWorldEdit.Plugin/GameWorld.cs")

# ---- WorldEditor ----------------------------------------------------------------------------------------------------
load("src/MoonWorldEdit.Plugin/WorldEditor.cs")
rep("""            case CommandKind.Redo:
                Redo(game!);
                break;""", """            case CommandKind.Redo:
                Redo(game!);
                break;
            case CommandKind.Delete:
                DeleteBox(game!);
                break;""")
rep("""        w.ChosenBelts = Selection.Belts(w.Belts, box).ToList();""",
    """        w.ChosenBelts = w.Belts.Where(b => Selection.Belts(new[] { b.Belt }, box).Any()).ToList();""")
rep("""            (w.B == null ? "Right click to set point B." : "Type /copy, or left click to start again."));""",
    """            (w.B == null ? "Right click to set point B." : "Type /copy or /delete, or left click to start again."));""")
rep("""        Save(Copy.From(w.Chosen, w.ChosenBelts, anchor, game.PlayerPose()));""",
    """        Save(Copy.From(w.Chosen, w.ChosenBelts.Select(b => b.Belt), anchor, game.PlayerPose()));""")

# /paste-relative snaps
rep("""        if (copy.AnchorFor(player) is not { } anchor)
        {
            Show("This copy was made before /paste-relative existed: /copy it again.");
            return;
        }""", """        if (copy.AnchorFor(player) is not { } exact)
        {
            Show("This copy was made before /paste-relative existed: /copy it again.");
            return;
        }
        // Requested: snap like /paste, at the spot relative to the player, keeping the turn nearest their facing.
        var anchorPiece = copy.Pieces.OrderBy(p => p.Relative.Position.LengthSquared()).FirstOrDefault();
        var how = "exact";
        var anchor = anchorPiece != null
            ? game.SnapPose(anchorPiece.Kind, GameWorld.ToUnity(exact.Position), 0, out how, exact)
            : exact;""")
rep("""        Place(game, copy, anchor, "relative to you");""", """        Place(game, copy, anchor, how + ", relative to you");""")

# history of edits: pastes (added) and deletions (removed)
rep("""        var belts = plan.Belts.Select(b => (game.Add(b), b)).ToList();
        game.Save();
        Remember(new Paste(placed, belts, Time.unscaledTime + RecheckSeconds));
        redoable.Clear();""", """        var belts = plan.Belts.Select(b => (game.Add(b), b)).ToList();
        game.Save();
        Remember(new Edit(placed, belts, true, Time.unscaledTime + RecheckSeconds));
        redoable.Clear();""")

start = s.index("    [HideFromIl2Cpp]\n    private void Remember(")
end = s.index("    // In a crew, a piece only one player has")
s = s[:start] + """    [HideFromIl2Cpp]
    private void Remember(Edit edit)
    {
        undoable.Add(edit);
        if (undoable.Count > HistoryKept)
            undoable.RemoveAt(0);
    }

    // Requested: delete everything in the wand's selection. No refund (as /undo of a paste gives none), and it goes in
    // the history, so /undo puts it back and /redo deletes it again.
    [HideFromIl2Cpp]
    private void DeleteBox(GameWorld game)
    {
        if (wand is not { A: not null, B: not null } w)
        {
            Show("Set points A and B with /wand first.");
            return;
        }
        if (w.Chosen.Count == 0 && w.ChosenBelts.Count == 0)
        {
            Show("Nothing built inside the box.");
            return;
        }
        var pieces = w.Chosen.Select(p => new PlacedPiece(p.Id, p.Kind, p.Pose, p.PlacedPose, p.Style, p.Support)).ToList();
        var deleted = new Edit(PastePlan.SupportsFirst(pieces).ToList(), w.ChosenBelts.ToList(), false, 0f) { Checked = true };
        RemoveAll(game, deleted);
        Remember(deleted);
        redoable.Clear();
        Show($"Deleted {deleted.Pieces.Count} pieces and {deleted.Belts.Count} belts. /undo puts them back.");
        EndWand();
    }

    [HideFromIl2Cpp]
    private void Undo(GameWorld game)
    {
        if (undoable.Count == 0)
        {
            Show("Nothing to undo.");
            return;
        }
        var edit = undoable[^1];
        undoable.RemoveAt(undoable.Count - 1);
        if (edit.Added)
        {
            RemoveAll(game, edit);
            redoable.Add(edit);
            Show($"Undid a paste ({edit.Pieces.Count} pieces, {edit.Belts.Count} belts). /redo puts it back.");
        }
        else
        {
            var back = AddAll(game, edit, added: false);
            redoable.Add(back);
            Show($"Undid a delete: put back {back.Pieces.Count} pieces and {back.Belts.Count} belts. /redo deletes them again.");
        }
    }

    [HideFromIl2Cpp]
    private void Redo(GameWorld game)
    {
        if (redoable.Count == 0)
        {
            Show("Nothing to redo.");
            return;
        }
        var edit = redoable[^1];
        if (edit.Added && !game.HasRoom(edit.Pieces.Count, edit.Belts.Count))
        {
            Show("The world is at the game's limit of structures or belts.");
            return;
        }
        redoable.RemoveAt(redoable.Count - 1);
        if (edit.Added)
        {
            var again = AddAll(game, edit, added: true);
            Remember(again);
            Show($"Redid a paste ({again.Pieces.Count} pieces, {again.Belts.Count} belts).");
        }
        else
        {
            RemoveAll(game, edit);
            Remember(edit);
            Show($"Redid a delete ({edit.Pieces.Count} pieces, {edit.Belts.Count} belts).");
        }
    }

    // Removes an edit's belts, then its pieces from the top down (what is held before what holds it), without refunds.
    [HideFromIl2Cpp]
    private static void RemoveAll(GameWorld game, Edit edit)
    {
        foreach (var (id, _) in Enumerable.Reverse(edit.Belts))
            game.RemoveBelt(id);
        foreach (var piece in Enumerable.Reverse(edit.Pieces))
            game.RemoveStructure(piece.Id);
        game.Save();
    }

    // Puts an edit's pieces back (supports first, fresh ids) and its belts; returns the edit with the new ids.
    [HideFromIl2Cpp]
    private Edit AddAll(GameWorld game, Edit edit, bool added)
    {
        game.PrepareCrewSync();
        var placed = PastePlan.Renumber(edit.Pieces, game.TakeStructureId).Where(game.Add).ToList();
        var belts = edit.Belts.Select(b => (game.Add(b.Belt), b.Belt)).ToList();
        game.Save();
        return new Edit(placed, belts, added, Time.unscaledTime + RecheckSeconds);
    }

""" + s[end:]

rep("""    private void Recheck(GameWorld game, Paste paste)""", """    private void Recheck(GameWorld game, Edit paste)""")
rep("""    private readonly List<Paste> undoable = new(); // newest last
    private readonly List<Paste> redoable = new(); // newest last; cleared by a new paste""",
    """    private readonly List<Edit> undoable = new(); // newest last
    private readonly List<Edit> redoable = new(); // newest last; cleared by a new paste or delete""")
rep("""    private sealed class Paste
    {
        public Paste(List<PlacedPiece> pieces, List<(int Id, WorldBelt Belt)> belts, float checkAt)
        {
            Pieces = pieces;
            Belts = belts;
            CheckAt = checkAt;
        }
""", """    // One step in the history: pieces and belts a paste added (Added) or a /delete removed (not Added).
    private sealed class Edit
    {
        public Edit(List<PlacedPiece> pieces, List<(int Id, WorldBelt Belt)> belts, bool added, float checkAt)
        {
            Pieces = pieces;
            Belts = belts;
            Added = added;
            CheckAt = checkAt;
        }

        public bool Added { get; }
""")
rep("""        public Wand(List<WorldPiece> pieces, Dictionary<int, System.Numerics.Vector3[]> shapes, List<WorldBelt> belts)""",
    """        public Wand(List<WorldPiece> pieces, Dictionary<int, System.Numerics.Vector3[]> shapes, List<(int Id, WorldBelt Belt)> belts)""")
rep("""        public List<WorldBelt> Belts { get; }""", """        public List<(int Id, WorldBelt Belt)> Belts { get; }""")
rep("""        public List<WorldBelt> ChosenBelts { get; set; } = new();""", """        public List<(int Id, WorldBelt Belt)> ChosenBelts { get; set; } = new();""")
save("src/MoonWorldEdit.Plugin/WorldEditor.cs")
print("patched")
