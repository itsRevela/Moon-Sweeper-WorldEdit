using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;

namespace MoonWorldEdit.Core;

/// <summary>
/// The clipboard as a JSON file, so a copy carries across saves and worlds. Reading validates everything the game
/// would reject (a record failing its checks is dropped by crewmates and then deleted), so a bad file fails loudly.
/// </summary>
public static class ClipboardFile
{
    public const int Version = 2; // 2 adds the player's pose (/paste-relative); 1 still reads
    public const int MinKind = 1, MaxKind = 192; // the game's structure kinds (MoonWorld.ReadDepot)
    public const int MaxPieces = 9999, MaxBelts = 20000; // the game's own caps

    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static string Write(Copy copy)
    {
        if (copy == null)
            throw new ArgumentNullException(nameof(copy));
        var file = new FileData
        {
            Version = Version,
            Pieces = copy.Pieces.Select(p => new PieceData
            {
                Ref = p.Ref,
                Kind = p.Kind,
                PlacedPose = p.PlacedPose,
                Style = p.Style,
                Support = p.SupportRef,
                X = p.Relative.Position.X,
                Y = p.Relative.Position.Y,
                Z = p.Relative.Position.Z,
                Qx = p.Relative.Rotation.X,
                Qy = p.Relative.Rotation.Y,
                Qz = p.Relative.Rotation.Z,
                Qw = p.Relative.Rotation.W,
            }).ToList(),
            FromPlayer = copy.FromPlayer is { } fp ? new[] { fp.Position.X, fp.Position.Y, fp.Position.Z,
                fp.Rotation.X, fp.Rotation.Y, fp.Rotation.Z, fp.Rotation.W } : null,
            Belts = copy.Belts.Select(b => new BeltData
            {
                Start = Floats(b.Start),
                StartDirection = Floats(b.StartDirection),
                End = Floats(b.End),
                EndDirection = Floats(b.EndDirection),
            }).ToList(),
        };
        return JsonSerializer.Serialize(file, Options);
    }

    public static Copy Read(string text)
    {
        FileData? file;
        try
        {
            file = JsonSerializer.Deserialize<FileData>(text ?? "", Options);
        }
        catch (JsonException e)
        {
            throw new InvalidDataException("The clipboard file isn't valid JSON: " + e.Message, e);
        }
        if (file == null || file.Pieces == null || file.Belts == null)
            throw new InvalidDataException("The clipboard file is empty or missing its pieces or belts.");
        if (file.Version != 1 && file.Version != Version)
            throw new InvalidDataException($"The clipboard file is version {file.Version}; this mod reads version {Version}.");
        if (file.Pieces.Count > MaxPieces || file.Belts.Count > MaxBelts)
            throw new InvalidDataException($"The clipboard holds more than the game allows ({MaxPieces} pieces, {MaxBelts} belts).");

        var refs = new HashSet<int>();
        foreach (var p in file.Pieces)
        {
            if (p.Ref < 1 || !refs.Add(p.Ref))
                throw new InvalidDataException($"Piece ref {p.Ref} is missing or repeated.");
            if (p.Kind < MinKind || p.Kind > MaxKind)
                throw new InvalidDataException($"Piece {p.Ref} has kind {p.Kind}, outside {MinKind}..{MaxKind}.");
            if (p.Style < 0)
                throw new InvalidDataException($"Piece {p.Ref} has a negative style.");
            if (!new[] { p.X, p.Y, p.Z, p.Qx, p.Qy, p.Qz, p.Qw }.All(float.IsFinite))
                throw new InvalidDataException($"Piece {p.Ref} has a number that isn't finite.");
        }
        foreach (var p in file.Pieces.Where(p => p.Support != 0 && !refs.Contains(p.Support)))
            throw new InvalidDataException($"Piece {p.Ref} stands on piece {p.Support}, which isn't in the clipboard.");

        var pieces = file.Pieces.Select(p =>
        {
            var rotation = new Quaternion(p.Qx, p.Qy, p.Qz, p.Qw);
            if (rotation.LengthSquared() < 1e-6f)
                throw new InvalidDataException($"Piece {p.Ref} has no rotation.");
            return new CopiedPiece(p.Ref, p.Kind, new Pose(new Vector3(p.X, p.Y, p.Z), Quaternion.Normalize(rotation)),
                p.PlacedPose, p.Style, p.Support);
        }).ToList();
        var belts = file.Belts.Select((b, i) => new WorldBelt(Vector(b.Start, i), Vector(b.StartDirection, i), Vector(b.End, i),
            Vector(b.EndDirection, i))).ToList();
        Pose? fromPlayer = null;
        if (file.FromPlayer != null)
        {
            var f = file.FromPlayer;
            if (f.Length != 7 || !f.All(float.IsFinite))
                throw new InvalidDataException("The player pose isn't seven finite numbers.");
            var q = new Quaternion(f[3], f[4], f[5], f[6]);
            if (q.LengthSquared() < 1e-6f)
                throw new InvalidDataException("The player pose has no rotation.");
            fromPlayer = new Pose(new Vector3(f[0], f[1], f[2]), Quaternion.Normalize(q));
        }
        return new Copy(pieces, belts, fromPlayer);
    }

    private static float[] Floats(Vector3 v) => new[] { v.X, v.Y, v.Z };

    private static Vector3 Vector(float[]? values, int belt)
    {
        if (values == null || values.Length != 3 || !values.All(float.IsFinite))
            throw new InvalidDataException($"Belt {belt} has a point or direction that isn't three finite numbers.");
        return new Vector3(values[0], values[1], values[2]);
    }

    private sealed class FileData
    {
        public int Version { get; set; }
        public List<PieceData>? Pieces { get; set; }
        public List<BeltData>? Belts { get; set; }
        public float[]? FromPlayer { get; set; }
    }

    private sealed class PieceData
    {
        public int Ref { get; set; }
        public int Kind { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public float Qx { get; set; }
        public float Qy { get; set; }
        public float Qz { get; set; }
        public float Qw { get; set; }
        public bool PlacedPose { get; set; }
        public int Style { get; set; }
        public int Support { get; set; }
    }

    private sealed class BeltData
    {
        public float[]? Start { get; set; }
        public float[]? StartDirection { get; set; }
        public float[]? End { get; set; }
        public float[]? EndDirection { get; set; }
    }
}
