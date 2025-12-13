using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace AlgoritmaPuncakMod.AI
{
    internal static class MaskedConditions
    {
        internal static bool HasPlayerTrack(BTContext context)
        {
            return !float.IsPositiveInfinity(context.Blackboard.LastKnownPlayerPosition.x);
        }

        internal static bool ConversionWindow(BTContext context)
        {
            return context.Blackboard.MaskedPlayerIsIsolated
                && context.Blackboard.TryGetMaskedIntercept(out _)
                && !context.Blackboard.MaskedIsFrozen;
        }

        internal static bool LureWindow(BTContext context)
        {
            return context.Blackboard.TryGetMaskedLoiter(out _)
                && !context.Blackboard.MaskedIsFrozen;
        }

        internal static bool CanStalk(BTContext context)
        {
            return HasPlayerTrack(context) && !context.Blackboard.MaskedIsFrozen;
        }
    }

    internal static class MaskedActions
    {
        private static readonly FieldInfo[] MimicClipFields = BuildMimicClipFields();
        private static readonly FieldInfo[] VoiceSourceFields = BuildVoiceSourceFields();

        internal static BTStatus ExecuteConversion(BTContext context)
        {
            var board = context.Blackboard;
            if (!board.TryGetMaskedIntercept(out var intercept))
            {
                return BTStatus.Failure;
            }

            float pursuitSpeed = Mathf.Lerp(6.25f, 9.75f, Mathf.Clamp01(board.PlayerVelocity / 7f));
            if (NavigationHelpers.TryMoveAgent(
                context,
                intercept,
                4f,
                48f,
                "MaskedConversion",
                pursuitSpeed,
                acceleration: 18f,
                stoppingDistance: 0.3f))
            {
                float separation = Vector3.Distance(context.Enemy.transform.position, intercept);
                if (separation <= 1.4f || board.DistanceToPlayer <= 2f)
                {
                    board.SetMaskedFreeze(1.1f);
                    board.ClearMaskedIntercept();
                    context.SetActiveAction("MaskedConversionStrike");
                    return BTStatus.Success;
                }

                return BTStatus.Running;
            }

            board.ClearMaskedIntercept();
            return BTStatus.Failure;
        }

        internal static BTStatus PerformLure(BTContext context)
        {
            var board = context.Blackboard;
            if (!board.TryGetMaskedLoiter(out var doorway))
            {
                return BTStatus.Failure;
            }

            float distance = Vector3.Distance(context.Enemy.transform.position, doorway);
            if (distance > 1.1f)
            {
                if (NavigationHelpers.TryMoveAgent(
                    context,
                    doorway,
                    3f,
                    32f,
                    "MaskedLoiterApproach",
                    3.1f,
                    acceleration: 6.5f,
                    stoppingDistance: 0.35f,
                    allowPartialPath: true))
                {
                    return BTStatus.Running;
                }

                board.ClearMaskedLoiter();
                return BTStatus.Failure;
            }

            context.Agent?.ResetPath();
            var focalPoint = board.MaskedPlayerIsIsolated && !float.IsPositiveInfinity(board.LastKnownPlayerPosition.x)
                ? board.LastKnownPlayerPosition
                : doorway + context.Enemy.transform.forward;
            FaceTarget(context, focalPoint);
            EmitVocalCue(context, 2.4f, 3.8f, 0.85f);
            context.SetActiveAction("MaskedLoiterHold");
            return BTStatus.Running;
        }

        internal static BTStatus ExecuteStalk(BTContext context)
        {
            var board = context.Blackboard;
            var target = board.LastKnownPlayerPosition;
            if (float.IsPositiveInfinity(target.x))
            {
                return BTStatus.Failure;
            }

            Vector3 offsetDir = board.PlayerVisible
                ? -board.LastKnownPlayerForward
                : (target - context.Enemy.transform.position).normalized;
            if (offsetDir.sqrMagnitude < 0.01f)
            {
                offsetDir = (target - context.Enemy.transform.position).normalized;
            }

            float leash = Mathf.Lerp(2.25f, 5.5f, Mathf.Clamp01(board.DistanceToPlayer / 14f));
            var destination = target + offsetDir * leash;
            destination += NavigationHelpers.GetAgentOffset(context.Enemy, 1.35f);

            float speed = board.PlayerVisible ? 4.6f : 3.35f;
            if (NavigationHelpers.TryMoveAgent(
                context,
                destination,
                4f,
                60f,
                "MaskedStalk",
                speed,
                acceleration: 8.5f,
                stoppingDistance: 0.45f,
                allowPartialPath: true))
            {
                if (board.DistanceToPlayer <= 3f && board.MaskedReadyToVocalize)
                {
                    float freeze = board.PlayerVisible ? 0.35f : 0.2f;
                    EmitVocalCue(context, 5.5f, 8.5f, freeze);
                }

                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        internal static BTStatus RunMimicryFlow(BTContext context)
        {
            var board = context.Blackboard;
            if (board.PlayerVisible && board.DistanceToPlayer < 8f)
            {
                board.ClearMaskedPatrol();
            }

            bool hasPatrol = board.TryGetMaskedPatrol(out var patrolAnchor, out var patrolFacing);
            if (!hasPatrol && MaskedMimicryPlanner.TryGetAnchor(context.Masked, board, out patrolAnchor, out patrolFacing))
            {
                board.SetMaskedPatrol(patrolAnchor, patrolFacing, UnityEngine.Random.Range(7f, 11f));
                hasPatrol = true;
            }

            Vector3 anchor = patrolAnchor;
            if (!hasPatrol)
            {
                anchor = board.LastKnownPlayerPosition;
                if (float.IsPositiveInfinity(anchor.x))
                {
                    anchor = board.TerritoryCenter != Vector3.zero
                        ? board.TerritoryCenter
                        : context.Enemy.transform.position;
                }
            }

            float orbit = board.PlayerVisible ? 2.4f : 4.6f;
            var orbitVec = new Vector3(Mathf.Cos(Time.time * 0.7f), 0f, Mathf.Sin(Time.time * 0.7f)) * orbit;
            var wander = anchor + orbitVec + NavigationHelpers.GetAgentOffset(context.Enemy, 0.6f);
            wander.y = anchor.y;

            if (NavigationHelpers.TryMoveAgent(
                context,
                wander,
                4f,
                48f,
                "MaskedMimicry",
                hasPatrol ? 3.35f : 2.8f,
                acceleration: hasPatrol ? 6.75f : 5.5f,
                stoppingDistance: 0.35f,
                allowPartialPath: true))
            {
                if (hasPatrol && Vector3.Distance(context.Enemy.transform.position, patrolAnchor) <= 1.2f && !float.IsPositiveInfinity(patrolFacing.x))
                {
                    FaceTarget(context, patrolFacing);
                }

                if (UnityEngine.Random.value < 0.35f)
                {
                    board.SetMaskedFreeze(UnityEngine.Random.Range(0.35f, 0.6f));
                }

                return BTStatus.Running;
            }

            return BTStatus.Failure;
        }

        private static void EmitVocalCue(BTContext context, float minCooldown = 4f, float maxCooldown = 7f, float freezeDuration = 0f)
        {
            if (!context.Blackboard.MaskedReadyToVocalize)
            {
                return;
            }

            var masked = context.Masked;
            var clips = ResolveClipBank(masked);
            var clip = clips != null && clips.Length > 0
                ? clips[UnityEngine.Random.Range(0, clips.Length)]
                : null;
            var voice = ResolveVoiceSource(masked);

            if (clip != null && voice != null)
            {
                voice.pitch = UnityEngine.Random.Range(0.93f, 1.07f);
                voice.PlayOneShot(clip, UnityEngine.Random.Range(0.55f, 0.85f));
            }

            float cooldown = UnityEngine.Random.Range(minCooldown, maxCooldown);
            if (context.Blackboard.MaskedPlayerIsIsolated)
            {
                cooldown *= 0.85f;
            }

            if (!context.Blackboard.PlayerVisible)
            {
                cooldown += 0.4f;
            }

            context.Blackboard.TriggerMaskedVocalCooldown(Mathf.Max(1.5f, cooldown));
            if (freezeDuration > 0f)
            {
                context.Blackboard.SetMaskedFreeze(freezeDuration);
            }
            context.Blackboard.ResetLureTimer();
        }

        private static void FaceTarget(BTContext context, Vector3 target)
        {
            var enemyTransform = context.Enemy.transform;
            var direction = target - enemyTransform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
            {
                return;
            }

            var look = Quaternion.LookRotation(direction.normalized, Vector3.up);
            enemyTransform.rotation = Quaternion.Lerp(enemyTransform.rotation, look, context.DeltaTime * 3.5f);
        }

        private static AudioClip[] ResolveClipBank(MaskedPlayerEnemy masked)
        {
            if (masked == null)
            {
                return null;
            }

            foreach (var field in MimicClipFields)
            {
                if (field == null)
                {
                    continue;
                }

                if (field.GetValue(masked) is AudioClip[] clips && clips.Length > 0)
                {
                    return clips;
                }
            }

            return null;
        }

        private static AudioSource ResolveVoiceSource(EnemyAI enemy)
        {
            if (enemy == null)
            {
                return null;
            }

            foreach (var field in VoiceSourceFields)
            {
                if (field == null)
                {
                    continue;
                }

                if (field.GetValue(enemy) is AudioSource voice && voice != null)
                {
                    return voice;
                }
            }

            return enemy.GetComponent<AudioSource>() ?? enemy.GetComponentInChildren<AudioSource>();
        }

        private static FieldInfo[] BuildMimicClipFields()
        {
            var names = new[] { "mimicVoiceClips", "mimicVoices", "mimicPlayerVoices" };
            var fields = new List<FieldInfo>();
            foreach (var name in names)
            {
                var field = AccessTools.Field(typeof(MaskedPlayerEnemy), name);
                if (field != null)
                {
                    fields.Add(field);
                }
            }

            return fields.ToArray();
        }

        private static FieldInfo[] BuildVoiceSourceFields()
        {
            var names = new[] { "creatureVoice", "enemyVoice", "creatureVoiceAudio" };
            var fields = new List<FieldInfo>();
            foreach (var name in names)
            {
                var field = AccessTools.Field(typeof(EnemyAI), name);
                if (field != null)
                {
                    fields.Add(field);
                }
            }

            return fields.ToArray();
        }
    }

    internal static partial class BehaviorTreeFactory
    {
        private static BTNode CreateMaskedTree()
        {
            return new BTPrioritySelector("MaskedRoot",
                BuildMaskedConversionBranch(),
                BuildMaskedLureBranch(),
                BuildMaskedStalkBranch(),
                new BTActionNode("MaskedMimicry", MaskedActions.RunMimicryFlow));
        }

        private static BTNode BuildMaskedConversionBranch()
        {
            return new BTSequence("MaskedConversion",
                new BTConditionNode("MaskedConversionReady", MaskedConditions.ConversionWindow),
                new BTActionNode("MaskedConversionAction", MaskedActions.ExecuteConversion));
        }

        private static BTNode BuildMaskedLureBranch()
        {
            return new BTSequence("MaskedLure",
                new BTConditionNode("MaskedHasLoiter", MaskedConditions.LureWindow),
                new BTActionNode("MaskedLureAction", MaskedActions.PerformLure));
        }

        private static BTNode BuildMaskedStalkBranch()
        {
            return new BTSequence("MaskedStalk",
                new BTConditionNode("MaskedHasTrack", MaskedConditions.CanStalk),
                new BTActionNode("MaskedStalkAction", MaskedActions.ExecuteStalk));
        }
    }
}
