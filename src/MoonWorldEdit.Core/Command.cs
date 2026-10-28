using System;
using System.Globalization;

namespace MoonWorldEdit.Core;

public enum CommandKind
{
    Invalid,
    Wand,
    Copy,
    Rotate,
    Paste,
    PasteRelative,
    Undo,
    Redo,
}

/// <summary>A line typed into the command line. Invalid lines carry a message for the player.</summary>
public sealed record Command(CommandKind Kind, float Degrees = 0f, string Error = "")
{
    public const float MaxDegrees = 3600f;
    public const string Help = "Commands: /wand, /copy, /rotate <degrees>, /paste, /paste-relative, /undo, /redo";

    public static Command Parse(string? text)
    {
        var parts = (text ?? "").Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !parts[0].StartsWith("/", StringComparison.Ordinal))
            return Invalid(Help);
        var name = parts[0].ToLowerInvariant();
        if (name == "/rotate")
        {
            if (parts.Length != 2 || !float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var degrees)
                || !float.IsFinite(degrees) || Math.Abs(degrees) > MaxDegrees)
                return Invalid("Usage: /rotate <degrees>, e.g. /rotate 90");
            return new Command(CommandKind.Rotate, degrees);
        }
        var kind = name switch
        {
            "/wand" => CommandKind.Wand,
            "/copy" => CommandKind.Copy,
            "/paste" => CommandKind.Paste,
            "/paste-relative" => CommandKind.PasteRelative,
            "/undo" => CommandKind.Undo,
            "/redo" => CommandKind.Redo,
            _ => CommandKind.Invalid,
        };
        if (kind == CommandKind.Invalid)
            return Invalid($"Unknown command {parts[0]}. {Help}");
        return parts.Length == 1 ? new Command(kind) : Invalid($"{parts[0]} takes nothing after it.");
    }

    private static Command Invalid(string error) => new(CommandKind.Invalid, 0f, error);
}
