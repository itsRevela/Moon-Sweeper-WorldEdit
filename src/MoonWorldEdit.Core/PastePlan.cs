using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace MoonWorldEdit.Core;

/// <summary>A piece to place: its new id, world pose and the new id of the piece it stands on (0: none).</summary>
public sealed record PlacedPiece(int Id, int Kind, Pose Pose, bool PlacedPose, int Style, int Support);

/// <summary>What /paste will build: pieces in an order where every support comes before what it holds, then belts.</summary>
public sealed class PastePlan
{
    private PastePlan(IReadOnlyList<PlacedPiece> pieces, IReadOnlyList<WorldBelt> belts)
    {
        Pieces = pieces;
        Belts = belts;
    }

    public IReadOnlyList<PlacedPiece> Pieces { get; }
    public IReadOnlyList<WorldBelt> Belts { get; }

    /// <param name="nextId">Hands out new structure ids (the game's next free id, in the player's id block).</param>
    public static PastePlan For(Copy copy, Pose anchor, Func<int> nextId)
    {
        if (copy == null)
            throw new ArgumentNullException(nameof(copy));
        if (nextId == null)
            throw new ArgumentNullException(nameof(nextId));

        var newIds = new Dictionary<int, int>();
        var pieces = new List<PlacedPiece>();
        foreach (var piece in SupportsFirst(copy.Pieces))
        {
            var id = nextId();
            newIds[piece.Ref] = id;
            pieces.Add(new PlacedPiece(id, piece.Kind, Layout.Place(anchor, piece.Relative), piece.PlacedPose, piece.Style,
                piece.SupportRef == 0 ? 0 : newIds[piece.SupportRef]));
        }
        var belts = copy.Belts.Select(b => new WorldBelt(
            anchor.Position + Vector3.Transform(b.Start, anchor.Rotation), Vector3.Transform(b.StartDirection, anchor.Rotation),
            anchor.Position + Vector3.Transform(b.End, anchor.Rotation), Vector3.Transform(b.EndDirection, anchor.Rotation))).ToList();
        return new PastePlan(pieces, belts);
    }

    /// <summary>
    /// The same pieces with fresh ids, for /redo: supports relinked to the new ids (dropped if outside the list). The
    /// order is kept, so supports still come first.
    /// </summary>
    public static IReadOnlyList<PlacedPiece> Renumber(IReadOnlyList<PlacedPiece> pieces, Func<int> nextId)
    {
        if (pieces == null)
            throw new ArgumentNullException(nameof(pieces));
        if (nextId == null)
            throw new ArgumentNullException(nameof(nextId));
        var newIds = new Dictionary<int, int>();
        var again = new List<PlacedPiece>(pieces.Count);
        foreach (var piece in pieces)
        {
            var id = nextId();
            newIds[piece.Id] = id;
            again.Add(piece with { Id = id, Support = newIds.TryGetValue(piece.Support, out var s) ? s : 0 });
        }
        return again;
    }

    private static List<CopiedPiece> SupportsFirst(IReadOnlyList<CopiedPiece> pieces)
    {
        var byRef = pieces.ToDictionary(p => p.Ref);
        var ordered = new List<CopiedPiece>(pieces.Count);
        var state = new Dictionary<int, bool>(); // false: being visited, true: done
        foreach (var piece in pieces)
            Visit(piece);
        return ordered;

        void Visit(CopiedPiece piece)
        {
            if (state.TryGetValue(piece.Ref, out var done))
            {
                if (!done)
                    throw new InvalidDataException($"Pieces {piece.Ref} and its supports hold each other up in a loop.");
                return;
            }
            state[piece.Ref] = false;
            if (piece.SupportRef != 0 && byRef.TryGetValue(piece.SupportRef, out var support))
                Visit(support);
            state[piece.Ref] = true;
            ordered.Add(piece);
        }
    }
}
