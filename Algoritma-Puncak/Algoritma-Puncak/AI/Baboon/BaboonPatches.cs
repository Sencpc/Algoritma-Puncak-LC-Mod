using HarmonyLib;

namespace AlgoritmaPuncakMod.AI
{
    [HarmonyPatch]
    internal static class BaboonPatches
    {
        // Override vanilla Update to let behavior tree fully control Baboon Hawk
        [HarmonyPatch(typeof(BaboonBirdAI), "Update")]
        [HarmonyPrefix]
        private static bool Prefix_OverrideUpdate(BaboonBirdAI __instance)
        {
            // Let vanilla handle death state
            if (__instance.isEnemyDead)
            {
                return true;
            }

            // Behavior tree handles all movement, pack dynamics, and decision-making
            // Skip vanilla state machine entirely
            return false;
        }
    }
}
