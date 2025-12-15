using HarmonyLib;

namespace AlgoritmaPuncakMod.AI
{
    [HarmonyPatch]
    internal static class FlowermanPatches
    {
        // Override vanilla Update to let behavior tree fully control Flowerman (Bracken)
        [HarmonyPatch(typeof(FlowermanAI), "Update")]
        [HarmonyPrefix]
        private static bool Prefix_OverrideUpdate(FlowermanAI __instance)
        {
            // Let vanilla handle death state
            if (__instance.isEnemyDead)
            {
                return true;
            }

            // Behavior tree handles all movement, stalking, and decision-making
            // Skip vanilla state machine entirely
            return false;
        }
    }
}
