using System.Collections.Generic;
using GameNetcodeStuff;
using UnityEngine;

namespace AlgoritmaPuncakMod.AI
{
    internal static class SandWormConditions
    {
        internal static bool HasStrikeOpportunity(BTContext context)
        {
            if (context.SandWorm == null)
            {
                return false;
            }

            var board = context.Blackboard;
            return board.SandWormHasHotspot && board.SandWormCooldownReady && board.SandWormHotspotHeat >= SandWormNoiseField.StrikeHeatThreshold;
        }

        internal static bool ShouldStalk(BTContext context)
        {
            if (context.SandWorm == null)
            {
                return false;
            }

            return context.Blackboard.SandWormHasHotspot;
        }
    }

    internal static class SandWormActions
    {
        internal static BTStatus ExecuteStrike(BTContext context)
        {
            var board = context.Blackboard;
            if (!board.SandWormHasHotspot || context.Agent == null)
            {
                board.ResetSandWormAttackState();
                return BTStatus.Failure;
            }

            if (board.SandWormStage == SandWormAttackStage.Idle)
            {
                board.SetSandWormStage(SandWormAttackStage.Approaching);
            }

            var hotspot = board.SandWormHotspot;
            var enemyPos = context.Enemy.transform.position;
            float normalizedHeat = board.SandWormHeatNormalized;
            float approachRadius = Mathf.Lerp(4f, 7.5f, normalizedHeat);
            float distance = Vector3.Distance(enemyPos, hotspot);

            if (board.SandWormStage == SandWormAttackStage.Approaching)
            {
                if (distance > approachRadius)
                {
                    if (NavigationHelpers.TryMoveAgent(
                        context,
                        hotspot,
                        8f,
                        180f,
                        "SandWormBurrow",
                        11f,
                        acceleration: 24f,
                        stoppingDistance: 1.25f,
                        allowPartialPath: true))
                    {
                        return BTStatus.Running;
                    }

                    return BTStatus.Failure;
                }

                board.BeginSandWormPrep(Mathf.Lerp(0.85f, 2.5f, 1f - normalizedHeat));
                board.SetSandWormStage(SandWormAttackStage.Charging);
                return BTStatus.Running;
            }

            if (board.SandWormStage == SandWormAttackStage.Charging)
            {
                if (board.SandWormPrepActive)
                {
                    context.SetActiveAction("SandWormCharge");
                    return BTStatus.Running;
                }

                board.TriggerSandWormRoar(2f);
                board.SetSandWormStage(SandWormAttackStage.Roaring);
                SandWormWorld.BroadcastRoar(hotspot);
                return BTStatus.Running;
            }

            if (board.SandWormStage == SandWormAttackStage.Roaring)
            {
                if (board.SandWormRoarActive)
                {
                    context.SetActiveAction("SandWormRoar");
                    return BTStatus.Running;
                }

                board.TriggerSandWormEruption(1.35f);
                board.SetSandWormStage(SandWormAttackStage.Erupting);
                SandWormWorld.TriggerEruption(context, hotspot, Mathf.Lerp(8f, 14f, normalizedHeat));
                board.BeginSandWormCooldown(Mathf.Lerp(6f, 10f, normalizedHeat));
                board.CoolSandWormHotspot(hotspot, 10f);
                context.SetActiveAction("SandWormErupt");
                return BTStatus.Running;
            }

            if (board.SandWormStage == SandWormAttackStage.Erupting)
            {
                if (board.SandWormEruptActive)
                {
                    context.SetActiveAction("SandWormErupt");
                    return BTStatus.Running;
                }

                board.ResetSandWormAttackState();
                return BTStatus.Success;
            }

            return BTStatus.Success;
        }

        internal static BTStatus StalkHotspot(BTContext context)
        {
            var board = context.Blackboard;
            if (!board.SandWormHasHotspot || context.Agent == null)
            {
                return BTStatus.Failure;
            }

            var hotspot = board.SandWormHotspot;
            var target = SandWormNoiseField.SamplePeripheralPoint(hotspot, 8f, 18f);

            if (NavigationHelpers.TryMoveAgent(
                context,
                target,
                6f,
                200f,
                "SandWormStalk",
                5.5f,
                acceleration: 9f,
                stoppingDistance: 1.5f,
                allowPartialPath: true))
            {
                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }
    }

    internal static class SandWormWorld
    {
        internal static void BroadcastRoar(Vector3 hotspot)
        {
            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShakeCamera(ScreenShakeType.Big);
            }

            SandWormNoiseField.RegisterNoiseBurst(hotspot, 2f);
        }

        internal static void TriggerEruption(BTContext context, Vector3 hotspot, float killRadius)
        {
            var round = StartOfRound.Instance;
            if (round?.allPlayerScripts == null)
            {
                return;
            }

            var worm = context.SandWorm;
            bool authoritative = worm == null || worm.IsOwner;
            if (!authoritative)
            {
                return;
            }

            foreach (var player in round.allPlayerScripts)
            {
                if (player == null || player.isPlayerDead || player.isInsideFactory)
                {
                    continue;
                }

                if (Vector3.Distance(player.transform.position, hotspot) <= killRadius)
                {
                    player.KillPlayer(Vector3.zero, false, CauseOfDeath.Unknown, 0, hotspot);
                }
            }

            if (HUDManager.Instance != null)
            {
                HUDManager.Instance.ShakeCamera(ScreenShakeType.VeryStrong);
            }
        }
    }

    internal static class SandWormNoiseField
    {
        internal const float StrikeHeatThreshold = 6f;
        private const float CellSize = 6f;
        private const float MaxHeat = 40f;
        private const float DecayPerSecond = 1.2f;

        private static readonly Dictionary<Vector2Int, CellData> Cells = new Dictionary<Vector2Int, CellData>();
        private static readonly List<Vector2Int> ReusableKeys = new List<Vector2Int>(32);

        internal static void RegisterFootstep(Vector3 position) => AddHeat(position, 1f);
        internal static void RegisterItemDrop(Vector3 position) => AddHeat(position, 5f);
        internal static void RegisterNoiseBurst(Vector3 position, float magnitude) => AddHeat(position, Mathf.Max(0.5f, magnitude));

        internal static bool TryGetHottestCell(out Vector3 position, out float heat)
        {
            position = Vector3.positiveInfinity;
            heat = 0f;
            foreach (var entry in Cells)
            {
                if (entry.Value.Heat > heat)
                {
                    heat = entry.Value.Heat;
                    position = entry.Value.Center;
                }
            }

            return !float.IsPositiveInfinity(position.x);
        }

        internal static void Tick(float deltaTime)
        {
            if (Cells.Count == 0 || deltaTime <= 0f)
            {
                return;
            }

            ReusableKeys.Clear();
            float decay = DecayPerSecond * deltaTime;
            foreach (var pair in Cells)
            {
                var data = pair.Value;
                data.Heat = Mathf.Max(0f, data.Heat - decay);
                if (data.Heat <= 0.01f)
                {
                    ReusableKeys.Add(pair.Key);
                }
                else
                {
                    Cells[pair.Key] = data;
                }
            }

            for (int i = 0; i < ReusableKeys.Count; i++)
            {
                Cells.Remove(ReusableKeys[i]);
            }
        }

        internal static float NormalizeHeat(float heat) => Mathf.Clamp01(heat / MaxHeat);

        internal static void Dampen(Vector3 center, float radius)
        {
            if (Cells.Count == 0)
            {
                return;
            }

            float radiusSqr = radius * radius;
            foreach (var pair in Cells)
            {
                var data = pair.Value;
                if ((data.Center - center).sqrMagnitude <= radiusSqr)
                {
                    data.Heat *= 0.2f;
                    Cells[pair.Key] = data;
                }
            }
        }

        internal static Vector3 SamplePeripheralPoint(Vector3 center, float minRadius, float maxRadius)
        {
            float radius = Random.Range(minRadius, maxRadius);
            var direction = Random.insideUnitCircle;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector2.up;
            }

            direction = direction.normalized * radius;
            return new Vector3(center.x + direction.x, center.y, center.z + direction.y);
        }

        private static void AddHeat(Vector3 position, float value)
        {
            if (float.IsInfinity(position.x) || float.IsInfinity(position.y) || float.IsInfinity(position.z))
            {
                return;
            }

            var coord = Quantize(position);
            if (!Cells.TryGetValue(coord, out var data))
            {
                data = new CellData
                {
                    Center = QuantizeCenter(coord, position.y)
                };
            }

            data.Center.y = position.y;
            data.Heat = Mathf.Clamp(data.Heat + value, 0f, MaxHeat);
            Cells[coord] = data;
        }

        private static Vector2Int Quantize(Vector3 position)
        {
            int x = Mathf.RoundToInt(position.x / CellSize);
            int z = Mathf.RoundToInt(position.z / CellSize);
            return new Vector2Int(x, z);
        }

        private static Vector3 QuantizeCenter(Vector2Int coord, float y)
        {
            return new Vector3(coord.x * CellSize + CellSize * 0.5f, y, coord.y * CellSize + CellSize * 0.5f);
        }

        private struct CellData
        {
            internal Vector3 Center;
            internal float Heat;
        }
    }

    internal static partial class BehaviorTreeFactory
    {
        private static BTNode CreateSandWormTree()
        {
            return new BTPrioritySelector("SandWormRoot",
                BuildSandWormAttackBranch(),
                BuildSandWormStalkBranch());
        }

        private static BTNode BuildSandWormAttackBranch()
        {
            return new BTSequence("SandWormAttack",
                new BTConditionNode("SandWormStrikeOpportunity", SandWormConditions.HasStrikeOpportunity),
                new BTActionNode("SandWormStrikeAction", SandWormActions.ExecuteStrike));
        }

        private static BTNode BuildSandWormStalkBranch()
        {
            return new BTSequence("SandWormStalk",
                new BTConditionNode("SandWormShouldStalk", SandWormConditions.ShouldStalk),
                new BTActionNode("SandWormStalkAction", SandWormActions.StalkHotspot));
        }
    }
}
