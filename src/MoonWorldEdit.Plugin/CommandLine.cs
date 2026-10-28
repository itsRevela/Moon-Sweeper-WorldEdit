using System;
using System.Text;
using MoonCleaners;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MoonWorldEdit.Plugin;

// The command line (the game has no chat): "/" opens it, Enter runs the line, Escape closes it. While it is open the
// game's own controls are held off the way the game holds them off for its menus (the walker's uiBlocked flag, and a
// neutral input for the builder). Drawn with the game's UI kit, as Moon Mahjong's HUD is.
internal sealed class CommandLine : IDisposable
{
    private const int MaxLength = 64;
    private const float MessageSeconds = 6f;
    private static readonly (Key Key, char Char)[] Keys = BuildKeys();

    private readonly MoonWorkshopKit kit = new();
    private readonly RectTransform stage;
    private readonly TMP_Text line;
    private readonly TMP_Text message;
    private readonly TMP_Text status;
    private readonly StringBuilder text = new();
    private MoonDepotBuilder? blockedBuilder;
    private float messageUntil;

    public CommandLine(Transform parent)
    {
        stage = kit.Stage(parent, "WorldEdit command line", 90, false);
        line = kit.Text(stage, "", 40f, MoonWorkshopKit.FrameH - 110f, 900f, 40f, 26f, MoonWorkshopKit.Orange, kit.head);
        message = kit.Text(stage, "", 40f, MoonWorkshopKit.FrameH - 160f, 1400f, 40f, 22f, MoonWorkshopKit.Soft);
        status = kit.Text(stage, "", 40f, MoonWorkshopKit.FrameH - 205f, 1400f, 40f, 22f, MoonWorkshopKit.Orange);
    }

    public bool IsOpen { get; private set; }

    /// <summary>A standing line (the wand's live selection count); empty to clear. Not logged.</summary>
    public void Status(string value)
    {
        if (status.text != value)
            status.text = value;
    }

    public void Show(string value)
    {
        message.text = value;
        messageUntil = Time.unscaledTime + MessageSeconds;
        Plugin.Logger.LogInfo("WorldEdit: " + value);
    }

    /// <summary>Reads the keyboard; returns a finished line when Enter is pressed.</summary>
    public string? Update(MoonDepotBuilder? builder)
    {
        if (message.text.Length > 0 && Time.unscaledTime > messageUntil)
            message.text = "";
        var keyboard = Keyboard.current;
        if (keyboard == null)
            return null;
        if (!IsOpen)
        {
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
        line.text = IsOpen ? text + "_" : "";
        return finished;
    }

    private void Open(MoonDepotBuilder builder)
    {
        IsOpen = true;
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
