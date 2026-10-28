"""One-off patch: /redo, and undo/redo history of the last pastes (applied 2026-10-27)."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
p = ROOT / "src/MoonWorldEdit.Plugin/WorldEditor.cs"
s = p.read_text(encoding="utf-8")


def rep(old, new):
    global s
    assert s.count(old) == 1, old[:60]
    s = s.replace(old, new)


rep("""    private Paste? lastPaste;""", """    private const int HistoryKept = 20; // pastes /undo can step back through
    private readonly List<Paste> undoable = new(); // newest last
    private readonly List<Paste> redoable = new(); // newest last; cleared by a new paste""")

rep("""            if (game != null && lastPaste is { Checked: false } paste && Time.unscaledTime >= paste.CheckAt)
                Recheck(game, paste);""", """            if (game != null && undoable.Count > 0 && undoable[^1] is { Checked: false } paste && Time.unscaledTime >= paste.CheckAt)
                Recheck(game, paste);""")

rep("""            case CommandKind.Undo:
                Undo(game!);
                break;""", """            case CommandKind.Undo:
                Undo(game!);
                break;
            case CommandKind.Redo:
                Redo(game!);
                break;""")

rep("""        var belts = plan.Belts.Select(game.Add).ToList();
        game.Save();
        lastPaste = new Paste(placed, belts, Time.unscaledTime + RecheckSeconds);""", """        var belts = plan.Belts.Select(b => (game.Add(b), b)).ToList();
        game.Save();
        Remember(new Paste(placed, belts, Time.unscaledTime + RecheckSeconds));
        redoable.Clear();""")

start = s.index("    [HideFromIl2Cpp]\n    private void Undo(GameWorld game)")
end = s.index("    // In a crew, a piece only one player has")
s = s[:start] + """    [HideFromIl2Cpp]
    private void Remember(Paste paste)
    {
        undoable.Add(paste);
        if (undoable.Count > HistoryKept)
            undoable.RemoveAt(0);
    }

    [HideFromIl2Cpp]
    private void Undo(GameWorld game)
    {
        if (undoable.Count == 0)
        {
            Show("Nothing to undo.");
            return;
        }
        var paste = undoable[^1];
        undoable.RemoveAt(undoable.Count - 1);
        foreach (var (id, _) in Enumerable.Reverse(paste.Belts))
            game.RemoveBelt(id);
        foreach (var piece in Enumerable.Reverse(paste.Pieces))
            game.RemoveStructure(piece.Id);
        game.Save();
        redoable.Add(paste);
        Show($"Undid a paste ({paste.Pieces.Count} pieces, {paste.Belts.Count} belts). /redo puts it back.");
    }

    // Requested: put the last undone paste back. Fresh ids (the old ones may have been reused), same poses.
    [HideFromIl2Cpp]
    private void Redo(GameWorld game)
    {
        if (redoable.Count == 0)
        {
            Show("Nothing to redo.");
            return;
        }
        var undone = redoable[^1];
        redoable.RemoveAt(redoable.Count - 1);
        if (!game.HasRoom(undone.Pieces.Count, undone.Belts.Count))
        {
            redoable.Add(undone);
            Show("The world is at the game's limit of structures or belts.");
            return;
        }
        game.PrepareCrewSync();
        var placed = PastePlan.Renumber(undone.Pieces, game.TakeStructureId).Where(game.Add).ToList();
        var belts = undone.Belts.Select(b => (game.Add(b.Belt), b.Belt)).ToList();
        game.Save();
        Remember(new Paste(placed, belts, Time.unscaledTime + RecheckSeconds));
        Show($"Redid a paste ({placed.Count} pieces, {belts.Count} belts).");
    }

""" + s[end:]

rep("""        public Paste(List<PlacedPiece> pieces, List<int> belts, float checkAt)""",
    """        public Paste(List<PlacedPiece> pieces, List<(int Id, WorldBelt Belt)> belts, float checkAt)""")
rep("""        public List<int> Belts { get; }""", """        public List<(int Id, WorldBelt Belt)> Belts { get; }""")
p.write_text(s, encoding="utf-8")
print("patched")
