using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BepInEx;
using Il2CppInterop.Runtime.Attributes;
using MoonWorldEdit.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using Pose = MoonWorldEdit.Core.Pose;

namespace MoonWorldEdit.Plugin;

// The commands: /wand (left click A, right click B), /copy, /rotate, /paste, /undo. The clipboard is a file in
// BepInEx/config/MoonWorldEdit so it carries across saves and joined worlds.
public sealed class WorldEditor : MonoBehaviour
{
    private const float VerticalMargin = 1f; // metres above and below the wand's corners
    private const float RecheckSeconds = 1.5f; // after a paste, look for pieces the crew sync dropped

    private static readonly string ClipboardPath = Path.Combine(Paths.ConfigPath, "MoonWorldEdit", "clipboard.json");

    private const int MaxFailures = 20;
    private readonly HashSet<string> reported = new();
    private int failures;
    private CommandLine? commandLine;
    private GameObject? uiRoot;
    private const int MaxOutlines = 400; // piece outlines drawn at once
    private const float BoxBeam = 0.06f, OutlineBeam = 0.025f; // metres
    private Wand? wand;
    private Hologram? boxHologram, pieceHologram;
    private Paste? lastPaste;

    public WorldEditor(IntPtr pointer) : base(pointer) { }

    private void Update()
    {
        try
        {
            var game = GameWorld.Find();
            // Built once a world is loaded and the game's UI fonts exist; text made before then doesn't show.
            if (commandLine == null && game != null && CommandLine.FontsReady())
            {
                // Its own scene object: UI under BepInEx's hidden manager object (this component's) never draws.
                uiRoot = new GameObject("WorldEdit UI");
                UnityEngine.Object.DontDestroyOnLoad(uiRoot);
                commandLine = new CommandLine(uiRoot.transform);
                commandLine.Show("WorldEdit ready: press / for commands.");
            }
            if (commandLine == null)
                return;
            var line = commandLine.Update(game?.Builder);
            if (line != null)
                Run(line, game);
            if (game != null && !commandLine.IsOpen && wand != null)
                UpdateWand(game, wand);
            if (game != null && lastPaste is { Checked: false } paste && Time.unscaledTime >= paste.CheckAt)
                Recheck(game, paste);
        }
        catch (Exception e)
        {
            // Log each distinct failure once (Update runs every frame), and stop after a few.
            if (reported.Add(e.GetType().Name + e.Message))
                Plugin.Logger.LogError($"WorldEdit error: {e}");
            commandLine?.Close();
            commandLine?.Show("WorldEdit hit an error; see the BepInEx log.");
            if (++failures >= MaxFailures)
            {
                Plugin.Logger.LogError("WorldEdit is off until the game restarts.");
                enabled = false;
            }
        }
    }

    [HideFromIl2Cpp]
    private void Run(string text, GameWorld? game)
    {
        var command = Command.Parse(text);
        if (command.Kind == CommandKind.Invalid)
        {
            Show(command.Error);
            return;
        }
        if (game == null && command.Kind != CommandKind.Rotate)
        {
            Show("Load into a world first.");
            return;
        }
        switch (command.Kind)
        {
            case CommandKind.Wand:
                var pieces = game!.Pieces();
                wand = new Wand(pieces, game.Shapes(pieces), game.Belts());
                EnsureHolograms(game);
                Show("Wand ready: left click point A, right click point B.");
                break;
            case CommandKind.Copy:
                CopyBox(game!);
                break;
            case CommandKind.Rotate:
                RotateClipboard(command.Degrees);
                break;
            case CommandKind.Paste:
                PasteHere(game!);
                break;
            case CommandKind.Undo:
                Undo(game!);
                break;
        }
    }

    // Left click sets A (and clears B), right click sets B. Until B is set, the box follows the aim, so the player
    // sees the selection grow; the box and the outline of every piece inside are drawn as holograms, with a count.
    [HideFromIl2Cpp]
    private void UpdateWand(GameWorld game, Wand w)
    {
        var mouse = Mouse.current;
        var left = mouse != null && mouse.leftButton.wasPressedThisFrame;
        var right = mouse != null && mouse.rightButton.wasPressedThisFrame;
        var aimed = game.Aim(out var point, out _, out _);
        if ((left || right) && !aimed)
            Show("Nothing there to mark; aim at a surface within 80 m.");
        else if (left)
        {
            w.A = GameWorld.ToNum(point);
            w.B = null;
            // Line the box up with the nearest structure, so it follows the build grid.
            var near = w.Pieces.OrderBy(p => System.Numerics.Vector3.DistanceSquared(p.Pose.Position, w.A.Value)).FirstOrDefault();
            var forward = near != null
                ? System.Numerics.Vector3.Transform(System.Numerics.Vector3.UnitZ, near.Pose.Rotation)
                : System.Numerics.Vector3.UnitZ;
            w.Frame = SurfaceFrame.At(w.A.Value, game.Centre, forward);
        }
        else if (right && w.A != null)
            w.B = GameWorld.ToNum(point);
        else if (right)
            Show("Left click point A first.");

        if (w.A is not { } a || w.Frame is not { } frame)
        {
            commandLine?.Status("Wand: left click point A.");
            return;
        }
        System.Numerics.Vector3? end = w.B ?? (aimed ? GameWorld.ToNum(point) : null);
        if (end is not { } b)
            return;
        var box = SelectionBox.Between(a, b, frame, VerticalMargin);
        w.Box = box;
        w.Chosen = Selection.Pieces(w.Pieces, box, w.Shapes).ToList();
        w.ChosenBelts = Selection.Belts(w.Belts, box).ToList();

        boxHologram?.Draw(new[] { BoxCorners(box) }, BoxBeam);
        var signature = string.Join(",", w.Chosen.Select(p => p.Id));
        if (signature != w.Drawn)
        {
            w.Drawn = signature;
            pieceHologram?.Draw(w.Chosen.Take(MaxOutlines)
                .Select(p => w.Shapes.TryGetValue(p.Id, out var c) ? Unity(c) : PointBox(p.Pose.Position)), OutlineBeam);
        }
        commandLine?.Status($"{w.Chosen.Count} pieces, {w.ChosenBelts.Count} belts selected. " +
            (w.B == null ? "Right click to set point B." : "Type /copy, or left click to start again."));
    }

    [HideFromIl2Cpp]
    private void CopyBox(GameWorld game)
    {
        if (wand is not { A: { } a, B: not null } w)
        {
            Show("Set points A and B with /wand first.");
            return;
        }
        if (w.Chosen.Count == 0 && w.ChosenBelts.Count == 0)
        {
            Show("Nothing built inside the box.");
            return;
        }
        // The anchor: the structural piece nearest A (it snaps on /paste), or any piece, or A itself.
        var anchorPiece = w.Chosen.OrderBy(p => p.PlacedPose ? 0 : 1)
            .ThenBy(p => System.Numerics.Vector3.DistanceSquared(p.Pose.Position, a)).FirstOrDefault();
        var anchor = anchorPiece?.Pose ?? new Pose(a, System.Numerics.Quaternion.Identity);
        Save(Copy.From(w.Chosen, w.ChosenBelts, anchor));
        Show($"Copied {w.Chosen.Count} pieces and {w.ChosenBelts.Count} belts. /rotate, then /paste.");
        EndWand();
    }

    [HideFromIl2Cpp]
    private void EnsureHolograms(GameWorld game)
    {
        if (boxHologram != null)
            return;
        var template = game.HologramMaterial();
        boxHologram = new Hologram("WorldEdit selection", template, new Color(0.35f, 0.9f, 1f, 0.55f));
        pieceHologram = new Hologram("WorldEdit selected pieces", template, new Color(0.45f, 1f, 0.55f, 0.45f));
    }

    [HideFromIl2Cpp]
    private void EndWand()
    {
        wand = null;
        boxHologram?.Hide();
        pieceHologram?.Hide();
        commandLine?.Status("");
    }

    [HideFromIl2Cpp]
    private static Vector3[] BoxCorners(SelectionBox box)
    {
        var corners = new Vector3[8];
        for (var i = 0; i < 8; i++)
            corners[i] = GameWorld.ToUnity(box.Frame.ToWorld(new System.Numerics.Vector3(
                (i & 4) != 0 ? box.Max.X : box.Min.X, (i & 2) != 0 ? box.Max.Y : box.Min.Y, (i & 1) != 0 ? box.Max.Z : box.Min.Z)));
        return corners;
    }

    [HideFromIl2Cpp]
    private static Vector3[] Unity(System.Numerics.Vector3[] corners) => corners.Select(GameWorld.ToUnity).ToArray();

    [HideFromIl2Cpp]
    private static Vector3[] PointBox(System.Numerics.Vector3 p)
    {
        var c = GameWorld.ToUnity(p);
        var h = new Vector3(0.3f, 0.3f, 0.3f);
        return GameWorld.Corners(c - h, c + h).Select(GameWorld.ToUnity).ToArray();
    }

    [HideFromIl2Cpp]
    private void RotateClipboard(float degrees)
    {
        var copy = Load();
        if (copy == null)
            return;
        Save(copy.Rotated(degrees));
        Show($"Clipboard turned {degrees:0.##}°.");
    }

    [HideFromIl2Cpp]
    private void PasteHere(GameWorld game)
    {
        var copy = Load();
        if (copy == null)
            return;
        if (!game.Aim(out var aim, out var target, out _))
        {
            Show("Aim at the ground or a structure to paste on.");
            return;
        }
        if (!game.HasRoom(copy.Pieces.Count, copy.Belts.Count))
        {
            Show("The world is at the game's limit of structures or belts.");
            return;
        }
        var anchorPiece = copy.Pieces.OrderBy(p => p.Relative.Position.LengthSquared()).FirstOrDefault();
        var how = "at the aim";
        var anchor = anchorPiece != null
            ? game.SnapPose(anchorPiece.Kind, aim, target, out how)
            : new Pose(GameWorld.ToNum(aim), System.Numerics.Quaternion.Identity);

        game.PrepareCrewSync();
        var plan = PastePlan.For(copy, anchor, game.TakeStructureId);
        var placed = new List<PlacedPiece>();
        var refused = 0;
        foreach (var piece in plan.Pieces)
        {
            if (game.Add(piece))
                placed.Add(piece);
            else
                refused++;
        }
        var belts = plan.Belts.Select(game.Add).ToList();
        game.Save();
        lastPaste = new Paste(placed, belts, Time.unscaledTime + RecheckSeconds);
        Show($"Pasted {placed.Count} pieces and {belts.Count} belts ({how}){(refused > 0 ? $"; the game refused to show {refused}" : "")}. /undo removes them.");
    }

    [HideFromIl2Cpp]
    private void Undo(GameWorld game)
    {
        if (lastPaste == null)
        {
            Show("Nothing to undo.");
            return;
        }
        foreach (var belt in Enumerable.Reverse(lastPaste.Belts))
            game.RemoveBelt(belt);
        foreach (var piece in Enumerable.Reverse(lastPaste.Pieces))
            game.RemoveStructure(piece.Id);
        game.Save();
        Show($"Undid the last paste ({lastPaste.Pieces.Count} pieces, {lastPaste.Belts.Count} belts).");
        lastPaste = null;
    }

    // In a crew, a piece only one player has can be removed by the game's reconciliation before it is sent. Put back
    // anything missing once.
    [HideFromIl2Cpp]
    private void Recheck(GameWorld game, Paste paste)
    {
        paste.Checked = true;
        var missing = paste.Pieces.Where(p => !game.HasStructure(p.Id)).ToList();
        if (missing.Count == 0)
            return;
        var back = missing.Count(game.Add);
        game.Save();
        Show($"{missing.Count} pasted pieces went missing in the crew sync; put {back} back.");
    }

    [HideFromIl2Cpp]
    private void Save(Copy copy)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ClipboardPath)!);
        File.WriteAllText(ClipboardPath, ClipboardFile.Write(copy));
    }

    [HideFromIl2Cpp]
    private Copy? Load()
    {
        if (!File.Exists(ClipboardPath))
        {
            Show("The clipboard is empty: /wand, then /copy.");
            return null;
        }
        try
        {
            return ClipboardFile.Read(File.ReadAllText(ClipboardPath));
        }
        catch (InvalidDataException e)
        {
            Show("The clipboard file is damaged: " + e.Message);
            return null;
        }
    }

    [HideFromIl2Cpp]
    private void Show(string text) => commandLine?.Show(text);

    private void OnDestroy()
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
    }

    private sealed class Paste
    {
        public Paste(List<PlacedPiece> pieces, List<int> belts, float checkAt)
        {
            Pieces = pieces;
            Belts = belts;
            CheckAt = checkAt;
        }

        public List<PlacedPiece> Pieces { get; }
        public List<int> Belts { get; }
        public float CheckAt { get; }
        public bool Checked { get; set; }
    }
}
