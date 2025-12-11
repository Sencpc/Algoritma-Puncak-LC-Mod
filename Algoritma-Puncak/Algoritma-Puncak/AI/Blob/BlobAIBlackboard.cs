using UnityEngine;

namespace AlgoritmaPuncakMod.AI
{
    internal sealed partial class AIBlackboard
    {
        private Vector3 _blobHazardPosition = Vector3.positiveInfinity;
        private Vector3 _blobHazardAvoidTarget = Vector3.positiveInfinity;
        private float _blobHazardCooldown;

        private Vector3 _blobInterceptTarget = Vector3.positiveInfinity;
        private float _blobInterceptTimer;

        private Vector3 _blobAmbushAnchor = Vector3.positiveInfinity;
        private float _blobAmbushTimer;

        internal bool BlobHazardActive => _blobHazardCooldown > 0f && !float.IsPositiveInfinity(_blobHazardAvoidTarget.x);
        internal Vector3 BlobHazardAvoidTarget => _blobHazardAvoidTarget;
        internal Vector3 BlobHazardPosition => _blobHazardPosition;

        internal bool BlobHasIntercept => !float.IsPositiveInfinity(_blobInterceptTarget.x);
        internal Vector3 BlobInterceptTarget => _blobInterceptTarget;

        internal bool BlobHasAmbush => !float.IsPositiveInfinity(_blobAmbushAnchor.x);
        internal Vector3 BlobAmbushAnchor => _blobAmbushAnchor;

        internal void SetBlobHazard(Vector3 hazardPosition, Vector3 rerouteTarget)
        {
            _blobHazardPosition = hazardPosition;
            _blobHazardAvoidTarget = rerouteTarget;
            _blobHazardCooldown = 3.5f;
        }

        internal void ClearBlobHazard()
        {
            _blobHazardCooldown = 0f;
            _blobHazardPosition = Vector3.positiveInfinity;
            _blobHazardAvoidTarget = Vector3.positiveInfinity;
        }

        internal void SetBlobIntercept(Vector3 interceptPosition)
        {
            _blobInterceptTarget = interceptPosition;
            _blobInterceptTimer = 5f;
        }

        internal void ClearBlobIntercept()
        {
            _blobInterceptTimer = 0f;
            _blobInterceptTarget = Vector3.positiveInfinity;
        }

        internal void SetBlobAmbushAnchor(Vector3 anchor)
        {
            _blobAmbushAnchor = anchor;
            _blobAmbushTimer = 8f;
        }

        internal void ClearBlobAmbush()
        {
            _blobAmbushAnchor = Vector3.positiveInfinity;
            _blobAmbushTimer = 0f;
        }

        partial void TickBlobSystems(float deltaTime)
        {
            if (_blobHazardCooldown > 0f)
            {
                _blobHazardCooldown = Mathf.Max(0f, _blobHazardCooldown - deltaTime);
                if (_blobHazardCooldown == 0f)
                {
                    _blobHazardPosition = Vector3.positiveInfinity;
                    _blobHazardAvoidTarget = Vector3.positiveInfinity;
                }
            }

            if (_blobInterceptTimer > 0f)
            {
                _blobInterceptTimer = Mathf.Max(0f, _blobInterceptTimer - deltaTime);
                if (_blobInterceptTimer == 0f)
                {
                    _blobInterceptTarget = Vector3.positiveInfinity;
                }
            }

            if (_blobAmbushTimer > 0f)
            {
                _blobAmbushTimer = Mathf.Max(0f, _blobAmbushTimer - deltaTime);
                if (_blobAmbushTimer == 0f)
                {
                    _blobAmbushAnchor = Vector3.positiveInfinity;
                }
            }
        }
    }
}
