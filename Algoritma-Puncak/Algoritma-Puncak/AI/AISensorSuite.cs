using System.Collections.Generic;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using UnityEngine.AI;

namespace AlgoritmaPuncakMod.AI
{
    internal sealed class AISensorSuite
    {
        private readonly Dictionary<int, PlayerSnapshot> _playerSnapshots = new Dictionary<int, PlayerSnapshot>();
        private readonly List<EnemyAI> _allyBuffer = new List<EnemyAI>(8);
        private readonly int _playerLayerMask = LayerMask.GetMask("Default", "Player", "Environment");
        private readonly SpiderIntelTracker _spiderIntel = new SpiderIntelTracker();

        internal void Scan(EnemyAI enemy, NavMeshAgent agent, AIBlackboard blackboard, AIBalanceProfile profile, float deltaTime)
        {
            if (enemy == null) return;

            var trackedPlayers = GatherPlayers();
            UpdatePlayerSnapshots(enemy, trackedPlayers, deltaTime);
            var snapshot = SelectTarget(enemy.transform.position, profile.HuntAggroDistance * 1.5f);

            if (snapshot != null)
            {
                bool visible = HasLineOfSight(enemy.transform.position + Vector3.up, snapshot.Position + Vector3.up * 0.8f);
                float noise = Mathf.Clamp01(snapshot.VelocityMagnitude * 0.25f);
                blackboard.UpdatePlayerInfo(enemy.transform.position, snapshot.Position, snapshot.CameraForward, visible, noise, deltaTime);
            }
            else
            {
                blackboard.MarkPlayerLost(deltaTime);
            }

            var allies = CollectNearbyAllies(enemy, profile.PackCohesionRadius);
            blackboard.UpdateAllies(allies);
            _spiderIntel.Update(enemy, blackboard);
            ProcessMouthDogStimuli(enemy, blackboard);
        }

        private static readonly List<PlayerControllerB> PlayerBuffer = new List<PlayerControllerB>(4);

        private static IReadOnlyList<PlayerControllerB> GatherPlayers()
        {
            PlayerBuffer.Clear();
            var round = StartOfRound.Instance;
            if (round?.allPlayerScripts == null)
            {
                return PlayerBuffer;
            }

            foreach (var player in round.allPlayerScripts)
            {
                if (player == null) continue;
                PlayerBuffer.Add(player);
            }

            return PlayerBuffer;
        }

        private void UpdatePlayerSnapshots(EnemyAI enemy, IReadOnlyList<PlayerControllerB> players, float deltaTime)
        {
            var visited = new HashSet<int>();

            foreach (var player in players)
            {
                if (!enemy.PlayerIsTargetable(player, cannotBeInShip: false, overrideInsideFactoryCheck: false)) continue;

                int id = player.GetInstanceID();
                visited.Add(id);

                if (!_playerSnapshots.TryGetValue(id, out var snapshot))
                {
                    snapshot = new PlayerSnapshot(player);
                    _playerSnapshots[id] = snapshot;
                }

                var cameraForward = player.gameplayCamera != null
                    ? player.gameplayCamera.transform.forward
                    : player.transform.forward;

                snapshot.Update(player, cameraForward, deltaTime);
            }

            var toRemove = new List<int>();
            foreach (var kvp in _playerSnapshots)
            {
                if (!visited.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (var id in toRemove)
            {
                _playerSnapshots.Remove(id);
            }
        }

        private PlayerSnapshot SelectTarget(Vector3 enemyPosition, float maxDistance)
        {
            PlayerSnapshot closest = null;
            float bestDistance = maxDistance;

            foreach (var snapshot in _playerSnapshots.Values)
            {
                float distance = Vector3.Distance(enemyPosition, snapshot.Position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    closest = snapshot;
                }
            }

            return closest;
        }

        private bool HasLineOfSight(Vector3 origin, Vector3 target)
        {
            var direction = target - origin;
            RaycastHit hitInfo;
            if (!Physics.Raycast(origin, direction.normalized, out hitInfo, direction.magnitude, _playerLayerMask, UnityEngine.QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            return hitInfo.collider != null && hitInfo.collider.CompareTag("Player");
        }

        private List<EnemyAI> CollectNearbyAllies(EnemyAI enemy, float radius)
        {
            _allyBuffer.Clear();

            var round = RoundManager.Instance;
            if (round?.SpawnedEnemies != null && round.SpawnedEnemies.Count > 0)
            {
                foreach (var candidate in round.SpawnedEnemies)
                {
                    if (candidate == null || candidate == enemy) continue;
                    if (Vector3.Distance(enemy.transform.position, candidate.transform.position) <= radius)
                    {
                        _allyBuffer.Add(candidate);
                    }
                }
            }
            else
            {
                var everyone = Object.FindObjectsOfType<EnemyAI>();
                foreach (var candidate in everyone)
                {
                    if (candidate == null || candidate == enemy) continue;
                    if (Vector3.Distance(enemy.transform.position, candidate.transform.position) <= radius)
                    {
                        _allyBuffer.Add(candidate);
                    }
                }
            }

            return _allyBuffer;
        }

        private void ProcessMouthDogStimuli(EnemyAI enemy, AIBlackboard blackboard)
        {
            bool isMouthDog = enemy is MouthDogAI;
            foreach (var snapshot in _playerSnapshots.Values)
            {
                if (!isMouthDog)
                {
                    snapshot.DiscardMouthDogNoise();
                    continue;
                }

                if (!snapshot.TryConsumeMouthDogNoise(out var position, out float lowStrength, out float highStrength))
                {
                    continue;
                }

                if (highStrength > 0f)
                {
                    blackboard.RegisterMouthDogNoise(position, highStrength, highPriority: true);
                }

                if (lowStrength > 0f)
                {
                    blackboard.RegisterMouthDogNoise(position, lowStrength, highPriority: false);
                }
            }
        }

        private sealed class PlayerSnapshot
        {
            private const float FootstepStride = 1.05f;
            private Vector3 _previousPosition;
            private float _stepAccumulator;
            private bool _wasHoldingItem;
            private float _pendingMouthDogLow;
            private float _pendingMouthDogHigh;

            internal PlayerSnapshot(PlayerControllerB player)
            {
                if (player == null)
                {
                    return;
                }

                Position = player.transform.position;
                _previousPosition = Position;
                CameraForward = player.gameplayCamera != null
                    ? player.gameplayCamera.transform.forward
                    : player.transform.forward;
                _wasHoldingItem = player.currentlyHeldObjectServer != null;
            }

            internal Vector3 Position { get; private set; } = Vector3.positiveInfinity;
            internal float VelocityMagnitude { get; private set; }
            internal Vector3 CameraForward { get; private set; } = Vector3.forward;

            internal void Update(PlayerControllerB player, Vector3 cameraForward, float deltaTime)
            {
                if (player == null)
                {
                    return;
                }

                var newPosition = player.transform.position;
                Position = newPosition;
                if (cameraForward.sqrMagnitude > 0.01f)
                {
                    CameraForward = cameraForward.normalized;
                }

                var displacement = newPosition - _previousPosition;
                _previousPosition = newPosition;
                if (deltaTime > 0f)
                {
                    VelocityMagnitude = displacement.magnitude / deltaTime;
                }

                HandleNoise(player, displacement, deltaTime);
            }

            private void HandleNoise(PlayerControllerB player, Vector3 displacement, float deltaTime)
            {
                if (player.isInsideFactory)
                {
                    _stepAccumulator = 0f;
                    _wasHoldingItem = player.currentlyHeldObjectServer != null;
                    _pendingMouthDogLow = 0f;
                    _pendingMouthDogHigh = 0f;
                    return;
                }

                float distance = displacement.magnitude;
                if (distance > 0.05f)
                {
                    _stepAccumulator += distance;
                    while (_stepAccumulator >= FootstepStride)
                    {
                        _stepAccumulator -= FootstepStride;
                        SandWormNoiseField.RegisterFootstep(Position);
                        _pendingMouthDogLow = Mathf.Max(_pendingMouthDogLow, 0.25f);
                    }
                }

                bool holding = player.currentlyHeldObjectServer != null;
                if (_wasHoldingItem && !holding)
                {
                    SandWormNoiseField.RegisterItemDrop(Position);
                    _pendingMouthDogHigh = Mathf.Max(_pendingMouthDogHigh, 0.85f);
                }

                _wasHoldingItem = holding;

                float speed = deltaTime > 0f ? distance / Mathf.Max(deltaTime, 0.0001f) : 0f;
                if (PlayerNoiseIntrospection.IsSprinting(player) || speed >= 4.25f)
                {
                    _pendingMouthDogHigh = Mathf.Max(_pendingMouthDogHigh, Mathf.Clamp01((speed - 3.5f) / 3f));
                }
                else if (speed > 0.8f)
                {
                    _pendingMouthDogLow = Mathf.Max(_pendingMouthDogLow, Mathf.Clamp01(speed / 4f));
                }

                if (PlayerNoiseIntrospection.HasActiveVoice(player))
                {
                    _pendingMouthDogHigh = Mathf.Max(_pendingMouthDogHigh, 0.65f);
                }
            }

            internal bool TryConsumeMouthDogNoise(out Vector3 position, out float lowStrength, out float highStrength)
            {
                position = Position;
                lowStrength = _pendingMouthDogLow;
                highStrength = _pendingMouthDogHigh;
                bool hasSignal = lowStrength > 0f || highStrength > 0f;
                _pendingMouthDogLow = 0f;
                _pendingMouthDogHigh = 0f;
                return hasSignal;
            }

            internal void DiscardMouthDogNoise()
            {
                _pendingMouthDogLow = 0f;
                _pendingMouthDogHigh = 0f;
            }
        }

        private static class PlayerNoiseIntrospection
        {
            private static readonly System.Reflection.FieldInfo SprintingField = AccessTools.Field(typeof(PlayerControllerB), "isSprinting");
            private static readonly System.Reflection.FieldInfo VoiceSourceField = AccessTools.Field(typeof(PlayerControllerB), "voiceChatAudioSource");

            internal static bool IsSprinting(PlayerControllerB player)
            {
                if (player == null || SprintingField == null)
                {
                    return false;
                }

                try
                {
                    return SprintingField.GetValue(player) is bool value && value;
                }
                catch
                {
                    return false;
                }
            }

            internal static bool HasActiveVoice(PlayerControllerB player)
            {
                if (player == null || VoiceSourceField == null)
                {
                    return false;
                }

                try
                {
                    if (VoiceSourceField.GetValue(player) is AudioSource source && source != null)
                    {
                        return source.isPlaying && source.volume > 0.01f;
                    }
                }
                catch
                {
                    return false;
                }

                return false;
            }
        }

        private sealed class SpiderIntelTracker
        {
            private readonly Dictionary<int, SpiderTrapSnapshot> _knownTraps = new Dictionary<int, SpiderTrapSnapshot>();
            private readonly List<int> _scratchIds = new List<int>();
            private readonly List<int> _removalBuffer = new List<int>();

            internal void Update(EnemyAI enemy, AIBlackboard blackboard)
            {
                if (!(enemy is SandSpiderAI spider))
                {
                    return;
                }

                blackboard.EnsureSpiderFortification(enemy.transform.position);
                var traps = spider.webTraps;
                if (traps == null)
                {
                    return;
                }

                _scratchIds.Clear();
                for (int i = 0; i < traps.Count; i++)
                {
                    var trap = traps[i];
                    if (trap == null)
                    {
                        continue;
                    }

                    _scratchIds.Add(trap.trapID);
                    var position = trap.centerOfWeb != null ? trap.centerOfWeb.position : trap.transform.position;
                    if (!_knownTraps.TryGetValue(trap.trapID, out var snapshot))
                    {
                        snapshot = new SpiderTrapSnapshot { Position = position };
                        _knownTraps[trap.trapID] = snapshot;
                        blackboard.RegisterSpiderWeb(position);
                    }
                    else
                    {
                        snapshot.Position = position;
                    }

                    if (trap.currentTrappedPlayer != null)
                    {
                        blackboard.NotifySpiderWebDisturbance(position, false);
                    }
                }

                _removalBuffer.Clear();
                foreach (var pair in _knownTraps)
                {
                    if (_scratchIds.Contains(pair.Key))
                    {
                        continue;
                    }

                    _removalBuffer.Add(pair.Key);
                    blackboard.HandleSpiderWebRemoved(pair.Value.Position);
                }

                for (int i = 0; i < _removalBuffer.Count; i++)
                {
                    _knownTraps.Remove(_removalBuffer[i]);
                }
            }

            private sealed class SpiderTrapSnapshot
            {
                internal Vector3 Position;
            }
        }
    }
}
