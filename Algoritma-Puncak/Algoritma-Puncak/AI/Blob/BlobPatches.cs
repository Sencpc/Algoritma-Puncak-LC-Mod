using HarmonyLib;

namespace AlgoritmaPuncakMod.AI
{
    [HarmonyPatch]
    internal static class BlobPatches
    {
        // Override vanilla Update to let behavior tree fully control Blob (Hygrodere)
        [HarmonyPatch(typeof(BlobAI), "Update")]
        [HarmonyPrefix]
        private static bool Prefix_OverrideUpdate(BlobAI __instance)
        {
            // Let vanilla handle death state
            if (__instance.isEnemyDead)
            {
                return true;
            }

            // Behavior tree handles all movement, splitting, and decision-making
            // Skip vanilla state machine entirely
            return false;
        }
    }
}
