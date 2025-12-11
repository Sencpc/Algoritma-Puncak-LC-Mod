using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace AlgoritmaPuncakMod.AI
{
    internal sealed partial class AIBlackboard
    {
        private readonly List<SpiderChokePoint> _spiderChokePoints = new List<SpiderChokePoint>();
        private Vector3 _spiderAnchor = Vector3.positiveInfinity;
        private bool _spiderFortificationInitialized;
        private float _spiderPerimeterRadius;
        private Vector3 _spiderHideSpot = Vector3.positiveInfinity;
        private float _spiderHideRefresh;
        private float _spiderAngerTimer;
        private Vector3 _spiderAlertPosition = Vector3.positiveInfinity;
        private float _spiderAlertTimer;
        private float _spiderSearchTimer;
        private Vector3 _spiderFortifyTarget = Vector3.positiveInfinity;
        private int _spiderActivePointId = -1;
        private int _spiderNextPointId;

        internal bool SpiderAlertActive => _spiderFortificationInitialized && _spiderAlertTimer > 0f;
        internal bool HasSpiderFortifyCandidate => _spiderFortificationInitialized && (!float.IsPositiveInfinity(_spiderFortifyTarget.x) || TrySelectSpiderFortificationTarget());
        internal Vector3 SpiderFortifyTarget => _spiderFortifyTarget;
        internal float SpiderSearchTimer => _spiderSearchTimer;
        internal float SpiderFortificationRadius => _spiderPerimeterRadius;
        internal IReadOnlyList<SpiderChokePoint> SpiderChokePoints => _spiderChokePoints;
        internal bool SpiderEnraged => _spiderAngerTimer > 0f;
        internal float SpiderAggression => Mathf.Clamp01(_spiderAngerTimer / 20f);

        internal void EnsureSpiderFortification(Vector3 center)
        {
            if (_spiderFortificationInitialized)
            {
                return;
            }

            _spiderFortificationInitialized = true;
            _spiderAnchor = center;
            _spiderPerimeterRadius = Mathf.Max(6f, TerritoryRadius > 0f ? TerritoryRadius * 0.75f : 8f);
            _spiderHideSpot = center;
            BuildSpiderRing(_spiderPerimeterRadius);
        }

        internal bool TryGetSpiderAlertPosition(out Vector3 position)
        {
            position = _spiderAlertPosition;
            return SpiderAlertActive && !float.IsPositiveInfinity(_spiderAlertPosition.x);
        }

        internal void NotifySpiderWebDisturbance(Vector3 position, bool urgent)
        {
            if (!_spiderFortificationInitialized)
            {
                return;
            }

            _spiderAlertPosition = position;
            _spiderAlertTimer = urgent ? 15f : Mathf.Max(_spiderAlertTimer, 10f);
            TriggerSpiderAnger(urgent ? 20f : 12f);
        }

        internal void RegisterSpiderWeb(Vector3 position)
        {
            var point = FindChokePoint(position, 4f);
            if (point == null)
            {
                return;
            }

            point.HasWeb = true;
            point.TimeSinceServiced = 0f;
        }

        internal void HandleSpiderWebRemoved(Vector3 position)
        {
            var point = FindChokePoint(position, 4f);
            if (point != null)
            {
                point.HasWeb = false;
                point.TimeSinceServiced = 0f;
                _spiderFortifyTarget = point.Position;
                _spiderActivePointId = point.Id;
            }

            NotifySpiderWebDisturbance(position, true);
        }

        internal void MarkSpiderFortificationServiced()
        {
            if (_spiderActivePointId == -1)
            {
                return;
            }

            for (int i = 0; i < _spiderChokePoints.Count; i++)
            {
                if (_spiderChokePoints[i].Id != _spiderActivePointId)
                {
                    continue;
                }

                _spiderChokePoints[i].HasWeb = true;
                _spiderChokePoints[i].TimeSinceServiced = 0f;
                break;
            }

            _spiderActivePointId = -1;
            _spiderFortifyTarget = Vector3.positiveInfinity;
        }

        internal void ClearSpiderFortifyTarget()
        {
            _spiderActivePointId = -1;
            _spiderFortifyTarget = Vector3.positiveInfinity;
        }

        internal void ClearSpiderAlert()
        {
            _spiderAlertTimer = 0f;
            _spiderAlertPosition = Vector3.positiveInfinity;
        }

        internal void BeginSpiderSearch(float duration)
        {
            _spiderSearchTimer = Mathf.Max(_spiderSearchTimer, duration);
            TriggerSpiderAnger(duration * 0.75f);
        }

        internal void ClearSpiderSearch()
        {
            _spiderSearchTimer = 0f;
        }

        internal Vector3 GetSpiderHideSpot()
        {
            if (!_spiderFortificationInitialized)
            {
                return TerritoryCenter;
            }

            if (_spiderHideRefresh <= 0f || float.IsPositiveInfinity(_spiderHideSpot.x))
            {
                var offset = UnityEngine.Random.insideUnitSphere * Mathf.Max(2f, _spiderPerimeterRadius * 0.25f);
                offset.y = 0f;
                var guess = _spiderAnchor + offset;
                if (!NavMesh.SamplePosition(guess, out var hit, 4f, NavMesh.AllAreas))
                {
                    hit.position = _spiderAnchor;
                }

                _spiderHideSpot = hit.position;
                _spiderHideRefresh = 5f + UnityEngine.Random.Range(0f, 5f);
            }

            return _spiderHideSpot;
        }

        partial void TickSpiderSystems(float deltaTime)
        {
            _spiderSearchTimer = Mathf.Max(0f, _spiderSearchTimer - deltaTime);
            if (!_spiderFortificationInitialized)
            {
                return;
            }

            if (_spiderAlertTimer > 0f)
            {
                _spiderAlertTimer = Mathf.Max(0f, _spiderAlertTimer - deltaTime);
                if (_spiderAlertTimer == 0f)
                {
                    _spiderAlertPosition = Vector3.positiveInfinity;
                }
            }

            if (_spiderAngerTimer > 0f)
            {
                _spiderAngerTimer = Mathf.Max(0f, _spiderAngerTimer - deltaTime);
            }

            if (_spiderHideRefresh > 0f)
            {
                _spiderHideRefresh -= deltaTime;
            }

            for (int i = 0; i < _spiderChokePoints.Count; i++)
            {
                _spiderChokePoints[i].TimeSinceServiced += deltaTime;
            }

            if (_spiderPerimeterRadius < TerritoryRadius + 10f && RingSecured(_spiderPerimeterRadius))
            {
                _spiderPerimeterRadius = Mathf.Min(TerritoryRadius + 10f, _spiderPerimeterRadius + 5f);
                BuildSpiderRing(_spiderPerimeterRadius);
            }
        }

        private bool TrySelectSpiderFortificationTarget()
        {
            SpiderChokePoint candidate = null;
            float bestScore = float.PositiveInfinity;

            for (int i = 0; i < _spiderChokePoints.Count; i++)
            {
                var point = _spiderChokePoints[i];
                if (point.RingRadius > _spiderPerimeterRadius + 0.5f)
                {
                    continue;
                }

                float score = point.HasWeb ? point.TimeSinceServiced : -100f - point.TimeSinceServiced;
                if (score < bestScore)
                {
                    bestScore = score;
                    candidate = point;
                }
            }

            if (candidate == null)
            {
                return false;
            }

            _spiderFortifyTarget = candidate.Position;
            _spiderActivePointId = candidate.Id;
            return true;
        }

        private void BuildSpiderRing(float radius)
        {
            if (!_spiderFortificationInitialized)
            {
                return;
            }

            int segments = Mathf.Clamp(Mathf.RoundToInt(radius), 6, 14);
            float increment = 360f / segments;
            for (int i = 0; i < segments; i++)
            {
                float angle = (increment * i + UnityEngine.Random.Range(-6f, 6f)) * Mathf.Deg2Rad;
                var direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                var guess = _spiderAnchor + direction * radius;
                if (!NavMesh.SamplePosition(guess, out var hit, 3.5f, NavMesh.AllAreas))
                {
                    continue;
                }

                _spiderChokePoints.Add(new SpiderChokePoint
                {
                    Id = _spiderNextPointId++,
                    Position = hit.position,
                    RingRadius = radius,
                    HasWeb = false,
                    TimeSinceServiced = 0f
                });
            }
        }

        private bool RingSecured(float radius)
        {
            for (int i = 0; i < _spiderChokePoints.Count; i++)
            {
                if (_spiderChokePoints[i].RingRadius > radius + 0.25f)
                {
                    continue;
                }

                if (!_spiderChokePoints[i].HasWeb)
                {
                    return false;
                }
            }

            return _spiderChokePoints.Count > 0;
        }

        private SpiderChokePoint FindChokePoint(Vector3 position, float maxDistance)
        {
            SpiderChokePoint closest = null;
            float best = maxDistance;

            for (int i = 0; i < _spiderChokePoints.Count; i++)
            {
                float distance = Vector3.Distance(position, _spiderChokePoints[i].Position);
                if (distance > best)
                {
                    continue;
                }

                best = distance;
                closest = _spiderChokePoints[i];
            }

            return closest;
        }

        internal void TriggerSpiderAnger(float durationSeconds)
        {
            _spiderAngerTimer = Mathf.Max(_spiderAngerTimer, durationSeconds);
        }

        internal void CoolSpiderAnger(float durationReduction)
        {
            if (durationReduction <= 0f)
            {
                return;
            }

            _spiderAngerTimer = Mathf.Max(0f, _spiderAngerTimer - durationReduction);
        }

        internal sealed class SpiderChokePoint
        {
            internal int Id;
            internal Vector3 Position;
            internal float RingRadius;
            internal bool HasWeb;
            internal float TimeSinceServiced;
        }
    }
}
