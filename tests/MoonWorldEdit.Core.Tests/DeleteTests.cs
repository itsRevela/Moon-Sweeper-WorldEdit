using System.Numerics;
using MoonWorldEdit.Core;

namespace MoonWorldEdit.Core.Tests;

// /delete (requested) removes what's on top before what holds it up, and /undo puts it back supports first.
public sealed class DeleteTests
{
    private static PlacedPiece Piece(int id, int support = 0) =>
        new(id, 110, new Pose(Vector3.One * id, Quaternion.Identity), true, 0, support);

    [Fact]
    public void SupportsFirst_PutsEachSupportBeforeWhatItHolds()
    {
        var ordered = PastePlan.SupportsFirst(new[] { Piece(3, support: 2), Piece(2, support: 1), Piece(1) });

        Assert.Equal(new[] { 1, 2, 3 }, ordered.Select(p => p.Id));
    }

    [Fact]
    public void SupportsFirst_SupportOutsideTheList_DoesNotMatter()
    {
        var ordered = PastePlan.SupportsFirst(new[] { Piece(5, support: 99), Piece(4) });

        Assert.Equal(2, ordered.Count);
    }

    [Fact]
    public void SupportsFirst_Loop_Throws()
    {
        Assert.Throws<InvalidDataException>(() => PastePlan.SupportsFirst(new[] { Piece(1, support: 2), Piece(2, support: 1) }));
    }

    [Fact]
    public void Parse_KnowsDelete()
    {
        Assert.Equal(CommandKind.Delete, Command.Parse("/delete").Kind);
    }
}
