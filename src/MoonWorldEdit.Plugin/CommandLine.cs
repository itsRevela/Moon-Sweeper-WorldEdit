using System;
using System.Text;
using MoonCleaners;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace MoonWorldEdit.Plugin;

// The command line: "/" opens it, Enter runs the line, Escape closes it. While it is open the game's controls are
// held off the way its crew chat does (MoonInput.Typing: typing no longer moves the player, seen in-game), plus the
// walker's uiBlocked flag and a neutral input for the builder; TalkGuard keeps Enter from opening the chat. Drawn as a console (requested): a dark see-through strip in the top-left corner, a
// "> " prompt, and the last few messages under it like a log, fading after a few seconds when it is closed.
internal sealed class CommandLine : IDisposable
{
    private const int MaxLength = 64;
    private const float Left = 24f, Top = 24f, Width = 900f, Pad = 12f;
    private const float LineHeight = 28f, FontSize = 20f;
    private const int HistoryLines = 5;
    private const float MessageSeconds = 8f;
    private static readonly Color Back = new(0.04f, 0.05f, 0.07f, 0.72f);
    private static readonly (Key Key, char Char)[] Keys = BuildKeys();

    private readonly MoonWorkshopKit kit = new();
    private readonly RectTransform stage;
    private readonly Image back;
    private readonly TMP_Text line;
    private readonly TMP_Text[] history = new TMP_Text[HistoryLines];
    private readonly TMP_Text status;
    private readonly System.Collections.Generic.List<(string Text, float At)> messages = new();
    private readonly StringBuilder text = new();
    private MoonDepotBuilder? blockedBuilder;

    public CommandLine(Transform parent)
    {
        stage = kit.Stage(parent, "WorldEdit command line", 120, false);
        back = new GameObject("WorldEdit console").AddComponent<Image>();
        back.transform.SetParent(stage, false);
        back.color = Back;
        back.raycastTarget = false;
        line = Row(0, MoonWorkshopKit.Orange);
        for (var i = 0; i < HistoryLines; i++)
            history[i] = Row(i + 1, MoonWorkshopKit.Soft);
        status = Row(HistoryLines + 1, MoonWorkshopKit.Orange);
        back.gameObject.SetActive(false);
        Plugin.Logger.LogInfo($"WorldEdit command line ready (fonts: {(kit.head != null ? kit.head.name : "none")}, " +
            $"{(kit.text != null ? kit.text.name : "none")})");
    }

    // One console row: left-aligned on one line, overflowing its box (the kit's text hides a line taller than its box).
    private TMP_Text Row(int index, Color color)
    {
        var t = kit.Text(stage, "", Left + Pad, Top + Pad + index * LineHeight, Width - 2 * Pad, LineHeight, FontSize, color,
            kit.text, TextAlignmentOptions.Left);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    /// <summary>Whether the game's UI fonts are loaded yet (text drawn without them doesn't show).</summary>
    public static bool FontsReady()
    {
        var probe = new MoonWorkshopKit();
        return probe.head != null && probe.text != null;
    }

    public bool IsOpen { get; private set; }

    /// <summary>True while the console is open and until the key that closed it is released (TalkGuard).</summary>
    public static bool Busy { get; private set; }

    /// <summary>A standing line (the wand's live selection count); empty to clear. Not logged.</summary>
    public void Status(string value)
    {
        if (status.text != value)
            status.text = value;
    }

    public void Show(string value)
    {
        messages.Add((value, Time.unscaledTime));
        if (messages.Count > HistoryLines)
            messages.RemoveAt(0);
        Plugin.Logger.LogInfo("WorldEdit: " + value);
    }

    // Open: the prompt and the whole recent log. Closed: only messages from the last few seconds, and the status.
    private void Draw()
    {
        var now = Time.unscaledTime;
        var shown = IsOpen ? messages : messages.FindAll(m => now - m.At < MessageSeconds);
        var rows = 0;
        line.text = IsOpen ? "> " + text + "_" : "";
        if (IsOpen)
            rows++;
        for (var i = 0; i < HistoryLines; i++)
        {
            var value = i < shown.Count ? shown[i].Text : "";
            if (history[i].text != value)
                history[i].text = value;
            if (value.Length > 0)
                rows = Math.Max(rows, i + 2);
        }
        if (status.text.Length > 0)
            rows = HistoryLines + 2;
        back.gameObject.SetActive(rows > 0);
        if (rows > 0)
            MoonWorkshopKit.Place(back.rectTransform, Left, Top, Width, 2 * Pad + rows * LineHeight);
    }

    /// <summary>Reads the keyboard; returns a finished line when Enter is pressed.</summary>
    public string? Update(MoonDepotBuilder? builder)
    {
        Draw();
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return null;
        if (!IsOpen)
        {
            if (Busy && !keyboard.enterKey.isPressed && !keyboard.numpadEnterKey.isPressed && !keyboard.escapeKey.isPressed)
                Busy = false;
            if (keyboard.slashKey.wasPressedThisFrame && builder != null)
                Open(builder);
            return null;
        }

        string? finished = null;
        if (keyboard.escapeKey.wasPressedThisFrame)
            Close();
        else if (keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame)
        {
            finished = text.ToString();
            Close();
        }
        else
        {
            if (keyboard.backspaceKey.wasPressedThisFrame && text.Length > 1)
                text.Length--;
            foreach (var (key, c) in Keys)
                if (text.Length < MaxLength && keyboard[key].wasPressedThisFrame)
                    text.Append(c);
        }
        Draw();
        return finished;
    }

    private void Open(MoonDepotBuilder builder)
    {
        IsOpen = true;
        Busy = true;
        MoonInput.Typing = true; // what the game's chat sets: play input (moving, tools) ignores the keyboard
        text.Clear().Append('/');
        blockedBuilder = builder;
        if (builder.walker != null)
            builder.walker.uiBlocked = true;
        builder.TestInput = new MoonDepotBuilder.TestBuildInput(); // the builder sees no keys while we type
    }

    public void Close()
    {
        if (!IsOpen)
            return;
        IsOpen = false;
        MoonInput.Typing = false;
        line.text = "";
        if (blockedBuilder != null)
        {
            if (blockedBuilder.walker != null)
                blockedBuilder.walker.uiBlocked = false;
            blockedBuilder.TestInput = null;
            blockedBuilder = null;
        }
    }

    public void Dispose()
    {
        Close();
        if (stage != null)
            UnityEngine.Object.Destroy(stage.gameObject);
    }

    private static (Key, char)[] BuildKeys()
    {
        var keys = new System.Collections.Generic.List<(Key, char)>();
        for (var c = 'a'; c <= 'z'; c++)
            keys.Add((Key.A + (c - 'a'), c));
        for (var d = 0; d <= 9; d++)
            keys.Add((Key.Digit0 + d, (char)('0' + d)));
        keys.Add((Key.Space, ' '));
        keys.Add((Key.Minus, '-'));
        keys.Add((Key.NumpadMinus, '-'));
        keys.Add((Key.Period, '.'));
        keys.Add((Key.NumpadPeriod, '.'));
        for (var d = 0; d <= 9; d++)
            keys.Add((Key.Numpad0 + d, (char)('0' + d)));
        return keys.ToArray();
    }
}
