using MoonWorldEdit.Core;

namespace MoonWorldEdit.Core.Tests;

// What the command line accepts: /wand, /copy, /rotate <degrees>, /paste, /undo.
public sealed class CommandTests
{
    [Theory]
    [InlineData("/wand", CommandKind.Wand)]
    [InlineData("/copy", CommandKind.Copy)]
    [InlineData("/paste", CommandKind.Paste)]
    [InlineData("/undo", CommandKind.Undo)]
    [InlineData("  /COPY  ", CommandKind.Copy)]
    public void Parse_KnowsEachCommand(string text, CommandKind kind)
    {
        Assert.Equal(kind, Command.Parse(text).Kind);
    }

    [Theory]
    [InlineData("/rotate 90", 90f)]
    [InlineData("/rotate -45", -45f)]
    [InlineData("/rotate 22.5", 22.5f)]
    public void Parse_RotateTakesDegrees(string text, float degrees)
    {
        Assert.Equal(degrees, Command.Parse(text).Degrees);
    }

    [Theory]
    [InlineData("")]
    [InlineData("copy")]
    [InlineData("/fly")]
    [InlineData("/rotate")]
    [InlineData("/rotate ninety")]
    [InlineData("/rotate NaN")]
    [InlineData("/rotate 1e9")]
    [InlineData("/copy now")]
    public void Parse_RejectsWhatItDoesNotKnow(string text)
    {
        var command = Command.Parse(text);

        Assert.Equal(CommandKind.Invalid, command.Kind);
        Assert.False(string.IsNullOrWhiteSpace(command.Error));
    }
}
