using HarmonyLib;

namespace AlgoritmaPuncakMod.AI
{
    [HarmonyPatch]
    internal static class ThumperPatches
    {
        // Override vanilla Update to let behavior tree fully control Thumper
        [HarmonyPatch(typeof(CrawlerAI), "Update")]
        [HarmonyPrefix]
        private static bool Prefix_OverrideUpdate(CrawlerAI __instance)
        {
            // Let vanilla handle death state
            if (__instance.isEnemyDead)
            {
                return true;
            }

            // Behavior tree handles all movement, lunging, and decision-making
            // Skip vanilla state machine entirely
            return false;
        }
    }
}
