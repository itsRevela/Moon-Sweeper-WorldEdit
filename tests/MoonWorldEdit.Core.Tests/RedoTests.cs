using System.Numerics;
using MoonWorldEdit.Core;

namespace MoonWorldEdit.Core.Tests;

// /redo (requested) puts an undone paste back with fresh ids, since the old ones may have been reused.
public sealed class RedoTests
{
    private static PlacedPiece Piece(int id, int support = 0) =>
        new(id, 110, new Pose(Vector3.One * id, Quaternion.Identity), true, 0, support);

    [Fact]
    public void Renumber_GivesFreshIds()
    {
        var next = 900;

        var again = PastePlan.Renumber(new[] { Piece(5), Piece(6, support: 5) }, () => next++);

        Assert.Equal(new[] { 900, 901 }, again.Select(p => p.Id));
    }

    [Fact]
    public void Renumber_RelinksSupports()
    {
        var next = 900;

        var again = PastePlan.Renumber(new[] { Piece(5), Piece(6, support: 5) }, () => next++);

        Assert.Equal(900, again[1].Support);
    }

    [Fact]
    public void Renumber_SupportOutsideTheList_IsDropped()
    {
        var next = 900;

        var again = PastePlan.Renumber(new[] { Piece(6, support: 42) }, () => next++);

        Assert.Equal(0, again[0].Support);
    }

    [Fact]
    public void Renumber_KeepsPosesAndKinds()
    {
        var next = 900;
        var piece = Piece(5);

        var again = PastePlan.Renumber(new[] { piece }, () => next++);

        Assert.Equal(piece with { Id = 900 }, again[0]);
    }

    [Fact]
    public void Parse_KnowsRedo()
    {
        Assert.Equal(CommandKind.Redo, Command.Parse("/redo").Kind);
    }
}
