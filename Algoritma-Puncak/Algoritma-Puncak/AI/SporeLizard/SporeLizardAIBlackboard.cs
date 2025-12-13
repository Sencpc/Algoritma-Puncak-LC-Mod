using UnityEngine;

namespace AlgoritmaPuncakMod.AI
{
    internal sealed partial class AIBlackboard
    {
        private SporeLizardState _sporeState;
        internal ref SporeLizardState SporeState => ref _sporeState;

        private const float CalmDownDelay = 6f;
        private const float MaxExposureWindow = 4f;
        private const float WarningCooldown = 2.5f;
        private const float OcclusionExpire = 8f;

        internal void TouchSporeThreat(Vector3 playerPosition, bool hasLineOfSight)
        {
            ref var state = ref _sporeState;
            state.LastThreatPosition = playerPosition;

            if (hasLineOfSight)
            {
                state.ExposureTimer = MaxExposureWindow;
            }

            if (state.CalmTimer <= 0f)
            {
                state.CalmTimer = CalmDownDelay;
            }
        }

        internal bool SporeRecentlySeenPlayer()
        {
            return _sporeState.ExposureTimer > 0f;
        }

        internal bool SporeReadyToWarn()
        {
            return _sporeState.WarningTimer <= 0f;
        }

        internal void TriggerSporeWarning()
        {
            _sporeState.WarningTimer = WarningCooldown;
        }

        internal bool SporeNeedsOcclusion()
        {
            if (_sporeState.ExposureTimer > 0f)
            {
                return true;
            }

            return _sporeState.CalmTimer > 0f && !_sporeState.HasOcclusion;
        }

        internal void ClearSporeOcclusion()
        {
            _sporeState.HasOcclusion = false;
            _sporeState.OcclusionLifetime = 0f;
            _sporeState.PathBlocked = false;
        }

        internal void AssignSporeOcclusion(Vector3 target)
        {
            _sporeState.HasOcclusion = true;
            _sporeState.OcclusionTarget = target;
            _sporeState.OcclusionLifetime = OcclusionExpire;
            _sporeState.PathBlocked = false;
        }

        internal void RefreshSporeOcclusion(Vector3 target)
        {
            _sporeState.HasOcclusion = true;
            _sporeState.OcclusionTarget = target;
            _sporeState.OcclusionLifetime = Mathf.Max(_sporeState.OcclusionLifetime, OcclusionExpire * 0.35f);
            _sporeState.PathBlocked = false;
        }

        internal bool TryGetSporeOcclusion(out Vector3 target)
        {
            target = _sporeState.OcclusionTarget;
            return _sporeState.HasOcclusion;
        }

        internal void FlagSporePathBlocked()
        {
            _sporeState.PathBlocked = true;
        }

        internal bool SporePathBlocked()
        {
            return _sporeState.PathBlocked;
        }

        internal bool SporeCornered(Vector3 lizardPosition, float playerDistance, float escapeThreshold)
        {
            if (playerDistance > escapeThreshold)
            {
                return false;
            }

            if (_sporeState.CalmTimer <= 0f)
            {
                return false;
            }

            if (_sporeState.HasOcclusion && !_sporeState.PathBlocked)
            {
                return false;
            }

            if (_sporeState.LastThreatPosition == Vector3.zero)
            {
                return false;
            }

            var delta = lizardPosition - _sporeState.LastThreatPosition;
            return delta.sqrMagnitude < escapeThreshold * escapeThreshold;
        }

        internal void RecordSporeFootsteps(Vector3 direction)
        {
            _sporeState.LastFootstepVector = direction;
        }

        internal Vector3 GetSporeFootstepVector()
        {
            return _sporeState.LastFootstepVector;
        }

        internal bool SporeNeedsRelocate()
        {
            return !_sporeState.HasOcclusion || _sporeState.PathBlocked || _sporeState.OcclusionLifetime <= 0f;
        }

        partial void TickSporeLizardSystems(float deltaTime)
        {
            if (_sporeState.ExposureTimer > 0f)
            {
                _sporeState.ExposureTimer = Mathf.Max(0f, _sporeState.ExposureTimer - deltaTime);
            }

            if (_sporeState.WarningTimer > 0f)
            {
                _sporeState.WarningTimer = Mathf.Max(0f, _sporeState.WarningTimer - deltaTime);
            }

            if (_sporeState.CalmTimer > 0f)
            {
                _sporeState.CalmTimer = Mathf.Max(0f, _sporeState.CalmTimer - deltaTime);
            }

            if (_sporeState.OcclusionCooldown > 0f)
            {
                _sporeState.OcclusionCooldown = Mathf.Max(0f, _sporeState.OcclusionCooldown - deltaTime);
            }

            if (_sporeState.OcclusionLifetime > 0f)
            {
                _sporeState.OcclusionLifetime = Mathf.Max(0f, _sporeState.OcclusionLifetime - deltaTime);
                if (_sporeState.OcclusionLifetime <= 0f)
                {
                    _sporeState.HasOcclusion = false;
                }
            }
        }
    }

    internal struct SporeLizardState
    {
        internal Vector3 LastThreatPosition;
        internal Vector3 OcclusionTarget;
        internal Vector3 LastFootstepVector;
        internal float ExposureTimer;
        internal float WarningTimer;
        internal float CalmTimer;
        internal float OcclusionCooldown;
        internal float OcclusionLifetime;
        internal bool HasOcclusion;
        internal bool PathBlocked;
    }
}
