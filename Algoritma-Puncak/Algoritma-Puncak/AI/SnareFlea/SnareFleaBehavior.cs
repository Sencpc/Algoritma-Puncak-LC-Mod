using System;
using System.Collections.Generic;
using HarmonyLib;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace AlgoritmaPuncakMod.AI
{
    internal static class SnareFleaConditions
    {
        internal static bool NeedsAmbush(BTContext context)
        {
            return !context.Blackboard.SnareHasAmbush || context.Blackboard.SnareRelocateRequested;
        }

        internal static bool RequiresApproach(BTContext context)
        {
            if (!context.Blackboard.SnareHasAmbush)
            {
                return false;
            }

            return !SnareFleaReflection.IsClinging(context.SnareFlea);
        }

        internal static bool OnAmbushHold(BTContext context)
        {
            return context.Blackboard.SnareHasAmbush && SnareFleaReflection.IsClinging(context.SnareFlea);
        }

        internal static bool PlayerInDropZone(BTContext context)
        {
            if (!context.Blackboard.SnareDropReady)
            {
                return false;
            }

            if (!SnareFleaReflection.IsClinging(context.SnareFlea))
            {
                return false;
            }

            var playerPos = context.Blackboard.LastKnownPlayerPosition;
            if (float.IsPositiveInfinity(playerPos.x))
            {
                return false;
            }

            var anchor = context.Blackboard.SnareAmbushAnchor;
            var ceiling = context.Blackboard.SnareAmbushCeiling;
            if (float.IsPositiveInfinity(anchor.x))
            {
                return false;
            }

            Vector3 playerFlat = new Vector3(playerPos.x, 0f, playerPos.z);
            Vector3 anchorFlat = new Vector3(anchor.x, 0f, anchor.z);
            float horizontal = Vector3.Distance(playerFlat, anchorFlat);
            if (horizontal > 1.85f)
            {
                return false;
            }

            float vertical = Mathf.Abs(ceiling.y - playerPos.y);
            if (vertical < 0.7f || vertical > 6.5f)
            {
                return false;
            }

            var dropVector = (playerPos - ceiling).normalized;
            return Vector3.Dot(dropVector, Vector3.down) > 0.65f;
        }

        internal static bool InPanic(BTContext context)
        {
            return context.Blackboard.SnarePanic;
        }
    }

    internal static class SnareFleaActions
    {
        internal static BTStatus PlanAmbush(BTContext context)
        {
            if (!SnareFleaPlanner.TrySelectAmbush(context.SnareFlea, context.Blackboard, out var anchor, out var ceiling))
            {
                context.Blackboard.ClearSnareAmbush();
                return BTStatus.Failure;
            }

            context.Blackboard.SetSnareAmbush(anchor, ceiling);
            if (AlgoritmaPuncakMod.DebugInstrumentation)
            {
                AlgoritmaPuncakMod.Log?.LogInfo(string.Format("[SnareFlea] New ambush at {0:F1},{1:F1},{2:F1}", anchor.x, anchor.y, anchor.z));
            }

            context.SetActiveAction("SnarePlan");
            return BTStatus.Success;
        }

        internal static BTStatus TravelToAmbush(BTContext context)
        {
            var anchor = context.Blackboard.SnareAmbushAnchor;
            if (float.IsPositiveInfinity(anchor.x))
            {
                return BTStatus.Failure;
            }

            float distance = Vector3.Distance(context.Enemy.transform.position, anchor);
            if (distance <= 0.9f)
            {
                context.Blackboard.ResolveSnarePanic();
                context.SetActiveAction("SnareArrived");
                return BTStatus.Success;
            }

            if (NavigationHelpers.TryMoveAgent(
                context,
                anchor,
                2.5f,
                32f,
                "SnareApproach",
                4.75f,
                acceleration: 9.5f,
                stoppingDistance: 0.4f,
                allowPartialPath: true))
            {
                return BTStatus.Running;
            }

            context.Blackboard.FlagSnareRelocate();
            return BTStatus.Failure;
        }

        internal static BTStatus AttemptCeilingLatch(BTContext context)
        {
            var flea = context.SnareFlea;
            if (flea == null)
            {
                return BTStatus.Failure;
            }

            if (SnareFleaReflection.IsClinging(flea))
            {
                context.SetActiveAction("SnareClingHold");
                return BTStatus.Success;
            }

            var ceiling = context.Blackboard.SnareAmbushCeiling;
            if (float.IsPositiveInfinity(ceiling.x))
            {
                return BTStatus.Failure;
            }

            if (Vector3.Distance(context.Enemy.transform.position, context.Blackboard.SnareAmbushAnchor) > 1.5f)
            {
                return BTStatus.Failure;
            }

            flea.SwitchToHidingOnCeilingServerRpc(ceiling);
            context.SetActiveAction("SnareCling");
            return BTStatus.Running;
        }

        internal static BTStatus HoldAmbush(BTContext context)
        {
            var flea = context.SnareFlea;
            if (flea == null)
            {
                return BTStatus.Failure;
            }

            if (!SnareFleaReflection.IsClinging(flea))
            {
                context.Blackboard.FlagSnareRelocate();
                return BTStatus.Failure;
            }

            if (flea.clingingToPlayer != null)
            {
                context.Blackboard.CompleteSnareLatch();
            }

            if (context.Blackboard.DistanceToPlayer <= 2.25f)
            {
                context.Blackboard.RegisterSnareTraffic();
            }

            context.SetActiveAction("SnareHold");
            return BTStatus.Running;
        }

        internal static BTStatus ExecuteDrop(BTContext context)
        {
            var flea = context.SnareFlea;
            if (flea == null || !SnareFleaReflection.IsClinging(flea))
            {
                return BTStatus.Failure;
            }

            ulong callerId = 0UL;
            if (NetworkManager.Singleton != null)
            {
                callerId = NetworkManager.Singleton.LocalClientId;
            }
            else if (flea.NetworkObject != null)
            {
                callerId = flea.NetworkObject.OwnerClientId;
            }

            flea.TriggerCentipedeFallServerRpc(callerId);
            context.Blackboard.RegisterSnareDrop();
            context.SetActiveAction("SnareDrop");
            return BTStatus.Success;
        }

        internal static BTStatus NavigatePanic(BTContext context)
        {
            context.Blackboard.FlagSnareRelocate();
            if (!SnareFleaPlanner.TrySelectEscape(context.SnareFlea, context.Blackboard, out var exitPoint))
            {
                exitPoint = context.Enemy.transform.position + UnityEngine.Random.insideUnitSphere * 4f;
                exitPoint.y = context.Enemy.transform.position.y;
            }

            if (NavigationHelpers.TryMoveAgent(
                context,
                exitPoint,
                3.5f,
                28f,
                "SnarePanic",
                8.5f,
                acceleration: 16f,
                stoppingDistance: 0.35f,
                allowPartialPath: true))
            {
                if (Vector3.Distance(context.Enemy.transform.position, exitPoint) <= 1.2f)
                {
                    context.Blackboard.ResolveSnarePanic();
                }

                return BTStatus.Running;
            }

            context.Blackboard.ResolveSnarePanic();
            return BTStatus.Failure;
        }

        internal static BTStatus Roam(BTContext context)
        {
            var wander = context.Enemy.transform.position + UnityEngine.Random.insideUnitSphere * 3f;
            wander.y = context.Enemy.transform.position.y;
            if (NavigationHelpers.TryMoveAgent(
                context,
                wander,
                3f,
                18f,
                "SnareRoam",
                3.25f,
                acceleration: 6f,
                stoppingDistance: 0.4f,
                allowPartialPath: true))
            {
                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }
    }

    internal static class SnareFleaPlanner
    {
        private struct AmbushCandidate
        {
            internal Vector3 Anchor;
            internal Vector3 Ceiling;
            internal float Score;
        }

        private sealed class CacheEntry
        {
            internal readonly List<AmbushCandidate> Candidates = new List<AmbushCandidate>();
            internal float NextRefresh;
        }

        private static readonly Dictionary<int, CacheEntry> Cache = new Dictionary<int, CacheEntry>();
        private static readonly Vector3[] CorridorChecks =
        {
            Vector3.forward,
            -Vector3.forward,
            Vector3.right,
            -Vector3.right
        };
        private static readonly Vector3[] VisibilityDirections =
        {
            (Vector3.forward - Vector3.up).normalized,
            (-Vector3.forward - Vector3.up).normalized,
            (Vector3.right - Vector3.up).normalized,
            (-Vector3.right - Vector3.up).normalized
        };
        private static readonly string[] MansionInteriorAliases = { "Rend", "Dine", "Mansion", "85", "7" };
        private static readonly string[] LevelLabelMembers = { "PlanetName", "planetName", "levelName", "sceneName", "riskLevel", "name" };
        private static Light[] _lights;
        private static float _nextLightRefresh;
        private static bool _cachedMansionBias;
        private static bool _hasInteriorSample;
        private static float _nextInteriorSample;

        internal static bool TrySelectAmbush(CentipedeAI flea, AIBlackboard board, out Vector3 anchor, out Vector3 ceiling)
        {
            anchor = Vector3.positiveInfinity;
            ceiling = Vector3.positiveInfinity;
            if (flea == null)
            {
                return false;
            }

            if (!Cache.TryGetValue(flea.GetInstanceID(), out var entry))
            {
                entry = new CacheEntry();
                Cache[flea.GetInstanceID()] = entry;
                entry.NextRefresh = 0f;
            }

            if (Time.time >= entry.NextRefresh)
            {
                BuildCandidates(flea, entry);
                entry.NextRefresh = Time.time + 8f;
            }

            if (entry.Candidates.Count == 0)
            {
                return false;
            }

            float bestScore = float.MinValue;
            AmbushCandidate best = default;
            foreach (var candidate in entry.Candidates)
            {
                float score = candidate.Score;
                if (!float.IsPositiveInfinity(board.LastKnownPlayerPosition.x))
                {
                    float dist = Vector3.Distance(candidate.Anchor, board.LastKnownPlayerPosition);
                    score += Mathf.Lerp(1.75f, -0.5f, Mathf.InverseLerp(3f, 24f, dist));
                }
                else
                {
                    float dist = Vector3.Distance(candidate.Anchor, board.TerritoryCenter);
                    score += Mathf.Lerp(0.8f, 0.1f, Mathf.InverseLerp(4f, 25f, dist));
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            if (bestScore <= float.MinValue)
            {
                return false;
            }

            anchor = best.Anchor;
            ceiling = best.Ceiling;
            return true;
        }

        internal static bool TrySelectEscape(CentipedeAI flea, AIBlackboard board, out Vector3 escapeTarget)
        {
            escapeTarget = Vector3.positiveInfinity;
            if (flea == null)
            {
                return false;
            }

            if (!Cache.TryGetValue(flea.GetInstanceID(), out var entry) || entry.Candidates.Count == 0)
            {
                return false;
            }

            float worstExposure = float.MinValue;
            foreach (var candidate in entry.Candidates)
            {
                float dist = float.IsPositiveInfinity(board.LastKnownPlayerPosition.x)
                    ? Vector3.Distance(candidate.Anchor, board.TerritoryCenter)
                    : Vector3.Distance(candidate.Anchor, board.LastKnownPlayerPosition);
                float score = dist;
                if (score > worstExposure)
                {
                    worstExposure = score;
                    escapeTarget = candidate.Anchor;
                }
            }

            return !float.IsPositiveInfinity(escapeTarget.x);
        }

        private static void BuildCandidates(CentipedeAI flea, CacheEntry entry)
        {
            entry.Candidates.Clear();
            bool mansionBias = ShouldBiasForMansionInterior();
            var nodes = flea.allAINodes;
            if (nodes == null || nodes.Length == 0)
            {
                return;
            }

            int geometryMask = StartOfRound.Instance != null && StartOfRound.Instance.collidersAndRoomMask != 0
                ? StartOfRound.Instance.collidersAndRoomMask
                : LayerMask.GetMask("Default", "Environment", "Room");

            foreach (var node in nodes)
            {
                if (node == null)
                {
                    continue;
                }

                if (!(node is GameObject nodeObject))
                {
                    continue;
                }

                var anchor = nodeObject.transform.position;
                if (!NavMesh.SamplePosition(anchor, out var navHit, 1.5f, NavMesh.AllAreas))
                {
                    continue;
                }

                anchor = navHit.position;
                if (!TryGetCeilingPoint(anchor, geometryMask, out var ceiling, out var clearance))
                {
                    continue;
                }

                float geometryScore = EvaluateGeometry(anchor, geometryMask);
                float visibilityScore = EvaluateVisibility(ceiling, geometryMask);
                float darknessScore = EvaluateDarkness(anchor);
                float total = geometryScore + visibilityScore + darknessScore;
                if (mansionBias)
                {
                    if (clearance > 6.75f)
                    {
                        continue;
                    }

                    total += EvaluateDoorwayAffinity(anchor, geometryMask);
                    total += EvaluateCeilingClearancePreference(clearance);
                }
                if (total <= 0.75f)
                {
                    continue;
                }

                entry.Candidates.Add(new AmbushCandidate
                {
                    Anchor = anchor,
                    Ceiling = ceiling,
                    Score = total
                });
            }
        }

        private static bool TryGetCeilingPoint(Vector3 anchor, int mask, out Vector3 ceiling, out float clearance)
        {
            var origin = anchor + Vector3.up * 0.5f;
            if (Physics.Raycast(origin, Vector3.up, out var hit, 10f, mask, QueryTriggerInteraction.Ignore))
            {
                if (hit.distance >= 1.75f)
                {
                    clearance = hit.distance;
                    ceiling = hit.point - Vector3.up * 0.6f;
                    return true;
                }
            }

            ceiling = Vector3.positiveInfinity;
            clearance = 0f;
            return false;
        }

        private static float EvaluateGeometry(Vector3 anchor, int mask)
        {
            float score = 0f;
            var origin = anchor + Vector3.up * 0.45f;
            foreach (var dir in CorridorChecks)
            {
                if (Physics.Raycast(origin, dir, out var hit, 2.25f, mask, QueryTriggerInteraction.Ignore))
                {
                    float closeness = 1f - Mathf.Clamp01(hit.distance / 2.25f);
                    score += closeness * 0.6f;
                }
            }

            return Mathf.Clamp(score, 0f, 2f);
        }

        private static float EvaluateVisibility(Vector3 ceiling, int mask)
        {
            float total = 0f;
            foreach (var dir in VisibilityDirections)
            {
                if (Physics.Raycast(ceiling, dir, out var hit, 8f, mask, QueryTriggerInteraction.Ignore))
                {
                    total += hit.distance;
                }
                else
                {
                    total += 8f;
                }
            }

            float avg = total / VisibilityDirections.Length;
            return Mathf.Clamp01((6f - avg) / 6f) * 2f;
        }

        private static float EvaluateDarkness(Vector3 anchor)
        {
            RefreshLights();
            if (_lights == null || _lights.Length == 0)
            {
                return 1f;
            }

            float closest = float.MaxValue;
            foreach (var light in _lights)
            {
                if (light == null || !light.enabled || light.intensity <= 0.1f)
                {
                    continue;
                }

                float range = Mathf.Max(4f, light.range);
                float distance = Vector3.Distance(anchor, light.transform.position);
                if (distance > range)
                {
                    continue;
                }

                float normalized = distance / range;
                if (normalized < closest)
                {
                    closest = normalized;
                }
            }

            if (closest == float.MaxValue)
            {
                return 1.5f;
            }

            return Mathf.Clamp01(1f - closest) * 0.8f;
        }

        private static void RefreshLights()
        {
            if (Time.time < _nextLightRefresh)
            {
                return;
            }

            _nextLightRefresh = Time.time + 10f;
            _lights = UnityEngine.Object.FindObjectsOfType<Light>();
        }

        private static float EvaluateDoorwayAffinity(Vector3 anchor, int mask)
        {
            var origin = anchor + Vector3.up * 1.35f;
            bool leftWall = Physics.Raycast(origin, -Vector3.right, out var leftHit, 1.4f, mask, QueryTriggerInteraction.Ignore);
            bool rightWall = Physics.Raycast(origin, Vector3.right, out var rightHit, 1.4f, mask, QueryTriggerInteraction.Ignore);
            bool forwardClear = !Physics.Raycast(origin, Vector3.forward, 1.65f, mask, QueryTriggerInteraction.Ignore);
            bool backwardClear = !Physics.Raycast(origin, -Vector3.forward, 1.65f, mask, QueryTriggerInteraction.Ignore);

            float widthScore = 0f;
            if (leftWall && rightWall)
            {
                float width = leftHit.distance + rightHit.distance;
                float deviation = Mathf.Abs(width - 2.3f);
                widthScore = Mathf.Clamp01(1.2f - deviation);
            }

            float laneScore = 0f;
            if (forwardClear)
            {
                laneScore += 0.6f;
            }
            if (backwardClear)
            {
                laneScore += 0.6f;
            }

            return Mathf.Clamp(widthScore * 1.2f + laneScore, 0f, 2.2f);
        }

        private static float EvaluateCeilingClearancePreference(float clearance)
        {
            if (clearance <= 1.8f)
            {
                return 0f;
            }

            float preferred = 3.2f;
            float reward = Mathf.Clamp01(1.4f - Mathf.Abs(clearance - preferred)) * 1.3f;
            float penalty = clearance > 4.7f ? Mathf.Clamp01((clearance - 4.7f) / 2.2f) * 1.2f : 0f;
            return reward - penalty;
        }

        private static bool ShouldBiasForMansionInterior()
        {
            if (_hasInteriorSample && Time.time < _nextInteriorSample)
            {
                return _cachedMansionBias;
            }

            _cachedMansionBias = EvaluateInteriorType();
            _hasInteriorSample = true;
            _nextInteriorSample = Time.time + 5f;
            return _cachedMansionBias;
        }

        private static bool EvaluateInteriorType()
        {
            var round = StartOfRound.Instance;
            var level = round?.currentLevel;
            if (level == null)
            {
                return false;
            }

            foreach (var alias in MansionInteriorAliases)
            {
                if (LevelMatchesAlias(level, alias))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool LevelMatchesAlias(object level, string alias)
        {
            if (level == null || string.IsNullOrWhiteSpace(alias))
            {
                return false;
            }

            foreach (var member in LevelLabelMembers)
            {
                string label = ReadLevelLabel(level, member);
                if (string.IsNullOrWhiteSpace(label))
                {
                    continue;
                }

                if (label.IndexOf(alias, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static string ReadLevelLabel(object level, string member)
        {
            if (level == null || string.IsNullOrWhiteSpace(member))
            {
                return null;
            }

            var type = level.GetType();
            var field = AccessTools.Field(type, member);
            if (field != null && field.GetValue(level) is string fieldValue && !string.IsNullOrWhiteSpace(fieldValue))
            {
                return fieldValue;
            }

            var property = AccessTools.Property(type, member);
            if (property?.GetGetMethod(true) != null)
            {
                var value = property.GetValue(level);
                if (value is string propertyValue)
                {
                    return propertyValue;
                }

                return value?.ToString();
            }

            return null;
        }
    }

    internal static class SnareFleaReflection
    {
        private static readonly FieldInfo ClingingField = AccessTools.Field(typeof(CentipedeAI), "clingingToCeiling");

        internal static bool IsClinging(CentipedeAI flea)
        {
            if (flea == null || ClingingField == null)
            {
                return false;
            }

            try
            {
                return ClingingField.GetValue(flea) is bool value && value;
            }
            catch
            {
                return false;
            }
        }
    }
}
