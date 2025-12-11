using UnityEngine;

namespace AlgoritmaPuncakMod.AI
{
    internal sealed partial class AIBlackboard
    {
        private enum ThumperChargeState
        {
            Idle,
            Priming,
            Charging,
            Recovering
        }

        private ThumperChargeState _thumperChargeState = ThumperChargeState.Idle;
        private float _thumperWindupTimer;
        private float _thumperChargeTimeRemaining;
        private float _thumperChargeTotalDuration;
        private float _thumperRecoveryTimer;
        private float _thumperChargeCooldown;
        private Vector3 _thumperChargeDirection = Vector3.forward;
        private Vector3 _thumperChargeTarget = Vector3.positiveInfinity;
        private Vector3 _thumperPatrolTarget = Vector3.positiveInfinity;
        private float _thumperPatrolRecalcTimer;
        private Vector3 _thumperSprintTarget = Vector3.positiveInfinity;
        private float _thumperSprintTimer;

        internal bool ThumperCanPrime => _thumperChargeState == ThumperChargeState.Idle && _thumperChargeCooldown <= 0f;
        internal bool ThumperPrimingActive => _thumperChargeState == ThumperChargeState.Priming;
        internal bool ShouldContinueThumperCharge => _thumperChargeState == ThumperChargeState.Charging && _thumperChargeTimeRemaining > 0f;
        internal bool ThumperNeedsRecovery => _thumperChargeState == ThumperChargeState.Recovering && _thumperRecoveryTimer > 0f;
        internal Vector3 ThumperChargeDestination => _thumperChargeTarget;
        internal Vector3 ThumperChargeDirection => _thumperChargeDirection;
        internal float ThumperChargeProgress => _thumperChargeTotalDuration <= 0f ? 0f : 1f - (_thumperChargeTimeRemaining / _thumperChargeTotalDuration);
        internal bool HasThumperPatrolTarget => !float.IsPositiveInfinity(_thumperPatrolTarget.x);
        internal Vector3 ThumperPatrolTarget => _thumperPatrolTarget;
        internal bool ShouldRefreshThumperPatrol => _thumperPatrolRecalcTimer <= 0f;
        internal bool HasThumperSprintTarget => !float.IsPositiveInfinity(_thumperSprintTarget.x);
        internal Vector3 ThumperSprintTarget => _thumperSprintTarget;

        internal bool ThumperReadyToBurst => _thumperChargeState == ThumperChargeState.Priming && _thumperWindupTimer <= 0f;

        internal void PrimeThumperCharge(Vector3 enemyPosition, Vector3 playerPosition)
        {
            if (!ThumperCanPrime)
            {
                return;
            }

            _thumperChargeState = ThumperChargeState.Priming;
            _thumperWindupTimer = 1f;
            var direction = playerPosition - enemyPosition;
            if (direction.sqrMagnitude < 0.01f)
            {
                direction = Vector3.forward;
            }

            _thumperChargeDirection = direction.normalized;
            float projectedDistance = Mathf.Max(8f, direction.magnitude + 6f);
            _thumperChargeTarget = enemyPosition + _thumperChargeDirection * projectedDistance;
        }

        internal void BeginThumperCharge(float durationSeconds)
        {
            _thumperChargeState = ThumperChargeState.Charging;
            _thumperChargeTimeRemaining = durationSeconds;
            _thumperChargeTotalDuration = durationSeconds;
            _thumperChargeCooldown = Mathf.Max(_thumperChargeCooldown, 3.5f);
        }

        internal void StopThumperCharge(bool stunned)
        {
            _thumperChargeState = ThumperChargeState.Recovering;
            _thumperRecoveryTimer = stunned ? 1f : 0.45f;
            _thumperChargeTarget = Vector3.positiveInfinity;
            _thumperChargeTimeRemaining = 0f;
            _thumperChargeTotalDuration = 0f;
        }

        internal void ForceThumperIdle()
        {
            _thumperChargeState = ThumperChargeState.Idle;
            _thumperChargeTarget = Vector3.positiveInfinity;
            _thumperChargeTimeRemaining = 0f;
            _thumperChargeTotalDuration = 0f;
            _thumperRecoveryTimer = 0f;
        }

        internal void SetThumperPatrolTarget(Vector3 target)
        {
            _thumperPatrolTarget = target;
            _thumperPatrolRecalcTimer = 6f;
        }

        internal void ClearThumperPatrolTarget()
        {
            _thumperPatrolTarget = Vector3.positiveInfinity;
            _thumperPatrolRecalcTimer = 0f;
        }

        internal void SetThumperSprintTarget(Vector3 target)
        {
            _thumperSprintTarget = target;
            _thumperSprintTimer = 5f;
        }

        internal void ClearThumperSprintTarget()
        {
            _thumperSprintTarget = Vector3.positiveInfinity;
            _thumperSprintTimer = 0f;
        }

        partial void TickThumperSystems(float deltaTime)
        {
            if (_thumperChargeCooldown > 0f)
            {
                _thumperChargeCooldown = Mathf.Max(0f, _thumperChargeCooldown - deltaTime);
            }

            if (_thumperChargeState == ThumperChargeState.Priming)
            {
                _thumperWindupTimer = Mathf.Max(0f, _thumperWindupTimer - deltaTime);
            }
            else if (_thumperChargeState == ThumperChargeState.Charging)
            {
                _thumperChargeTimeRemaining = Mathf.Max(0f, _thumperChargeTimeRemaining - deltaTime);
                if (_thumperChargeTimeRemaining <= 0f)
                {
                    StopThumperCharge(stunned: false);
                }
            }
            else if (_thumperChargeState == ThumperChargeState.Recovering)
            {
                _thumperRecoveryTimer = Mathf.Max(0f, _thumperRecoveryTimer - deltaTime);
                if (_thumperRecoveryTimer <= 0f)
                {
                    _thumperChargeState = ThumperChargeState.Idle;
                }
            }

            if (_thumperPatrolRecalcTimer > 0f)
            {
                _thumperPatrolRecalcTimer -= deltaTime;
                if (_thumperPatrolRecalcTimer <= 0f)
                {
                    ClearThumperPatrolTarget();
                }
            }

            if (_thumperSprintTimer > 0f)
            {
                _thumperSprintTimer -= deltaTime;
                if (_thumperSprintTimer <= 0f)
                {
                    ClearThumperSprintTarget();
                }
            }
        }
    }
}
