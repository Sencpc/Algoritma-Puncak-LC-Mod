using HarmonyLib;
using UnityEngine;

namespace AlgoritmaPuncakMod.AI
{
    [HarmonyPatch]
    internal static class MouthDogPatches
    {
        // Store previous positions for each MouthDogAI instance
        private static readonly System.Collections.Generic.Dictionary<MouthDogAI, Vector3> previousPositions = new System.Collections.Generic.Dictionary<MouthDogAI, Vector3>();

        // Override vanilla Update to let behavior tree fully control the dog
        [HarmonyPatch(typeof(MouthDogAI), "Update")]
        [HarmonyPrefix]
        private static bool Prefix_OverrideUpdate(MouthDogAI __instance)
        {
            // Let vanilla handle death/stun animations and basic state
            if (__instance.isEnemyDead)
            {
                __instance.creatureAnimator.SetLayerWeight(1, 0f);
                return false;
            }

            if (!__instance.ventAnimationFinished)
            {
                return true; // Let vanilla handle vent animation
            }

            // Handle stun layer
            if (__instance.stunNormalizedTimer > 0f)
            {
                __instance.creatureAnimator.SetLayerWeight(1, 1f);
            }
            else
            {
                __instance.creatureAnimator.SetLayerWeight(1, 0f);
            }

            // Update speed multiplier animation based on actual movement
            Vector3 prevPos;
            if (!previousPositions.TryGetValue(__instance, out prevPos))
            {
                prevPos = __instance.transform.position;
            }
            __instance.creatureAnimator.SetFloat("speedMultiplier",
                Vector3.ClampMagnitude(__instance.transform.position - prevPos, 1f).sqrMagnitude / (Time.deltaTime / 4f));
            previousPositions[__instance] = __instance.transform.position;

            // Behavior tree handles all movement and decision-making
            // Skip vanilla state machine entirely
            return false;
        }

        // Register noise detections to heatmap so dog marks and investigates hot areas
        [HarmonyPatch(typeof(MouthDogAI), nameof(MouthDogAI.DetectNoise))]
        [HarmonyPostfix]
        private static void Postfix_DetectNoise(MouthDogAI __instance, Vector3 noisePosition, float noiseLoudness, int timesNoisePlayedInOneSpot)
        {
            // Skip if stunned, in kill animation, or noise is filtered
            if (__instance.stunNormalizedTimer > 0f || __instance.isEnemyDead || timesNoisePlayedInOneSpot > 15)
            {
                return;
            }

            // Calculate effective loudness (same logic as vanilla DetectNoise)
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
            // This creates persistent heat that makes the dog investigate and linger
            // Low noise: footstep-level heat (1.0)
            // Medium noise: moderate burst (2-4)
            // High noise: strong burst (5-8) that persists longer
            float heatMagnitude = Mathf.Lerp(1f, 8f, effectiveLoudness);
            SandWormNoiseField.RegisterNoiseBurst(noisePosition, heatMagnitude);
        }

        // Prevent MouthDog from attacking certain enemies on collision when configured
        [HarmonyPatch(typeof(MouthDogAI), nameof(MouthDogAI.OnCollideWithEnemy))]
        [HarmonyPrefix]
        private static bool Prefix_OnCollideWithEnemy(Collider other, EnemyAI collidedEnemy)
        {
            if (collidedEnemy == null)
            {
                return true;
            }

            // If configured to ignore, honor list and skip attacking those enemies
            if (AlgoritmaPuncakMod.DogIgnoreOtherEnemies)
            {
                var tname = collidedEnemy.GetType().Name;
                if (AlgoritmaPuncakMod.DogIgnoredTypeNames.Contains(tname))
                {
                    return false;
                }
            }

            // Avoid chasing other mobs when no living players are present anywhere
            if (!AnyLivingPlayerPresent())
            {
                return false;
            }

            return true;
        }

        private static bool AnyLivingPlayerPresent()
        {
            var round = StartOfRound.Instance;
            var scripts = round?.allPlayerScripts;
            if (scripts == null)
            {
                return true; // fallback: allow
            }

            for (int i = 0; i < scripts.Length; i++)
            {
                var p = scripts[i];
                if (p != null && !p.isPlayerDead)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
