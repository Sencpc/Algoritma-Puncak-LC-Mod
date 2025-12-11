using UnityEngine;

namespace AlgoritmaPuncakMod.AI
{
    internal sealed partial class AIBlackboard
    {
        private const float MouthDogMaxStimulusStrength = 2.5f;
        private Vector3 _mouthDogHighStimulus = Vector3.positiveInfinity;
        private Vector3 _mouthDogLowStimulus = Vector3.positiveInfinity;
        private float _mouthDogHighStrength;
        private float _mouthDogLowStrength;
        private float _mouthDogHighMemory;
        private float _mouthDogLowMemory;
        private bool _mouthDogHighInterrupt;
        private bool _mouthDogLowInterrupt;
        private float _mouthDogChargeCooldown;
        private Vector3 _mouthDogRoamPoint = Vector3.positiveInfinity;
        private float _mouthDogRoamTimer;

        internal bool MouthDogHasHighStimulus => _mouthDogHighMemory > 0f;
        internal bool MouthDogHasLowStimulus => _mouthDogLowMemory > 0f;
        internal Vector3 MouthDogHighStimulus => _mouthDogHighStimulus;
        internal Vector3 MouthDogLowStimulus => _mouthDogLowStimulus;
        internal bool MouthDogChargeReady => _mouthDogChargeCooldown <= 0f;
        internal float MouthDogHighIntensityNormalized => Mathf.Clamp01(_mouthDogHighStrength / MouthDogMaxStimulusStrength);
        internal float MouthDogLowIntensityNormalized => Mathf.Clamp01(_mouthDogLowStrength / MouthDogMaxStimulusStrength);

        internal void RegisterMouthDogNoise(Vector3 position, float strength, bool highPriority)
        {
            if (float.IsInfinity(position.x))
            {
                return;
            }

            float clampedStrength = Mathf.Clamp(strength, 0f, MouthDogMaxStimulusStrength);
            if (highPriority)
            {
                if (clampedStrength >= _mouthDogHighStrength - 0.05f || _mouthDogHighMemory <= 0f)
                {
                    _mouthDogHighStimulus = position;
                    _mouthDogHighStrength = clampedStrength;
                    _mouthDogHighInterrupt = true;
                }

                _mouthDogHighMemory = Mathf.Max(_mouthDogHighMemory, Mathf.Lerp(0.9f, 2.4f, clampedStrength / MouthDogMaxStimulusStrength));
                return;
            }

            if (clampedStrength >= _mouthDogLowStrength - 0.05f || _mouthDogLowMemory <= 0f)
            {
                _mouthDogLowStimulus = position;
                _mouthDogLowStrength = clampedStrength;
                _mouthDogLowInterrupt = true;
            }

            _mouthDogLowMemory = Mathf.Max(_mouthDogLowMemory, Mathf.Lerp(1.8f, 4.5f, clampedStrength / MouthDogMaxStimulusStrength));
        }

        internal bool MouthDogConsumeHighInterrupt()
        {
            if (!_mouthDogHighInterrupt)
            {
                return false;
            }

            _mouthDogHighInterrupt = false;
            return true;
        }

        internal bool MouthDogConsumeLowInterrupt()
        {
            if (!_mouthDogLowInterrupt)
            {
                return false;
            }

            _mouthDogLowInterrupt = false;
            return true;
        }

        internal void ClearMouthDogHighStimulus()
        {
            _mouthDogHighStimulus = Vector3.positiveInfinity;
            _mouthDogHighStrength = 0f;
            _mouthDogHighMemory = 0f;
            _mouthDogHighInterrupt = false;
        }

        internal void ClearMouthDogLowStimulus()
        {
            _mouthDogLowStimulus = Vector3.positiveInfinity;
            _mouthDogLowStrength = 0f;
            _mouthDogLowMemory = 0f;
            _mouthDogLowInterrupt = false;
        }

        internal void BeginMouthDogChargeCooldown(float seconds)
        {
            _mouthDogChargeCooldown = Mathf.Max(_mouthDogChargeCooldown, seconds);
        }

        internal Vector3 GetMouthDogRoamPoint(Vector3 origin)
        {
            if (_mouthDogRoamTimer > 0f && !float.IsPositiveInfinity(_mouthDogRoamPoint.x))
            {
                return _mouthDogRoamPoint;
            }

            var offset = Random.insideUnitSphere * 6f;
            offset.y = 0f;
            _mouthDogRoamPoint = origin + offset;
            _mouthDogRoamTimer = Random.Range(0.8f, 1.6f);
            return _mouthDogRoamPoint;
        }

        partial void TickMouthDogSystems(float deltaTime)
        {
            if (_mouthDogHighMemory > 0f)
            {
                _mouthDogHighMemory = Mathf.Max(0f, _mouthDogHighMemory - deltaTime);
                if (_mouthDogHighMemory <= 0f)
                {
                    ClearMouthDogHighStimulus();
                }
            }

            if (_mouthDogLowMemory > 0f)
            {
                _mouthDogLowMemory = Mathf.Max(0f, _mouthDogLowMemory - deltaTime * 0.65f);
                if (_mouthDogLowMemory <= 0f)
                {
                    ClearMouthDogLowStimulus();
                }
            }

            if (_mouthDogChargeCooldown > 0f)
            {
                _mouthDogChargeCooldown = Mathf.Max(0f, _mouthDogChargeCooldown - deltaTime);
            }

            if (_mouthDogRoamTimer > 0f)
            {
                _mouthDogRoamTimer = Mathf.Max(0f, _mouthDogRoamTimer - deltaTime);
                if (_mouthDogRoamTimer <= 0f)
                {
                    _mouthDogRoamPoint = Vector3.positiveInfinity;
                }
            }
        }
    }
}
