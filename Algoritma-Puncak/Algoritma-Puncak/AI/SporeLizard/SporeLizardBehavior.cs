using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;
using AlgoritmaPuncakMod.AI;

namespace AlgoritmaPuncakMod.AI.SporeLizard
{
    internal static class SporeLizardConditions
    {
        private const float CornerDistance = 4.5f;

        internal static bool Cornered(BTContext context)
        {
            var lizard = context.SporeLizard;
            if (lizard == null)
            {
                return false;
            }

            return context.Blackboard.SporeCornered(lizard.transform.position, context.Blackboard.DistanceToPlayer, CornerDistance);
        }

        internal static bool ShouldIntimidate(BTContext context)
        {
            var lizard = context.SporeLizard;
            if (lizard == null || !context.Blackboard.PlayerVisible)
            {
                return false;
            }

            if (context.Blackboard.DistanceToPlayer > 12f)
            {
                return false;
            }

            if (!context.Blackboard.SporeReadyToWarn())
            {
                return false;
            }

            var playerPos = context.Blackboard.LastKnownPlayerPosition;
            if (float.IsPositiveInfinity(playerPos.x))
            {
                return false;
            }

            var toLizard = lizard.transform.position - playerPos;
            if (toLizard.sqrMagnitude < 0.5f)
            {
                return false;
            }

            toLizard.Normalize();
            return Vector3.Dot(context.Blackboard.LastKnownPlayerForward, toLizard) > 0.45f;
        }

        internal static bool NeedsOcclusion(BTContext context)
        {
            var lizard = context.SporeLizard;
            return lizard != null && context.Blackboard.SporeNeedsOcclusion();
        }
    }

    internal static class SporeLizardActions
    {
        internal static BTStatus ExecuteCorneredBurst(BTContext context)
        {
            var lizard = context.SporeLizard;
            if (lizard == null)
            {
                return BTStatus.Failure;
            }

            FaceThreat(lizard, context.Blackboard, context.DeltaTime);
            SporeLizardReflection.TryBite(lizard);
            SporeLizardReflection.TryTriggerSporeBurst(lizard);

            var escapeTarget = ComputeEscapeVector(context, aggressive: true);
            if (NavigationHelpers.TryMoveAgent(
                context,
                escapeTarget,
                2.5f,
                22f,
                "SporeCornered",
                8.25f,
                acceleration: 16f,
                stoppingDistance: 0.25f,
                allowPartialPath: true))
            {
                context.Blackboard.ClearSporeOcclusion();
                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        internal static BTStatus ExecuteIntimidation(BTContext context)
        {
            var lizard = context.SporeLizard;
            if (lizard == null)
            {
                return BTStatus.Failure;
            }

            FaceThreat(lizard, context.Blackboard, context.DeltaTime);
            if (context.Blackboard.SporeReadyToWarn())
            {
                context.Blackboard.TriggerSporeWarning();
                SporeLizardReflection.TryPlayWarning(lizard);
            }

            var retreatTarget = ComputeEscapeVector(context, aggressive: false);
            if (NavigationHelpers.TryMoveAgent(
                context,
                retreatTarget,
                1.5f,
                14f,
                "SporeIntimidate",
                3.4f,
                acceleration: 8f,
                stoppingDistance: 0.3f,
                allowPartialPath: true))
            {
                return BTStatus.Running;
            }

            return BTStatus.Success;
        }

        internal static BTStatus SeekOcclusion(BTContext context)
        {
            var lizard = context.SporeLizard;
            if (lizard == null)
            {
                return BTStatus.Failure;
            }

            var board = context.Blackboard;
            if (!board.TryGetSporeOcclusion(out var target) || board.SporeNeedsRelocate())
            {
                if (!SporeLizardPlanner.TrySelectOcclusion(lizard, board, out target))
                {
                    board.FlagSporePathBlocked();
                    return BTStatus.Failure;
                }

                board.AssignSporeOcclusion(target);
            }

            float distance = Vector3.Distance(context.Enemy.transform.position, target);
            if (distance <= 1.15f)
            {
                board.RefreshSporeOcclusion(target);
                return BTStatus.Success;
            }

            if (NavigationHelpers.TryMoveAgent(
                context,
                target,
                2.35f,
                38f,
                "SporeOcclusion",
                5.5f,
                acceleration: 11f,
                stoppingDistance: 0.35f,
                allowPartialPath: true))
            {
                return BTStatus.Running;
            }

            board.FlagSporePathBlocked();
            return BTStatus.Failure;
        }

        internal static BTStatus PerformSkulk(BTContext context)
        {
            var lizard = context.SporeLizard;
            if (lizard == null)
            {
                return BTStatus.Failure;
            }

            var direction = DetermineSkulkVector(context);
            if (direction.sqrMagnitude < 0.01f)
            {
                return BTStatus.Failure;
            }

            float stride = Mathf.Lerp(4f, 7.5f, Mathf.Clamp01(context.Blackboard.PlayerNoiseLevel + (context.Blackboard.PlayerVisible ? 0.25f : 0f)));
            var offset = direction * stride + NavigationHelpers.GetAgentOffset(context.Enemy, 1.25f);
            var target = lizard.transform.position + offset;
            target.y = lizard.transform.position.y;

            if (NavigationHelpers.TryMoveAgent(
                context,
                target,
                1.85f,
                20f,
                "SporeSkulk",
                4.25f,
                acceleration: 8.5f,
                stoppingDistance: 0.35f,
                allowPartialPath: true))
            {
                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        private static void FaceThreat(PufferAI lizard, AIBlackboard board, float delta)
        {
            var playerPos = board.LastKnownPlayerPosition;
            if (float.IsPositiveInfinity(playerPos.x))
            {
                return;
            }

            var toPlayer = playerPos - lizard.transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude < 0.01f)
            {
                return;
            }

            var desired = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
            lizard.transform.rotation = Quaternion.Slerp(lizard.transform.rotation, desired, Mathf.Clamp01(delta * 6f));
        }

        private static Vector3 ComputeEscapeVector(BTContext context, bool aggressive)
        {
            var playerPos = context.Blackboard.LastKnownPlayerPosition;
            var position = context.Enemy.transform.position;
            var away = float.IsPositiveInfinity(playerPos.x)
                ? -context.Enemy.transform.forward
                : (position - playerPos);

            away.y = 0f;
            if (away.sqrMagnitude < 0.25f)
            {
                away = -context.Enemy.transform.forward;
            }

            away = away.normalized;
            var sidestep = Vector3.Cross(Vector3.up, away).normalized;
            float lateralBias = aggressive ? 0.55f : 0.3f;
            float sign = Mathf.Sign(Mathf.Sin(Time.time * 0.85f + context.Enemy.GetInstanceID() * 0.3f));
            var direction = (away + sidestep * lateralBias * sign).normalized;
            float distance = aggressive ? 8f : 4f;
            var destination = position + direction * distance;
            destination.y = position.y;
            return destination;
        }

        private static Vector3 DetermineSkulkVector(BTContext context)
        {
            var vector = context.Blackboard.GetSporeFootstepVector();
            if (vector.sqrMagnitude < 0.05f)
            {
                if (!float.IsPositiveInfinity(context.Blackboard.LastKnownPlayerPosition.x))
                {
                    vector = context.Blackboard.LastKnownPlayerPosition - context.Enemy.transform.position;
                }
                else
                {
                    vector = context.Enemy.transform.forward;
                }
            }

            vector.y = 0f;
            if (vector.sqrMagnitude < 0.05f)
            {
                vector = UnityEngine.Random.insideUnitSphere;
                vector.y = 0f;
            }

            var perpendicular = Vector3.Cross(Vector3.up, vector).normalized;
            float rotation = UnityEngine.Random.Range(-0.55f, 0.55f);
            var blended = Vector3.RotateTowards(vector.normalized, perpendicular * Mathf.Sign(rotation), Mathf.Abs(rotation) * Mathf.PI * 0.5f, 0f);
            return blended.normalized;
        }
    }

    internal static class SporeLizardPlanner
    {
        private static readonly Vector3[] CornerDirections =
        {
            Vector3.forward,
            -Vector3.forward,
            Vector3.right,
            -Vector3.right
        };

        private static readonly int OcclusionMask = LayerMask.GetMask("Default", "Environment", "Room", "Metal", "Terrain");
        private static readonly NavMeshPath SharedPath = new NavMeshPath();
        private static Light[] _lights;
        private static float _nextLightRefresh;

        internal static bool TrySelectOcclusion(PufferAI lizard, AIBlackboard board, out Vector3 target)
        {
            target = Vector3.positiveInfinity;
            if (lizard == null)
            {
                return false;
            }

            var nodes = lizard.allAINodes;
            if (nodes == null || nodes.Length == 0)
            {
                return false;
            }

            var playerPos = board.LastKnownPlayerPosition;
            bool hasPlayer = !float.IsPositiveInfinity(playerPos.x);
            float bestScore = float.MinValue;

            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                if (node == null)
                {
                    continue;
                }

                var transform = node.transform;
                if (transform == null)
                {
                    continue;
                }

                var candidate = transform.position;
                float distance = Vector3.Distance(lizard.transform.position, candidate);
                if (distance < 4f || distance > 34f)
                {
                    continue;
                }

                if (!NavMesh.SamplePosition(candidate, out var hit, 1.5f, NavMesh.AllAreas))
                {
                    continue;
                }

                candidate = hit.position;
                float coverScore = EvaluateCover(candidate, playerPos, hasPlayer);
                if (coverScore <= 0f)
                {
                    continue;
                }

                float cornerScore = EvaluateCorners(candidate);
                float darknessScore = EvaluateDarkness(candidate);
                float pathScore = EvaluatePath(lizard.transform.position, candidate);
                if (pathScore <= 0f)
                {
                    continue;
                }

                float score = coverScore * 1.7f + cornerScore * 0.6f + darknessScore * 0.5f + pathScore;
                score -= Mathf.InverseLerp(4f, 34f, distance);

                if (score > bestScore)
                {
                    bestScore = score;
                    target = candidate;
                }
            }

            if (float.IsPositiveInfinity(target.x))
            {
                return false;
            }

            return true;
        }

        private static float EvaluateCover(Vector3 candidate, Vector3 threat, bool hasThreat)
        {
            if (!hasThreat)
            {
                return 0.45f;
            }

            var origin = candidate + Vector3.up * 0.4f;
            var target = threat + Vector3.up * 0.8f;
            var direction = target - origin;
            float length = direction.magnitude;
            if (length < 0.25f)
            {
                return 0.2f;
            }

            if (Physics.Raycast(origin, direction.normalized, out var hit, length, OcclusionMask, QueryTriggerInteraction.Ignore))
            {
                float slope = 1f - Mathf.Abs(Vector3.Dot(hit.normal, Vector3.up));
                return 1.25f + slope;
            }

            return 0.1f;
        }

        private static float EvaluateCorners(Vector3 candidate)
        {
            float score = 0f;
            var origin = candidate + Vector3.up * 0.35f;
            for (int i = 0; i < CornerDirections.Length; i++)
            {
                if (Physics.Raycast(origin, CornerDirections[i], out var hit, 1.4f, OcclusionMask, QueryTriggerInteraction.Ignore))
                {
                    score += 0.5f;
                    score += Mathf.Clamp01(1f - hit.distance / 1.4f) * 0.2f;
                }
            }

            return Mathf.Clamp(score, 0f, 2f);
        }

        private static float EvaluateDarkness(Vector3 candidate)
        {
            RefreshLights();
            if (_lights == null || _lights.Length == 0)
            {
                return 0.5f;
            }

            float exposure = 0f;
            foreach (var light in _lights)
            {
                if (light == null || !light.enabled || light.intensity <= 0.05f)
                {
                    continue;
                }

                float range = Mathf.Max(3f, light.range);
                float distance = Vector3.Distance(candidate, light.transform.position);
                if (distance > range)
                {
                    continue;
                }

                float normalized = 1f - Mathf.Clamp01(distance / range);
                exposure = Mathf.Max(exposure, normalized * light.intensity);
            }

            return 0.75f - Mathf.Clamp01(exposure) * 0.5f;
        }

        private static float EvaluatePath(Vector3 origin, Vector3 target)
        {
            if (!NavMesh.CalculatePath(origin, target, NavMesh.AllAreas, SharedPath))
            {
                return 0f;
            }

            if (SharedPath.status != NavMeshPathStatus.PathComplete)
            {
                return 0f;
            }

            var corners = SharedPath.corners;
            if (corners == null || corners.Length < 2)
            {
                return 0f;
            }

            float length = 0f;
            for (int i = 1; i < corners.Length; i++)
            {
                length += Vector3.Distance(corners[i - 1], corners[i]);
            }

            return Mathf.Clamp01(1f - Mathf.InverseLerp(4f, 32f, length));
        }

        private static void RefreshLights()
        {
            if (Time.time < _nextLightRefresh)
            {
                return;
            }

            _nextLightRefresh = Time.time + 6f;
            _lights = UnityEngine.Object.FindObjectsOfType<Light>();
        }
    }

    internal static class SporeLizardReflection
    {
        private static readonly MethodInfo TailWarnMethod = Locate("shake");
        private static readonly MethodInfo BurstMethod = Locate("puff") ?? Locate("burst");
        private static readonly MethodInfo BiteMethod = Locate("bite");

        internal static void TryPlayWarning(PufferAI lizard)
        {
            Invoke(TailWarnMethod, lizard);
        }

        internal static void TryTriggerSporeBurst(PufferAI lizard)
        {
            Invoke(BurstMethod, lizard);
        }

        internal static void TryBite(PufferAI lizard)
        {
            Invoke(BiteMethod, lizard);
        }

        private static MethodInfo Locate(string hint)
        {
            if (string.IsNullOrWhiteSpace(hint))
            {
                return null;
            }

            var methods = typeof(PufferAI).GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int i = 0; i < methods.Length; i++)
            {
                var method = methods[i];
                if (method.Name.IndexOf(hint, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                if (method.GetParameters().Length > 0)
                {
                    continue;
                }

                return method;
            }

            return null;
        }

        private static void Invoke(MethodInfo method, PufferAI lizard)
        {
            if (method == null || lizard == null)
            {
                return;
            }

            try
            {
                method.Invoke(lizard, Array.Empty<object>());
            }
            catch (Exception ex)
            {
                if (AlgoritmaPuncakMod.DebugInstrumentation)
                {
                    AlgoritmaPuncakMod.Log?.LogWarning($"[SporeLizard] {method.Name} invoke failed: {ex.Message}");
                }
            }
        }
    }
}
