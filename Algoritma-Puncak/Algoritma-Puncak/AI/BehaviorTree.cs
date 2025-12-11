using System;
using System.Collections.Generic;
using HarmonyLib;
using System.Reflection;
using UnityEngine;
using UnityEngine.AI;

namespace AlgoritmaPuncakMod.AI
{
    internal enum BTStatus
    {
        Failure,
        Success,
        Running
    }

    internal sealed class BTContext
    {
        internal BTContext(EnemyAI enemy, NavMeshAgent agent, AIBlackboard blackboard)
        {
            Enemy = enemy;
            Agent = agent;
            Blackboard = blackboard;
            Spider = enemy as SandSpiderAI;
            Bracken = enemy as FlowermanAI;
            Hoarder = enemy as HoarderBugAI;
            Coilhead = enemy as SpringManAI;
            Baboon = enemy as BaboonBirdAI;
            SandWorm = enemy as SandWormAI;
            MouthDog = enemy as MouthDogAI;
        }

        internal EnemyAI Enemy { get; }
        internal NavMeshAgent Agent { get; }
        internal AIBlackboard Blackboard { get; }
        internal SandSpiderAI Spider { get; }
        internal FlowermanAI Bracken { get; }
        internal HoarderBugAI Hoarder { get; }
        internal SpringManAI Coilhead { get; }
        internal BaboonBirdAI Baboon { get; }
        internal SandWormAI SandWorm { get; }
        internal MouthDogAI MouthDog { get; }
        internal AIBalanceProfile Profile { get; private set; }
        internal float DeltaTime { get; private set; }
        internal string ActiveAction { get; private set; }

        internal void Update(AIBalanceProfile profile, float deltaTime)
        {
            Profile = profile;
            DeltaTime = deltaTime;
            ActiveAction = null;
        }

        internal void SetActiveAction(string actionName)
        {
            ActiveAction = actionName;
        }
    }

    internal abstract class BTNode
    {
        protected BTNode(string name)
        {
            Name = name;
        }

        internal string Name { get; }
        internal abstract BTStatus Tick(BTContext context);
    }

    internal abstract class BTComposite : BTNode
    {
        protected BTComposite(string name, IEnumerable<BTNode> children) : base(name)
        {
            _children = new List<BTNode>(children);
        }

        protected readonly List<BTNode> _children;
    }

    internal sealed class BTPrioritySelector : BTComposite
    {
        internal BTPrioritySelector(string name, params BTNode[] children) : base(name, children) { }

        internal override BTStatus Tick(BTContext context)
        {
            foreach (var child in _children)
            {
                var status = child.Tick(context);
                if (status != BTStatus.Failure)
                {
                    return status;
                }
            }

            return BTStatus.Failure;
        }
    }

    internal sealed class BTSequence : BTComposite
    {
        internal BTSequence(string name, params BTNode[] children) : base(name, children) { }

        internal override BTStatus Tick(BTContext context)
        {
            for (int i = 0; i < _children.Count; i++)
            {
                var status = _children[i].Tick(context);
                if (status != BTStatus.Success)
                {
                    return status;
                }
            }

            return BTStatus.Success;
        }
    }

    internal sealed class BTConditionNode : BTNode
    {
        private readonly Func<BTContext, bool> _predicate;

        internal BTConditionNode(string name, Func<BTContext, bool> predicate) : base(name)
        {
            _predicate = predicate;
        }

        internal override BTStatus Tick(BTContext context)
        {
            return _predicate(context) ? BTStatus.Success : BTStatus.Failure;
        }
    }

    internal sealed class BTActionNode : BTNode
    {
        private readonly Func<BTContext, BTStatus> _action;

        internal BTActionNode(string name, Func<BTContext, BTStatus> action) : base(name)
        {
            _action = action;
        }

        internal override BTStatus Tick(BTContext context)
        {
            return _action(context);
        }
    }

    internal static partial class BehaviorConditions
    {
        internal static bool PlayerInsideTerritory(BTContext context)
        {
            return context.Blackboard.PlayerInsideTerritory(context.Enemy.transform.position);
        }

        internal static bool ShouldSwarm(BTContext context)
        {
            return context.Blackboard.ShouldSwarm(context.Profile);
        }

        internal static bool ShouldHunt(BTContext context)
        {
            return context.Blackboard.ShouldHunt(context.Profile);
        }

        internal static bool ShouldLure(BTContext context)
        {
            return context.Blackboard.ShouldLure(context.Profile);
        }

        internal static bool ShouldStalk(BTContext context)
        {
            return context.Blackboard.ShouldStalk(context.Profile);
        }

    }

    internal static class NavigationHelpers
    {
        private static readonly float[] SampleMultipliers = new[] { 1f, 1.5f, 2.25f };

        internal static bool TryMoveAgent(
            BTContext context,
            Vector3 target,
            float sampleRadius,
            float maxPathLength,
            string actionLabel,
            float desiredSpeed,
            float acceleration = -1f,
            float stoppingDistance = -1f,
            bool allowPartialPath = false)
        {
            var agent = context.Agent;
            if (agent == null)
            {
                return false;
            }

            if (!TryBuildPath(agent, target, sampleRadius, maxPathLength, allowPartialPath, out var path))
            {
                return false;
            }

            ConfigureAgent(agent, desiredSpeed, acceleration, stoppingDistance);
            agent.autoBraking = false;
            agent.SetPath(path);
            context.SetActiveAction(actionLabel);
            return true;
        }

        internal static Vector3 GetAgentOffset(EnemyAI enemy, float radius)
        {
            if (radius <= 0f)
            {
                return Vector3.zero;
            }

            int hash = enemy.GetInstanceID();
            float angle = (hash % 360) * Mathf.Deg2Rad;
            float scale = 0.35f + ((hash & 0xFF) / 255f) * 0.65f;
            return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius * scale;
        }

        private static void ConfigureAgent(NavMeshAgent agent, float speed, float acceleration, float stoppingDistance)
        {
            agent.speed = Mathf.Max(0.1f, speed);
            if (acceleration > 0f)
            {
                agent.acceleration = acceleration;
            }

            if (stoppingDistance >= 0f)
            {
                agent.stoppingDistance = stoppingDistance;
            }
        }

        private static bool TryBuildPath(
            NavMeshAgent agent,
            Vector3 target,
            float sampleRadius,
            float maxPathLength,
            bool allowPartialPath,
            out NavMeshPath selectedPath)
        {
            selectedPath = null;
            float clampedRadius = Mathf.Max(0.35f, sampleRadius);

            for (int i = 0; i < SampleMultipliers.Length; i++)
            {
                float radius = clampedRadius * SampleMultipliers[i];
                if (!NavMesh.SamplePosition(target, out var hit, radius, NavMesh.AllAreas))
                {
                    continue;
                }

                var path = new NavMeshPath();
                if (!NavMesh.CalculatePath(agent.transform.position, hit.position, NavMesh.AllAreas, path))
                {
                    continue;
                }

                if (!allowPartialPath && path.status != NavMeshPathStatus.PathComplete)
                {
                    continue;
                }

                if (maxPathLength > 0f && EstimatePathLength(path) > maxPathLength)
                {
                    continue;
                }

                selectedPath = path;
                return true;
            }

            return false;
        }

        private static float EstimatePathLength(NavMeshPath path)
        {
            if (path == null || path.corners == null || path.corners.Length < 2)
            {
                return 0f;
            }

            float length = 0f;
            var corners = path.corners;
            for (int i = 1; i < corners.Length; i++)
            {
                length += Vector3.Distance(corners[i - 1], corners[i]);
            }

            return length;
        }
    }

    internal static class BehaviorActions
    {
        internal static BTStatus ExecuteTerritorial(BTContext context)
        {
            var agent = context.Agent;
            if (agent == null)
            {
                return BTStatus.Failure;
            }

            var playerPos = context.Blackboard.LastKnownPlayerPosition;
            var enemyPos = context.Enemy.transform.position;
            bool playerInside = context.Blackboard.PlayerInsideTerritory(enemyPos);

            Vector3 target = playerInside && !float.IsPositiveInfinity(playerPos.x)
                ? playerPos
                : context.Blackboard.TerritoryCenter;

            var speed = playerInside ? 7f : 3.5f;
            var accel = playerInside ? 14f : 8f;
            var stopping = playerInside ? 0.35f : 1.25f;
            if (NavigationHelpers.TryMoveAgent(
                context,
                target,
                context.Profile.TerritoryRadius * 0.5f,
                context.Profile.TerritoryRadius * 2.5f,
                "Territorial",
                speed,
                accel,
                stopping))
            {
                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        internal static BTStatus ExecutePack(BTContext context)
        {
            var agent = context.Agent;
            if (agent == null)
            {
                return BTStatus.Failure;
            }

            var allies = context.Blackboard.NearbyAllies;
            if (allies.Count == 0)
            {
                return BTStatus.Failure;
            }

            Vector3 separation = Vector3.zero;
            Vector3 alignment = Vector3.zero;
            Vector3 cohesion = Vector3.zero;

            for (int i = 0; i < allies.Count; i++)
            {
                var ally = allies[i];
                if (ally == null)
                {
                    continue;
                }

                var toAlly = ally.transform.position - context.Enemy.transform.position;
                separation -= toAlly / Mathf.Max(toAlly.sqrMagnitude, 0.1f);
                alignment += ally.transform.forward;
                cohesion += ally.transform.position;
            }

            separation /= allies.Count;
            alignment = (alignment / allies.Count).normalized;
            cohesion = ((cohesion / allies.Count) - context.Enemy.transform.position).normalized;

            var playerPos = context.Blackboard.LastKnownPlayerPosition;
            var target = playerPos.Equals(Vector3.positiveInfinity) ? context.Blackboard.PackCenter : playerPos;
            var direction = (separation * 1.5f + alignment + cohesion).normalized;
            var offset = direction * context.Profile.PackCohesionRadius * 0.5f;

            if (target != Vector3.zero)
            {
                target += offset + NavigationHelpers.GetAgentOffset(context.Enemy, context.Profile.PackCohesionRadius * 0.25f);
            }

            if (NavigationHelpers.TryMoveAgent(
                context,
                target,
                context.Profile.PackCohesionRadius,
                context.Profile.PackCohesionRadius * 2.5f,
                "Pack",
                5.25f,
                acceleration: 10f,
                stoppingDistance: 1.1f,
                allowPartialPath: true))
            {
                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        internal static BTStatus ExecuteHunt(BTContext context)
        {
            var agent = context.Agent;
            if (agent == null)
            {
                return BTStatus.Failure;
            }

            var enemyPos = context.Enemy.transform.position;
            var lastPos = context.Blackboard.LastKnownPlayerPosition;
            if (float.IsPositiveInfinity(lastPos.x))
            {
                return BTStatus.Failure;
            }

            var direction = (lastPos - enemyPos).normalized;
            var predicted = lastPos + direction * context.Profile.HuntPredictionLead;

            if (NavigationHelpers.TryMoveAgent(
                context,
                predicted,
                6f,
                context.Profile.HuntAggroDistance * 1.5f,
                "Hunt",
                7.5f,
                acceleration: 18f,
                stoppingDistance: 0.4f))
            {
                return BTStatus.Running;
            }

            var bias = NavigationHelpers.GetAgentOffset(context.Enemy, 3f);
            if (NavigationHelpers.TryMoveAgent(
                context,
                lastPos + bias,
                6f,
                context.Profile.HuntAggroDistance * 1.5f,
                "Hunt",
                6.5f,
                acceleration: 16f,
                stoppingDistance: 0.45f))
            {
                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        internal static BTStatus ExecuteLure(BTContext context)
        {
            var agent = context.Agent;
            if (agent == null)
            {
                return BTStatus.Failure;
            }

            context.Blackboard.ResetLureTimer();

            var anchor = context.Enemy.transform.position + UnityEngine.Random.insideUnitSphere * 4f;
            anchor.y = context.Enemy.transform.position.y;

            if (NavigationHelpers.TryMoveAgent(
                context,
                anchor,
                3f,
                context.Profile.TerritoryRadius,
                "Lure",
                2.75f,
                acceleration: 6f,
                stoppingDistance: 0.75f,
                allowPartialPath: true))
            {
                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        internal static BTStatus ExecuteStalk(BTContext context)
        {
            var agent = context.Agent;
            if (agent == null)
            {
                return BTStatus.Failure;
            }

            if (context.Blackboard.LastKnownPlayerPosition.Equals(Vector3.positiveInfinity))
            {
                return BTStatus.Failure;
            }

            var enemyPos = context.Enemy.transform.position;
            var toPlayer = context.Blackboard.LastKnownPlayerPosition - enemyPos;
            float distance = toPlayer.magnitude;
            var desired = context.Blackboard.LastKnownPlayerPosition - toPlayer.normalized * context.Profile.StalkMinDistance;

            if (distance > context.Profile.StalkMaxDistance)
            {
                desired = context.Blackboard.LastKnownPlayerPosition - toPlayer.normalized * (context.Profile.StalkMaxDistance * 0.85f);
            }

            desired += NavigationHelpers.GetAgentOffset(context.Enemy, context.Profile.StalkMinDistance * 0.35f);

            if (NavigationHelpers.TryMoveAgent(
                context,
                desired,
                5f,
                context.Profile.StalkMaxDistance * 1.15f,
                "Stalk",
                3.25f,
                acceleration: 8f,
                stoppingDistance: context.Profile.StalkMinDistance * 0.45f))
            {
                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        internal static BTStatus ExecuteReactive(BTContext context)
        {
            var agent = context.Agent;
            if (agent == null)
            {
                return BTStatus.Failure;
            }

            var threat = context.Blackboard.ReactiveThreatScore(context.Profile);
            agent.speed = Mathf.Lerp(2f, 6f, Mathf.Clamp01(threat));

            var patrolDirection = Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.25f) * 45f, 0f) * context.Enemy.transform.forward;
            var target = context.Enemy.transform.position + patrolDirection * 3f;

            if (NavigationHelpers.TryMoveAgent(
                context,
                target,
                3f,
                context.Profile.TerritoryRadius,
                "Reactive",
                agent.speed,
                allowPartialPath: true))
            {
                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }
    }

    internal static partial class BehaviorTreeFactory
    {
        internal static BTNode CreateTree(EnemyAI enemy)
        {
            if (enemy is SandSpiderAI)
            {
                return CreateSandSpiderTree();
            }

            if (enemy is CrawlerAI)
            {
                return CreateThumperTree();
            }

            if (enemy is BlobAI)
            {
                return CreateBlobTree();
            }

            if (enemy is FlowermanAI)
            {
                return CreateFlowermanTree();
            }

            if (enemy is HoarderBugAI)
            {
                return CreateHoarderTree();
            }

            if (enemy is SpringManAI)
            {
                return CreateCoilheadTree();
            }

            if (enemy is SandWormAI)
            {
                return CreateSandWormTree();
            }

            if (enemy is BaboonBirdAI)
            {
                return CreateBaboonTree();
            }

            if (enemy is MouthDogAI)
            {
                return CreateMouthDogTree();
            }

            return CreateDefaultTree();
        }

        private static BTNode CreateDefaultTree()
        {
            return new BTPrioritySelector("Root",
                BuildTerritorialBranch(),
                BuildPackBranch(),
                BuildHuntBranch(),
                BuildLureBranch(),
                BuildStalkBranch(),
                new BTActionNode("Reactive", BehaviorActions.ExecuteReactive));
        }

        private static BTNode BuildTerritorialBranch()
        {
            return new BTSequence("TerritorialSequence",
                new BTConditionNode("PlayerInsideTerritory", BehaviorConditions.PlayerInsideTerritory),
                new BTActionNode("TerritorialAction", BehaviorActions.ExecuteTerritorial));
        }

        private static BTNode BuildPackBranch()
        {
            return new BTSequence("PackSequence",
                new BTConditionNode("ShouldSwarm", BehaviorConditions.ShouldSwarm),
                new BTActionNode("PackAction", BehaviorActions.ExecutePack));
        }

        private static BTNode BuildHuntBranch()
        {
            return new BTSequence("HuntSequence",
                new BTConditionNode("ShouldHunt", BehaviorConditions.ShouldHunt),
                new BTActionNode("HuntAction", BehaviorActions.ExecuteHunt));
        }

        private static BTNode BuildLureBranch()
        {
            return new BTSequence("LureSequence",
                new BTConditionNode("ShouldLure", BehaviorConditions.ShouldLure),
                new BTActionNode("LureAction", BehaviorActions.ExecuteLure));
        }

        private static BTNode BuildStalkBranch()
        {
            return new BTSequence("StalkSequence",
                new BTConditionNode("ShouldStalk", BehaviorConditions.ShouldStalk),
                new BTActionNode("StalkAction", BehaviorActions.ExecuteStalk));
        }

        private static BTNode CreateHoarderTree()
        {
            return new BTPrioritySelector("HoarderRoot",
                BuildHoarderCombatBranch(),
                BuildHoarderScavengeBranch(),
                new BTActionNode("HoarderIdle", HoarderActions.IdleAroundNest));
        }

        private static BTNode BuildHoarderCombatBranch()
        {
            return new BTPrioritySelector("HoarderCombat",
                new BTSequence("HoarderFatal",
                    new BTConditionNode("HoarderFatalAggro", HoarderConditions.FatalAggro),
                    new BTActionNode("HoarderFatalAction", HoarderActions.ExecuteFatalAggro)),
                new BTSequence("HoarderHard",
                    new BTConditionNode("HoarderHardAggro", HoarderConditions.HardAggro),
                    new BTActionNode("HoarderHardAction", HoarderActions.ExecuteHardAggro)),
                new BTSequence("HoarderSoft",
                    new BTConditionNode("HoarderSoftAggro", HoarderConditions.SoftAggro),
                    new BTActionNode("HoarderSoftAction", HoarderActions.ExecuteSoftAggro)));
        }

        private static BTNode BuildHoarderScavengeBranch()
        {
            return new BTPrioritySelector("HoarderScavenge",
                new BTSequence("HoarderCarryHome",
                    new BTConditionNode("HoarderCarryingLoot", HoarderConditions.CarryingLoot),
                    new BTActionNode("HoarderReturnNest", HoarderActions.ReturnLoot)),
                new BTSequence("HoarderMigrate",
                    new BTConditionNode("HoarderNestThreat", HoarderConditions.NestCompromised),
                    new BTActionNode("HoarderMoveNest", HoarderActions.MigrateNest)),
                new BTActionNode("HoarderSearch", HoarderActions.SearchForItems));
        }

    }
}
