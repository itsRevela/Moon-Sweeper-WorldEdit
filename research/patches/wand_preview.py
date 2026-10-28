"""One-off patch: live wand preview with holograms and a status line (applied 2026-10-27)."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
s = ""


def rep(old, new):
    global s
    assert s.count(old) == 1, old[:60]
    s = s.replace(old, new)


p = ROOT / "src/MoonWorldEdit.Plugin/WorldEditor.cs"
s = p.read_text(encoding="utf-8")

rep("""    private bool wandArmed;
    private Vector3? pointA, pointB;
    private Paste? lastPaste;""", """    private const int MaxOutlines = 400; // piece outlines drawn at once
    private const float BoxBeam = 0.06f, OutlineBeam = 0.025f; // metres
    private Wand? wand;
    private Hologram? boxHologram, pieceHologram;
    private Paste? lastPaste;""")

rep("""            if (game != null && !commandLine.IsOpen && wandArmed)
                UpdateWand(game);""", """            if (game != null && !commandLine.IsOpen && wand != null)
                UpdateWand(game, wand);""")

rep("""            case CommandKind.Wand:
                wandArmed = true;
                pointA = pointB = null;
                Show("Wand ready: left click point A, right click point B.");
                break;""", """            case CommandKind.Wand:
                var pieces = game!.Pieces();
                wand = new Wand(pieces, game.Shapes(pieces), game.Belts());
                EnsureHolograms(game);
                Show("Wand ready: left click point A, right click point B.");
                break;""")

start = s.index("    [HideFromIl2Cpp]\n    private void UpdateWand(GameWorld game)")
end = s.index("    [HideFromIl2Cpp]\n    private void RotateClipboard")
s = s[:start] + (ROOT / "research/patches/wand_preview.cs.txt").read_text(encoding="utf-8") + s[end:]

rep("""    private void OnDestroy() => commandLine?.Dispose();""", """    private void OnDestroy()
    {
        commandLine?.Dispose();
        boxHologram?.Dispose();
        pieceHologram?.Dispose();
    }

    private sealed class Wand
    {
        public Wand(List<WorldPiece> pieces, Dictionary<int, System.Numerics.Vector3[]> shapes, List<WorldBelt> belts)
        {
            Pieces = pieces;
            Shapes = shapes;
            Belts = belts;
        }

        public List<WorldPiece> Pieces { get; }
        public Dictionary<int, System.Numerics.Vector3[]> Shapes { get; }
        public List<WorldBelt> Belts { get; }
        public System.Numerics.Vector3? A { get; set; }
        public System.Numerics.Vector3? B { get; set; }
        public SurfaceFrame? Frame { get; set; }
        public SelectionBox? Box { get; set; }
        public List<WorldPiece> Chosen { get; set; } = new();
        public List<WorldBelt> ChosenBelts { get; set; } = new();
        public string Drawn { get; set; } = "";
    }""")
p.write_text(s, encoding="utf-8")

p = ROOT / "src/MoonWorldEdit.Plugin/CommandLine.cs"
s = p.read_text(encoding="utf-8")
rep("""    private readonly TMP_Text message;""", """    private readonly TMP_Text message;
    private readonly TMP_Text status;""")
rep("""        message = kit.Text(stage, "", 40f, MoonWorkshopKit.FrameH - 160f, 1400f, 40f, 22f, MoonWorkshopKit.Soft);""",
    """        message = kit.Text(stage, "", 40f, MoonWorkshopKit.FrameH - 160f, 1400f, 40f, 22f, MoonWorkshopKit.Soft);
        status = kit.Text(stage, "", 40f, MoonWorkshopKit.FrameH - 205f, 1400f, 40f, 22f, MoonWorkshopKit.Orange);""")
rep("""    public bool IsOpen { get; private set; }""", """    public bool IsOpen { get; private set; }

    /// <summary>A standing line (the wand's live selection count); empty to clear. Not logged.</summary>
    public void Status(string value)
    {
        if (status.text != value)
            status.text = value;
    }""")
p.write_text(s, encoding="utf-8")
print("patched")
