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
    private bool wandArmed;
    private Vector3? pointA, pointB;
    private Paste? lastPaste;

    public WorldEditor(IntPtr pointer) : base(pointer) { }

    private void Update()
    {
        try
        {
            var game = GameWorld.Find();
            commandLine ??= new CommandLine(transform);
            var line = commandLine.Update(game?.Builder);
            if (line != null)
                Run(line, game);
            if (game != null && !commandLine.IsOpen && wandArmed)
                UpdateWand(game);
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
                wandArmed = true;
                pointA = pointB = null;
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

    [HideFromIl2Cpp]
    private void UpdateWand(GameWorld game)
    {
        var mouse = Mouse.current;
        if (mouse == null)
            return;
        var left = mouse.leftButton.wasPressedThisFrame;
        var right = mouse.rightButton.wasPressedThisFrame;
        if (!left && !right)
            return;
        if (!game.Aim(out var point, out _, out _))
        {
            Show("Nothing there to mark; aim at a surface within 80 m.");
            return;
        }
        if (left)
            pointA = point;
        else
            pointB = point;
        Show($"Point {(left ? "A" : "B")} set{(pointA != null && pointB != null ? ". Both set: type /copy." : ".")}");
    }

    [HideFromIl2Cpp]
    private void CopyBox(GameWorld game)
    {
        if (pointA is not { } a || pointB is not { } b)
        {
            Show("Set points A and B with /wand first.");
            return;
        }
        var centre = game.Centre;
        var aNum = GameWorld.ToNum(a);
        var pieces = game.Pieces();
        // Lay the box out along the nearest structure at A, so it lines up with the build grid.
        var near = pieces.OrderBy(p => System.Numerics.Vector3.DistanceSquared(p.Pose.Position, aNum)).FirstOrDefault();
        var forward = near != null ? System.Numerics.Vector3.Transform(System.Numerics.Vector3.UnitZ, near.Pose.Rotation) : System.Numerics.Vector3.UnitZ;
        var frame = SurfaceFrame.At(aNum, centre, forward);
        var box = SelectionBox.Between(aNum, GameWorld.ToNum(b), frame, VerticalMargin);
        var chosen = Selection.Pieces(pieces, box).ToList();
        var belts = Selection.Belts(game.Belts(), box).ToList();
        if (chosen.Count == 0 && belts.Count == 0)
        {
            Show("Nothing built inside the box.");
            return;
        }
        // The anchor: the structural piece nearest A (it snaps on /paste), or any piece, or A itself.
        var anchorPiece = chosen.Where(p => p.PlacedPose).Concat(chosen)
            .OrderBy(p => p.PlacedPose ? 0 : 1).ThenBy(p => System.Numerics.Vector3.DistanceSquared(p.Pose.Position, aNum)).FirstOrDefault();
        var anchor = anchorPiece?.Pose ?? new Pose(aNum, System.Numerics.Quaternion.Identity);
        var copy = Copy.From(chosen, belts, anchor);
        Save(copy);
        Show($"Copied {chosen.Count} pieces and {belts.Count} belts. /rotate, then /paste.");
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

    private void OnDestroy() => commandLine?.Dispose();

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
