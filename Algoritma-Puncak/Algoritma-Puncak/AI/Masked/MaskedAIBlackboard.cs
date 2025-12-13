using UnityEngine;

namespace AlgoritmaPuncakMod.AI
{
    internal sealed partial class AIBlackboard
    {
        private bool _maskedPlayerIsIsolated;
        private Vector3 _maskedInterceptTarget = Vector3.positiveInfinity;
        private float _maskedInterceptTimer;
        private Vector3 _maskedLoiterDoor = Vector3.positiveInfinity;
        private float _maskedLoiterTimer;
        private float _maskedVocalCooldown;
        private float _maskedFreezeTimer;
        private Vector3 _maskedPatrolAnchor = Vector3.positiveInfinity;
        private Vector3 _maskedPatrolFacing = Vector3.positiveInfinity;
        private float _maskedPatrolTimer;

        internal bool MaskedPlayerIsIsolated => _maskedPlayerIsIsolated;

        internal void SetMaskedIsolation(bool isolated)
        {
            _maskedPlayerIsIsolated = isolated;
        }

        internal void SetMaskedIntercept(Vector3 predictedPosition)
        {
            if (float.IsPositiveInfinity(predictedPosition.x))
            {
                return;
            }

            _maskedInterceptTarget = predictedPosition;
            _maskedInterceptTimer = 1.5f;
        }

        internal bool TryGetMaskedIntercept(out Vector3 intercept)
        {
            intercept = _maskedInterceptTarget;
            return _maskedInterceptTimer > 0f && !float.IsPositiveInfinity(intercept.x);
        }

        internal void ClearMaskedIntercept()
        {
            _maskedInterceptTimer = 0f;
            _maskedInterceptTarget = Vector3.positiveInfinity;
        }

        internal bool MaskedReadyToVocalize => _maskedVocalCooldown <= 0f;

        internal void TriggerMaskedVocalCooldown(float duration)
        {
            _maskedVocalCooldown = Mathf.Max(_maskedVocalCooldown, duration);
        }

        internal void BeginMaskedLoiter(Vector3 doorwayPosition)
        {
            if (float.IsPositiveInfinity(doorwayPosition.x))
            {
                return;
            }

            _maskedLoiterDoor = doorwayPosition;
            _maskedLoiterTimer = 6f;
        }

        internal bool TryGetMaskedLoiter(out Vector3 doorway)
        {
            doorway = _maskedLoiterDoor;
            return _maskedLoiterTimer > 0f && !float.IsPositiveInfinity(doorway.x);
        }

        internal void ClearMaskedLoiter()
        {
            _maskedLoiterDoor = Vector3.positiveInfinity;
            _maskedLoiterTimer = 0f;
        }

        internal void SetMaskedPatrol(Vector3 anchor, Vector3 facing, float duration)
        {
            if (float.IsPositiveInfinity(anchor.x))
            {
                return;
            }

            _maskedPatrolAnchor = anchor;
            _maskedPatrolFacing = float.IsPositiveInfinity(facing.x) ? Vector3.positiveInfinity : facing;
            _maskedPatrolTimer = Mathf.Max(_maskedPatrolTimer, duration);
        }

        internal bool TryGetMaskedPatrol(out Vector3 anchor, out Vector3 facing)
        {
            anchor = _maskedPatrolAnchor;
            facing = _maskedPatrolFacing;
            return _maskedPatrolTimer > 0f && !float.IsPositiveInfinity(anchor.x);
        }

        internal void ClearMaskedPatrol()
        {
            _maskedPatrolAnchor = Vector3.positiveInfinity;
            _maskedPatrolFacing = Vector3.positiveInfinity;
            _maskedPatrolTimer = 0f;
        }

        internal void SetMaskedFreeze(float duration)
        {
            _maskedFreezeTimer = Mathf.Max(_maskedFreezeTimer, duration);
        }

        internal bool MaskedIsFrozen => _maskedFreezeTimer > 0f;

        partial void TickMaskedSystems(float deltaTime)
        {
            if (_maskedInterceptTimer > 0f)
            {
                _maskedInterceptTimer = Mathf.Max(0f, _maskedInterceptTimer - deltaTime);
                if (_maskedInterceptTimer <= 0f)
                {
                    _maskedInterceptTarget = Vector3.positiveInfinity;
                }
            }

            if (_maskedLoiterTimer > 0f)
            {
                _maskedLoiterTimer = Mathf.Max(0f, _maskedLoiterTimer - deltaTime);
                if (_maskedLoiterTimer <= 0f)
                {
                    _maskedLoiterDoor = Vector3.positiveInfinity;
                }
            }

            if (_maskedVocalCooldown > 0f)
            {
                _maskedVocalCooldown = Mathf.Max(0f, _maskedVocalCooldown - deltaTime);
            }

            if (_maskedFreezeTimer > 0f)
            {
                _maskedFreezeTimer = Mathf.Max(0f, _maskedFreezeTimer - deltaTime);
            }

            if (_maskedPatrolTimer > 0f)
            {
                _maskedPatrolTimer = Mathf.Max(0f, _maskedPatrolTimer - deltaTime);
                if (_maskedPatrolTimer <= 0f)
                {
                    _maskedPatrolAnchor = Vector3.positiveInfinity;
                    _maskedPatrolFacing = Vector3.positiveInfinity;
                }
            }
        }
    }
}
