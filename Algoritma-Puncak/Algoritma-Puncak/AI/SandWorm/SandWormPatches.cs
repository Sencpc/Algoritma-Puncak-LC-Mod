using HarmonyLib;
using UnityEngine;

namespace AlgoritmaPuncakMod.AI
{
    [HarmonyPatch]
    internal static class SandWormPatches
    {
        // Register noise detections to heatmap so SandWorm marks and tracks hot areas
        [HarmonyPatch(typeof(SandWormAI), nameof(SandWormAI.DetectNoise))]
        [HarmonyPostfix]
        private static void Postfix_DetectNoise(SandWormAI __instance, Vector3 noisePosition, float noiseLoudness, int timesNoisePlayedInOneSpot)
        {
            // Skip if dead or noise is spam/filtered
            if (__instance.isEnemyDead || timesNoisePlayedInOneSpot > 15)
            {
                return;
            }

            // Calculate effective loudness (same logic as vanilla might use)
            float effectiveLoudness = noiseLoudness;
            if (Physics.Linecast(__instance.transform.position, noisePosition, 256))
            {
                effectiveLoudness /= 2f;
            }

            // Only register significant noise
            if (effectiveLoudness < 0.25f)
            {
                return;
            }

            // Register to heatmap with strength based on loudness
            // This creates persistent heat that makes the worm track and strike hot areas
            // Low noise: footstep-level heat (1.0)
            // Medium noise: moderate burst (2-4)
            // High noise: strong burst (5-8) that builds up for strike threshold
            float heatMagnitude = Mathf.Lerp(1f, 8f, effectiveLoudness);
            SandWormNoiseField.RegisterNoiseBurst(noisePosition, heatMagnitude);
        }
    }
}
