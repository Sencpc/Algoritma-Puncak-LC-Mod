using UnityEngine;

namespace AlgoritmaPuncakMod.AI
{
    internal sealed partial class AIBlackboard
    {
        private Vector3 _baboonNest = Vector3.positiveInfinity;
        private float _baboonAlertTimer;
        private float _baboonScreamCooldown;
        private float _baboonShipOverrideTimer;
        private float _baboonLeaderAttackTimer;
        private float _baboonLeaderMoveTimer;
        private Vector3 _baboonLeaderTarget = Vector3.positiveInfinity;
        private Vector3 _baboonLeaderMove = Vector3.forward;
        private bool _baboonIsLeader;
        private int _baboonPackKey;

        internal Vector3 BaboonNest => _baboonNest;
        internal bool BaboonAlertActive => _baboonAlertTimer > 0f;
        internal bool BaboonCanScream => _baboonScreamCooldown <= 0f;
        internal bool BaboonShipOverride => _baboonShipOverrideTimer > 0f;
        internal bool BaboonLeaderAttacking => _baboonLeaderAttackTimer > 0f;
        internal bool BaboonLeaderAdvancing => _baboonLeaderMoveTimer > 0f;
        internal Vector3 BaboonLeaderTarget => _baboonLeaderTarget;
        internal Vector3 BaboonLeaderMove => _baboonLeaderMove;
        internal bool BaboonIsLeader => _baboonIsLeader;
        internal int BaboonPackKey => _baboonPackKey;

        internal void EnsureBaboonNest(Vector3 fallbackPosition)
        {
            if (!float.IsPositiveInfinity(_baboonNest.x))
            {
                return;
            }

            if (BaboonBirdAI.baboonCampPosition != Vector3.zero)
            {
                _baboonNest = BaboonBirdAI.baboonCampPosition;
            }
            else
            {
                _baboonNest = fallbackPosition;
            }

            _baboonPackKey = HashBaboonPosition(_baboonNest);
        }

        internal void TriggerBaboonAlert(float duration)
        {
            _baboonAlertTimer = Mathf.Max(_baboonAlertTimer, duration);
        }

        internal void BeginBaboonScreamCooldown(float duration)
        {
            _baboonScreamCooldown = Mathf.Max(_baboonScreamCooldown, duration);
        }

        internal void AllowBaboonShipApproach(float duration)
        {
            _baboonShipOverrideTimer = Mathf.Max(_baboonShipOverrideTimer, duration);
        }

        internal void MarkBaboonLeadership(bool isLeader)
        {
            _baboonIsLeader = isLeader;
        }

        internal void BroadcastBaboonAttack(Vector3 target, float duration)
        {
            _baboonLeaderTarget = target;
            _baboonLeaderAttackTimer = Mathf.Max(_baboonLeaderAttackTimer, duration);
        }

        internal void BroadcastBaboonMovement(Vector3 moveDirection, float duration)
        {
            if (moveDirection.sqrMagnitude > 0.001f)
            {
                _baboonLeaderMove = moveDirection.normalized;
            }

            _baboonLeaderMoveTimer = Mathf.Max(_baboonLeaderMoveTimer, duration);
        }

        internal int ComputeBaboonPackKey(Vector3 position) => HashBaboonPosition(position);

        partial void TickBaboonSystems(float deltaTime)
        {
            if (_baboonAlertTimer > 0f)
            {
                _baboonAlertTimer = Mathf.Max(0f, _baboonAlertTimer - deltaTime);
            }

            if (_baboonScreamCooldown > 0f)
            {
                _baboonScreamCooldown = Mathf.Max(0f, _baboonScreamCooldown - deltaTime);
            }

            if (_baboonShipOverrideTimer > 0f)
            {
                _baboonShipOverrideTimer = Mathf.Max(0f, _baboonShipOverrideTimer - deltaTime);
            }

            if (_baboonLeaderAttackTimer > 0f)
            {
                _baboonLeaderAttackTimer = Mathf.Max(0f, _baboonLeaderAttackTimer - deltaTime);
                if (_baboonLeaderAttackTimer <= 0f)
                {
                    _baboonLeaderTarget = Vector3.positiveInfinity;
                }
            }

            if (_baboonLeaderMoveTimer > 0f)
            {
                _baboonLeaderMoveTimer = Mathf.Max(0f, _baboonLeaderMoveTimer - deltaTime);
            }
        }

        private static int HashBaboonPosition(Vector3 position)
        {
            int x = Mathf.RoundToInt(position.x / 4f);
            int z = Mathf.RoundToInt(position.z / 4f);
            return (x * 73856093) ^ (z * 19349663);
        }
    }
}
