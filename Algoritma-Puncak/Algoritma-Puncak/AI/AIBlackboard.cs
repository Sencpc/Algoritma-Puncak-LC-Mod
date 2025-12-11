using System.Collections.Generic;
using UnityEngine;

namespace AlgoritmaPuncakMod.AI
{
    internal sealed partial class AIBlackboard
    {
        private readonly List<EnemyAI> _nearbyAllies = new List<EnemyAI>();
        partial void TickSpiderSystems(float deltaTime);
        partial void TickThumperSystems(float deltaTime);
        partial void TickBlobSystems(float deltaTime);
        partial void TickFlowermanSystems(float deltaTime);
        partial void TickHoarderSystems(float deltaTime);
        partial void TickCoilheadSystems(float deltaTime);
        partial void TickBaboonSystems(float deltaTime);
        partial void TickSandWormSystems(float deltaTime);
        partial void TickMouthDogSystems(float deltaTime);

        internal Vector3 LastKnownPlayerPosition { get; private set; } = Vector3.positiveInfinity;
        internal Vector3 LastKnownPlayerForward { get; private set; } = Vector3.forward;
        internal float DistanceToPlayer { get; private set; } = float.PositiveInfinity;
        internal bool PlayerVisible { get; private set; }
        internal float PlayerNoiseLevel { get; private set; }
        internal float TimeSincePlayerSeen { get; private set; } = float.PositiveInfinity;
        internal Vector3 TerritoryCenter { get; private set; } = Vector3.zero;
        internal float TerritoryRadius { get; private set; }
        internal Vector3 PackCenter { get; private set; } = Vector3.zero;
        internal float TimeSinceLastLure { get; private set; } = float.PositiveInfinity;

        internal IReadOnlyList<EnemyAI> NearbyAllies => _nearbyAllies;

        internal void InitializeTerritory(Vector3 center, float radius)
        {
            TerritoryCenter = center;
            TerritoryRadius = Mathf.Max(0f, radius);
        }

        internal void UpdatePlayerInfo(Vector3 enemyPosition, Vector3 playerPosition, Vector3 playerForward, bool visible, float noiseLevel, float deltaTime)
        {
            LastKnownPlayerPosition = playerPosition;
            if (playerForward.sqrMagnitude > 0.01f)
            {
                LastKnownPlayerForward = playerForward.normalized;
            }
            if (float.IsInfinity(playerPosition.x) || float.IsInfinity(playerPosition.y) || float.IsInfinity(playerPosition.z))
            {
                DistanceToPlayer = float.PositiveInfinity;
            }
            else
            {
                DistanceToPlayer = Vector3.Distance(enemyPosition, playerPosition);
            }
            PlayerVisible = visible;
            PlayerNoiseLevel = Mathf.Clamp01(noiseLevel);
            TimeSincePlayerSeen = visible ? 0f : TimeSincePlayerSeen + deltaTime;
        }

        internal void MarkPlayerLost(float deltaTime)
        {
            PlayerVisible = false;
            DistanceToPlayer = float.PositiveInfinity;
            TimeSincePlayerSeen += deltaTime;
        }

        internal void UpdateAllies(List<EnemyAI> allies)
        {
            _nearbyAllies.Clear();
            if (allies == null || allies.Count == 0)
            {
                PackCenter = Vector3.zero;
                return;
            }

            Vector3 aggregate = Vector3.zero;
            foreach (var ally in allies)
            {
                if (ally == null) continue;
                _nearbyAllies.Add(ally);
                aggregate += ally.transform.position;
            }

            PackCenter = _nearbyAllies.Count > 0 ? aggregate / _nearbyAllies.Count : Vector3.zero;
        }

        internal void TickTimers(float deltaTime)
        {
            TimeSinceLastLure += deltaTime;
            if (!PlayerVisible)
            {
                TimeSincePlayerSeen += deltaTime;
            }

            TickSpiderSystems(deltaTime);
            TickThumperSystems(deltaTime);
            TickBlobSystems(deltaTime);
            TickFlowermanSystems(deltaTime);
            TickHoarderSystems(deltaTime);
            TickCoilheadSystems(deltaTime);
            TickBaboonSystems(deltaTime);
            TickSandWormSystems(deltaTime);
            TickMouthDogSystems(deltaTime);
        }

        internal void ResetLureTimer() => TimeSinceLastLure = 0f;

        internal bool PlayerInsideTerritory(Vector3 enemyPosition)
        {
            if (TerritoryRadius <= 0f) return false;
            var distance = Vector3.Distance(LastKnownPlayerPosition, TerritoryCenter);
            if (float.IsInfinity(distance))
            {
                distance = Vector3.Distance(enemyPosition, TerritoryCenter);
            }

            return distance <= TerritoryRadius;
        }

        internal bool ShouldHunt(AIBalanceProfile profile) => PlayerVisible && DistanceToPlayer <= profile.HuntAggroDistance;

        internal bool ShouldStalk(AIBalanceProfile profile)
        {
            if (!PlayerVisible) return false;
            return DistanceToPlayer > profile.StalkMinDistance && DistanceToPlayer < profile.StalkMaxDistance;
        }

        internal bool ShouldSwarm(AIBalanceProfile profile)
        {
            if (_nearbyAllies.Count < 2) return false;
            return PlayerVisible || PlayerNoiseLevel > 0.4f || TimeSincePlayerSeen < 4f;
        }

        internal bool ShouldLure(AIBalanceProfile profile)
        {
            if (PlayerVisible) return false;
            if (TimeSincePlayerSeen < 5f) return false;
            return PlayerNoiseLevel < 0.25f && TimeSinceLastLure >= profile.LureCooldown;
        }

        internal float ReactiveThreatScore(AIBalanceProfile profile)
        {
            var visibility = PlayerVisible ? 1f : 0f;
            var noise = PlayerNoiseLevel;
            var memory = Mathf.Clamp01(10f / (TimeSincePlayerSeen + 1f));
            return (visibility + noise + memory) * profile.ReactiveAggressionMultiplier;
        }

    }
}
