using UnityEngine;

namespace AlgoritmaPuncakMod.AI
{
    internal static class FlowermanConditions
    {
        internal static bool HasTarget(BTContext context)
        {
            return !float.IsPositiveInfinity(context.Blackboard.LastKnownPlayerPosition.x);
        }

        internal static bool BeingWatched(BTContext context)
        {
            return HasTarget(context) && context.Blackboard.FlowermanPlayerWatching(context.Enemy.transform.position);
        }

        internal static bool ShouldAggro(BTContext context)
        {
            if (!HasTarget(context))
            {
                return false;
            }

            if (context.Blackboard.FlowermanAngerReady)
            {
                return true;
            }

            return context.Blackboard.FlowermanPlayerWatching(context.Enemy.transform.position)
                && context.Blackboard.DistanceToPlayer <= 5f;
        }

        internal static bool CanStalk(BTContext context)
        {
            if (!HasTarget(context))
            {
                return false;
            }

            return context.Blackboard.DistanceToPlayer > 2.5f;
        }
    }

    internal static class FlowermanActions
    {
        internal static BTStatus UpdateVectors(BTContext context)
        {
            context.Blackboard.RefreshFlowermanVectors(context.Enemy.transform.position);
            if (context.Blackboard.FlowermanHasFlank || context.Blackboard.FlowermanHasBlindSpot)
            {
                context.SetActiveAction("FlowermanPlanVectors");
                return BTStatus.Success;
            }

            return BTStatus.Failure;
        }

        internal static BTStatus StareDown(BTContext context)
        {
            var board = context.Blackboard;
            var agent = context.Agent;
            if (agent != null)
            {
                agent.ResetPath();
                agent.speed = 0f;
                agent.acceleration = 4f;
            }

            var target = board.LastKnownPlayerPosition;
            if (!float.IsPositiveInfinity(target.x))
            {
                var enemyTransform = context.Enemy.transform;
                var lookDir = target - enemyTransform.position;
                lookDir.y = 0f;
                if (lookDir.sqrMagnitude > 0.01f)
                {
                    var lookRotation = Quaternion.LookRotation(lookDir.normalized, Vector3.up);
                    enemyTransform.rotation = Quaternion.Lerp(enemyTransform.rotation, lookRotation, context.DeltaTime * 6f);
                }
            }

            float closeBoost = Mathf.Clamp01((8f - board.DistanceToPlayer) / 8f);
            board.AddFlowermanAnger(context.DeltaTime * (1.5f + closeBoost));
            context.SetActiveAction("FlowermanStare");
            return BTStatus.Success;
        }

        internal static BTStatus BreakLineOfSight(BTContext context)
        {
            var board = context.Blackboard;
            if (!board.FlowermanPlayerWatching(context.Enemy.transform.position))
            {
                return BTStatus.Success;
            }

            if (!board.FlowermanHasEscape)
            {
                board.RequestFlowermanEscape(context.Enemy.transform.position);
            }

            var target = board.FlowermanEscapeTarget;
            if (float.IsPositiveInfinity(target.x))
            {
                return BTStatus.Failure;
            }

            if (Vector3.Distance(context.Enemy.transform.position, target) < 0.8f)
            {
                board.CoolFlowermanAnger(context.DeltaTime * 0.2f);
                return BTStatus.Success;
            }

            if (NavigationHelpers.TryMoveAgent(
                context,
                target,
                3f,
                24f,
                "FlowermanFlee",
                5.5f,
                acceleration: 10.5f,
                stoppingDistance: 0.4f,
                allowPartialPath: true))
            {
                board.AddFlowermanAnger(context.DeltaTime * 0.5f);
                return BTStatus.Running;
            }

            board.RequestFlowermanEscape(context.Enemy.transform.position);
            return BTStatus.Failure;
        }

        internal static BTStatus SweepShadow(BTContext context)
        {
            var board = context.Blackboard;
            var enemyPos = context.Enemy.transform.position;
            board.RefreshFlowermanVectors(enemyPos);

            var blindSpot = board.FlowermanBlindSpot;
            if (!float.IsPositiveInfinity(blindSpot.x))
            {
                float blindDistance = Vector3.Distance(enemyPos, blindSpot);
                if (blindDistance > 1.25f)
                {
                    float sneakSpeed = Mathf.Lerp(1.6f, 3f, Mathf.Clamp01(blindDistance / 6f));
                    if (NavigationHelpers.TryMoveAgent(
                        context,
                        blindSpot,
                        3f,
                        30f,
                        "FlowermanSneak",
                        sneakSpeed,
                        acceleration: 4.5f,
                        stoppingDistance: 0.35f,
                        allowPartialPath: true))
                    {
                        context.SetActiveAction("FlowermanSneak");
                        return BTStatus.Running;
                    }
                }
            }

            var flank = board.FlowermanFlankTarget;
            if (float.IsPositiveInfinity(flank.x))
            {
                return BTStatus.Failure;
            }

            float speed = 3.75f;
            if (board.DistanceToPlayer > 12f)
            {
                speed = 4.5f;
            }

            if (NavigationHelpers.TryMoveAgent(
                context,
                flank,
                4f,
                60f,
                "FlowermanStalk",
                speed,
                acceleration: 7f,
                stoppingDistance: 0.6f,
                allowPartialPath: true))
            {
                board.AddFlowermanAnger(context.DeltaTime * 0.2f);
                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        internal static BTStatus ExecuteAggro(BTContext context)
        {
            var board = context.Blackboard;
            var target = board.LastKnownPlayerPosition;
            if (float.IsPositiveInfinity(target.x))
            {
                return BTStatus.Failure;
            }

            float aggression = Mathf.Lerp(0.85f, 1.35f, board.FlowermanAngerRatio);
            if (NavigationHelpers.TryMoveAgent(
                context,
                target,
                4f,
                board.TerritoryRadius * 4f + 30f,
                "FlowermanAggro",
                8.5f * aggression,
                acceleration: 16f,
                stoppingDistance: 0.25f))
            {
                if (Vector3.Distance(context.Enemy.transform.position, target) <= 1.8f)
                {
                    board.ResetFlowermanAnger();
                    context.SetActiveAction("FlowermanSnapWindow");
                    return BTStatus.Success;
                }

                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        internal static BTStatus IdleHaunt(BTContext context)
        {
            var board = context.Blackboard;
            var anchor = board.TerritoryCenter == Vector3.zero ? context.Enemy.transform.position : board.TerritoryCenter;
            var wander = anchor + UnityEngine.Random.insideUnitSphere * 3f;
            wander.y = anchor.y;

            if (NavigationHelpers.TryMoveAgent(
                context,
                wander,
                3f,
                20f,
                "FlowermanIdle",
                2.2f,
                acceleration: 4f,
                stoppingDistance: 0.35f,
                allowPartialPath: true))
            {
                board.CoolFlowermanAnger(context.DeltaTime * 0.4f);
                return BTStatus.Running;
            }

            board.CoolFlowermanAnger(context.DeltaTime * 0.5f);
            return BTStatus.Failure;
        }
    }

    internal static partial class BehaviorTreeFactory
    {
        private static BTNode CreateFlowermanTree()
        {
            return new BTPrioritySelector("FlowermanRoot",
                BuildFlowermanAggroBranch(),
                BuildFlowermanStareBranch(),
                BuildFlowermanStalkBranch(),
                new BTActionNode("FlowermanIdle", FlowermanActions.IdleHaunt));
        }

        private static BTNode BuildFlowermanAggroBranch()
        {
            return new BTSequence("FlowermanAggro",
                new BTConditionNode("FlowermanAggroReady", FlowermanConditions.ShouldAggro),
                new BTActionNode("FlowermanVectorRefresh", FlowermanActions.UpdateVectors),
                new BTActionNode("FlowermanAggroExecute", FlowermanActions.ExecuteAggro));
        }

        private static BTNode BuildFlowermanStareBranch()
        {
            return new BTSequence("FlowermanStare",
                new BTConditionNode("FlowermanObserved", FlowermanConditions.BeingWatched),
                new BTActionNode("FlowermanStareAction", FlowermanActions.StareDown),
                new BTActionNode("FlowermanBreakSight", FlowermanActions.BreakLineOfSight));
        }

        private static BTNode BuildFlowermanStalkBranch()
        {
            return new BTSequence("FlowermanStalk",
                new BTConditionNode("FlowermanCanStalk", FlowermanConditions.CanStalk),
                new BTActionNode("FlowermanPlan", FlowermanActions.UpdateVectors),
                new BTActionNode("FlowermanShadow", FlowermanActions.SweepShadow));
        }
    }
}
