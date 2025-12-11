using System;
using GameNetcodeStuff;
using HarmonyLib;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace AlgoritmaPuncakMod.AI
{
    internal static class CoilheadConditions
    {
        internal static bool BeingObserved(BTContext context)
        {
            if (context.Coilhead == null)
            {
                return false;
            }

            bool observed = CoilheadWorld.IsObserved(context.Coilhead);
            if (observed)
            {
                context.Blackboard.MarkCoilheadObservation();
            }

            return observed || context.Blackboard.CoilheadFreezeActive;
        }

        internal static bool HasTarget(BTContext context)
        {
            return CoilheadWorld.TrySelectTarget(context, 6f);
        }
    }

    internal static class CoilheadActions
    {
        internal static BTStatus Freeze(BTContext context)
        {
            var coilhead = context.Coilhead;
            if (coilhead == null)
            {
                return BTStatus.Failure;
            }

            var agent = context.Agent;
            if (agent != null)
            {
                agent.ResetPath();
                agent.speed = 0f;
            }

            if (!context.Blackboard.CoilheadFrozen)
            {
                TrySendStopRpc(coilhead);
                context.Blackboard.SetCoilheadFrozen(true);
            }

            context.SetActiveAction("CoilheadFreeze");
            return BTStatus.Running;
        }

        internal static BTStatus RapidApproach(BTContext context)
        {
            var coilhead = context.Coilhead;
            var agent = context.Agent;
            if (coilhead == null || agent == null)
            {
                return BTStatus.Failure;
            }

            var board = context.Blackboard;
            TrySendGoRpc(coilhead, board);

            CoilheadWorld.TrySelectTarget(context, 6f);
            var target = board.CoilheadTarget;
            if (float.IsPositiveInfinity(target.x))
            {
                return BTStatus.Failure;
            }

            if (board.CoilheadDoorEngaged)
            {
                return HandleDoorPause(context, agent, board);
            }

            if (!CoilheadDoorHelper.TryBuildChasePath(agent, target, out var path))
            {
                return BTStatus.Failure;
            }

            var doorInfo = CoilheadDoorHelper.InspectPath(path);
            if (doorInfo.HasDoor && !doorInfo.IsOpen)
            {
                board.BeginCoilheadDoorPause(doorInfo.Door, doorInfo.Position, 0.5f);
                return HandleDoorPause(context, agent, board);
            }

            ConfigureChaseAgent(agent, board.CoilheadHasAggro);
            agent.SetPath(path);
            context.SetActiveAction("CoilheadRapidApproach");
            return BTStatus.Running;
        }

        internal static BTStatus Patrol(BTContext context)
        {
            var coilhead = context.Coilhead;
            var agent = context.Agent;
            if (coilhead == null || agent == null)
            {
                return BTStatus.Failure;
            }

            var board = context.Blackboard;
            TrySendGoRpc(coilhead, board);

            var anchor = board.TerritoryCenter == Vector3.zero ? coilhead.transform.position : board.TerritoryCenter;
            var wander = anchor + UnityEngine.Random.insideUnitSphere * 5f;
            wander.y = anchor.y;

            if (!NavMesh.SamplePosition(wander, out var hit, 5f, NavMesh.AllAreas))
            {
                return BTStatus.Failure;
            }

            agent.speed = 6.5f;
            agent.acceleration = 10f;
            agent.autoBraking = true;
            agent.stoppingDistance = 0.2f;
            agent.SetDestination(hit.position);
            context.SetActiveAction("CoilheadPatrol");
            return BTStatus.Running;
        }

        private static BTStatus HandleDoorPause(BTContext context, NavMeshAgent agent, AIBlackboard board)
        {
            if (agent != null)
            {
                agent.ResetPath();
                agent.speed = 0f;
            }

            var focus = board.CoilheadDoorFocus;
            if (!float.IsPositiveInfinity(focus.x))
            {
                var root = context.Enemy.transform;
                var dir = focus - root.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.01f)
                {
                    var look = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    root.rotation = Quaternion.RotateTowards(root.rotation, look, 540f * context.DeltaTime);
                }
            }

            CoilheadDoorHelper.ForceDoorOpen(board.CoilheadDoorComponent);
            context.SetActiveAction("CoilheadDoorBreach");

            if (board.CoilheadDoorReady)
            {
                board.FinishCoilheadDoorPause();
                return BTStatus.Success;
            }

            return BTStatus.Running;
        }

        private static void ConfigureChaseAgent(NavMeshAgent agent, bool enraged)
        {
            float speed = enraged ? 18f : 9f;
            agent.speed = speed;
            agent.acceleration = Mathf.Max(agent.acceleration, speed * 2.1f);
            agent.autoBraking = false;
            agent.stoppingDistance = 0.35f;
        }

        private static void TrySendGoRpc(SpringManAI coilhead, AIBlackboard board)
        {
            if (!board.CoilheadFrozen)
            {
                return;
            }

            try
            {
                coilhead.SetAnimationGoServerRpc();
            }
            catch (Exception ex)
            {
                AlgoritmaPuncakMod.Log?.LogDebug($"[Coilhead] Failed to send go RPC: {ex.Message}");
            }

            board.SetCoilheadFrozen(false);
        }

        private static void TrySendStopRpc(SpringManAI coilhead)
        {
            try
            {
                coilhead.SetAnimationStopServerRpc();
            }
            catch (Exception ex)
            {
                AlgoritmaPuncakMod.Log?.LogDebug($"[Coilhead] Failed to send stop RPC: {ex.Message}");
            }
        }
    }

    internal static class CoilheadWorld
    {
        internal static bool TrySelectTarget(BTContext context, float memoryDuration)
        {
            var coilhead = context.Coilhead;
            if (coilhead == null)
            {
                return false;
            }

            var round = StartOfRound.Instance;
            var scripts = round?.allPlayerScripts;
            if (scripts == null || scripts.Length == 0)
            {
                return context.Blackboard.CoilheadHasAggro && !float.IsPositiveInfinity(context.Blackboard.CoilheadTarget.x);
            }

            float bestDistance = float.MaxValue;
            Vector3 bestPosition = Vector3.positiveInfinity;
            for (int i = 0; i < scripts.Length; i++)
            {
                var player = scripts[i];
                if (player == null)
                {
                    continue;
                }

                if (!coilhead.PlayerIsTargetable(player, false, false))
                {
                    continue;
                }

                float distance = Vector3.Distance(coilhead.transform.position, player.transform.position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestPosition = player.transform.position;
                }
            }

            if (!float.IsPositiveInfinity(bestPosition.x))
            {
                context.Blackboard.SetCoilheadTarget(bestPosition, memoryDuration);
                return true;
            }

            return context.Blackboard.CoilheadHasAggro && !float.IsPositiveInfinity(context.Blackboard.CoilheadTarget.x);
        }

        internal static bool IsObserved(SpringManAI coilhead)
        {
            var round = StartOfRound.Instance;
            var scripts = round?.allPlayerScripts;
            if (coilhead == null || scripts == null)
            {
                return false;
            }

            var origin = coilhead.transform.position + Vector3.up * 0.6f;
            for (int i = 0; i < scripts.Length; i++)
            {
                var player = scripts[i];
                if (player == null)
                {
                    continue;
                }

                if (!coilhead.PlayerIsTargetable(player, false, false))
                {
                    continue;
                }

                bool los = player.HasLineOfSightToPosition(origin, 68f, 60, -1f) ||
                           player.HasLineOfSightToPosition(origin + Vector3.up * 2.4f, 68f, 60, -1f);
                if (!los)
                {
                    continue;
                }

                if (coilhead.PlayerHasHorizontalLOS(player))
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal static class CoilheadDoorHelper
    {
        private static readonly string[] DoorTypeNames =
        {
            "DoorLock",
            "ManualDoor",
            "SecurityDoor",
            "ShutterDoor",
            "LC_Door"
        };

        private static readonly string[] DoorOpenFieldNames = { "isDoorOpen", "isDoorOpened", "doorOpen", "opened" };
        private static readonly string[] DoorOpenPropertyNames = { "IsDoorOpen", "DoorIsOpen" };
        private static readonly string[] DoorOpenMethodNames = { "OpenDoor", "SetDoorOpen", "ToggleDoor", "ServerToggleDoor" };

        private static NavMeshLink[] _cachedLinks = Array.Empty<NavMeshLink>();
        private static float _cacheTimer;

        internal static bool TryBuildChasePath(NavMeshAgent agent, Vector3 target, out NavMeshPath path)
        {
            path = null;
            if (agent == null)
            {
                return false;
            }

            if (!NavMesh.SamplePosition(target, out var hit, 4f, NavMesh.AllAreas))
            {
                return false;
            }

            var candidate = new NavMeshPath();
            if (!NavMesh.CalculatePath(agent.transform.position, hit.position, NavMesh.AllAreas, candidate))
            {
                return false;
            }

            if (candidate.status == NavMeshPathStatus.PathInvalid)
            {
                return false;
            }

            path = candidate;
            return true;
        }

        internal static DoorInfo InspectPath(NavMeshPath path)
        {
            RefreshLinks();
            if (path == null || path.corners == null || path.corners.Length < 2)
            {
                return DoorInfo.None;
            }

            for (int i = 0; i < _cachedLinks.Length; i++)
            {
                var link = _cachedLinks[i];
                if (link == null || !link.enabled)
                {
                    continue;
                }

                float distance = DistanceToPath(path, link.transform.position);
                if (distance > 0.9f)
                {
                    continue;
                }

                var door = ResolveDoorComponent(link.gameObject);
                if (door == null)
                {
                    continue;
                }

                bool isOpen = GetDoorOpenState(door) ?? true;
                return new DoorInfo(door, link.transform.position, isOpen);
            }

            return DoorInfo.None;
        }

        internal static void ForceDoorOpen(Component door)
        {
            if (door == null)
            {
                return;
            }

            var type = door.GetType();
            for (int i = 0; i < DoorOpenMethodNames.Length; i++)
            {
                var method = AccessTools.Method(type, DoorOpenMethodNames[i], Type.EmptyTypes);
                if (method != null)
                {
                    method.Invoke(door, null);
                    return;
                }

                var methodBool = AccessTools.Method(type, DoorOpenMethodNames[i], new[] { typeof(bool) });
                if (methodBool != null)
                {
                    methodBool.Invoke(door, new object[] { true });
                    return;
                }
            }

            for (int i = 0; i < DoorOpenFieldNames.Length; i++)
            {
                var field = AccessTools.Field(type, DoorOpenFieldNames[i]);
                if (field != null && field.FieldType == typeof(bool))
                {
                    field.SetValue(door, true);
                    return;
                }
            }

            for (int i = 0; i < DoorOpenPropertyNames.Length; i++)
            {
                var property = AccessTools.Property(type, DoorOpenPropertyNames[i]);
                if (property != null && property.CanWrite && property.PropertyType == typeof(bool) && property.GetIndexParameters().Length == 0)
                {
                    property.SetValue(door, true, null);
                    return;
                }
            }
        }

        private static bool? GetDoorOpenState(Component door)
        {
            if (door == null)
            {
                return null;
            }

            var type = door.GetType();
            for (int i = 0; i < DoorOpenFieldNames.Length; i++)
            {
                var field = AccessTools.Field(type, DoorOpenFieldNames[i]);
                if (field != null && field.FieldType == typeof(bool))
                {
                    return (bool)field.GetValue(door);
                }
            }

            for (int i = 0; i < DoorOpenPropertyNames.Length; i++)
            {
                var property = AccessTools.Property(type, DoorOpenPropertyNames[i]);
                if (property != null && property.PropertyType == typeof(bool) && property.GetIndexParameters().Length == 0)
                {
                    return (bool)property.GetValue(door, null);
                }
            }

            return null;
        }

        private static Component ResolveDoorComponent(GameObject origin)
        {
            if (origin == null)
            {
                return null;
            }

            for (int i = 0; i < DoorTypeNames.Length; i++)
            {
                var type = AccessTools.TypeByName(DoorTypeNames[i]);
                if (type == null)
                {
                    continue;
                }

                var component = origin.GetComponentInParent(type);
                if (component != null)
                {
                    return component;
                }
            }

            return null;
        }

        private static float DistanceToPath(NavMeshPath path, Vector3 point)
        {
            float best = float.MaxValue;
            var corners = path.corners;
            for (int i = 1; i < corners.Length; i++)
            {
                var a = corners[i - 1];
                var b = corners[i];
                var closest = ClosestPointOnSegment(a, b, point);
                float distance = Vector3.Distance(point, closest);
                if (distance < best)
                {
                    best = distance;
                }
            }

            return best;
        }

        private static Vector3 ClosestPointOnSegment(Vector3 a, Vector3 b, Vector3 point)
        {
            var ab = b - a;
            float denominator = Vector3.Dot(ab, ab);
            if (denominator <= 0.0001f)
            {
                return a;
            }

            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / denominator);
            return a + ab * t;
        }

        private static void RefreshLinks()
        {
            _cacheTimer -= Time.deltaTime;
            if (_cacheTimer > 0f)
            {
                return;
            }

            _cachedLinks = UnityEngine.Object.FindObjectsOfType<NavMeshLink>();
            _cacheTimer = 2.5f;
        }

        internal readonly struct DoorInfo
        {
            internal static readonly DoorInfo None = new DoorInfo(null, Vector3.positiveInfinity, true);

            internal DoorInfo(Component door, Vector3 position, bool isOpen)
            {
                Door = door;
                Position = position;
                IsOpen = isOpen;
            }

            internal Component Door { get; }
            internal Vector3 Position { get; }
            internal bool IsOpen { get; }
            internal bool HasDoor => Door != null;
        }
    }

    internal static partial class BehaviorTreeFactory
    {
        private static BTNode CreateCoilheadTree()
        {
            return new BTPrioritySelector("CoilheadRoot",
                BuildCoilheadObservationBranch(),
                BuildCoilheadChaseBranch(),
                new BTActionNode("CoilheadPatrol", CoilheadActions.Patrol));
        }

        private static BTNode BuildCoilheadObservationBranch()
        {
            return new BTSequence("CoilheadObservation",
                new BTConditionNode("CoilheadObserved", CoilheadConditions.BeingObserved),
                new BTActionNode("CoilheadFreezeAction", CoilheadActions.Freeze));
        }

        private static BTNode BuildCoilheadChaseBranch()
        {
            return new BTSequence("CoilheadChase",
                new BTConditionNode("CoilheadHasTarget", CoilheadConditions.HasTarget),
                new BTActionNode("CoilheadChaseAction", CoilheadActions.RapidApproach));
        }
    }
}
