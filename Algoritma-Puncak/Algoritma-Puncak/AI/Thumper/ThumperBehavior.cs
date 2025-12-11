using UnityEngine;
using UnityEngine.AI;

namespace AlgoritmaPuncakMod.AI
{
    internal static class ThumperConditions
    {
        internal static bool NeedsRecovery(BTContext context) => context.Blackboard.ThumperNeedsRecovery;

        internal static bool CanEngageTarget(BTContext context)
        {
            var board = context.Blackboard;
            return board.PlayerVisible || board.ThumperPrimingActive || board.ShouldContinueThumperCharge;
        }

        internal static bool CanPrimeCharge(BTContext context)
        {
            var board = context.Blackboard;
            return board.ThumperPrimingActive || board.ShouldContinueThumperCharge || board.ThumperCanPrime;
        }

        internal static bool ShouldHuntFromMemory(BTContext context)
        {
            if (context.Blackboard.PlayerVisible)
            {
                return false;
            }

            var lastPos = context.Blackboard.LastKnownPlayerPosition;
            return !float.IsPositiveInfinity(lastPos.x) && context.Blackboard.TimeSincePlayerSeen < 8f;
        }
    }

    internal static class ThumperActions
    {
        private static readonly int EnvironmentMask = LayerMask.GetMask("Default", "Environment", "Terrain");

        internal static BTStatus Recover(BTContext context)
        {
            var agent = context.Agent;
            if (agent != null)
            {
                agent.ResetPath();
                agent.speed = 0f;
                agent.acceleration = 6f;
            }

            context.SetActiveAction("ThumperRecover");
            return context.Blackboard.ThumperNeedsRecovery ? BTStatus.Running : BTStatus.Success;
        }

        internal static BTStatus PrimeCharge(BTContext context)
        {
            var board = context.Blackboard;
            if (board.ShouldContinueThumperCharge)
            {
                return BTStatus.Success;
            }

            if (!board.PlayerVisible)
            {
                board.ForceThumperIdle();
                return BTStatus.Failure;
            }

            if (!board.ThumperPrimingActive)
            {
                if (!board.ThumperCanPrime)
                {
                    return BTStatus.Failure;
                }

                board.PrimeThumperCharge(context.Enemy.transform.position, board.LastKnownPlayerPosition);
            }

            context.SetActiveAction("ThumperPrime");
            if (context.Agent != null)
            {
                context.Agent.ResetPath();
            }

            return board.ThumperReadyToBurst ? BTStatus.Success : BTStatus.Running;
        }

        internal static BTStatus ExecuteCharge(BTContext context)
        {
            var board = context.Blackboard;
            if (!board.ShouldContinueThumperCharge)
            {
                board.BeginThumperCharge(2.75f);
            }

            if (!board.ShouldContinueThumperCharge)
            {
                return BTStatus.Success;
            }

            var agent = context.Agent;
            if (agent == null)
            {
                board.StopThumperCharge(stunned: false);
                return BTStatus.Failure;
            }

            var destination = board.ThumperChargeDestination;
            if (float.IsPositiveInfinity(destination.x))
            {
                destination = context.Enemy.transform.position + board.ThumperChargeDirection * 10f;
            }

            float progress = Mathf.Clamp01(board.ThumperChargeProgress);
            float speed = Mathf.Lerp(6f, 16f, progress);
            float acceleration = Mathf.Lerp(12f, 40f, progress);

            if (!NavigationHelpers.TryMoveAgent(
                context,
                destination,
                1.5f,
                60f,
                "ThumperCharge",
                speed,
                acceleration,
                stoppingDistance: 0.05f,
                allowPartialPath: true))
            {
                board.StopThumperCharge(stunned: false);
                return BTStatus.Failure;
            }

            if (DetectImpact(context))
            {
                board.StopThumperCharge(stunned: true);
                context.SetActiveAction("ThumperImpact");
                return BTStatus.Running;
            }

            context.SetActiveAction("ThumperCharge");
            return board.ShouldContinueThumperCharge ? BTStatus.Running : BTStatus.Success;
        }

        private static bool DetectImpact(BTContext context)
        {
            var agent = context.Agent;
            if (agent == null)
            {
                return false;
            }

            if (agent.velocity.sqrMagnitude < 1.5f && agent.remainingDistance > 1.25f)
            {
                return true;
            }

            var origin = agent.transform.position + Vector3.up * 0.25f;
            return Physics.SphereCast(origin, 0.35f, context.Blackboard.ThumperChargeDirection, out var hit, 0.9f, EnvironmentMask, QueryTriggerInteraction.Ignore)
                && !hit.collider.CompareTag("Player");
        }

        internal static BTStatus MoveToLastKnown(BTContext context)
        {
            var lastPos = context.Blackboard.LastKnownPlayerPosition;
            if (float.IsPositiveInfinity(lastPos.x))
            {
                return BTStatus.Failure;
            }

            if (Vector3.Distance(context.Enemy.transform.position, lastPos) < 1f)
            {
                context.SetActiveAction("ThumperTrack");
                return BTStatus.Success;
            }

            if (NavigationHelpers.TryMoveAgent(
                context,
                lastPos,
                3f,
                context.Profile.HuntAggroDistance * 1.5f,
                "ThumperTrack",
                6.5f,
                acceleration: 14f,
                stoppingDistance: 0.35f,
                allowPartialPath: true))
            {
                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        internal static BTStatus ExecutePredictiveSprint(BTContext context)
        {
            var board = context.Blackboard;
            if (!board.HasThumperSprintTarget)
            {
                var anchor = board.LastKnownPlayerPosition;
                if (!ThumperNavigation.TryProjectCorridor(anchor, context.Enemy.transform.forward, context.Profile.HuntAggroDistance * 1.75f, out var sprintTarget))
                {
                    return BTStatus.Failure;
                }

                board.SetThumperSprintTarget(sprintTarget);
            }

            var target = board.ThumperSprintTarget;
            if (Vector3.Distance(context.Enemy.transform.position, target) < 1.2f || board.PlayerVisible)
            {
                board.ClearThumperSprintTarget();
                return BTStatus.Success;
            }

            if (NavigationHelpers.TryMoveAgent(
                context,
                target,
                2f,
                context.Profile.HuntAggroDistance * 2f,
                "ThumperPredict",
                8.5f,
                acceleration: 18f,
                stoppingDistance: 0.4f,
                allowPartialPath: true))
            {
                return BTStatus.Running;
            }

            board.ClearThumperSprintTarget();
            return BTStatus.Failure;
        }

        internal static BTStatus ExecutePatrol(BTContext context)
        {
            var board = context.Blackboard;
            if (!board.HasThumperPatrolTarget || board.ShouldRefreshThumperPatrol)
            {
                if (!ThumperNavigation.TrySelectPatrolTarget(
                    context.Enemy.transform.position,
                    context.Enemy.transform.forward,
                    board.TerritoryCenter,
                    Mathf.Max(6f, board.TerritoryRadius),
                    out var patrolTarget))
                {
                    return BTStatus.Failure;
                }

                board.SetThumperPatrolTarget(patrolTarget);
            }

            var target = board.ThumperPatrolTarget;
            if (Vector3.Distance(context.Enemy.transform.position, target) < 1.25f)
            {
                board.ClearThumperPatrolTarget();
                return BTStatus.Success;
            }

            if (NavigationHelpers.TryMoveAgent(
                context,
                target,
                3f,
                board.TerritoryRadius * 2.5f,
                "ThumperPatrol",
                5.25f,
                acceleration: 10f,
                stoppingDistance: 0.5f,
                allowPartialPath: true))
            {
                return BTStatus.Running;
            }

            board.ClearThumperPatrolTarget();
            return BTStatus.Failure;
        }
    }

    internal static class ThumperNavigation
    {
        private static readonly Vector3[] DirectionSeeds;

        static ThumperNavigation()
        {
            DirectionSeeds = new Vector3[16];
            for (int i = 0; i < DirectionSeeds.Length; i++)
            {
                float angle = (360f / DirectionSeeds.Length) * i * Mathf.Deg2Rad;
                DirectionSeeds[i] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }
        }

        internal static bool TryProjectCorridor(Vector3 origin, Vector3 forward, float maxDistance, out Vector3 target)
        {
            target = origin;
            if (!NavMesh.SamplePosition(origin, out var anchor, 2f, NavMesh.AllAreas))
            {
                anchor.position = origin;
            }

            float bestScore = float.MinValue;
            for (int i = 0; i < DirectionSeeds.Length; i++)
            {
                var dir = DirectionSeeds[i];
                if (!TryProjectDirection(anchor.position, dir, maxDistance, out var candidate))
                {
                    continue;
                }

                float travel = Vector3.Distance(anchor.position, candidate);
                float alignment = Mathf.Abs(Vector3.Dot(forward.normalized, dir));
                float score = travel * 0.7f + alignment * 3f;
                if (score > bestScore)
                {
                    bestScore = score;
                    target = candidate;
                }
            }

            return bestScore > 0f;
        }

        internal static bool TrySelectPatrolTarget(Vector3 origin, Vector3 forward, Vector3 territoryCenter, float radius, out Vector3 target)
        {
            target = origin;
            if (!NavMesh.SamplePosition(origin, out var anchor, 3f, NavMesh.AllAreas))
            {
                anchor.position = origin;
            }

            float bestScore = float.MinValue;
            for (int i = 0; i < DirectionSeeds.Length; i++)
            {
                var dir = DirectionSeeds[i];
                if (!TryProjectDirection(anchor.position, dir, radius * 1.35f, out var candidate))
                {
                    continue;
                }

                float travel = Vector3.Distance(anchor.position, candidate);
                float turnPenalty = Vector3.Angle(forward, dir) / 90f;
                float centerBias = Vector3.Distance(candidate, territoryCenter) / Mathf.Max(1f, radius);
                float straightness = Mathf.Abs(Vector3.Dot((candidate - anchor.position).normalized, dir));
                float score = travel * 0.6f + straightness * 3f - turnPenalty * 2.1f - centerBias * 1.2f;
                if (score > bestScore)
                {
                    bestScore = score;
                    target = candidate;
                }
            }

            return bestScore > float.MinValue;
        }

        private static bool TryProjectDirection(Vector3 origin, Vector3 direction, float distance, out Vector3 destination)
        {
            if (direction.sqrMagnitude < 0.001f)
            {
                destination = origin;
                return false;
            }

            direction.Normalize();
            var target = origin + direction * distance;
            if (NavMesh.Raycast(origin, target, out var hit, NavMesh.AllAreas))
            {
                if (hit.distance < 2f)
                {
                    destination = origin;
                    return false;
                }

                destination = hit.position;
                return true;
            }

            destination = target;
            return true;
        }
    }

    internal static partial class BehaviorTreeFactory
    {
        private static BTNode CreateThumperTree()
        {
            return new BTPrioritySelector("ThumperRoot",
                BuildThumperRecoveryBranch(),
                BuildThumperChargeBranch(),
                BuildThumperHuntBranch(),
                new BTActionNode("ThumperPatrol", ThumperActions.ExecutePatrol));
        }

        private static BTNode BuildThumperRecoveryBranch()
        {
            return new BTSequence("ThumperRecover",
                new BTConditionNode("ThumperNeedsRecovery", ThumperConditions.NeedsRecovery),
                new BTActionNode("ThumperRecoverAction", ThumperActions.Recover));
        }

        private static BTNode BuildThumperChargeBranch()
        {
            return new BTSequence("ThumperChargeSequence",
                new BTConditionNode("ThumperSeesPlayer", ThumperConditions.CanEngageTarget),
                new BTConditionNode("ThumperCanPrime", ThumperConditions.CanPrimeCharge),
                new BTActionNode("ThumperPrimeAction", ThumperActions.PrimeCharge),
                new BTActionNode("ThumperChargeAction", ThumperActions.ExecuteCharge));
        }

        private static BTNode BuildThumperHuntBranch()
        {
            return new BTSequence("ThumperHunt",
                new BTConditionNode("ThumperHuntMemory", ThumperConditions.ShouldHuntFromMemory),
                new BTActionNode("ThumperMoveToLKP", ThumperActions.MoveToLastKnown),
                new BTActionNode("ThumperPredict", ThumperActions.ExecutePredictiveSprint));
        }
    }
}
