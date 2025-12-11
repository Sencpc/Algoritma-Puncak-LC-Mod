using System.Collections.Generic;
using UnityEngine;

namespace AlgoritmaPuncakMod.AI
{
    internal static class AIBehaviorCoordinator
    {
        private static readonly Dictionary<int, AIBehaviorController> Controllers = new Dictionary<int, AIBehaviorController>();
        private static float _defaultTerritoryRadius = 12f;

        internal static void Initialize(float defaultTerritoryRadius)
        {
            _defaultTerritoryRadius = Mathf.Max(1f, defaultTerritoryRadius);
            Controllers.Clear();
        }

        internal static void Update(EnemyAI enemy)
        {
            if (enemy == null) return;

            if (!Controllers.TryGetValue(enemy.GetInstanceID(), out var controller))
            {
                controller = new AIBehaviorController(enemy, _defaultTerritoryRadius);
                Controllers.Add(enemy.GetInstanceID(), controller);
            }

            controller.Tick(Time.deltaTime);
        }

        internal static void Reset()
        {
            Controllers.Clear();
        }
    }
}
