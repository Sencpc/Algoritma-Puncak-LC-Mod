using UnityEngine;

namespace AlgoritmaPuncakMod.AI
{
    internal static class MouthDogConditions
    {
        internal static bool HasHighPriorityCue(BTContext context)
        {
            return context.MouthDog != null && context.Blackboard.MouthDogHasHighStimulus && context.Blackboard.MouthDogChargeReady;
        }

        internal static bool ShouldInvestigate(BTContext context)
        {
            return context.MouthDog != null && context.Blackboard.MouthDogHasLowStimulus;
        }
    }

    internal static class MouthDogActions
    {
        internal static BTStatus ChargeHighNoise(BTContext context)
        {
            if (context.Agent == null || context.MouthDog == null || !context.Blackboard.MouthDogHasHighStimulus)
            {
                return BTStatus.Failure;
            }

            if (context.Blackboard.MouthDogConsumeHighInterrupt())
            {
                // High-priority cues must immediately replace any inflight navigation path.
                context.Agent.ResetPath();
                context.Agent.velocity = Vector3.zero;
                context.Agent.isStopped = false;
            }

            var target = context.Blackboard.MouthDogHighStimulus;
            var enemyPos = context.Enemy.transform.position;
            float distance = Vector3.Distance(enemyPos, target);
            if (distance <= 0.75f)
            {
                context.Blackboard.ClearMouthDogHighStimulus();
                TrySetAnimatorBool(context.Enemy, "StartedChase", false);
                return BTStatus.Success;
            }

            float intensity = Mathf.Clamp01(context.Blackboard.MouthDogHighIntensityNormalized);
            float speed = Mathf.Lerp(8f, 12.5f, intensity);
            float acceleration = Mathf.Lerp(18f, 30f, intensity);

            if (NavigationHelpers.TryMoveAgent(
                context,
                target,
                3.5f,
                60f,
                "MouthDogCharge",
                speed,
                acceleration: acceleration,
                stoppingDistance: 0.1f,
                allowPartialPath: true))
            {
                context.Blackboard.BeginMouthDogChargeCooldown(Mathf.Lerp(0.35f, 0.85f, intensity));
                TrySetAnimatorBool(context.Enemy, "StartedChase", true);
                TrySetAnimatorFloat(context.Enemy, "speedMultiplier", context.Agent.velocity.magnitude);
                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        internal static BTStatus InvestigateNoise(BTContext context)
        {
            if (context.Agent == null || context.MouthDog == null || !context.Blackboard.MouthDogHasLowStimulus)
            {
                return BTStatus.Failure;
            }

            if (context.Blackboard.MouthDogConsumeLowInterrupt())
            {
                // Low-priority shuffles still interrupt the agent to keep the dog reactive.
                context.Agent.ResetPath();
                context.Agent.velocity = Vector3.zero;
                context.Agent.isStopped = false;
            }

            var target = context.Blackboard.MouthDogLowStimulus;
            var enemyPos = context.Enemy.transform.position;
            float distance = Vector3.Distance(enemyPos, target);
            if (distance <= 0.9f)
            {
                context.Blackboard.ClearMouthDogLowStimulus();
                TrySetAnimatorBool(context.Enemy, "StartedChase", false);
                return BTStatus.Success;
            }

            float intensity = Mathf.Max(0.2f, context.Blackboard.MouthDogLowIntensityNormalized);
            if (NavigationHelpers.TryMoveAgent(
                context,
                target,
                3f,
                50f,
                "MouthDogInvestigate",
                Mathf.Lerp(4.25f, 7.25f, intensity),
                acceleration: Mathf.Lerp(9f, 18f, intensity),
                stoppingDistance: 0.35f,
                allowPartialPath: true))
            {
                // Use a moderate speed animation while investigating
                TrySetAnimatorBool(context.Enemy, "StartedChase", true);
                TrySetAnimatorFloat(context.Enemy, "speedMultiplier", context.Agent.velocity.magnitude);
                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        internal static BTStatus ProwlTerritory(BTContext context)
        {
            if (context.Agent == null || context.MouthDog == null)
            {
                return BTStatus.Failure;
            }

            var origin = context.Enemy.transform.position;
            var roam = context.Blackboard.GetMouthDogRoamPoint(origin) + NavigationHelpers.GetAgentOffset(context.Enemy, 0.65f);

            if (NavigationHelpers.TryMoveAgent(
                context,
                roam,
                4f,
                30f,
                "MouthDogProwl",
                4.25f,
                acceleration: 8.5f,
                stoppingDistance: 0.6f,
                allowPartialPath: true))
            {
                // Idle/walk state; ensure chase flag off so lounge isn’t stuck
                TrySetAnimatorBool(context.Enemy, "StartedChase", false);
                TrySetAnimatorFloat(context.Enemy, "speedMultiplier", context.Agent.velocity.magnitude);
                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        private static void TrySetAnimatorBool(EnemyAI enemy, string name, bool value)
        {
            try
            {
                var anim = enemy?.creatureAnimator;
                if (anim != null)
                {
                    anim.SetBool(name, value);
                }
            }
            catch { }
        }

        private static void TrySetAnimatorFloat(EnemyAI enemy, string name, float value)
        {
            try
            {
                var anim = enemy?.creatureAnimator;
                if (anim != null)
                {
                    anim.SetFloat(name, value);
                }
            }
            catch { }
        }
    }

    internal static partial class BehaviorTreeFactory
    {
        private static BTNode CreateMouthDogTree()
        {
            return new BTPrioritySelector("MouthDogRoot",
                BuildMouthDogHighPriorityBranch(),
                BuildMouthDogInvestigateBranch(),
                new BTActionNode("MouthDogProwlAction", MouthDogActions.ProwlTerritory));
        }

        private static BTNode BuildMouthDogHighPriorityBranch()
        {
            return new BTSequence("MouthDogCharge",
                new BTConditionNode("MouthDogHighNoise", MouthDogConditions.HasHighPriorityCue),
                new BTActionNode("MouthDogChargeAction", MouthDogActions.ChargeHighNoise));
        }

        private static BTNode BuildMouthDogInvestigateBranch()
        {
            return new BTSequence("MouthDogInvestigate",
                new BTConditionNode("MouthDogLowNoise", MouthDogConditions.ShouldInvestigate),
                new BTActionNode("MouthDogInvestigateAction", MouthDogActions.InvestigateNoise));
        }
    }
}
