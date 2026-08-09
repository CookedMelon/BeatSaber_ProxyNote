using System;
using System.Reflection;
using HarmonyLib;

namespace ProxyNote.Patches
{
    [HarmonyPatch]
    internal static class NoteCutEffectScopePatch
    {
        [ThreadStatic]
        private static int _replacementCutDepth;

        internal static bool IsReplacementCut => _replacementCutDepth > 0;

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                typeof(NoteCutCoreEffectsSpawner),
                "SpawnNoteCutEffect");
        }

        private static void Prefix(NoteController noteController, out bool __state)
        {
            __state = ProxyNoteVisualController.IsReplacing(noteController);
            if (__state)
            {
                _replacementCutDepth++;
            }
        }

        private static Exception Finalizer(Exception __exception, bool __state)
        {
            if (__state)
            {
                _replacementCutDepth--;
            }

            return __exception;
        }
    }

    [HarmonyPatch(typeof(NoteDebrisSpawner), nameof(NoteDebrisSpawner.SpawnDebris))]
    internal static class NoteDebrisPatch
    {
        private static bool Prefix()
        {
            return !PluginConfig.Instance.SuppressVanillaDebris ||
                   !NoteCutEffectScopePatch.IsReplacementCut;
        }
    }
}
