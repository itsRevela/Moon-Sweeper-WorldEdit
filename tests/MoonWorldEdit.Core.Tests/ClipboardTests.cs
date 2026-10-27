using System.Numerics;
using MoonWorldEdit.Core;

namespace MoonWorldEdit.Core.Tests;

// What /copy records, how /rotate turns it, how /paste lays it out, and the clipboard file that carries it between
// saves and worlds.
public sealed class ClipboardTests
{
    private static readonly Vector3 Base = new(10f, 2000f, -5f);

    private static WorldPiece Piece(int id, int kind, Vector3 offset, int support = 0, bool placedPose = true, int style = 0) =>
        new(id, kind, new Pose(Base + offset, Quaternion.Identity), placedPose, style, support);

    private static WorldBelt Belt(Vector3 start, Vector3 end) =>
        new(Base + start, Vector3.UnitX, Base + end, Vector3.UnitX);

    private static readonly Pose Anchor = new(Base, Quaternion.Identity);

    private static Copy CopyOf(IEnumerable<WorldPiece> pieces, IEnumerable<WorldBelt>? belts = null) =>
        Copy.From(pieces, belts ?? Array.Empty<WorldBelt>(), Anchor);

    // ---- copy -------------------------------------------------------------------------------------------------------

    [Fact]
    public void Copy_StoresPiecesRelativeToTheAnchor()
    {
        var copy = CopyOf(new[] { Piece(7, 110, new Vector3(3, 0, 4)) });

        Assert.Equal(new Vector3(3, 0, 4), copy.Pieces[0].Relative.Position);
    }

    [Fact]
    public void Copy_KeepsSupportsInsideTheCopy()
    {
        var copy = CopyOf(new[] { Piece(1, 110, Vector3.Zero), Piece(2, 85, Vector3.UnitY, support: 1) });

        Assert.Equal(1, copy.Pieces.Single(p => p.Ref == 2).SupportRef);
    }

    [Fact]
    public void Copy_DropsSupportsOutsideTheCopy()
    {
        var copy = CopyOf(new[] { Piece(2, 85, Vector3.UnitY, support: 99) });

        Assert.Equal(0, copy.Pieces[0].SupportRef);
    }

    [Fact]
    public void Copy_StoresBeltsRelativeToTheAnchor()
    {
        var copy = CopyOf(Array.Empty<WorldPiece>(), new[] { Belt(new Vector3(1, 0, 0), new Vector3(5, 0, 0)) });

        Assert.Equal(new Vector3(5, 0, 0), copy.Belts[0].End);
    }

    [Fact]
    public void Copy_Empty_IsAllowedButEmpty()
    {
        Assert.True(CopyOf(Array.Empty<WorldPiece>()).IsEmpty);
    }

    // ---- selection --------------------------------------------------------------------------------------------------

    [Fact]
    public void Select_TakesPiecesWhoseBaseIsInsideTheBox()
    {
        var frame = SurfaceFrame.At(Base, Vector3.Zero, Vector3.UnitZ);
        var box = SelectionBox.Between(frame.ToWorld(new Vector3(-2, 0, -2)), frame.ToWorld(new Vector3(2, 0, 2)), frame, 1f);
        var inside = Piece(1, 110, Vector3.Zero);
        var outside = new WorldPiece(2, 110, new Pose(frame.ToWorld(new Vector3(5, 0, 0)), Quaternion.Identity), true, 0, 0);

        Assert.Equal(new[] { 1 }, Selection.Pieces(new[] { inside, outside }, box).Select(p => p.Id));
    }

    [Fact]
    public void Select_TakesBeltsOnlyWhenBothEndsAreInside()
    {
        var frame = SurfaceFrame.At(Base, Vector3.Zero, Vector3.UnitZ);
        var box = SelectionBox.Between(frame.ToWorld(new Vector3(-2, 0, -2)), frame.ToWorld(new Vector3(2, 0, 2)), frame, 1f);
        var both = new WorldBelt(frame.ToWorld(new Vector3(-1, 0, 0)), Vector3.UnitX, frame.ToWorld(new Vector3(1, 0, 0)), Vector3.UnitX);
        var half = new WorldBelt(frame.ToWorld(new Vector3(-1, 0, 0)), Vector3.UnitX, frame.ToWorld(new Vector3(9, 0, 0)), Vector3.UnitX);

        Assert.Single(Selection.Belts(new[] { both, half }, box));
    }

    // ---- rotate -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Rotate_TurnsPiecesAndBeltsTogether()
    {
        var copy = CopyOf(new[] { Piece(1, 110, new Vector3(1, 0, 0)) }, new[] { Belt(new Vector3(1, 0, 0), new Vector3(2, 0, 0)) });

        var turned = copy.Rotated(180f);

        Assert.True(Vector3.Distance(new Vector3(-1, 0, 0), turned.Pieces[0].Relative.Position) < 1e-4f);
        Assert.True(Vector3.Distance(new Vector3(-2, 0, 0), turned.Belts[0].End) < 1e-4f);
    }

    [Fact]
    public void Rotate_TurnsBeltDirections()
    {
        var copy = CopyOf(Array.Empty<WorldPiece>(), new[] { Belt(Vector3.Zero, Vector3.UnitX) });

        var turned = copy.Rotated(180f);

        Assert.True(Vector3.Distance(-Vector3.UnitX, turned.Belts[0].StartDirection) < 1e-4f);
    }

    // ---- paste plan -------------------------------------------------------------------------------------------------

    [Fact]
    public void Paste_GivesEveryPieceANewId()
    {
        var copy = CopyOf(new[] { Piece(1, 110, Vector3.Zero), Piece(2, 85, Vector3.UnitY, support: 1) });
        var next = 500;

        var plan = PastePlan.For(copy, Anchor, () => next++);

        Assert.Equal(new[] { 500, 501 }, plan.Pieces.Select(p => p.Id).OrderBy(id => id));
    }

    [Fact]
    public void Paste_RelinksSupportsToTheNewIds()
    {
        var copy = CopyOf(new[] { Piece(1, 110, Vector3.Zero), Piece(2, 85, Vector3.UnitY, support: 1) });
        var next = 500;

        var plan = PastePlan.For(copy, Anchor, () => next++);

        var floor = plan.Pieces.Single(p => p.Kind == 110);
        Assert.Equal(floor.Id, plan.Pieces.Single(p => p.Kind == 85).Support);
    }

    [Fact]
    public void Paste_PlacesSupportsBeforeWhatTheyHold()
    {
        // Listed holder first: the plan must still put the support first.
        var copy = CopyOf(new[] { Piece(2, 85, Vector3.UnitY, support: 1), Piece(1, 110, Vector3.Zero) });
        var next = 1;

        var plan = PastePlan.For(copy, Anchor, () => next++);

        Assert.Equal(110, plan.Pieces[0].Kind);
    }

    [Fact]
    public void Paste_SupportCycle_Throws()
    {
        var copy = CopyOf(new[] { Piece(1, 110, Vector3.Zero, support: 2), Piece(2, 110, Vector3.UnitX, support: 1) });
        var next = 1;

        Assert.Throws<InvalidDataException>(() => PastePlan.For(copy, Anchor, () => next++));
    }

    [Fact]
    public void Paste_MovesEverythingWithTheAnchor()
    {
        var copy = CopyOf(new[] { Piece(1, 110, new Vector3(3, 0, 4)) }, new[] { Belt(Vector3.Zero, new Vector3(2, 0, 0)) });
        var target = new Pose(new Vector3(-50, 1990, 7), Quaternion.Identity);
        var next = 1;

        var plan = PastePlan.For(copy, target, () => next++);

        Assert.Equal(new Vector3(-47, 1990, 11), plan.Pieces[0].Pose.Position);
        Assert.Equal(new Vector3(-48, 1990, 7), plan.Belts[0].End);
    }

    // ---- clipboard file ---------------------------------------------------------------------------------------------

    [Fact]
    public void File_RoundTrips()
    {
        var copy = CopyOf(new[] { Piece(1, 110, new Vector3(3, 0, 4), style: 2), Piece(2, 85, Vector3.UnitY, support: 1, placedPose: false) },
            new[] { Belt(Vector3.Zero, new Vector3(2, 0, 0)) }).Rotated(90f);

        var back = ClipboardFile.Read(ClipboardFile.Write(copy));

        Assert.Equal(ClipboardFile.Write(copy), ClipboardFile.Write(back));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{\"version\":99,\"pieces\":[],\"belts\":[]}")]
    public void File_Unreadable_Throws(string text)
    {
        Assert.Throws<InvalidDataException>(() => ClipboardFile.Read(text));
    }

    [Fact]
    public void File_KindOutOfRange_Throws()
    {
        var text = ClipboardFile.Write(CopyOf(new[] { Piece(1, 110, Vector3.Zero) })).Replace("\"kind\":110", "\"kind\":500");

        Assert.Throws<InvalidDataException>(() => ClipboardFile.Read(text));
    }

    [Fact]
    public void File_NonFiniteNumber_Throws()
    {
        var text = ClipboardFile.Write(CopyOf(new[] { Piece(1, 110, new Vector3(3, 0, 4)) })).Replace("\"x\":3", "\"x\":\"NaN\"");

        Assert.Throws<InvalidDataException>(() => ClipboardFile.Read(text));
    }

    [Fact]
    public void File_SupportPointingNowhere_Throws()
    {
        var text = ClipboardFile.Write(CopyOf(new[] { Piece(1, 110, Vector3.Zero), Piece(2, 85, Vector3.UnitY, support: 1) }))
            .Replace("\"support\":1", "\"support\":42");

        Assert.Throws<InvalidDataException>(() => ClipboardFile.Read(text));
    }

    // ---- machine yaw ------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(0f)]
    [InlineData(37f)]
    [InlineData(-120f)]
    [InlineData(250f)]
    public void Yaw_RecoversTheAngleThatProducesAForward(float yaw)
    {
        // A stand-in for the game's DepotPose: forward turned by yaw about the up axis, in some fixed sense.
        var up = Vector3.Normalize(new Vector3(0.2f, 1f, -0.1f));
        var zero = Vector3.Normalize(Vector3.Cross(up, Vector3.UnitX));
        Vector3 At(float degrees) => Vector3.Transform(zero, Quaternion.CreateFromAxisAngle(up, -degrees * MathF.PI / 180f));

        var solved = YawSolver.Solve(At(0), At(90), At(yaw), up);

        Assert.Equal(0f, Delta(yaw, solved), 2);
    }

    private static float Delta(float a, float b)
    {
        var d = (a - b) % 360f;
        return d > 180f ? d - 360f : d < -180f ? d + 360f : d;
    }
}
