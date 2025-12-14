using System.Collections.Generic;
using GameNetcodeStuff;
using UnityEngine;

namespace AlgoritmaPuncakMod.AI
{
    internal sealed partial class AIBlackboard
    {
        private const float CoilheadMemoryDuration = 14f;

        private sealed class CoilheadMemoryEntry
        {
            internal Vector3 Position;
            internal float TimeRemaining;
        }

        private float _coilheadAggroMemory;
        private Vector3 _coilheadTrackedTarget = Vector3.positiveInfinity;
        private bool _coilheadAggroLocked;
        private bool _coilheadFrozen;
        private float _coilheadFreezeBuffer;
        private bool _coilheadObservedThisTick;
        private Component _coilheadDoorComponent;
        private Vector3 _coilheadDoorPosition = Vector3.positiveInfinity;
        private float _coilheadDoorHoldTimer;
        private readonly Dictionary<PlayerControllerB, CoilheadMemoryEntry> _coilheadSightings = new Dictionary<PlayerControllerB, CoilheadMemoryEntry>();
        private readonly List<PlayerControllerB> _coilheadExpiredSightings = new List<PlayerControllerB>();

        internal bool CoilheadHasAggro => _coilheadAggroLocked || _coilheadAggroMemory > 0f;
        internal Vector3 CoilheadTarget => _coilheadTrackedTarget;
        internal bool CoilheadFrozen => _coilheadFrozen;
        internal bool CoilheadFreezeActive => _coilheadObservedThisTick || _coilheadFreezeBuffer > 0f;
        internal bool CoilheadDoorEngaged => _coilheadDoorComponent != null;
        internal bool CoilheadDoorReady => _coilheadDoorComponent != null && _coilheadDoorHoldTimer <= 0f;
        internal Component CoilheadDoorComponent => _coilheadDoorComponent;
        internal Vector3 CoilheadDoorFocus => _coilheadDoorPosition;
        internal bool CoilheadAggroLocked => _coilheadAggroLocked;

        internal void SetCoilheadTarget(Vector3 position, float memoryDuration, bool lockAggro = false)
        {
            _coilheadTrackedTarget = position;
            _coilheadAggroMemory = Mathf.Max(_coilheadAggroMemory, memoryDuration);
            if (lockAggro)
            {
                _coilheadAggroLocked = true;
            }
        }

        internal void ClearCoilheadTarget()
        {
            _coilheadTrackedTarget = Vector3.positiveInfinity;
        }

        internal void ReleaseCoilheadAggroLock()
        {
            _coilheadAggroLocked = false;
            if (_coilheadAggroMemory <= 0f)
            {
                _coilheadTrackedTarget = Vector3.positiveInfinity;
            }
        }

        internal void MarkCoilheadObservation()
        {
            _coilheadObservedThisTick = true;
            _coilheadFreezeBuffer = 0.35f;
        }

        internal void SetCoilheadFrozen(bool frozen)
        {
            _coilheadFrozen = frozen;
        }

        internal void ClearCoilheadSightings()
        {
            _coilheadSightings.Clear();
            _coilheadExpiredSightings.Clear();
        }

        internal void RememberCoilheadPlayer(PlayerControllerB player)
        {
            if (player == null)
            {
                return;
            }

            if (!_coilheadSightings.TryGetValue(player, out var entry))
            {
                entry = new CoilheadMemoryEntry();
                _coilheadSightings[player] = entry;
            }

            entry.Position = player.transform.position;
            entry.TimeRemaining = CoilheadMemoryDuration;
        }

        internal bool TryGetCoilheadPersistentTarget(Vector3 origin, out Vector3 target)
        {
            target = Vector3.positiveInfinity;
            float bestScore = float.MaxValue;

            foreach (var pair in _coilheadSightings)
            {
                var entry = pair.Value;
                if (entry == null || entry.TimeRemaining <= 0f)
                {
                    continue;
                }

                float distance = Vector3.Distance(origin, entry.Position);
                float score = distance - entry.TimeRemaining;
                if (score < bestScore)
                {
                    bestScore = score;
                    target = entry.Position;
                }
            }

            return !float.IsPositiveInfinity(target.x);
        }

        internal void BeginCoilheadDoorPause(Component door, Vector3 position, float durationSeconds)
        {
            if (door == null)
            {
                return;
            }

            _coilheadDoorComponent = door;
            _coilheadDoorPosition = position;
            _coilheadDoorHoldTimer = Mathf.Max(_coilheadDoorHoldTimer, durationSeconds);
        }

        internal void FinishCoilheadDoorPause()
        {
            _coilheadDoorComponent = null;
            _coilheadDoorPosition = Vector3.positiveInfinity;
            _coilheadDoorHoldTimer = 0f;
        }

        partial void TickCoilheadSystems(float deltaTime)
        {
            _coilheadAggroMemory = Mathf.Max(0f, _coilheadAggroMemory - deltaTime);
            if (_coilheadAggroMemory <= 0f && !_coilheadAggroLocked)
            {
                _coilheadTrackedTarget = Vector3.positiveInfinity;
            }

            _coilheadObservedThisTick = false;
            if (_coilheadFreezeBuffer > 0f)
            {
                _coilheadFreezeBuffer = Mathf.Max(0f, _coilheadFreezeBuffer - deltaTime);
            }

            if (_coilheadDoorHoldTimer > 0f)
            {
                _coilheadDoorHoldTimer = Mathf.Max(0f, _coilheadDoorHoldTimer - deltaTime);
            }

            if (_coilheadSightings.Count > 0)
            {
                _coilheadExpiredSightings.Clear();
                foreach (var pair in _coilheadSightings)
                {
                    var player = pair.Key;
                    var entry = pair.Value;
                    if (entry == null)
                    {
                        _coilheadExpiredSightings.Add(player);
                        continue;
                    }

                    entry.TimeRemaining = Mathf.Max(0f, entry.TimeRemaining - deltaTime);
                    if (player == null || player.isPlayerDead || !player.isInsideFactory || entry.TimeRemaining <= 0f)
                    {
                        _coilheadExpiredSightings.Add(player);
                    }
                }

                for (int i = 0; i < _coilheadExpiredSightings.Count; i++)
                {
                    _coilheadSightings.Remove(_coilheadExpiredSightings[i]);
                }
            }
        }
    }
}
