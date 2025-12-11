using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace AlgoritmaPuncakMod.AI
{
    internal static class BaboonConditions
    {
        internal static bool NestThreatened(BTContext context)
        {
            if (context.Baboon == null)
            {
                return false;
            }

            var board = context.Blackboard;
            board.EnsureBaboonNest(context.Enemy.transform.position);

            if (board.BaboonAlertActive)
            {
                return true;
            }

            var playerPos = board.LastKnownPlayerPosition;
            if (float.IsPositiveInfinity(playerPos.x))
            {
                return false;
            }

            float nestDistance = Vector3.Distance(playerPos, board.BaboonNest);
            return nestDistance <= 9f || (board.PlayerVisible && nestDistance <= 15f);
        }

        internal static bool PackReadyToStrike(BTContext context)
        {
            if (context.Baboon == null)
            {
                return false;
            }

            var board = context.Blackboard;
            if (!board.PlayerVisible && board.TimeSincePlayerSeen > 3f)
            {
                return false;
            }

            return BaboonWorld.CountPackmates(board) >= 2;
        }

        internal static bool ShouldScavenge(BTContext context)
        {
            if (context.Baboon == null)
            {
                return false;
            }

            var board = context.Blackboard;
            return !board.PlayerVisible && board.TimeSincePlayerSeen > 2.5f;
        }
    }

    internal static class BaboonActions
    {
        internal static BTStatus DefendNest(BTContext context)
        {
            var agent = context.Agent;
            if (agent == null)
            {
                return BTStatus.Failure;
            }

            var board = context.Blackboard;
            board.EnsureBaboonNest(context.Enemy.transform.position);
            var nest = board.BaboonNest;
            var playerPos = board.LastKnownPlayerPosition;

            Vector3 guardPoint = nest;
            if (!float.IsPositiveInfinity(playerPos.x))
            {
                var dirToPlayer = playerPos - nest;
                if (dirToPlayer.sqrMagnitude > 0.01f)
                {
                    guardPoint = nest + dirToPlayer.normalized * 3.5f;
                }
                else
                {
                    guardPoint = nest + Vector3.forward * 2.5f;
                }
            }

            BaboonWorld.UpdateLeadership(context);
            var desired = guardPoint - context.Enemy.transform.position;
            var flock = BaboonWorld.ComputeClusterVector(context, desired);
            var destination = guardPoint + flock * 1.5f + NavigationHelpers.GetAgentOffset(context.Enemy, 1.4f);

            if (NavigationHelpers.TryMoveAgent(
                context,
                destination,
                4.5f,
                28f,
                "BaboonDefendNest",
                board.PlayerVisible ? 8.25f : 5.75f,
                acceleration: 13f,
                stoppingDistance: 0.55f,
                allowPartialPath: true))
            {
                if (board.PlayerVisible)
                {
                    board.TriggerBaboonAlert(4.5f);
                    if (board.BaboonIsLeader)
                    {
                        board.BroadcastBaboonAttack(playerPos, 2f);
                        board.BroadcastBaboonMovement(flock, 1.5f);
                    }

                    if (Vector3.Distance(context.Enemy.transform.position, playerPos) < 6f && board.BaboonCanScream)
                    {
                        board.BeginBaboonScreamCooldown(5f);
                    }
                }

                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        internal static BTStatus ClusterStrike(BTContext context)
        {
            var agent = context.Agent;
            if (agent == null)
            {
                return BTStatus.Failure;
            }

            var board = context.Blackboard;
            BaboonWorld.UpdateLeadership(context);

            var target = board.BaboonLeaderAttacking ? board.BaboonLeaderTarget : board.LastKnownPlayerPosition;
            if (float.IsPositiveInfinity(target.x))
            {
                return BTStatus.Failure;
            }

            if (board.BaboonIsLeader)
            {
                board.BroadcastBaboonAttack(target, 2.75f);
                var moveVector = (target - context.Enemy.transform.position);
                board.BroadcastBaboonMovement(moveVector, 1.5f);
            }
            else if (board.BaboonLeaderAttacking)
            {
                target = board.BaboonLeaderTarget;
            }

            var desired = target - context.Enemy.transform.position;
            var flock = BaboonWorld.ComputeClusterVector(context, desired);
            var destination = target - flock * 1.35f + NavigationHelpers.GetAgentOffset(context.Enemy, context.Profile.PackCohesionRadius * 0.3f);

            if (NavigationHelpers.TryMoveAgent(
                context,
                destination,
                context.Profile.PackCohesionRadius,
                context.Profile.HuntAggroDistance * 1.5f,
                "BaboonClusterStrike",
                9.25f,
                acceleration: 19f,
                stoppingDistance: 0.45f,
                allowPartialPath: true))
            {
                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        internal static BTStatus Scavenge(BTContext context)
        {
            var agent = context.Agent;
            if (agent == null)
            {
                return BTStatus.Failure;
            }

            var board = context.Blackboard;
            board.EnsureBaboonNest(context.Enemy.transform.position);
            BaboonWorld.UpdateLeadership(context);

            var anchor = board.BaboonNest;
            var searchPoint = BaboonWorld.SelectWaypoint(anchor, 4f, context.Profile.TerritoryRadius * 0.8f);
            var destination = searchPoint;

            if (HoarderWorld.TryFindLootNear(searchPoint, 7f, out GrabbableObject loot) && loot != null)
            {
                destination = loot.transform.position;
                if (BaboonWorld.DistanceToShip(destination) < 14f)
                {
                    board.AllowBaboonShipApproach(4f);
                }

                if (board.BaboonIsLeader)
                {
                    board.BroadcastBaboonMovement(destination - context.Enemy.transform.position, 1.25f);
                }
            }

            if (NavigationHelpers.TryMoveAgent(
                context,
                destination,
                4f,
                context.Profile.TerritoryRadius * 2.5f,
                "BaboonScavenge",
                5.75f,
                acceleration: 10f,
                stoppingDistance: 0.6f,
                allowPartialPath: true))
            {
                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        internal static BTStatus ClusterPatrol(BTContext context)
        {
            var agent = context.Agent;
            if (agent == null)
            {
                return BTStatus.Failure;
            }

            var board = context.Blackboard;
            board.EnsureBaboonNest(context.Enemy.transform.position);
            BaboonWorld.UpdateLeadership(context);

            var anchor = board.PackCenter != Vector3.zero ? board.PackCenter : board.BaboonNest;
            var waypoint = BaboonWorld.SelectWaypoint(anchor, context.Profile.PackCohesionRadius * 0.3f, context.Profile.PackCohesionRadius * 0.9f);
            var direction = waypoint - context.Enemy.transform.position;
            var flock = BaboonWorld.ComputeClusterVector(context, direction);
            waypoint += flock * 1.1f;

            if (NavigationHelpers.TryMoveAgent(
                context,
                waypoint,
                context.Profile.PackCohesionRadius,
                context.Profile.TerritoryRadius * 1.5f,
                "BaboonClusterPatrol",
                6.25f,
                acceleration: 12f,
                stoppingDistance: 0.75f,
                allowPartialPath: true))
            {
                if (board.BaboonIsLeader)
                {
                    board.BroadcastBaboonMovement(flock, 1f);
                }

                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        internal static BTStatus IdleNearNest(BTContext context)
        {
            var agent = context.Agent;
            if (agent == null)
            {
                return BTStatus.Failure;
            }

            var board = context.Blackboard;
            board.EnsureBaboonNest(context.Enemy.transform.position);
            var perch = BaboonWorld.SelectWaypoint(board.BaboonNest, 0.5f, 2.5f);

            if (NavigationHelpers.TryMoveAgent(
                context,
                perch,
                2f,
                12f,
                "BaboonIdle",
                3.5f,
                acceleration: 6f,
                stoppingDistance: 0.4f,
                allowPartialPath: true))
            {
                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }
    }

    internal static class BaboonWorld
    {
        private static readonly string[] ShipFieldCandidates = { "shipDock", "shipRoom", "shipDoorTransform", "hangarShipDoor", "shipTransform" };
        private static Transform _shipTransform;
        private static float _nextShipLookupTime;

        internal static int CountPackmates(AIBlackboard board)
        {
            int count = 0;
            var allies = board.NearbyAllies;
            for (int i = 0; i < allies.Count; i++)
            {
                if (allies[i] is BaboonBirdAI)
                {
                    count++;
                }
            }

            return count;
        }

        internal static void UpdateLeadership(BTContext context)
        {
            var board = context.Blackboard;
            board.EnsureBaboonNest(context.Enemy.transform.position);

            int packKey = board.BaboonPackKey != 0
                ? board.BaboonPackKey
                : board.ComputeBaboonPackKey(board.BaboonNest);

            int leaderId = context.Enemy.GetInstanceID();
            int bestId = leaderId;
            var allies = board.NearbyAllies;
            for (int i = 0; i < allies.Count; i++)
            {
                if (!(allies[i] is BaboonBirdAI ally))
                {
                    continue;
                }

                int allyKey = board.ComputeBaboonPackKey(ally.transform.position);
                if (allyKey != packKey)
                {
                    continue;
                }

                int allyId = ally.GetInstanceID();
                if (allyId < bestId)
                {
                    bestId = allyId;
                }
            }

            board.MarkBaboonLeadership(bestId == leaderId);
        }

        internal static Vector3 ComputeClusterVector(BTContext context, Vector3 desiredDirection)
        {
            var direction = desiredDirection.sqrMagnitude < 0.01f
                ? context.Enemy.transform.forward
                : desiredDirection.normalized;

            var board = context.Blackboard;
            Vector3 separation = Vector3.zero;
            Vector3 alignment = Vector3.zero;
            Vector3 cohesion = Vector3.zero;
            int crowdCount = 0;

            var allies = board.NearbyAllies;
            for (int i = 0; i < allies.Count; i++)
            {
                if (!(allies[i] is BaboonBirdAI ally))
                {
                    continue;
                }

                var toAlly = ally.transform.position - context.Enemy.transform.position;
                float sqr = Mathf.Max(toAlly.sqrMagnitude, 0.1f);
                separation -= toAlly / sqr;
                alignment += ally.transform.forward;
                cohesion += ally.transform.position;
                crowdCount++;
            }

            if (crowdCount > 0)
            {
                separation /= crowdCount;
                alignment = (alignment / crowdCount).normalized;
                cohesion = ((cohesion / crowdCount) - context.Enemy.transform.position).normalized;
            }

            var shipAvoidance = GetShipAvoidanceVector(context);
            var leaderMove = board.BaboonLeaderAdvancing ? board.BaboonLeaderMove : Vector3.zero;

            var vector = direction +
                         separation * 1.15f +
                         alignment * 0.5f +
                         cohesion * 0.65f +
                         shipAvoidance * 2f +
                         leaderMove * 0.8f;

            return vector.sqrMagnitude > 0.01f ? vector.normalized : direction.normalized;
        }

        internal static Vector3 SelectWaypoint(Vector3 origin, float minRadius, float maxRadius)
        {
            float clampedMin = Mathf.Max(0f, minRadius);
            float clampedMax = Mathf.Max(clampedMin + 0.1f, maxRadius);

            for (int attempt = 0; attempt < 6; attempt++)
            {
                var offset = Random.onUnitSphere;
                offset.y = 0f;
                if (offset.sqrMagnitude < 0.01f)
                {
                    continue;
                }

                float range = Random.Range(clampedMin, clampedMax);
                var guess = origin + offset.normalized * range;
                if (NavMesh.SamplePosition(guess, out var hit, 5f, NavMesh.AllAreas))
                {
                    return hit.position;
                }
            }

            return origin;
        }

        internal static Vector3 GetShipAvoidanceVector(BTContext context)
        {
            if (context.Blackboard.BaboonShipOverride)
            {
                return Vector3.zero;
            }

            var shipCenter = ResolveShipCenter();
            if (float.IsPositiveInfinity(shipCenter.x))
            {
                return Vector3.zero;
            }

            var toShip = shipCenter - context.Enemy.transform.position;
            float distance = toShip.magnitude;
            const float radius = 18f;
            if (distance >= radius || distance <= 0.01f)
            {
                return Vector3.zero;
            }

            float weight = 1f - Mathf.Clamp01(distance / radius);
            return -toShip.normalized * weight;
        }

        internal static float DistanceToShip(Vector3 position)
        {
            var center = ResolveShipCenter();
            if (float.IsPositiveInfinity(center.x))
            {
                return float.PositiveInfinity;
            }

            return Vector3.Distance(position, center);
        }

        private static Vector3 ResolveShipCenter()
        {
            if (_shipTransform == null && Time.time >= _nextShipLookupTime)
            {
                _nextShipLookupTime = Time.time + 2f;
                _shipTransform = LocateShipTransform();
            }

            return _shipTransform != null ? _shipTransform.position : Vector3.positiveInfinity;
        }

        private static Transform LocateShipTransform()
        {
            var round = StartOfRound.Instance;
            if (round != null)
            {
                for (int i = 0; i < ShipFieldCandidates.Length; i++)
                {
                    var field = AccessTools.Field(round.GetType(), ShipFieldCandidates[i]);
                    if (field == null)
                    {
                        continue;
                    }

                    var candidate = field.GetValue(round);
                    var transform = ExtractTransform(candidate);
                    if (transform != null)
                    {
                        return transform;
                    }
                }
            }

            var doorType = AccessTools.TypeByName("HangarShipDoor");
            if (doorType != null)
            {
                var door = Object.FindObjectOfType(doorType) as Component;
                if (door != null)
                {
                    return door.transform;
                }
            }

            return null;
        }

        private static Transform ExtractTransform(object value)
        {
            if (value == null)
            {
                return null;
            }

            if (value is Transform transform)
            {
                return transform;
            }

            if (value is Component component)
            {
                return component.transform;
            }

            if (value is GameObject go)
            {
                return go.transform;
            }

            return null;
        }
    }

    internal static partial class BehaviorTreeFactory
    {
        private static BTNode CreateBaboonTree()
        {
            return new BTPrioritySelector("BaboonRoot",
                BuildBaboonDefenseBranch(),
                BuildBaboonStrikeBranch(),
                BuildBaboonScavengeBranch(),
                new BTActionNode("BaboonClusterPatrol", BaboonActions.ClusterPatrol),
                new BTActionNode("BaboonIdle", BaboonActions.IdleNearNest));
        }

        private static BTNode BuildBaboonDefenseBranch()
        {
            return new BTSequence("BaboonDefense",
                new BTConditionNode("BaboonNestThreat", BaboonConditions.NestThreatened),
                new BTActionNode("BaboonDefendNestAction", BaboonActions.DefendNest));
        }

        private static BTNode BuildBaboonStrikeBranch()
        {
            return new BTSequence("BaboonStrike",
                new BTConditionNode("BaboonPackReady", BaboonConditions.PackReadyToStrike),
                new BTActionNode("BaboonClusterStrikeAction", BaboonActions.ClusterStrike));
        }

        private static BTNode BuildBaboonScavengeBranch()
        {
            return new BTSequence("BaboonScavenge",
                new BTConditionNode("BaboonShouldScavenge", BaboonConditions.ShouldScavenge),
                new BTActionNode("BaboonScavengeAction", BaboonActions.Scavenge));
        }
    }
}
