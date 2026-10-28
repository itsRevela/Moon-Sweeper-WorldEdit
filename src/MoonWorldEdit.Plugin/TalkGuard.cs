using HarmonyLib;
using MoonCleaners;

namespace MoonWorldEdit.Plugin;

// The game's crew chat (MoonTalk) opens on Enter, so pressing Enter to run a console command also opened the chat
// (seen in-game). Its Update is skipped while the console is open and until the key that closed it is released.
[HarmonyPatch(typeof(MoonTalk), nameof(MoonTalk.Update))]
internal static class TalkGuard
{
    private static bool Prefix() => !CommandLine.Busy;
}
