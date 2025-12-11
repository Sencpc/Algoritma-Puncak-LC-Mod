using System;
using GameNetcodeStuff;
using HarmonyLib;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

namespace AlgoritmaPuncakMod.AI
{
    internal static class HoarderConditions
    {
        internal static bool FatalAggro(BTContext context)
        {
            return context.Hoarder != null && context.Blackboard.HoarderAggroState >= AIBlackboard.HoarderAggroLevel.Fatal;
        }

        internal static bool HardAggro(BTContext context)
        {
            return context.Hoarder != null && context.Blackboard.HoarderAggroState == AIBlackboard.HoarderAggroLevel.Hard;
        }

        internal static bool SoftAggro(BTContext context)
        {
            return context.Hoarder != null && context.Blackboard.HoarderAggroState == AIBlackboard.HoarderAggroLevel.Soft;
        }

        internal static bool CarryingLoot(BTContext context)
        {
            return context.Hoarder != null && context.Hoarder.heldItem != null;
        }

        internal static bool NestCompromised(BTContext context)
        {
            if (context.Hoarder == null)
            {
                return false;
            }

            var board = context.Blackboard;
            bool playerCampingNest = board.PlayerVisible && !float.IsPositiveInfinity(board.HoarderNest.x) && Vector3.Distance(board.LastKnownPlayerPosition, board.HoarderNest) < 6f;
            return board.HoarderShouldMigrate || playerCampingNest;
        }
    }

    internal static class HoarderActions
    {
        private static readonly MethodInfo DropItemMethod = AccessTools.Method(typeof(HoarderBugAI), "DropItemAndCallDropRPC", new[] { typeof(NetworkObject), typeof(bool) });

        internal static BTStatus ExecuteFatalAggro(BTContext context)
        {
            if (context.Hoarder == null)
            {
                return BTStatus.Failure;
            }

            var board = context.Blackboard;
            var target = board.LastKnownPlayerPosition;
            if (float.IsPositiveInfinity(target.x))
            {
                target = board.HoarderNest;
            }

            if (MovementTowards(context, target, 12f, 24f, "HoarderFatal"))
            {
                if (Vector3.Distance(context.Enemy.transform.position, target) <= 1.6f)
                {
                    TryHookDroppedItem(context, target);
                    return BTStatus.Success;
                }

                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        internal static BTStatus ExecuteHardAggro(BTContext context)
        {
            if (!context.Blackboard.PlayerVisible)
            {
                return BTStatus.Failure;
            }

            var board = context.Blackboard;
            var enemyPos = context.Enemy.transform.position;
            var toPlayer = (board.LastKnownPlayerPosition - enemyPos).normalized;
            var lateral = Vector3.Cross(Vector3.up, toPlayer);
            if (lateral.sqrMagnitude < 0.01f)
            {
                lateral = UnityEngine.Random.insideUnitSphere;
                lateral.y = 0f;
            }

            var buzzPoint = board.LastKnownPlayerPosition + toPlayer * 4f + lateral.normalized * 2.5f;
            if (MovementTowards(context, buzzPoint, 8f, 18f, "HoarderBuzz"))
            {
                context.Blackboard.TriggerHoarderAggro(1.5f);
                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        internal static BTStatus ExecuteSoftAggro(BTContext context)
        {
            var board = context.Blackboard;
            if (!board.PlayerVisible)
            {
                return BTStatus.Failure;
            }

            var offsetDir = (context.Enemy.transform.position - board.LastKnownPlayerPosition).normalized;
            if (offsetDir.sqrMagnitude < 0.01f)
            {
                offsetDir = UnityEngine.Random.insideUnitSphere;
            }

            var warnPoint = board.HoarderNest + offsetDir * 3f;
            board.TriggerHoarderAggro(1f);
            if (MovementTowards(context, warnPoint, 6f, 12f, "HoarderWarn"))
            {
                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        internal static BTStatus ReturnLoot(BTContext context)
        {
            if (context.Hoarder?.heldItem == null)
            {
                return BTStatus.Success;
            }

            var board = context.Blackboard;
            board.EnsureHoarderNest(context.Enemy.transform.position);
            var nest = board.HoarderNest;
            if (nest.Equals(Vector3.positiveInfinity))
            {
                return BTStatus.Failure;
            }

            if (Vector3.Distance(context.Enemy.transform.position, nest) <= 1.15f)
            {
                AttemptDropLoot(context.Hoarder);
                board.RecordHoarderLootDelivered();
                context.SetActiveAction("HoarderDropLoot");
                return BTStatus.Success;
            }

            if (MovementTowards(context, nest, 6.5f, 20f, "HoarderReturn"))
            {
                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        internal static BTStatus SearchForItems(BTContext context)
        {
            var hoarder = context.Hoarder;
            if (hoarder == null)
            {
                return BTStatus.Failure;
            }

            var board = context.Blackboard;
            board.EnsureHoarderNest(context.Enemy.transform.position);

            if (hoarder.targetItem != null)
            {
                var targetPos = hoarder.targetItem.transform.position;
                if (MovementTowards(context, targetPos, 6.5f, 30f, "HoarderPursueScrap"))
                {
                    context.SetActiveAction("HoarderPursueScrap");
                    return BTStatus.Running;
                }

                return BTStatus.Failure;
            }

            if (!board.TryGetNextHoarderSearchTarget(context.Enemy.transform.position, out var searchTarget))
            {
                return BTStatus.Failure;
            }

            if (Vector3.Distance(context.Enemy.transform.position, searchTarget) <= 1.25f)
            {
                if (HoarderWorld.TryFindLootNear(searchTarget, 6f, out var loot))
                {
                    hoarder.targetItem = loot;
                    board.MarkHoarderItemLocated();
                }
                else
                {
                    board.TriggerHoarderAggro(0.35f);
                }
            }

            if (MovementTowards(context, searchTarget, 5.5f, 30f, "HoarderScavenge"))
            {
                context.SetActiveAction("HoarderScavenge");
                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        internal static BTStatus MigrateNest(BTContext context)
        {
            var board = context.Blackboard;
            board.EnsureHoarderNest(context.Enemy.transform.position);

            if (!board.HoarderMigrationPending)
            {
                if (!TrySelectMigrationTarget(context, out var candidate))
                {
                    return BTStatus.Failure;
                }

                board.RequestHoarderMigration(candidate);
            }

            if (!board.TryConsumeHoarderMigration(out var targetNest))
            {
                return BTStatus.Failure;
            }

            if (MovementTowards(context, targetNest, 6f, 40f, "HoarderMigrate"))
            {
                if (Vector3.Distance(context.Enemy.transform.position, targetNest) < 1.15f)
                {
                    board.ConfirmHoarderMigration(targetNest);
                    if (context.Hoarder != null)
                    {
                        context.Hoarder.nestPosition = targetNest;
                    }
                    context.SetActiveAction("HoarderNestSet");
                    return BTStatus.Success;
                }

                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        internal static BTStatus IdleAroundNest(BTContext context)
        {
            var board = context.Blackboard;
            board.EnsureHoarderNest(context.Enemy.transform.position);
            var nest = board.HoarderNest;
            var wander = nest + UnityEngine.Random.insideUnitSphere * 2.5f;
            wander.y = nest.y;

            if (MovementTowards(context, wander, 3f, 10f, "HoarderIdle"))
            {
                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        private static bool MovementTowards(BTContext context, Vector3 target, float speed, float maxDistance, string label)
        {
            if (float.IsPositiveInfinity(target.x))
            {
                return false;
            }

            if (NavigationHelpers.TryMoveAgent(
                context,
                target,
                4f,
                maxDistance,
                label,
                speed,
                acceleration: speed * 1.8f,
                stoppingDistance: 0.35f,
                allowPartialPath: true))
            {
                return true;
            }

            return false;
        }

        private static void AttemptDropLoot(HoarderBugAI hoarder)
        {
            if (hoarder.heldItem == null || DropItemMethod == null)
            {
                return;
            }

            try
            {
                var netObj = hoarder.heldItem.itemGrabbableObject.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    DropItemMethod.Invoke(hoarder, new object[] { netObj, true });
                }
            }
            catch (Exception ex)
            {
                AlgoritmaPuncakMod.Log?.LogError($"Hoarder drop invoke failed: {ex}");
            }
        }

        private static void TryHookDroppedItem(BTContext context, Vector3 origin)
        {
            if (context.Hoarder == null || context.Hoarder.heldItem != null)
            {
                return;
            }

            if (HoarderWorld.TryFindLootNear(origin, 3f, out var loot))
            {
                context.Hoarder.targetItem = loot;
                context.Blackboard.MarkHoarderItemLocated();
            }
        }

        private static bool TrySelectMigrationTarget(BTContext context, out Vector3 target)
        {
            target = Vector3.positiveInfinity;
            var nodes = context.Enemy.allAINodes;
            if (nodes != null && nodes.Length > 0)
            {
                float best = 0f;
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

                    float distance = Vector3.Distance(nodeTransform.position, context.Blackboard.HoarderNest);
                    if (distance <= best || distance < 18f)
                    {
                        continue;
                    }

                    best = distance;
                    target = nodeTransform.position;
                }

                if (!float.IsPositiveInfinity(target.x))
                {
                    return true;
                }
            }

            for (int attempt = 0; attempt < 6; attempt++)
            {
                var offset = UnityEngine.Random.onUnitSphere;
                offset.y = 0f;
                if (offset.sqrMagnitude < 0.1f)
                {
                    continue;
                }

                offset = offset.normalized * UnityEngine.Random.Range(22f, 34f);
                var guess = context.Blackboard.HoarderNest + offset;
                if (NavMesh.SamplePosition(guess, out var hit, 8f, NavMesh.AllAreas))
                {
                    target = hit.position;
                    return true;
                }
            }

            return false;
        }
    }

    internal static class HoarderWorld
    {
        internal static bool TryFindLootNear(Vector3 origin, float radius, out GrabbableObject loot)
        {
            loot = null;
            float bestDistance = radius;
            var pool = HoarderBugAI.grabbableObjectsInMap;
            if (pool == null)
            {
                return false;
            }

            for (int i = 0; i < pool.Count; i++)
            {
                var go = pool[i];
                if (go == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(origin, go.transform.position);
                if (distance > bestDistance)
                {
                    continue;
                }

                var grabbable = go.GetComponent<GrabbableObject>();
                if (grabbable == null || grabbable.isHeld || grabbable.deactivated)
                {
                    continue;
                }

                bestDistance = distance;
                loot = grabbable;
            }

            return loot != null;
        }
    }
}
