using System;
using System.Collections.Generic;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.AI;

namespace AlgoritmaPuncakMod.AI
{
    internal static class BlobConditions
    {
        internal static bool HazardDetected(BTContext context)
        {
            return BlobActions.TryDetectHazard(context);
        }

        internal static bool ShouldAmbush(BTContext context)
        {
            bool playerFar = context.Blackboard.DistanceToPlayer > context.Profile.TerritoryRadius * 1.25f;
            bool lostPlayer = context.Blackboard.TimeSincePlayerSeen > 4f;
            return playerFar || lostPlayer;
        }
    }

    internal static class BlobActions
    {
        private static readonly int HazardMask = Physics.DefaultRaycastLayers;
        private static readonly string[] HazardKeywords = { "spike", "trap", "blade", "saw" };

        internal static bool TryDetectHazard(BTContext context)
        {
            if (context.Blackboard.BlobHazardActive)
            {
                return true;
            }

            var agent = context.Agent;
            if (agent == null)
            {
                return false;
            }

            var direction = agent.hasPath ? agent.steeringTarget - agent.transform.position : agent.transform.forward;
            if (direction.sqrMagnitude < 0.1f)
            {
                direction = context.Enemy.transform.forward;
            }

            if (direction.sqrMagnitude < 0.1f)
            {
                return false;
            }

            var origin = agent.transform.position + Vector3.up * 0.2f;
            if (Physics.SphereCast(origin, 0.55f, direction.normalized, out var hit, 3.75f, HazardMask, QueryTriggerInteraction.Ignore))
            {
                if (IsSpikeTrap(hit.collider))
                {
                    var lateral = Vector3.Cross(Vector3.up, direction).normalized;
                    if (lateral.sqrMagnitude < 0.01f)
                    {
                        lateral = Vector3.Cross(direction, Vector3.up).normalized;
                    }

                    var rerouteGuess = origin + lateral * 3.5f;
                    if (!NavMesh.SamplePosition(rerouteGuess, out var nav, 4f, NavMesh.AllAreas))
                    {
                        rerouteGuess = origin - lateral * 3.5f;
                        if (!NavMesh.SamplePosition(rerouteGuess, out nav, 4f, NavMesh.AllAreas))
                        {
                            return false;
                        }
                    }

                    context.Blackboard.SetBlobHazard(hit.point, nav.position);
                    return true;
                }
            }

            return false;
        }

        internal static BTStatus RerouteHazard(BTContext context)
        {
            var target = context.Blackboard.BlobHazardAvoidTarget;
            if (float.IsPositiveInfinity(target.x))
            {
                context.Blackboard.ClearBlobHazard();
                return BTStatus.Failure;
            }

            if (Vector3.Distance(context.Enemy.transform.position, target) < 0.9f)
            {
                context.Blackboard.ClearBlobHazard();
                context.SetActiveAction("BlobHazardCleared");
                return BTStatus.Success;
            }

            if (NavigationHelpers.TryMoveAgent(
                context,
                target,
                1.25f,
                18f,
                "BlobAvoidHazard",
                2.2f,
                acceleration: 4.5f,
                stoppingDistance: 0.35f,
                allowPartialPath: true))
            {
                return BTStatus.Running;
            }

            context.Blackboard.ClearBlobHazard();
            return BTStatus.Failure;
        }

        internal static BTStatus MoveToAmbush(BTContext context)
        {
            if (!EnsureAmbushAnchor(context))
            {
                return BTStatus.Failure;
            }

            var target = context.Blackboard.BlobAmbushAnchor;
            if (Vector3.Distance(context.Enemy.transform.position, target) < 1.25f)
            {
                context.SetActiveAction("BlobAmbushHold");
                return BTStatus.Success;
            }

            if (NavigationHelpers.TryMoveAgent(
                context,
                target,
                2.5f,
                context.Profile.TerritoryRadius * 3f,
                "BlobAmbush",
                1.8f,
                acceleration: 4f,
                stoppingDistance: 0.4f,
                allowPartialPath: true))
            {
                return BTStatus.Running;
            }

            context.Blackboard.ClearBlobAmbush();
            return BTStatus.Failure;
        }

        internal static BTStatus UpdateIntercept(BTContext context)
        {
            var players = BlobNavigation.GatherLivePlayers();
            if (BlobExitLocator.TryGetPreferredExit(players, out _, out var intercept))
            {
                if (NavMesh.SamplePosition(intercept, out var hit, 3f, NavMesh.AllAreas))
                {
                    intercept = hit.position;
                }

                context.Blackboard.SetBlobIntercept(intercept);
                return BTStatus.Success;
            }

            context.Blackboard.ClearBlobIntercept();
            return BTStatus.Failure;
        }

        internal static BTStatus MoveToIntercept(BTContext context)
        {
            var target = context.Blackboard.BlobInterceptTarget;
            if (float.IsPositiveInfinity(target.x))
            {
                return BTStatus.Failure;
            }

            float distance = Vector3.Distance(context.Enemy.transform.position, target);
            if (distance < 1.25f)
            {
                context.SetActiveAction("BlobBlockingExit");
                return BTStatus.Success;
            }

            if (NavigationHelpers.TryMoveAgent(
                context,
                target,
                3f,
                120f,
                "BlobIntercept",
                2.9f,
                acceleration: 6f,
                stoppingDistance: 0.35f,
                allowPartialPath: true))
            {
                return BTStatus.Running;
            }

            context.Blackboard.ClearBlobIntercept();
            return BTStatus.Failure;
        }

        internal static BTStatus PursueOmniscient(BTContext context)
        {
            if (!TryGetNearestPlayer(context.Enemy.transform.position, out var playerPosition))
            {
                return BTStatus.Failure;
            }

            if (NavigationHelpers.TryMoveAgent(
                context,
                playerPosition,
                2.5f,
                160f,
                "BlobPursue",
                3.4f,
                acceleration: 7f,
                stoppingDistance: 0.2f,
                allowPartialPath: true))
            {
                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        internal static BTStatus IdleCreep(BTContext context)
        {
            var origin = context.Enemy.transform.position;
            var offset = UnityEngine.Random.insideUnitSphere;
            offset.y = 0f;
            offset = offset.normalized * UnityEngine.Random.Range(0.5f, 2f);
            var target = origin + offset;

            if (NavigationHelpers.TryMoveAgent(
                context,
                target,
                1.5f,
                8f,
                "BlobIdle",
                1.2f,
                acceleration: 3f,
                stoppingDistance: 0.3f,
                allowPartialPath: true))
            {
                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        private static bool EnsureAmbushAnchor(BTContext context)
        {
            if (context.Blackboard.BlobHasAmbush)
            {
                return true;
            }

            var players = BlobNavigation.GatherLivePlayers();
            if (!BlobExitLocator.TryGetPreferredExit(players, out _, out var anchor))
            {
                return false;
            }

            if (NavMesh.SamplePosition(anchor, out var hit, 3f, NavMesh.AllAreas))
            {
                anchor = hit.position;
            }

            context.Blackboard.SetBlobAmbushAnchor(anchor);
            return true;
        }

        private static bool TryGetNearestPlayer(Vector3 enemyPosition, out Vector3 playerPosition)
        {
            playerPosition = Vector3.positiveInfinity;
            var players = BlobNavigation.GatherLivePlayers();
            float best = float.MaxValue;

            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null)
                {
                    continue;
                }

                float distance = Vector3.Distance(enemyPosition, player.transform.position);
                if (distance < best)
                {
                    best = distance;
                    playerPosition = player.transform.position;
                }
            }

            return best < float.MaxValue;
        }

        private static bool IsSpikeTrap(Collider collider)
        {
            if (collider == null)
            {
                return false;
            }

            if (collider.CompareTag("Trap"))
            {
                return true;
            }

            var name = collider.name ?? string.Empty;
            name = name.ToLowerInvariant();
            for (int i = 0; i < HazardKeywords.Length; i++)
            {
                if (name.Contains(HazardKeywords[i]))
                {
                    return true;
                }
            }

            return false;
        }
    }

    internal static class BlobNavigation
    {
        private static readonly List<PlayerControllerB> PlayerBuffer = new List<PlayerControllerB>(8);

        internal static IReadOnlyList<PlayerControllerB> GatherLivePlayers()
        {
            PlayerBuffer.Clear();
            var round = StartOfRound.Instance;
            if (round?.allPlayerScripts == null)
            {
                return PlayerBuffer;
            }

            for (int i = 0; i < round.allPlayerScripts.Length; i++)
            {
                var player = round.allPlayerScripts[i];
                if (player == null || player.isPlayerDead)
                {
                    continue;
                }

                PlayerBuffer.Add(player);
            }

            return PlayerBuffer;
        }
    }

    internal static class BlobExitLocator
    {
        internal struct ExitDescriptor
        {
            internal Vector3 Position;
            internal Vector3 Forward;
            internal bool IsValid => !float.IsPositiveInfinity(Position.x);

            internal Vector3 ApproachPoint(float offset)
            {
                var forward = Forward.sqrMagnitude < 0.01f ? Vector3.forward : Forward.normalized;
                return Position - forward * offset;
            }
        }

        private static bool _initialized;
        private static ExitDescriptor _mainEntrance;
        private static ExitDescriptor _fireExit;

        internal static bool TryGetExits(out ExitDescriptor main, out ExitDescriptor fire)
        {
            if (!_initialized)
            {
                Initialize();
            }

            main = _mainEntrance;
            fire = _fireExit;
            return main.IsValid || fire.IsValid;
        }

        private static void Initialize()
        {
            _initialized = true;
            var teleports = UnityEngine.Object.FindObjectsOfType<EntranceTeleport>(true);
            if (teleports == null || teleports.Length == 0)
            {
                _mainEntrance = default;
                _fireExit = default;
                return;
            }

            Array.Sort(teleports, (a, b) => string.Compare(a.name, b.name, StringComparison.Ordinal));
            _mainEntrance = CreateDescriptor(teleports[0]);

            if (teleports.Length > 1)
            {
                _fireExit = CreateDescriptor(teleports[teleports.Length - 1]);
            }
            else
            {
                _fireExit = _mainEntrance;
            }
        }

        private static ExitDescriptor CreateDescriptor(EntranceTeleport teleport)
        {
            if (teleport == null)
            {
                return default;
            }

            var anchor = teleport.transform;
            var forward = anchor.forward.sqrMagnitude > 0.001f ? anchor.forward.normalized : Vector3.forward;
            return new ExitDescriptor
            {
                Position = anchor.position,
                Forward = forward
            };
        }

        internal static bool TryGetPreferredExit(IReadOnlyList<PlayerControllerB> players, out ExitDescriptor exit, out Vector3 approach)
        {
            exit = default;
            approach = Vector3.positiveInfinity;

            if (!TryGetExits(out var main, out var fire))
            {
                return false;
            }

            float bestScore = float.MaxValue;

            if (players != null && players.Count > 0)
            {
                for (int i = 0; i < players.Count; i++)
                {
                    var player = players[i];
                    if (player == null)
                    {
                        continue;
                    }

                    var position = player.transform.position;
                    EvaluateExitCandidate(main, position, ref bestScore, ref exit, ref approach);
                    EvaluateExitCandidate(fire, position, ref bestScore, ref exit, ref approach);
                }
            }

            if (bestScore < float.MaxValue)
            {
                return true;
            }

            exit = main.IsValid ? main : fire;
            approach = exit.ApproachPoint(3f);
            return exit.IsValid;
        }

        private static void EvaluateExitCandidate(ExitDescriptor descriptor, Vector3 playerPosition, ref float bestScore, ref ExitDescriptor exit, ref Vector3 approach)
        {
            if (!descriptor.IsValid)
            {
                return;
            }

            float distance = Vector3.Distance(playerPosition, descriptor.Position);
            if (distance < bestScore)
            {
                bestScore = distance;
                exit = descriptor;
                approach = descriptor.ApproachPoint(3f);
            }
        }
    }

    internal static partial class BehaviorTreeFactory
    {
        private static BTNode CreateBlobTree()
        {
            return new BTPrioritySelector("BlobRoot",
                BuildBlobHazardBranch(),
                BuildBlobAmbushBranch(),
                BuildBlobPursuitBranch(),
                new BTActionNode("BlobIdle", BlobActions.IdleCreep));
        }

        private static BTNode BuildBlobHazardBranch()
        {
            return new BTSequence("BlobHazardSequence",
                new BTConditionNode("BlobHazardDetected", BlobConditions.HazardDetected),
                new BTActionNode("BlobHazardReroute", BlobActions.RerouteHazard));
        }

        private static BTNode BuildBlobAmbushBranch()
        {
            return new BTSequence("BlobAmbushSequence",
                new BTConditionNode("BlobShouldAmbush", BlobConditions.ShouldAmbush),
                new BTActionNode("BlobAmbushMove", BlobActions.MoveToAmbush));
        }

        private static BTNode BuildBlobPursuitBranch()
        {
            return new BTPrioritySelector("BlobPursuit",
                new BTSequence("BlobFlowIntercept",
                    new BTActionNode("BlobUpdateIntercept", BlobActions.UpdateIntercept),
                    new BTActionNode("BlobMoveIntercept", BlobActions.MoveToIntercept)),
                new BTActionNode("BlobPursueDirect", BlobActions.PursueOmniscient));
        }
    }
}
