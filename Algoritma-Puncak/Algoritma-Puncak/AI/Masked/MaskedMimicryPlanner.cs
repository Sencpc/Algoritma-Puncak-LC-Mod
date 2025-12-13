using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace AlgoritmaPuncakMod.AI
{
    internal static class MaskedMimicryPlanner
    {
        private sealed class CacheEntry
        {
            internal readonly List<Vector3> Anchors = new List<Vector3>();
            internal readonly List<Vector3> Facings = new List<Vector3>();
            internal int Cursor = -1;
            internal float NextRefresh;
        }

        private static readonly Dictionary<int, CacheEntry> Cache = new Dictionary<int, CacheEntry>();
        private static readonly Vector3[] CorridorAxes =
        {
            Vector3.forward,
            Vector3.right,
            (Vector3.forward + Vector3.right).normalized,
            (Vector3.forward - Vector3.right).normalized
        };
        private static readonly int GeometryMask = LayerMask.GetMask("Default", "Environment", "Room", "Metal", "Terrain");

        internal static bool TryGetAnchor(MaskedPlayerEnemy masked, AIBlackboard board, out Vector3 anchor, out Vector3 facing)
        {
            anchor = Vector3.positiveInfinity;
            facing = Vector3.positiveInfinity;
            if (masked == null)
            {
                return false;
            }

            int id = masked.GetInstanceID();
            if (!Cache.TryGetValue(id, out var entry))
            {
                entry = new CacheEntry();
                Cache[id] = entry;
                entry.NextRefresh = 0f;
            }

            if (Time.time >= entry.NextRefresh || entry.Anchors.Count == 0)
            {
                RebuildAnchors(masked, board, entry);
                entry.NextRefresh = Time.time + 6f;
            }

            if (entry.Anchors.Count == 0)
            {
                return false;
            }

            entry.Cursor = (entry.Cursor + 1) % entry.Anchors.Count;
            anchor = entry.Anchors[entry.Cursor];
            facing = entry.Facings[entry.Cursor];
            return !float.IsPositiveInfinity(anchor.x);
        }

        private static void RebuildAnchors(MaskedPlayerEnemy masked, AIBlackboard board, CacheEntry entry)
        {
            entry.Anchors.Clear();
            entry.Facings.Clear();

            var nodes = masked.allAINodes;
            if (nodes == null || nodes.Length == 0)
            {
                return;
            }

            var origin = masked.transform.position;
            var playerPos = board.LastKnownPlayerPosition;
            bool hasPlayer = !float.IsPositiveInfinity(playerPos.x);

            for (int i = 0; i < nodes.Length; i++)
            {
                var node = nodes[i];
                if (node == null)
                {
                    continue;
                }

                var nodeTransform = node.transform;
                if (nodeTransform == null)
                {
                    continue;
                }

                var candidate = nodeTransform.position;
                if (!NavMesh.SamplePosition(candidate, out var hit, 1.5f, NavMesh.AllAreas))
                {
                    continue;
                }

                candidate = hit.position;
                float distanceFromOrigin = Vector3.Distance(origin, candidate);
                if (distanceFromOrigin < 5f || distanceFromOrigin > 42f)
                {
                    continue;
                }

                float chokeScore = EvaluateChoke(candidate);
                float coverScore = EvaluateCover(candidate);
                float lureScore = EvaluateLureBias(candidate, board, hasPlayer, playerPos);
                float total = chokeScore * 1.25f + coverScore + lureScore * 0.75f;
                if (total < 1.15f)
                {
                    continue;
                }

                var facing = DetermineFacingVector(candidate);
                entry.Anchors.Add(candidate);
                entry.Facings.Add(facing);

                if (entry.Anchors.Count >= 18)
                {
                    break;
                }
            }

            entry.Cursor = entry.Anchors.Count > 0 ? UnityEngine.Random.Range(-1, entry.Anchors.Count) : -1;
        }

        private static float EvaluateChoke(Vector3 candidate)
        {
            var origin = candidate + Vector3.up * 0.45f;
            float score = 0f;
            for (int i = 0; i < CorridorAxes.Length; i++)
            {
                var axis = CorridorAxes[i];
                bool forward = Physics.Raycast(origin, axis, out var forwardHit, 2.4f, GeometryMask, QueryTriggerInteraction.Ignore);
                bool backward = Physics.Raycast(origin, -axis, out var backwardHit, 2.4f, GeometryMask, QueryTriggerInteraction.Ignore);
                if (forward && backward)
                {
                    float width = forwardHit.distance + backwardHit.distance;
                    score += Mathf.Clamp01(1.25f - width * 0.35f);
                }
                else if (forward || backward)
                {
                    float singleWidth = forward ? forwardHit.distance : backwardHit.distance;
                    score += Mathf.Clamp01(0.6f - singleWidth * 0.2f);
                }
            }

            return Mathf.Clamp(score, 0f, 4f);
        }

        private static float EvaluateCover(Vector3 candidate)
        {
            var origin = candidate + Vector3.up * 0.35f;
            float cover = 0f;
            for (int i = 0; i < CorridorAxes.Length; i++)
            {
                var axis = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                if (Physics.Raycast(origin, axis, out var hit, 3.2f, GeometryMask, QueryTriggerInteraction.Ignore))
                {
                    cover += Mathf.Clamp01(1f - hit.distance / 3.2f) * 0.5f;
                }
            }

            return Mathf.Clamp(cover, 0f, 2f);
        }

        private static float EvaluateLureBias(Vector3 candidate, AIBlackboard board, bool hasPlayer, Vector3 playerPos)
        {
            float score = 0.25f;
            if (hasPlayer)
            {
                float playerDistance = Vector3.Distance(candidate, playerPos);
                score += Mathf.InverseLerp(18f, 6f, playerDistance);
                if (board.MaskedPlayerIsIsolated)
                {
                    score += 0.15f;
                }
            }

            if (board.TerritoryCenter != Vector3.zero)
            {
                float territoryDistance = Vector3.Distance(candidate, board.TerritoryCenter);
                score += Mathf.InverseLerp(28f, 8f, territoryDistance) * 0.35f;
            }

            return score;
        }

        private static Vector3 DetermineFacingVector(Vector3 anchor)
        {
            var origin = anchor + Vector3.up * 0.35f;
            Vector3 bestDirection = Vector3.forward;
            float bestDistance = 0.5f;
            for (int i = 0; i < 12; i++)
            {
                var direction = Quaternion.Euler(0f, i * 30f, 0f) * Vector3.forward;
                float distance = 10f;
                if (Physics.Raycast(origin, direction, out var hit, 10f, GeometryMask, QueryTriggerInteraction.Ignore))
                {
                    distance = hit.distance;
                }

                if (distance > bestDistance)
                {
                    bestDistance = distance;
                    bestDirection = direction;
                }
            }

            return anchor + bestDirection * Mathf.Max(3f, bestDistance * 0.6f);
        }
    }
}
