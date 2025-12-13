using UnityEngine;

namespace AlgoritmaPuncakMod.AI
{
    internal sealed partial class AIBlackboard
    {
        private Vector3 _snareAmbushAnchor = Vector3.positiveInfinity;
        private Vector3 _snareAmbushCeiling = Vector3.positiveInfinity;
        private float _snareTrafficTimer = 12f;
        private float _snareExposureTimer = 5f;
        private float _snareRelocateCooldown;
        private float _snareDropCooldown;
        private float _snareDropTimer;
        private bool _snareRelocateRequested;
        private bool _snareDropPending;
        private bool _snarePanic;

        internal bool SnareHasAmbush => !float.IsPositiveInfinity(_snareAmbushAnchor.x);
        internal Vector3 SnareAmbushAnchor => _snareAmbushAnchor;
        internal Vector3 SnareAmbushCeiling => _snareAmbushCeiling;
        internal bool SnareRelocateRequested => _snareRelocateRequested;
        internal bool SnarePanic => _snarePanic;
        internal bool SnareDropReady => _snareDropCooldown <= 0f && !_snareDropPending && SnareHasAmbush;
        internal float SnareTrafficTimer => _snareTrafficTimer;
        internal float SnareExposureTimer => _snareExposureTimer;

        internal void SetSnareAmbush(Vector3 anchor, Vector3 ceiling)
        {
            _snareAmbushAnchor = anchor;
            _snareAmbushCeiling = ceiling;
            _snareRelocateRequested = false;
            _snareTrafficTimer = 0f;
            _snareExposureTimer = 5f;
            _snareDropPending = false;
            _snareDropTimer = 0f;
        }

        internal void ClearSnareAmbush()
        {
            _snareAmbushAnchor = Vector3.positiveInfinity;
            _snareAmbushCeiling = Vector3.positiveInfinity;
            _snareRelocateRequested = true;
        }

        internal void FlagSnareRelocate()
        {
            if (_snareRelocateCooldown > 0f)
            {
                return;
            }

            _snareRelocateRequested = true;
            _snareRelocateCooldown = 4f;
        }

        internal void RegisterSnareTraffic()
        {
            _snareTrafficTimer = 0f;
        }

        internal void RegisterSnareExposure()
        {
            _snareExposureTimer = 0f;
        }

        internal void RegisterSnareDrop()
        {
            _snareDropPending = true;
            _snareDropTimer = 0f;
            _snareDropCooldown = 5f;
            _snareTrafficTimer = 0f;
        }

        internal void CompleteSnareLatch()
        {
            _snareDropPending = false;
            _snareDropTimer = 0f;
            _snarePanic = false;
        }

        internal void TriggerSnarePanic()
        {
            _snarePanic = true;
            _snareDropPending = false;
            if (!_snareRelocateRequested)
            {
                _snareRelocateRequested = true;
                _snareRelocateCooldown = 5f;
            }
        }

        internal void ResolveSnarePanic()
        {
            _snarePanic = false;
        }

        partial void TickSnareSystems(float deltaTime)
        {
            if (_snareRelocateCooldown > 0f)
            {
                _snareRelocateCooldown = Mathf.Max(0f, _snareRelocateCooldown - deltaTime);
            }

            if (_snareDropCooldown > 0f)
            {
                _snareDropCooldown = Mathf.Max(0f, _snareDropCooldown - deltaTime);
            }

            if (_snareDropPending)
            {
                _snareDropTimer += deltaTime;
                if (_snareDropTimer >= 3f)
                {
                    _snareDropPending = false;
                    TriggerSnarePanic();
                }
            }

            if (SnareHasAmbush)
            {
                if (!float.IsPositiveInfinity(LastKnownPlayerPosition.x))
                {
                    Vector3 anchorFlat = new Vector3(_snareAmbushAnchor.x, 0f, _snareAmbushAnchor.z);
                    Vector3 playerFlat = new Vector3(LastKnownPlayerPosition.x, 0f, LastKnownPlayerPosition.z);
                    float horizontal = Vector3.Distance(anchorFlat, playerFlat);
                    if (horizontal <= 2.25f)
                    {
                        _snareTrafficTimer = 0f;
                    }
                    else
                    {
                        _snareTrafficTimer += deltaTime;
                    }

                    if (PlayerVisible && DistanceToPlayer <= 12f)
                    {
                        var toSpot = (_snareAmbushAnchor - LastKnownPlayerPosition);
                        if (toSpot.sqrMagnitude > 0.5f)
                        {
                            toSpot.Normalize();
                            float facing = Vector3.Dot(LastKnownPlayerForward, toSpot);
                            if (facing > 0.7f)
                            {
                                _snareExposureTimer = 0f;
                            }
                            else
                            {
                                _snareExposureTimer += deltaTime;
                            }
                        }
                        else
                        {
                            _snareExposureTimer += deltaTime;
                        }
                    }
                    else
                    {
                        _snareExposureTimer += deltaTime;
                    }
                }
                else
                {
                    _snareTrafficTimer += deltaTime;
                    _snareExposureTimer += deltaTime;
                }

                if ((_snareTrafficTimer > 14f || (_snareExposureTimer < 1.25f && PlayerVisible)) && !_snareRelocateRequested)
                {
                    FlagSnareRelocate();
                }
            }
            else
            {
                _snareTrafficTimer += deltaTime;
                _snareExposureTimer += deltaTime;
            }
        }
    }
}
