using System;
using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace AlgoritmaPuncakMod.AI
{
    internal static partial class BehaviorConditions
    {
        internal static bool SpiderAlertActive(BTContext context)
        {
            return context.Spider != null && context.Blackboard.SpiderAlertActive;
        }

        internal static bool SpiderNeedsFortification(BTContext context)
        {
            return context.Spider != null && context.Blackboard.HasSpiderFortifyCandidate;
        }

        internal static bool SpiderSeesIntruder(BTContext context)
        {
            if (context.Spider == null)
            {
                return false;
            }

            return context.Blackboard.PlayerVisible && context.Blackboard.PlayerInsideTerritory(context.Enemy.transform.position);
        }

        internal static bool SpiderPlayerInsideTerritory(BTContext context)
        {
            if (context.Spider == null)
            {
                return false;
            }

            return context.Blackboard.PlayerVisible && context.Blackboard.PlayerInsideTerritory(context.Enemy.transform.position);
        }

        internal static bool SpiderEnraged(BTContext context)
        {
            return context.Spider != null && context.Blackboard.SpiderEnraged;
        }
    }

    internal static class SandSpiderActions
    {
        private static readonly MethodInfo AttemptPlaceWebTrap = AccessTools.Method(typeof(SandSpiderAI), "AttemptPlaceWebTrap");

        internal static BTStatus Enrage(BTContext context)
        {
            context.Blackboard.TriggerSpiderAnger(15f);
            context.SetActiveAction("SpiderAngered");
            return BTStatus.Success;
        }

        internal static BTStatus MoveToAlert(BTContext context)
        {
            if (context.Spider == null || !context.Blackboard.TryGetSpiderAlertPosition(out var target))
            {
                return BTStatus.Failure;
            }

            if (Vector3.Distance(context.Enemy.transform.position, target) < 1.2f)
            {
                context.SetActiveAction("SpiderInvestigate");
                return BTStatus.Success;
            }

            if (NavigationHelpers.TryMoveAgent(
                context,
                target,
                4f,
                context.Blackboard.SpiderFortificationRadius + 6f,
                "SpiderInvestigate",
                4.75f,
                acceleration: 9f,
                stoppingDistance: 0.45f))
            {
                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        internal static BTStatus SearchAlert(BTContext context)
        {
            if (context.Spider == null || !context.Blackboard.TryGetSpiderAlertPosition(out var anchor))
            {
                return BTStatus.Failure;
            }

            if (context.Blackboard.SpiderSearchTimer <= 0f)
            {
                context.Blackboard.BeginSpiderSearch(10f);
            }

            if (context.Blackboard.SpiderSearchTimer <= 0.15f)
            {
                context.Blackboard.ClearSpiderSearch();
                context.Blackboard.ClearSpiderAlert();
                return BTStatus.Success;
            }

            if (context.Agent != null && context.Agent.remainingDistance <= 0.8f)
            {
                var offset2D = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(0.35f, 2.4f);
                var wander = anchor + new Vector3(offset2D.x, 0f, offset2D.y);
                NavigationHelpers.TryMoveAgent(
                    context,
                    wander,
                    2.5f,
                    12f,
                    "SpiderSearch",
                    2.8f,
                    acceleration: 5f,
                    stoppingDistance: 0.4f,
                    allowPartialPath: true);
            }

            context.SetActiveAction("SpiderSearch");
            return BTStatus.Running;
        }

        internal static BTStatus MoveToFortification(BTContext context)
        {
            if (context.Spider == null || !context.Blackboard.HasSpiderFortifyCandidate)
            {
                return BTStatus.Failure;
            }

            var target = context.Blackboard.SpiderFortifyTarget;
            if (float.IsPositiveInfinity(target.x))
            {
                return BTStatus.Failure;
            }

            if (Vector3.Distance(context.Enemy.transform.position, target) < 1.05f)
            {
                context.SetActiveAction("SpiderFortifyPosition");
                return BTStatus.Success;
            }

            if (NavigationHelpers.TryMoveAgent(
                context,
                target,
                3.5f,
                context.Blackboard.SpiderFortificationRadius + 6f,
                "SpiderFortify",
                3.85f,
                acceleration: 8.5f,
                stoppingDistance: 0.55f))
            {
                return BTStatus.Running;
            }

            context.Blackboard.ClearSpiderFortifyTarget();
            return BTStatus.Failure;
        }

        internal static BTStatus PlaceReplacementWeb(BTContext context)
        {
            if (context.Spider == null || AttemptPlaceWebTrap == null)
            {
                return BTStatus.Failure;
            }

            bool placed = false;
            try
            {
                placed = (bool)AttemptPlaceWebTrap.Invoke(context.Spider, Array.Empty<object>());
            }
            catch (Exception ex)
            {
                Debug.LogError(string.Format("SandSpiderActions.PlaceReplacementWeb failed: {0}", ex));
            }

            if (placed)
            {
                context.Blackboard.MarkSpiderFortificationServiced();
                context.Blackboard.ClearSpiderSearch();
                context.SetActiveAction("SpiderWebWeave");
                return BTStatus.Success;
            }

            return BTStatus.Running;
        }

        internal static BTStatus RushIntruder(BTContext context)
        {
            if (!context.Blackboard.PlayerVisible)
            {
                return BTStatus.Failure;
            }

            var playerPos = context.Blackboard.LastKnownPlayerPosition;
            if (float.IsPositiveInfinity(playerPos.x))
            {
                return BTStatus.Failure;
            }

            var enemyPos = context.Enemy.transform.position;
            var direction = (playerPos - enemyPos).normalized;
            var cutoff = playerPos - direction * 0.75f;
            cutoff += NavigationHelpers.GetAgentOffset(context.Enemy, 1.5f);

            var aggression = Mathf.Lerp(0.85f, 1.35f, context.Blackboard.SpiderAggression);
            if (NavigationHelpers.TryMoveAgent(
                context,
                cutoff,
                4f,
                context.Profile.TerritoryRadius * 1.25f,
                "SpiderChase",
                6f * aggression,
                acceleration: Mathf.Lerp(10f, 16f, context.Blackboard.SpiderAggression),
                stoppingDistance: 0.35f))
            {
                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        internal static BTStatus AttemptStrike(BTContext context)
        {
            if (!context.Blackboard.PlayerVisible)
            {
                context.SetActiveAction("SpiderStrikeLost");
                return BTStatus.Failure;
            }

            var playerPos = context.Blackboard.LastKnownPlayerPosition;
            if (float.IsPositiveInfinity(playerPos.x))
            {
                return BTStatus.Failure;
            }

            float distance = Vector3.Distance(context.Enemy.transform.position, playerPos);
            if (distance <= 1.75f)
            {
                context.SetActiveAction("SpiderAttack");
                context.Blackboard.TriggerSpiderAnger(8f);
                return BTStatus.Success;
            }

            context.SetActiveAction("SpiderPress");
            return BTStatus.Running;
        }

        internal static BTStatus ReturnToHide(BTContext context)
        {
            var hideSpot = context.Blackboard.GetSpiderHideSpot();
            if (Vector3.Distance(context.Enemy.transform.position, hideSpot) < 0.9f)
            {
                context.Blackboard.ClearSpiderFortifyTarget();
                context.Blackboard.CoolSpiderAnger(2f);
                context.SetActiveAction("SpiderHide");
                return BTStatus.Success;
            }

            if (NavigationHelpers.TryMoveAgent(
                context,
                hideSpot,
                3f,
                context.Blackboard.SpiderFortificationRadius + 4f,
                "SpiderHide",
                3.1f,
                acceleration: 6f,
                stoppingDistance: 0.6f,
                allowPartialPath: true))
            {
                return BTStatus.Running;
            }

            context.Blackboard.ClearSpiderFortifyTarget();
            context.Blackboard.CoolSpiderAnger(1.5f);
            return BTStatus.Failure;
        }

        internal static BTStatus IdleAroundNest(BTContext context)
        {
            var anchor = context.Blackboard.GetSpiderHideSpot();
            var offset = NavigationHelpers.GetAgentOffset(context.Enemy, 2.5f);
            var wander = anchor + offset + UnityEngine.Random.insideUnitSphere * 0.6f;
            wander.y = anchor.y;

            if (NavigationHelpers.TryMoveAgent(
                context,
                wander,
                2.5f,
                context.Blackboard.SpiderFortificationRadius + 3f,
                "SpiderIdle",
                2.2f,
                acceleration: 4.5f,
                stoppingDistance: 0.5f,
                allowPartialPath: true))
            {
                context.Blackboard.CoolSpiderAnger(0.5f);
                return BTStatus.Running;
            }

            context.Blackboard.CoolSpiderAnger(0.5f);
            return BTStatus.Failure;
        }
    }

    internal static partial class BehaviorTreeFactory
    {
        private static BTNode CreateSandSpiderTree()
        {
            return new BTPrioritySelector("SpiderRoot",
                BuildSpiderWebResponseBranch(),
                BuildSpiderChaseBranch(),
                BuildSpiderFortifyBranch(),
                new BTActionNode("SpiderIdle", SandSpiderActions.IdleAroundNest));
        }

        private static BTNode BuildSpiderWebResponseBranch()
        {
            return new BTSequence("SpiderWebResponse",
                new BTConditionNode("SpiderAlert", BehaviorConditions.SpiderAlertActive),
                new BTActionNode("SpiderSetAnger", SandSpiderActions.Enrage),
                new BTActionNode("MoveToAlert", SandSpiderActions.MoveToAlert),
                new BTActionNode("SearchAlert", SandSpiderActions.SearchAlert),
                new BTActionNode("ReplaceWeb", SandSpiderActions.PlaceReplacementWeb));
        }

        private static BTNode BuildSpiderChaseBranch()
        {
            return new BTSequence("SpiderChase",
                new BTConditionNode("SpiderTerritoryEngage", BehaviorConditions.SpiderPlayerInsideTerritory),
                new BTActionNode("SpiderRush", SandSpiderActions.RushIntruder),
                new BTActionNode("SpiderStrike", SandSpiderActions.AttemptStrike));
        }

        private static BTNode BuildSpiderFortifyBranch()
        {
            return new BTSequence("SpiderFortify",
                new BTConditionNode("SpiderNeedsFortify", BehaviorConditions.SpiderNeedsFortification),
                new BTActionNode("MoveToChokePoint", SandSpiderActions.MoveToFortification),
                new BTActionNode("SpiderWeave", SandSpiderActions.PlaceReplacementWeb),
                new BTActionNode("SpiderRetreat", SandSpiderActions.ReturnToHide));
        }
    }
}
