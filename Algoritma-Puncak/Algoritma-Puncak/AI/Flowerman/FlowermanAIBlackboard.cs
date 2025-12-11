using UnityEngine;
using UnityEngine.AI;

namespace AlgoritmaPuncakMod.AI
{
    internal sealed partial class AIBlackboard
    {
        private const float FlowermanAngerThreshold = 12f;

        private float _flowermanAnger;
        private float _flowermanCalmTimer;
        private Vector3 _flowermanCachedForward = Vector3.forward;

        private Vector3 _flowermanBlindSpot = Vector3.positiveInfinity;
        private float _flowermanBlindSpotCooldown;

        private Vector3 _flowermanFlankTarget = Vector3.positiveInfinity;
        private float _flowermanFlankCooldown;

        private Vector3 _flowermanEscapeTarget = Vector3.positiveInfinity;
        private float _flowermanEscapeTimer;

        internal bool FlowermanAngerReady => _flowermanAnger >= FlowermanAngerThreshold;
        internal float FlowermanAngerRatio => Mathf.Clamp01(_flowermanAnger / FlowermanAngerThreshold);
        internal bool FlowermanHasBlindSpot => !float.IsPositiveInfinity(_flowermanBlindSpot.x);
        internal Vector3 FlowermanBlindSpot => _flowermanBlindSpot;
        internal bool FlowermanHasFlank => !float.IsPositiveInfinity(_flowermanFlankTarget.x);
        internal Vector3 FlowermanFlankTarget => _flowermanFlankTarget;
        internal bool FlowermanHasEscape => !float.IsPositiveInfinity(_flowermanEscapeTarget.x);
        internal Vector3 FlowermanEscapeTarget => _flowermanEscapeTarget;

        internal void AddFlowermanAnger(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            _flowermanAnger = Mathf.Min(FlowermanAngerThreshold * 1.35f, _flowermanAnger + amount);
            _flowermanCalmTimer = 2f;
        }

        internal void CoolFlowermanAnger(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            _flowermanAnger = Mathf.Max(0f, _flowermanAnger - amount);
        }

        internal void ResetFlowermanAnger()
        {
            _flowermanAnger = 0f;
            _flowermanCalmTimer = 0f;
        }

        internal bool FlowermanPlayerWatching(Vector3 enemyPosition, float toleranceDot = 0.65f)
        {
            if (float.IsPositiveInfinity(LastKnownPlayerPosition.x))
            {
                return false;
            }

            var toEnemy = (enemyPosition - LastKnownPlayerPosition).normalized;
            if (toEnemy.sqrMagnitude < 0.01f)
            {
                return false;
            }

            var view = LastKnownPlayerForward.sqrMagnitude < 0.01f ? -toEnemy : LastKnownPlayerForward.normalized;
            return Vector3.Dot(view, toEnemy) >= toleranceDot;
        }

        internal void RefreshFlowermanVectors(Vector3 enemyPosition)
        {
            if (float.IsPositiveInfinity(LastKnownPlayerPosition.x))
            {
                _flowermanBlindSpot = Vector3.positiveInfinity;
                _flowermanFlankTarget = Vector3.positiveInfinity;
                return;
            }

            var currentForward = LastKnownPlayerForward.sqrMagnitude < 0.01f
                ? (enemyPosition - LastKnownPlayerPosition).normalized
                : LastKnownPlayerForward.normalized;

            if (currentForward.sqrMagnitude < 0.01f)
            {
                currentForward = Vector3.forward;
            }

            if (Vector3.Dot(_flowermanCachedForward, currentForward) < 0.75f)
            {
                _flowermanBlindSpotCooldown = 0f;
                _flowermanFlankCooldown = 0f;
            }

            _flowermanCachedForward = currentForward;

            if (_flowermanBlindSpotCooldown <= 0f)
            {
                var blindGuess = LastKnownPlayerPosition - currentForward * Mathf.Max(4.5f, DistanceToPlayer * 0.6f);
                SampleNavigation(blindGuess, 4.5f, out _flowermanBlindSpot);
                _flowermanBlindSpotCooldown = 0.25f;
            }

            if (_flowermanFlankCooldown <= 0f)
            {
                var flankGuess = LastKnownPlayerPosition - currentForward * 10f;
                SampleNavigation(flankGuess, 6f, out _flowermanFlankTarget);
                _flowermanFlankCooldown = 0.2f;
            }
        }

        internal void RequestFlowermanEscape(Vector3 enemyPosition)
        {
            if (float.IsPositiveInfinity(LastKnownPlayerPosition.x))
            {
                _flowermanEscapeTarget = Vector3.positiveInfinity;
                return;
            }

            var view = LastKnownPlayerForward.sqrMagnitude < 0.01f
                ? (LastKnownPlayerPosition - enemyPosition).normalized
                : LastKnownPlayerForward.normalized;

            var lateral = Vector3.Cross(Vector3.up, view);
            if (lateral.sqrMagnitude < 0.01f)
            {
                lateral = Vector3.Cross(view, Vector3.up);
            }

            lateral = lateral.normalized;
            var offset = lateral * (DistanceToPlayer < 6f ? 8f : 5f);
            if (!SampleNavigation(enemyPosition + offset, 5f, out _flowermanEscapeTarget))
            {
                SampleNavigation(enemyPosition - offset, 5f, out _flowermanEscapeTarget);
            }

            _flowermanEscapeTimer = 2.5f;
        }

        private static bool SampleNavigation(Vector3 guess, float radius, out Vector3 result)
        {
            if (NavMesh.SamplePosition(guess, out var hit, radius, NavMesh.AllAreas))
            {
                result = hit.position;
                return true;
            }

            result = Vector3.positiveInfinity;
            return false;
        }

        partial void TickFlowermanSystems(float deltaTime)
        {
            if (PlayerVisible)
            {
                AddFlowermanAnger(deltaTime * 0.25f);
                if (DistanceToPlayer < 9f)
                {
                    AddFlowermanAnger(deltaTime * 0.15f);
                }
            }
            else if (_flowermanCalmTimer <= 0f)
            {
                CoolFlowermanAnger(deltaTime * 0.5f);
            }

            if (_flowermanCalmTimer > 0f)
            {
                _flowermanCalmTimer = Mathf.Max(0f, _flowermanCalmTimer - deltaTime);
            }

            if (_flowermanBlindSpotCooldown > 0f)
            {
                _flowermanBlindSpotCooldown = Mathf.Max(0f, _flowermanBlindSpotCooldown - deltaTime);
            }

            if (_flowermanFlankCooldown > 0f)
            {
                _flowermanFlankCooldown = Mathf.Max(0f, _flowermanFlankCooldown - deltaTime);
            }

            if (_flowermanEscapeTimer > 0f)
            {
                _flowermanEscapeTimer = Mathf.Max(0f, _flowermanEscapeTimer - deltaTime);
                if (_flowermanEscapeTimer == 0f)
                {
                    _flowermanEscapeTarget = Vector3.positiveInfinity;
                }
            }
        }
    }
}
