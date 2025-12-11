using UnityEngine;

namespace AlgoritmaPuncakMod.AI
{
    internal sealed partial class AIBlackboard
    {
        private Vector3 _sandWormHotspot = Vector3.positiveInfinity;
        private float _sandWormHotspotHeat;
        private float _sandWormAttackCooldown;
        private float _sandWormPrepTimer;
        private float _sandWormRoarTimer;
        private float _sandWormEruptTimer;
        private SandWormAttackStage _sandWormStage = SandWormAttackStage.Idle;

        internal bool SandWormHasHotspot => !float.IsPositiveInfinity(_sandWormHotspot.x) && _sandWormHotspotHeat > 0.1f;
        internal Vector3 SandWormHotspot => _sandWormHotspot;
        internal float SandWormHotspotHeat => _sandWormHotspotHeat;
        internal float SandWormHeatNormalized => SandWormNoiseField.NormalizeHeat(_sandWormHotspotHeat);
        internal bool SandWormCooldownReady => _sandWormAttackCooldown <= 0f;
        internal bool SandWormPrepActive => _sandWormPrepTimer > 0f;
        internal bool SandWormRoarActive => _sandWormRoarTimer > 0f;
        internal bool SandWormEruptActive => _sandWormEruptTimer > 0f;
        internal SandWormAttackStage SandWormStage => _sandWormStage;

        internal void SetSandWormStage(SandWormAttackStage stage)
        {
            _sandWormStage = stage;
        }

        internal void BeginSandWormPrep(float duration)
        {
            _sandWormPrepTimer = Mathf.Max(_sandWormPrepTimer, duration);
        }

        internal void TriggerSandWormRoar(float delay)
        {
            _sandWormRoarTimer = Mathf.Max(_sandWormRoarTimer, delay);
        }

        internal void TriggerSandWormEruption(float duration)
        {
            _sandWormEruptTimer = Mathf.Max(_sandWormEruptTimer, duration);
        }

        internal void BeginSandWormCooldown(float duration)
        {
            _sandWormAttackCooldown = Mathf.Max(_sandWormAttackCooldown, duration);
        }

        internal void CoolSandWormHotspot(Vector3 position, float radius)
        {
            SandWormNoiseField.Dampen(position, radius);
        }

        internal void ResetSandWormAttackState()
        {
            _sandWormStage = SandWormAttackStage.Idle;
            _sandWormPrepTimer = 0f;
            _sandWormRoarTimer = 0f;
            _sandWormEruptTimer = 0f;
        }

        partial void TickSandWormSystems(float deltaTime)
        {
            SandWormNoiseField.Tick(deltaTime);
            if (SandWormNoiseField.TryGetHottestCell(out var hotspot, out var heat))
            {
                _sandWormHotspot = hotspot;
                _sandWormHotspotHeat = heat;
            }
            else
            {
                _sandWormHotspot = Vector3.positiveInfinity;
                _sandWormHotspotHeat = 0f;
            }

            if (_sandWormAttackCooldown > 0f)
            {
                _sandWormAttackCooldown = Mathf.Max(0f, _sandWormAttackCooldown - deltaTime);
            }

            if (_sandWormPrepTimer > 0f)
            {
                _sandWormPrepTimer = Mathf.Max(0f, _sandWormPrepTimer - deltaTime);
            }

            if (_sandWormRoarTimer > 0f)
            {
                _sandWormRoarTimer = Mathf.Max(0f, _sandWormRoarTimer - deltaTime);
            }

            if (_sandWormEruptTimer > 0f)
            {
                _sandWormEruptTimer = Mathf.Max(0f, _sandWormEruptTimer - deltaTime);
            }

            if (_sandWormStage == SandWormAttackStage.Erupting && _sandWormEruptTimer <= 0f)
            {
                _sandWormStage = SandWormAttackStage.Idle;
            }
        }
    }

    internal enum SandWormAttackStage
    {
        Idle,
        Approaching,
        Charging,
        Roaring,
        Erupting
    }
}
