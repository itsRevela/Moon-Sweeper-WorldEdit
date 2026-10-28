using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Il2CppInterop.Runtime.Injection;

namespace MoonWorldEdit.Plugin;

// WorldEdit for Moon Sweepers: /wand, /copy, /rotate, /paste and /undo (see docs/design.md). Runs on the patched
// BepInEx that Moon Sweeper Mahjong installs.
[BepInPlugin(Guid, Name, Version)]
public sealed class Plugin : BasePlugin
{
    public const string Guid = "itsrevela.moonsweepers.worldedit";
    public const string Name = "Moon WorldEdit";
    public const string Version = "0.1.0";

    internal static ManualLogSource Logger { get; private set; } = null!;

    public override void Load()
    {
        Logger = Log;
        ClassInjector.RegisterTypeInIl2Cpp<WorldEditor>();
        AddComponent<WorldEditor>();
        new Harmony(Guid).PatchAll(typeof(Plugin).Assembly);
        Logger.LogInfo($"{Name} {Version} loaded");
    }
}
