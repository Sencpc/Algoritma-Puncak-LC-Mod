using UnityEngine;

namespace AlgoritmaPuncakMod.AI
{
    internal sealed partial class AIBlackboard
    {
        private float _coilheadAggroMemory;
        private Vector3 _coilheadTrackedTarget = Vector3.positiveInfinity;
        private bool _coilheadFrozen;
        private float _coilheadFreezeBuffer;
        private bool _coilheadObservedThisTick;
        private Component _coilheadDoorComponent;
        private Vector3 _coilheadDoorPosition = Vector3.positiveInfinity;
        private float _coilheadDoorHoldTimer;

        internal bool CoilheadHasAggro => _coilheadAggroMemory > 0f;
        internal Vector3 CoilheadTarget => _coilheadTrackedTarget;
        internal bool CoilheadFrozen => _coilheadFrozen;
        internal bool CoilheadFreezeActive => _coilheadObservedThisTick || _coilheadFreezeBuffer > 0f;
        internal bool CoilheadDoorEngaged => _coilheadDoorComponent != null;
        internal bool CoilheadDoorReady => _coilheadDoorComponent != null && _coilheadDoorHoldTimer <= 0f;
        internal Component CoilheadDoorComponent => _coilheadDoorComponent;
        internal Vector3 CoilheadDoorFocus => _coilheadDoorPosition;

        internal void SetCoilheadTarget(Vector3 position, float memoryDuration)
        {
            _coilheadTrackedTarget = position;
            _coilheadAggroMemory = Mathf.Max(_coilheadAggroMemory, memoryDuration);
        }

        internal void ClearCoilheadTarget()
        {
            _coilheadTrackedTarget = Vector3.positiveInfinity;
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
            if (_coilheadAggroMemory <= 0f)
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
        }
    }
}
