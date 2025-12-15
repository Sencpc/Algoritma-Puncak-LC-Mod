using HarmonyLib;

namespace AlgoritmaPuncakMod.AI
{
    [HarmonyPatch]
    internal static class SandSpiderPatches
    {
        // Override vanilla Update to let behavior tree fully control SandSpider
        [HarmonyPatch(typeof(SandSpiderAI), "Update")]
        [HarmonyPrefix]
        private static bool Prefix_OverrideUpdate(SandSpiderAI __instance)
        {
            // Let vanilla handle death state
            if (__instance.isEnemyDead)
            {
                return true;
            }

            // Behavior tree handles all movement, web mechanics, and decision-making
            // Skip vanilla state machine entirely
            return false;
        }
    }
}
