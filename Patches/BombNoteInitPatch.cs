using HarmonyLib;

namespace ProxyNote.Patches
{
    [HarmonyPatch(typeof(BombNoteController), nameof(BombNoteController.Init))]
    internal static class BombNoteInitPatch
    {
        private static void Postfix(
            BombNoteController __instance,
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
