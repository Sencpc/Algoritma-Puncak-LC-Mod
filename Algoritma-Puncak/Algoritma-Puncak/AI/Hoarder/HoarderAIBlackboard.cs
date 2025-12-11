using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace AlgoritmaPuncakMod.AI
{
    internal sealed partial class AIBlackboard
    {
        internal enum HoarderAggroLevel
        {
            None,
            Soft,
            Hard,
            Fatal
        }

        private static readonly Vector3[] HoarderSearchOffsets =
        {
            new Vector3(1f, 0f, 0f),
            new Vector3(-1f, 0f, 0f),
            new Vector3(0f, 0f, 1f),
            new Vector3(0f, 0f, -1f),
            new Vector3(0.707f, 0f, 0.707f),
            new Vector3(-0.707f, 0f, 0.707f),
            new Vector3(0.707f, 0f, -0.707f),
            new Vector3(-0.707f, 0f, -0.707f)
        };

        private readonly Dictionary<int, float> _hoarderExploredCells = new Dictionary<int, float>();
        private Vector3 _hoarderNest = Vector3.positiveInfinity;
        private int _hoarderLayerIndex;
        private int _hoarderOffsetIndex;
        private float _hoarderScanMaxRadius;
        private int _hoarderItemsLocated;
        private bool _hoarderMigrationRequested;
        private Vector3 _hoarderMigrationTarget = Vector3.positiveInfinity;
        private float _hoarderAggroGrace;
        private float _hoarderMigrationCooldown;

        internal bool HoarderHasNest => !float.IsPositiveInfinity(_hoarderNest.x);
        internal Vector3 HoarderNest => _hoarderNest;
        internal HoarderAggroLevel HoarderAggroState => EvaluateHoarderAggro();
        internal bool HoarderShouldMigrate => HoarderReadyToMigrate && !_hoarderMigrationRequested;
        internal bool HoarderMigrationPending => _hoarderMigrationRequested;
        internal Vector3 HoarderPendingNest => _hoarderMigrationTarget;

        internal void EnsureHoarderNest(Vector3 fallbackPosition)
        {
            if (HoarderHasNest)
            {
                return;
            }

            _hoarderNest = fallbackPosition;
            if (TerritoryRadius <= 0f)
            {
                InitializeTerritory(fallbackPosition, 15f);
            }
        }

        internal bool TryGetNextHoarderSearchTarget(Vector3 fallbackPosition, out Vector3 target)
        {
            EnsureHoarderNest(fallbackPosition);
            const int maxAttempts = 48;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                if (_hoarderOffsetIndex >= HoarderSearchOffsets.Length)
                {
                    _hoarderOffsetIndex = 0;
                    _hoarderLayerIndex++;
                }

                float radius = Mathf.Max(5f, (_hoarderLayerIndex + 1) * 6f);
                var offset = HoarderSearchOffsets[_hoarderOffsetIndex++];
                var guess = _hoarderNest + offset * radius;
                if (!NavMesh.SamplePosition(guess, out var hit, 6f, NavMesh.AllAreas))
                {
                    continue;
                }

                if (!IsCellFresh(hit.position))
                {
                    continue;
                }

                target = hit.position;
                MarkCellExplored(target);
                _hoarderScanMaxRadius = Mathf.Max(_hoarderScanMaxRadius, Vector3.Distance(_hoarderNest, target));
                return true;
            }

            target = Vector3.positiveInfinity;
            ResetHoarderSearchSweep();
            return false;
        }

        internal void MarkHoarderItemLocated()
        {
            _hoarderItemsLocated++;
            _hoarderScanMaxRadius = 0f;
        }

        internal void RecordHoarderLootDelivered()
        {
            if (_hoarderItemsLocated > 0)
            {
                _hoarderItemsLocated--;
            }
        }

        internal bool TryConsumeHoarderMigration(out Vector3 newNest)
        {
            if (!_hoarderMigrationRequested)
            {
                newNest = Vector3.positiveInfinity;
                return false;
            }

            newNest = _hoarderMigrationTarget;
            _hoarderMigrationRequested = false;
            _hoarderMigrationTarget = Vector3.positiveInfinity;
            return true;
        }

        internal void RequestHoarderMigration(Vector3 target)
        {
            _hoarderMigrationRequested = true;
            _hoarderMigrationTarget = target;
        }

        internal void ConfirmHoarderMigration(Vector3 newNest)
        {
            _hoarderNest = newNest;
            _hoarderLayerIndex = 0;
            _hoarderOffsetIndex = 0;
            _hoarderScanMaxRadius = 0f;
            _hoarderItemsLocated = 0;
            _hoarderExploredCells.Clear();
            _hoarderMigrationCooldown = 15f;
        }

        internal void TriggerHoarderAggro(float durationSeconds)
        {
            _hoarderAggroGrace = Mathf.Max(_hoarderAggroGrace, durationSeconds);
        }

        internal bool HoarderReadyToMigrate => _hoarderScanMaxRadius > 30f && _hoarderItemsLocated == 0 && _hoarderMigrationCooldown <= 0f;

        partial void TickHoarderSystems(float deltaTime)
        {
            if (_hoarderAggroGrace > 0f)
            {
                _hoarderAggroGrace = Mathf.Max(0f, _hoarderAggroGrace - deltaTime);
            }

            if (_hoarderMigrationCooldown > 0f)
            {
                _hoarderMigrationCooldown = Mathf.Max(0f, _hoarderMigrationCooldown - deltaTime);
            }

            if (_hoarderExploredCells.Count > 0)
            {
                _cellScratch.Clear();
                foreach (var pair in _hoarderExploredCells)
                {
                    float remaining = pair.Value - deltaTime;
                    if (remaining <= 0f)
                    {
                        _cellScratch.Add(pair.Key);
                    }
                    else
                    {
                        _hoarderExploredCells[pair.Key] = remaining;
                    }
                }

                for (int i = 0; i < _cellScratch.Count; i++)
                {
                    _hoarderExploredCells.Remove(_cellScratch[i]);
                }
            }
        }

        private readonly List<int> _cellScratch = new List<int>(16);

        private void ResetHoarderSearchSweep()
        {
            _hoarderLayerIndex = 0;
            _hoarderOffsetIndex = 0;
        }

        private bool IsCellFresh(Vector3 position)
        {
            int hash = HashPosition(position);
            return !_hoarderExploredCells.ContainsKey(hash);
        }

        private void MarkCellExplored(Vector3 position)
        {
            int hash = HashPosition(position);
            _hoarderExploredCells[hash] = 120f;
        }

        private static int HashPosition(Vector3 position)
        {
            int x = Mathf.RoundToInt(position.x / 4f);
            int z = Mathf.RoundToInt(position.z / 4f);
            return (x * 73856093) ^ (z * 19349663);
        }

        private HoarderAggroLevel EvaluateHoarderAggro()
        {
            bool playerNearNest = !float.IsPositiveInfinity(LastKnownPlayerPosition.x) && Vector3.Distance(LastKnownPlayerPosition, HoarderNest) < 6f;
            if (PlayerVisible)
            {
                if (DistanceToPlayer <= 3f || (playerNearNest && DistanceToPlayer <= 5f))
                {
                    return HoarderAggroLevel.Fatal;
                }

                if (DistanceToPlayer <= 8f)
                {
                    return HoarderAggroLevel.Hard;
                }

                if (DistanceToPlayer <= 12f)
                {
                    return HoarderAggroLevel.Soft;
                }
            }

            if (_hoarderAggroGrace > 2f)
            {
                return HoarderAggroLevel.Hard;
            }

            if (_hoarderAggroGrace > 0f)
            {
                return HoarderAggroLevel.Soft;
            }

            return HoarderAggroLevel.None;
        }
    }
}
