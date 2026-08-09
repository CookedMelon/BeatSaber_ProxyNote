using HarmonyLib;

namespace ProxyNote.Patches
{
    [HarmonyPatch(typeof(BurstSliderGameNoteController), nameof(BurstSliderGameNoteController.Init))]
    internal static class BurstSliderGameNoteInitPatch
    {
        private static void Postfix(
            BurstSliderGameNoteController __instance,
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
