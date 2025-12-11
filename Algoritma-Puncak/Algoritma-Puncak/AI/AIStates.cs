using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace AlgoritmaPuncakMod.AI
{
    internal enum BehaviorType
    {
        Stalk,
        Hunt,
        Territorial,
        Lure,
        Pack,
        Reactive
    }

    internal readonly struct AIStateContext
    {
        internal AIStateContext(EnemyAI enemy, NavMeshAgent agent, AIBlackboard blackboard, AIBalanceProfile profile, float deltaTime)
        {
            Enemy = enemy;
            Agent = agent;
            Blackboard = blackboard;
            Profile = profile;
            DeltaTime = deltaTime;
        }

        internal EnemyAI Enemy { get; }
        internal NavMeshAgent Agent { get; }
        internal AIBlackboard Blackboard { get; }
        internal AIBalanceProfile Profile { get; }
        internal float DeltaTime { get; }
    }

    internal abstract class AIState
    {
        protected AIState(BehaviorType type) => Type = type;

        internal BehaviorType Type { get; }
        internal virtual void Enter(AIStateContext context) { }
        internal virtual void Exit(AIStateContext context) { }
        internal abstract void Tick(AIStateContext context);
    }

    internal sealed class StalkState : AIState
    {
        internal StalkState() : base(BehaviorType.Stalk) { }

        internal override void Tick(AIStateContext context)
        {
            var agent = context.Agent;
            if (agent == null || context.Blackboard.LastKnownPlayerPosition.Equals(Vector3.positiveInfinity)) return;

            var enemyPos = context.Enemy.transform.position;
            var toPlayer = context.Blackboard.LastKnownPlayerPosition - enemyPos;
            float distance = toPlayer.magnitude;
            var desired = context.Blackboard.LastKnownPlayerPosition - toPlayer.normalized * context.Profile.StalkMinDistance;

            if (distance > context.Profile.StalkMaxDistance)
            {
                desired = context.Blackboard.LastKnownPlayerPosition - toPlayer.normalized * (context.Profile.StalkMaxDistance * 0.85f);
            }

            agent.speed = Mathf.Min(agent.speed, 3.5f);
            agent.stoppingDistance = context.Profile.StalkMinDistance * 0.5f;
            if (NavMesh.SamplePosition(desired, out var hit, 5f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }
    }

    internal sealed class HuntState : AIState
    {
        internal HuntState() : base(BehaviorType.Hunt) { }

        internal override void Tick(AIStateContext context)
        {
            var agent = context.Agent;
            if (agent == null) return;

            var enemyPos = context.Enemy.transform.position;
            var lastPos = context.Blackboard.LastKnownPlayerPosition;
            if (float.IsPositiveInfinity(lastPos.x)) return;

            var direction = (lastPos - enemyPos).normalized;
            var predicted = lastPos + direction * context.Profile.HuntPredictionLead;
            if (NavMesh.SamplePosition(predicted, out var hit, 8f, NavMesh.AllAreas))
            {
                agent.speed = Mathf.Max(agent.speed, 6f);
                agent.acceleration = Mathf.Max(agent.acceleration, 16f);
                agent.stoppingDistance = 0.5f;
                agent.SetDestination(hit.position);
            }
        }
    }

    internal sealed class TerritorialState : AIState
    {
        internal TerritorialState() : base(BehaviorType.Territorial) { }

        internal override void Tick(AIStateContext context)
        {
            var agent = context.Agent;
            if (agent == null) return;

            var playerPos = context.Blackboard.LastKnownPlayerPosition;
            var enemyPos = context.Enemy.transform.position;
            bool playerInside = context.Blackboard.PlayerInsideTerritory(enemyPos);

            Vector3 target = playerInside && !float.IsPositiveInfinity(playerPos.x)
                ? playerPos
                : context.Blackboard.TerritoryCenter;

            if (NavMesh.SamplePosition(target, out var hit, context.Profile.TerritoryRadius, NavMesh.AllAreas))
            {
                agent.speed = playerInside ? 7f : 3f;
                agent.SetDestination(hit.position);
            }
        }
    }

    internal sealed class LureState : AIState
    {
        internal LureState() : base(BehaviorType.Lure) { }

        internal override void Enter(AIStateContext context)
        {
            context.Blackboard.ResetLureTimer();
            AlgoritmaPuncakMod.Log?.LogDebug($"[{context.Enemy.name}] Deploying lure stimulus.");
        }

        internal override void Tick(AIStateContext context)
        {
            var agent = context.Agent;
            if (agent == null) return;

            var anchor = context.Enemy.transform.position + Random.insideUnitSphere * 4f;
            anchor.y = context.Enemy.transform.position.y;

            if (NavMesh.SamplePosition(anchor, out var hit, 3f, NavMesh.AllAreas))
            {
                agent.speed = 2.5f;
                agent.SetDestination(hit.position);
            }
        }
    }

    internal sealed class PackState : AIState
    {
        internal PackState() : base(BehaviorType.Pack) { }

        internal override void Tick(AIStateContext context)
        {
            var agent = context.Agent;
            if (agent == null) return;

            var allies = context.Blackboard.NearbyAllies;
            if (allies.Count == 0)
            {
                return;
            }

            Vector3 separation = Vector3.zero;
            Vector3 alignment = Vector3.zero;
            Vector3 cohesion = Vector3.zero;

            foreach (var ally in allies)
            {
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
                target += offset;
            }

            if (NavMesh.SamplePosition(target, out var hit, context.Profile.PackCohesionRadius, NavMesh.AllAreas))
            {
                agent.speed = 5f;
                agent.SetDestination(hit.position);
            }
        }
    }

    internal sealed class ReactiveState : AIState
    {
        internal ReactiveState() : base(BehaviorType.Reactive) { }

        internal override void Tick(AIStateContext context)
        {
            var agent = context.Agent;
            if (agent == null) return;

            var threat = context.Blackboard.ReactiveThreatScore(context.Profile);
            agent.speed = Mathf.Lerp(2f, 6f, Mathf.Clamp01(threat));

            var patrolDirection = Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.25f) * 45f, 0f) * context.Enemy.transform.forward;
            var target = context.Enemy.transform.position + patrolDirection * 3f;
            if (NavMesh.SamplePosition(target, out var hit, 3f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
            }
        }
    }
}
