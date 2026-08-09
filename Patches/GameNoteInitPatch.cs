using HarmonyLib;

namespace ProxyNote.Patches
{
    [HarmonyPatch(typeof(GameNoteController), nameof(GameNoteController.Init))]
    internal static class GameNoteInitPatch
    {
        private static void Postfix(
            GameNoteController __instance,
            NoteData noteData,
            ref NoteSpawnData noteSpawnData)
        {
            if (!PluginConfig.Instance.Enabled)
            {
                return;
            }

            ProxyNoteVisualController visual =
                __instance.GetComponent<ProxyNoteVisualController>() ??
                __instance.gameObject.AddComponent<ProxyNoteVisualController>();

            visual.Initialize(__instance, noteData, in noteSpawnData);
        }
    }
}
