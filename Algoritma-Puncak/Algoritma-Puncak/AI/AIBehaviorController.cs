using UnityEngine;
using UnityEngine.AI;

namespace AlgoritmaPuncakMod.AI
{
    internal sealed class AIBehaviorController
    {
        private readonly EnemyAI _enemy;
        private readonly NavMeshAgent _agent;
        private readonly AIBlackboard _blackboard = new AIBlackboard();
        private readonly AISensorSuite _sensors = new AISensorSuite();
        private readonly BTContext _context;
        private readonly BTNode _behaviorTree;
        private string _lastActionName;

        internal AIBehaviorController(EnemyAI enemy, float defaultTerritoryRadius)
        {
            _enemy = enemy;
            _agent = enemy.GetComponent<NavMeshAgent>();
            _blackboard.InitializeTerritory(enemy.transform.position, defaultTerritoryRadius);
            _context = new BTContext(_enemy, _agent, _blackboard);
            _behaviorTree = BehaviorTreeFactory.CreateTree(enemy);
        }

        internal void Tick(float deltaTime)
        {
            if (_enemy == null)
            {
                return;
            }

            var profile = AlgoritmaPuncakMod.BalanceProfile;
            _blackboard.TickTimers(deltaTime);
            _sensors.Scan(_enemy, _agent, _blackboard, profile, deltaTime);

            _context.Update(profile, deltaTime);
            _behaviorTree.Tick(_context);
            LogActionChange(_context.ActiveAction);
        }

        private void LogActionChange(string currentAction)
        {
            if (string.IsNullOrEmpty(currentAction) || currentAction == _lastActionName)
            {
                return;
            }

            _lastActionName = currentAction;
            var logger = AlgoritmaPuncakMod.Log;
            if (logger != null)
            {
                logger.LogDebug(string.Format("[{0}] BT action -> {1}", _enemy.name, currentAction));
            }
        }

    }

}