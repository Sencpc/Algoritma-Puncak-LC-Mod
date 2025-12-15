using HarmonyLib;

namespace AlgoritmaPuncakMod.AI
{
    [HarmonyPatch]
    internal static class CoilheadPatches
    {
        // Override vanilla Update to let behavior tree fully control Coilhead
        [HarmonyPatch(typeof(SpringManAI), "Update")]
        [HarmonyPrefix]
        private static bool Prefix_OverrideUpdate(SpringManAI __instance)
        {
            // Let vanilla handle death state
            if (__instance.isEnemyDead)
            {
                return true;
            }

            // Behavior tree handles all movement, observation, and decision-making
            // Skip vanilla state machine entirely
            return false;
        }
    }
}
