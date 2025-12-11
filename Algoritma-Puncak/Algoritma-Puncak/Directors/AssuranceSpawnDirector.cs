using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace AlgoritmaPuncakMod.Directors
{
    internal static class AssuranceSpawnDirector
    {
        private const string LogPrefix = "[AlgoritmaPuncak]";
        private const string TargetMoonName = "Assurance";
        private const string TargetMoonId = "220";
        private const int IndoorMin = 5;
        private const int IndoorMax = 7;
        private const int OutdoorMin = 3;
        private const int OutdoorMax = 5;

        private static readonly Dictionary<string, int> InsideWeights = BuildInsideMap();
        private static readonly Dictionary<string, int> OutsideWeights = BuildOutsideMap();

        private static object _lastLevel;
        private static bool _applied;
        private static string _lastLevelLabel;

        internal static void Tick()
        {
            try
            {
                var round = RoundManager.Instance;
                if (round == null)
                {
                    Reset("RoundManager missing");
                    return;
                }

                var level = round.currentLevel;
                if (level == null)
                {
                    Reset("No current level");
                    return;
                }

                if (!IsAssurance(level))
                {
                    Reset("Non-Assurance level active");
                    return;
                }

                if (!ReferenceEquals(level, _lastLevel))
                {
                    _lastLevel = level;
                    _lastLevelLabel = DescribeLevel(level);
                    LogInfo($"Assurance detected ({_lastLevelLabel}). Applying spawn policy...");
                    _applied = false;
                }

                if (_applied)
                {
                    return;
                }

                if (Apply(level, out int insideMatches, out int outsideMatches))
                {
                    _applied = true;
                    LogInfo($"Assurance spawn caps set: inside {IndoorMin}-{IndoorMax}, outside {OutdoorMin}-{OutdoorMax}, matched weights (inside={insideMatches}, outside={outsideMatches}).");
                }
            }
            catch (Exception ex)
            {
                LogError($"AssuranceSpawnDirector failure: {ex}");
            }
        }

        internal static void Reset(string reason = null)
        {
            bool hadState = _applied || _lastLevel != null;
            _applied = false;
            _lastLevel = null;
            _lastLevelLabel = null;
            if (hadState && !string.IsNullOrWhiteSpace(reason))
            {
                LogInfo($"AssuranceSpawnDirector reset: {reason}.");
            }
        }

        private static bool Apply(object level, out int insideMatches, out int outsideMatches)
        {
            bool changed = false;
            changed |= SetIntRange(level, "enemyAmount", IndoorMin, IndoorMax);
            changed |= SetIntRange(level, "daytimeEnemyAmount", IndoorMin, IndoorMax);
            changed |= SetIntRange(level, "outsideEnemyAmount", OutdoorMin, OutdoorMax);
            changed |= SetIntRange(level, "daytimeOutsideEnemyAmount", OutdoorMin, OutdoorMax);
            changed |= ApplyWeights(level, "Enemies", InsideWeights, out insideMatches);
            if (insideMatches == 0)
            {
                LogWarning("Assurance inside spawn list had no matching aliases; all entries disabled.");
            }

            changed |= ApplyWeights(level, "OutsideEnemies", OutsideWeights, out outsideMatches);
            if (outsideMatches == 0)
            {
                LogWarning("Assurance outside spawn list had no matching aliases; all entries disabled.");
            }

            return changed;
        }

        private static bool IsAssurance(object level)
        {
            string planet = ReadString(level, "PlanetName") ?? ReadString(level, "planetName");
            string levelName = ReadString(level, "levelName");
            string sceneName = ReadString(level, "sceneName");

            return MatchLabel(planet) || MatchLabel(levelName) || MatchLabel(sceneName);
        }

        private static bool MatchLabel(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return value.IndexOf(TargetMoonName, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf(TargetMoonId, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ReadString(object target, string memberName)
        {
            if (target == null)
            {
                return null;
            }

            var type = target.GetType();
            var field = AccessTools.Field(type, memberName);
            if (field != null)
            {
                return field.GetValue(target) as string;
            }

            var property = AccessTools.Property(type, memberName);
            if (property?.GetGetMethod(true) != null)
            {
                return property.GetValue(target) as string;
            }

            return null;
        }

        private static bool SetIntRange(object target, string fieldName, int min, int max)
        {
            if (target == null)
            {
                return false;
            }

            var type = target.GetType();
            var rangeField = AccessTools.Field(type, fieldName);
            if (rangeField == null)
            {
                return false;
            }

            var rangeValue = rangeField.GetValue(target) ?? Activator.CreateInstance(rangeField.FieldType);
            if (rangeValue == null)
            {
                return false;
            }

            var minField = AccessTools.Field(rangeField.FieldType, "min");
            var maxField = AccessTools.Field(rangeField.FieldType, "max");
            if (minField == null || maxField == null)
            {
                return false;
            }

            minField.SetValue(rangeValue, min);
            maxField.SetValue(rangeValue, max);
            rangeField.SetValue(target, rangeValue);
            return true;
        }

        private static bool ApplyWeights(object target, string listFieldName, IReadOnlyDictionary<string, int> weights, out int matchedEntries)
        {
            matchedEntries = 0;
            if (target == null)
            {
                return false;
            }

            var listField = AccessTools.Field(target.GetType(), listFieldName);
            if (listField == null)
            {
                return false;
            }

            if (!(listField.GetValue(target) is IList list) || list.Count == 0)
            {
                return false;
            }

            var spawnableType = AccessTools.TypeByName("SpawnableEnemyWithRarity");
            if (spawnableType == null)
            {
                return false;
            }

            var enemyTypeField = AccessTools.Field(spawnableType, "enemyType");
            var rarityField = AccessTools.Field(spawnableType, "rarity");
            if (enemyTypeField == null || rarityField == null)
            {
                return false;
            }

            bool applied = false;
            for (int i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                if (entry == null)
                {
                    continue;
                }

                var enemyType = enemyTypeField.GetValue(entry);
                var resolvedName = ResolveEnemyName(enemyType);
                if (!string.IsNullOrEmpty(resolvedName) && weights.TryGetValue(resolvedName, out int weight))
                {
                    rarityField.SetValue(entry, Math.Max(1, weight));
                    applied = true;
                    matchedEntries++;
                }
                else
                {
                    rarityField.SetValue(entry, 0);
                }
            }

            return applied;
        }

        private static string ResolveEnemyName(object enemyType)
        {
            if (enemyType == null)
            {
                return string.Empty;
            }

            var type = enemyType.GetType();
            var nameField = AccessTools.Field(type, "enemyName");
            if (nameField != null && nameField.GetValue(enemyType) is string name && !string.IsNullOrWhiteSpace(name))
            {
                return name;
            }

            var prefabField = AccessTools.Field(type, "enemyPrefab");
            if (prefabField != null && prefabField.GetValue(enemyType) is GameObject prefab && prefab != null)
            {
                return prefab.name;
            }

            return type.Name;
        }

        private static string DescribeLevel(object level)
        {
            if (level == null)
            {
                return "unknown";
            }

            var parts = new List<string>(4);
            string planet = ReadString(level, "PlanetName") ?? ReadString(level, "planetName");
            string levelName = ReadString(level, "levelName");
            string scene = ReadString(level, "sceneName");
            if (!string.IsNullOrWhiteSpace(planet))
            {
                parts.Add($"planet={planet}");
            }

            if (!string.IsNullOrWhiteSpace(levelName))
            {
                parts.Add($"level={levelName}");
            }

            if (!string.IsNullOrWhiteSpace(scene))
            {
                parts.Add($"scene={scene}");
            }

            return parts.Count > 0 ? string.Join(", ", parts) : level.GetType().Name;
        }

        private static Dictionary<string, int> BuildInsideMap()
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Register(map, 15, "Bunker Spider", "BunkerSpider");
            Register(map, 20, "Thumper");
            Register(map, 15, "Hoarding Bug", "HoardingBug", "Hoarder Bug", "HoarderBug");
            Register(map, 20, "Flowerman", "Bracken");
            Register(map, 20, "Baboon Hawk", "BaboonHawk", "Baboon Bird", "BaboonBird");
            Register(map, 10, "Sand Spider", "SandSpider");
            Register(map, 10, "Coil-Head", "Coil Head", "CoilHead", "Spring Man", "SpringMan");
            return map;
        }

        private static Dictionary<string, int> BuildOutsideMap()
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Register(map, 50, "Eyeless Dog", "EyelessDog", "MouthDog", "Mouth Dog");
            Register(map, 35, "Baboon Hawk", "BaboonHawk", "Baboon Bird", "BaboonBird");
            Register(map, 15, "Sand Spider", "SandSpider");
            Register(map, 15, "Hoarding Bug", "HoardingBug", "Hoarder Bug", "HoarderBug");
            return map;
        }

        private static void Register(Dictionary<string, int> map, int weight, params string[] aliases)
        {
            if (aliases == null)
            {
                return;
            }

            for (int i = 0; i < aliases.Length; i++)
            {
                var alias = aliases[i];
                if (string.IsNullOrWhiteSpace(alias))
                {
                    continue;
                }

                map[alias] = weight;
            }
        }

        private static void LogInfo(string message)
        {
            AlgoritmaPuncakMod.Log?.LogInfo($"{LogPrefix} {message}");
        }

        private static void LogWarning(string message)
        {
            AlgoritmaPuncakMod.Log?.LogWarning($"{LogPrefix} {message}");
        }

        private static void LogError(string message)
        {
            AlgoritmaPuncakMod.Log?.LogError($"{LogPrefix} {message}");
        }
    }
}
