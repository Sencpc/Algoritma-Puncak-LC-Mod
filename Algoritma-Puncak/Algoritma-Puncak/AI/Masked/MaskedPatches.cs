using HarmonyLib;

namespace AlgoritmaPuncakMod.AI
{
    [HarmonyPatch]
    internal static class MaskedPatches
    {
        // Override vanilla Update to let behavior tree fully control Masked
        [HarmonyPatch(typeof(MaskedPlayerEnemy), "Update")]
        [HarmonyPrefix]
        private static bool Prefix_OverrideUpdate(MaskedPlayerEnemy __instance)
        {
            // Let vanilla handle death state
            if (__instance.isEnemyDead)
            {
                return true;
            }

            // Behavior tree handles all movement, mimicry, and decision-making
            // Skip vanilla state machine entirely
            return false;
        }
    }
}
