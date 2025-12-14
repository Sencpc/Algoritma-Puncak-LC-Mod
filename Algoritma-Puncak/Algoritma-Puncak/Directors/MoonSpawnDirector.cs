using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace AlgoritmaPuncakMod.Directors
{
    internal static class MoonSpawnDirector
    {
        private const string LogPrefix = "[AlgoritmaPuncak]";

        private static readonly Dictionary<string, int> DefaultInsideWeights = BuildDefaultInsideMap();
        private static readonly Dictionary<string, int> DefaultOutsideWeights = BuildDefaultOutsideMap();
        private static readonly List<MoonSpawnPolicy> Policies = BuildPolicies();
        private static readonly SpawnCapProfile BeginnerSafeFactoryCaps = new SpawnCapProfile(3, 4, 1, 2);
        private static readonly SpawnCapProfile BeginnerRuggedFactoryCaps = new SpawnCapProfile(4, 6, 2, 3);
        private static readonly SpawnCapProfile BeginnerForestCaps = new SpawnCapProfile(3, 5, 2, 4);
        private static readonly SpawnCapProfile IntermediateFactoryCaps = new SpawnCapProfile(5, 7, 3, 4);
        private static readonly SpawnCapProfile FloodedFactoryCaps = new SpawnCapProfile(6, 8, 3, 5);
        private static readonly SpawnCapProfile DenseFactoryCaps = new SpawnCapProfile(6, 8, 4, 6);
        private static readonly SpawnCapProfile MansionBlizzardCaps = new SpawnCapProfile(7, 9, 4, 6);
        private static readonly SpawnCapProfile MansionSiegeCaps = new SpawnCapProfile(8, 10, 4, 6);
        private static readonly SpawnCapProfile ExtremeFactoryCaps = new SpawnCapProfile(9, 11, 5, 7);
        private static readonly SpawnCapProfile WarehouseChaosCaps = new SpawnCapProfile(9, 11, 6, 7);
        private static readonly SpawnCapProfile TrapMoonCaps = new SpawnCapProfile(8, 10, 6, 8);
        private static readonly SpawnCapProfile GeneralUnknownCaps = new SpawnCapProfile(5, 7, 3, 5);
        private static readonly MoonSpawnPolicy GeneralFallbackPolicy = BuildGeneralFallbackPolicy();
        private static readonly string[] FacilitySizeMemberNames =
        {
            "factorySizeMultiplier",
            "FactorySizeMultiplier",
            "factorySizeModifier",
            "FactorySizeModifier",
            "factorySize",
            "FactorySize",
            "facilitySize",
            "FacilitySize",
            "dungeonSize",
            "DungeonSize",
            "mapSize",
            "MapSize",
            "sizeMultiplier",
            "SizeMultiplier"
        };

        private static object _lastLevel;
        private static MoonSpawnPolicy _activePolicy;
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

                var policy = FindPolicy(level) ?? GeneralFallbackPolicy;

                if (!ReferenceEquals(level, _lastLevel) || policy != _activePolicy)
                {
                    _lastLevel = level;
                    _activePolicy = policy;
                    _lastLevelLabel = DescribeLevel(level);
                    _applied = false;
                    LogInfo($"{policy.Name} detected ({policy.Difficulty}). Applying spawn policy -> {_lastLevelLabel}.");
                }

                if (_applied)
                {
                    return;
                }

                if (ApplyPolicy(policy, level, out var appliedIndoorRange, out var appliedOutdoorRange, out int insideMatches, out int outsideMatches))
                {
                    _applied = true;
                    LogInfo($"{policy.Name} spawn caps set: inside {appliedIndoorRange.Min}-{appliedIndoorRange.Max}, outside {appliedOutdoorRange.Min}-{appliedOutdoorRange.Max} (weights applied: inside={insideMatches}, outside={outsideMatches}).");
                }
            }
            catch (Exception ex)
            {
                LogError($"MoonSpawnDirector failure: {ex}");
            }
        }

        internal static void Reset(string reason = null)
        {
            bool hadState = _applied || _lastLevel != null;
            _applied = false;
            _lastLevel = null;
            _activePolicy = null;
            _lastLevelLabel = null;
            if (hadState && !string.IsNullOrWhiteSpace(reason))
            {
                LogInfo($"MoonSpawnDirector reset: {reason}.");
            }
        }

        private static MoonSpawnPolicy FindPolicy(object level)
        {
            foreach (var policy in Policies)
            {
                if (policy.Matches(level))
                {
                    return policy;
                }
            }

            return null;
        }

        private static bool ApplyPolicy(
            MoonSpawnPolicy policy,
            object level,
            out SpawnRange resolvedIndoorRange,
            out SpawnRange resolvedOutdoorRange,
            out int insideMatches,
            out int outsideMatches)
        {
            resolvedIndoorRange = policy?.IndoorRange ?? default;
            resolvedOutdoorRange = policy?.OutdoorRange ?? default;
            insideMatches = 0;
            outsideMatches = 0;

            if (policy == null)
            {
                return false;
            }

            if (policy?.EnableFacilityScaling == true)
            {
                float facilityScale = GetFacilityScale(level);
                resolvedIndoorRange = ScaleRange(resolvedIndoorRange, facilityScale);
                resolvedOutdoorRange = ScaleRange(resolvedOutdoorRange, facilityScale);
            }

            bool changed = false;
            changed |= SetIntRange(level, "enemyAmount", resolvedIndoorRange.Min, resolvedIndoorRange.Max);
            changed |= SetIntRange(level, "daytimeEnemyAmount", resolvedIndoorRange.Min, resolvedIndoorRange.Max);
            changed |= SetIntRange(level, "outsideEnemyAmount", resolvedOutdoorRange.Min, resolvedOutdoorRange.Max);
            changed |= SetIntRange(level, "daytimeOutsideEnemyAmount", resolvedOutdoorRange.Min, resolvedOutdoorRange.Max);

            var indoorWeights = policy.IndoorWeights ?? DefaultInsideWeights;
            var outdoorWeights = policy.OutdoorWeights ?? DefaultOutsideWeights;
            changed |= ApplyWeights(level, "Enemies", indoorWeights, policy.DisableUnlistedEntries, policy.IndoorWeightScale, out insideMatches);
            changed |= ApplyWeights(level, "OutsideEnemies", outdoorWeights, policy.DisableUnlistedEntries, policy.OutdoorWeightScale, out outsideMatches);

            return changed;
        }

        private static bool ApplyWeights(
            object target,
            string listFieldName,
            IReadOnlyDictionary<string, int> overrides,
            bool disableUnlisted,
            float difficultyScale,
            out int matchedEntries)
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

            int fallbackWeight = CalculateFallbackWeight(overrides);
            bool applied = false;
            for (int i = 0; i < list.Count; i++)
            {
                var entry = list[i];
                if (entry == null)
                {
                    continue;
                }

                string resolvedName = ResolveEnemyName(enemyTypeField.GetValue(entry));
                int overrideValue = 0;
                bool hasOverride = overrides != null
                    && !string.IsNullOrEmpty(resolvedName)
                    && overrides.TryGetValue(resolvedName, out overrideValue);
                int currentRarity = 0;
                try
                {
                    currentRarity = Convert.ToInt32(rarityField.GetValue(entry));
                }
                catch
                {
                    currentRarity = 0;
                }

                int? newValue = null;
                if (hasOverride)
                {
                    newValue = overrideValue;
                    matchedEntries++;
                }
                else if (disableUnlisted)
                {
                    newValue = 0;
                }
                else if (fallbackWeight > 0 && currentRarity <= 0)
                {
                    newValue = fallbackWeight;
                }

                if (newValue.HasValue)
                {
                    int staged = newValue.Value;
                    if (staged > 0 && Math.Abs(difficultyScale - 1f) > 0.01f)
                    {
                        staged = Mathf.Clamp(Mathf.RoundToInt(staged * difficultyScale), 1, 100);
                    }

                    if (staged != currentRarity)
                    {
                        rarityField.SetValue(entry, Math.Max(0, staged));
                        applied = true;
                    }

                    continue;
                }

                if (Mathf.Abs(difficultyScale - 1f) > 0.01f && currentRarity > 0)
                {
                    int scaled = Mathf.Clamp(Mathf.RoundToInt(currentRarity * difficultyScale), 1, 100);
                    if (scaled != currentRarity)
                    {
                        rarityField.SetValue(entry, scaled);
                        applied = true;
                    }
                }
            }

            return applied;
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
            string risk = ReadString(level, "riskLevel");

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

            if (!string.IsNullOrWhiteSpace(risk))
            {
                parts.Add($"risk={risk}");
            }

            return parts.Count > 0 ? string.Join(", ", parts) : level.GetType().Name;
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

        private static SpawnRange ScaleRange(SpawnRange original, float scale)
        {
            if (scale <= 0f || Mathf.Approximately(scale, 1f))
            {
                return original;
            }

            int scaledMin = Mathf.Clamp(Mathf.RoundToInt(original.Min * scale), 0, 64);
            int scaledMax = Mathf.Clamp(Mathf.RoundToInt(original.Max * scale), 0, 64);

            if (scaledMin <= 0 && original.Min > 0)
            {
                scaledMin = 1;
            }

            if (scaledMax < scaledMin)
            {
                scaledMax = scaledMin;
            }

            return new SpawnRange(scaledMin, scaledMax);
        }

        private static float GetFacilityScale(object level)
        {
            if (level == null)
            {
                return 1f;
            }

            for (int i = 0; i < FacilitySizeMemberNames.Length; i++)
            {
                string member = FacilitySizeMemberNames[i];
                if (TryReadFloat(level, member, out float value) && value > 0.01f)
                {
                    return Mathf.Clamp(value, 0.6f, 2.5f);
                }
            }

            return 1f;
        }

        private static bool TryReadFloat(object target, string memberName, out float value)
        {
            value = 0f;
            if (target == null || string.IsNullOrWhiteSpace(memberName))
            {
                return false;
            }

            var type = target.GetType();
            var field = AccessTools.Field(type, memberName);
            if (field != null && TryConvertToFloat(field.GetValue(target), out value))
            {
                return true;
            }

            var property = AccessTools.Property(type, memberName);
            if (property?.GetGetMethod(true) != null && TryConvertToFloat(property.GetValue(target), out value))
            {
                return true;
            }

            return false;
        }

        private static bool TryConvertToFloat(object value, out float result)
        {
            switch (value)
            {
                case null:
                    result = 0f;
                    return false;
                case float f:
                    result = f;
                    return true;
                case double d:
                    result = (float)d;
                    return true;
                case int i:
                    result = i;
                    return true;
                case long l:
                    result = l;
                    return true;
                case short s:
                    result = s;
                    return true;
                case byte b:
                    result = b;
                    return true;
                default:
                    try
                    {
                        result = Convert.ToSingle(value);
                        return true;
                    }
                    catch
                    {
                        result = 0f;
                        return false;
                    }
            }
        }

        private static Dictionary<string, int> BuildDefaultInsideMap()
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Register(map, 32, "Hoarding Bug", "HoardingBug", "Hoarder Bug", "HoarderBug");
            Register(map, 26, "Snare Flea", "SnareFlea", "Centipede", "CentipedeAI");
            Register(map, 24, "Bunker Spider", "BunkerSpider");
            Register(map, 22, "Flowerman", "Bracken");
            Register(map, 22, "Thumper");
            Register(map, 20, "Sand Spider", "SandSpider");
            Register(map, 18, "Coil-Head", "Coil Head", "CoilHead", "Spring Man", "SpringMan");
            Register(map, 18, "Baboon Hawk", "BaboonHawk", "Baboon Bird", "BaboonBird");
            Register(map, 18, "Blob", "Hygrodere", "Slime");
            Register(map, 16, "Spore Lizard", "SporeLizard", "Puffer", "PufferAI");
            Register(map, 14, "Jester");
            Register(map, 12, "Nutcracker", "Nut Cracker");
            Register(map, 12, "Masked", "MaskedPlayerEnemy");
            Register(map, 10, "Butler", "ButlerEnemy");
            Register(map, 12, "Barber", "BarberEnemy", "ScissorEnemy", "Scissors");
            Register(map, 10, "Maneater", "Baby", "BabyManeater");
            Register(map, 8, "Ghost Girl", "Little Girl", "LittleGirl", "GhostGirl");
            Register(map, 8, "Kidnapper Fox", "KidnapperFox", "FoxEnemy");
            return map;
        }

        private static Dictionary<string, int> BuildDefaultOutsideMap()
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            Register(map, 38, "Eyeless Dog", "EyelessDog", "MouthDog", "Mouth Dog");
            Register(map, 30, "Baboon Hawk", "BaboonHawk", "Baboon Bird", "BaboonBird");
            Register(map, 26, "Forest Keeper", "ForestKeeper");
            Register(map, 22, "Sand Worm", "SandWorm");
            Register(map, 20, "Sand Spider", "SandSpider");
            Register(map, 18, "Blob", "Hygrodere", "Slime");
            Register(map, 16, "Hoarding Bug", "HoardingBug", "Hoarder Bug", "HoarderBug");
            Register(map, 14, "Spore Lizard", "SporeLizard", "Puffer", "PufferAI");
            Register(map, 12, "Manticoil", "ManticoilBird");
            Register(map, 20, "Old Bird", "OldBird", "MechBird", "Mech");
            Register(map, 16, "Circuit Bees", "CircuitBees", "Bee Swarm", "BeeSwarm", "Hive", "Beehive");
            Register(map, 14, "Tulip Snake", "TulipSnake", "Flying Snake");
            Register(map, 12, "Roaming Locusts", "Locust", "Locusts");
            return map;
        }

        private static List<MoonSpawnPolicy> BuildPolicies()
        {
            var policies = new List<MoonSpawnPolicy>
            {
                new MoonSpawnPolicy(
                    name: "Experimentation",
                    difficulty: "Training",
                    aliases: new[] { "Experimentation", "41" },
                    indoorRange: BeginnerSafeFactoryCaps.IndoorRange,
                    outdoorRange: BeginnerSafeFactoryCaps.OutdoorRange,
                    indoorWeights: CloneInsideWeights(dict =>
                    {
                        dict["Jester"] = 4;
                        dict["Nutcracker"] = 3;
                        dict["Thumper"] = 14;
                        dict["Flowerman"] = 16;
                    }),
                    outdoorWeights: CloneOutsideWeights(dict =>
                    {
                        dict["Forest Keeper"] = 6;
                        dict["Eyeless Dog"] = 18;
                        dict["Sand Worm"] = 8;
                    }),
                    indoorScale: 0.85f,
                    outdoorScale: 0.8f),
                new MoonSpawnPolicy(
                    name: "Vow",
                    difficulty: "Training",
                    aliases: new[] { "Vow", "12" },
                    indoorRange: BeginnerForestCaps.IndoorRange,
                    outdoorRange: BeginnerForestCaps.OutdoorRange,
                    indoorWeights: CloneInsideWeights(dict =>
                    {
                        dict["Jester"] = 6;
                        dict["Nutcracker"] = 5;
                    }),
                    outdoorWeights: CloneOutsideWeights(dict =>
                    {
                        dict["Forest Keeper"] = 8;
                        dict["Sand Worm"] = 10;
                    }),
                    indoorScale: 0.9f,
                    outdoorScale: 0.9f),
                new MoonSpawnPolicy(
                    name: "Assurance",
                    difficulty: "Standard",
                    aliases: new[] { "Assurance", "220" },
                    indoorRange: BeginnerRuggedFactoryCaps.IndoorRange,
                    outdoorRange: BeginnerRuggedFactoryCaps.OutdoorRange,
                    indoorWeights: CloneInsideWeights(dict =>
                    {
                        dict["Hoarding Bug"] = 35;
                        dict["Snare Flea"] = 28;
                        dict["Bunker Spider"] = 25;
                        dict["Thumper"] = 25;
                        dict["Sand Spider"] = 18;
                        dict["Coil-Head"] = 16;
                        dict["Baboon Hawk"] = 14;
                        dict["Flowerman"] = 12;
                        dict["Blob"] = 18;
                        dict["Spore Lizard"] = 16;
                    }),
                    outdoorWeights: CloneOutsideWeights(dict =>
                    {
                        dict["Eyeless Dog"] = 40;
                        dict["Baboon Hawk"] = 30;
                        dict["Sand Spider"] = 20;
                        dict["Sand Worm"] = 18;
                        dict["Blob"] = 14;
                        dict["Hoarding Bug"] = 12;
                    }),
                    indoorScale: 1f,
                    outdoorScale: 1f),
                new MoonSpawnPolicy(
                    name: "Offense",
                    difficulty: "Standard",
                    aliases: new[] { "Offense", "71" },
                    indoorRange: IntermediateFactoryCaps.IndoorRange,
                    outdoorRange: IntermediateFactoryCaps.OutdoorRange,
                    indoorWeights: CloneInsideWeights(dict =>
                    {
                        dict["Thumper"] = 26;
                        dict["Flowerman"] = 18;
                        dict["Coil-Head"] = 18;
                        dict["Jester"] = 10;
                    }),
                    outdoorWeights: CloneOutsideWeights(dict =>
                    {
                        dict["Forest Keeper"] = 18;
                        dict["Eyeless Dog"] = 34;
                        dict["Sand Worm"] = 22;
                    }),
                    indoorScale: 1.05f,
                    outdoorScale: 1.05f),
                new MoonSpawnPolicy(
                    name: "March",
                    difficulty: "Industrial",
                    aliases: new[] { "March", "56" },
                    indoorRange: FloodedFactoryCaps.IndoorRange,
                    outdoorRange: FloodedFactoryCaps.OutdoorRange,
                    indoorWeights: CloneInsideWeights(dict =>
                    {
                        dict["Flowerman"] = 20;
                        dict["Snare Flea"] = 26;
                        dict["Coil-Head"] = 20;
                        dict["Jester"] = 14;
                    }),
                    outdoorWeights: CloneOutsideWeights(),
                    indoorScale: 1.1f,
                    outdoorScale: 1.05f),
                new MoonSpawnPolicy(
                    name: "Adamance",
                    difficulty: "Industrial",
                    aliases: new[] { "Adamance", "68" },
                    indoorRange: DenseFactoryCaps.IndoorRange,
                    outdoorRange: DenseFactoryCaps.OutdoorRange,
                    indoorWeights: CloneInsideWeights(dict =>
                    {
                        dict["Thumper"] = 28;
                        dict["Sand Spider"] = 24;
                        dict["Jester"] = 16;
                        dict["Nutcracker"] = 14;
                    }),
                    outdoorWeights: CloneOutsideWeights(dict =>
                    {
                        dict["Sand Worm"] = 24;
                        dict["Forest Keeper"] = 24;
                    }),
                    indoorScale: 1.12f,
                    outdoorScale: 1.08f),
                new MoonSpawnPolicy(
                    name: "Rend",
                    difficulty: "Hazard (Paid)",
                    aliases: new[] { "Rend", "87" },
                    indoorRange: MansionBlizzardCaps.IndoorRange,
                    outdoorRange: MansionBlizzardCaps.OutdoorRange,
                    indoorWeights: CloneInsideWeights(dict =>
                    {
                        dict["Flowerman"] = 28;
                        dict["Thumper"] = 30;
                        dict["Jester"] = 22;
                        dict["Nutcracker"] = 18;
                        dict["Masked"] = 18;
                    }),
                    outdoorWeights: CloneOutsideWeights(dict =>
                    {
                        dict["Forest Keeper"] = 32;
                        dict["Eyeless Dog"] = 44;
                        dict["Sand Worm"] = 26;
                    }),
                    indoorScale: 1.2f,
                    outdoorScale: 1.18f),
                new MoonSpawnPolicy(
                    name: "Dine",
                    difficulty: "Hazard (Paid)",
                    aliases: new[] { "Dine", "74" },
                    indoorRange: MansionSiegeCaps.IndoorRange,
                    outdoorRange: MansionSiegeCaps.OutdoorRange,
                    indoorWeights: CloneInsideWeights(dict =>
                    {
                        dict["Snare Flea"] = 30;
                        dict["Bunker Spider"] = 28;
                        dict["Jester"] = 20;
                        dict["Nutcracker"] = 18;
                    }),
                    outdoorWeights: CloneOutsideWeights(dict =>
                    {
                        dict["Forest Keeper"] = 28;
                        dict["Sand Worm"] = 28;
                        dict["Baboon Hawk"] = 36;
                    }),
                    indoorScale: 1.2f,
                    outdoorScale: 1.15f),
                new MoonSpawnPolicy(
                    name: "Titan",
                    difficulty: "Extreme (Paid)",
                    aliases: new[] { "Titan", "85" },
                    indoorRange: ExtremeFactoryCaps.IndoorRange,
                    outdoorRange: ExtremeFactoryCaps.OutdoorRange,
                    indoorWeights: CloneInsideWeights(dict =>
                    {
                        dict["Flowerman"] = 32;
                        dict["Thumper"] = 32;
                        dict["Jester"] = 24;
                        dict["Nutcracker"] = 20;
                        dict["Masked"] = 18;
                        dict["Coil-Head"] = 24;
                    }),
                    outdoorWeights: CloneOutsideWeights(dict =>
                    {
                        dict["Forest Keeper"] = 34;
                        dict["Eyeless Dog"] = 46;
                        dict["Sand Worm"] = 30;
                    }),
                    indoorScale: 1.28f,
                    outdoorScale: 1.25f),
                new MoonSpawnPolicy(
                    name: "Artifice",
                    difficulty: "Extreme (Paid)",
                    aliases: new[] { "Artifice", "8" },
                    indoorRange: WarehouseChaosCaps.IndoorRange,
                    outdoorRange: WarehouseChaosCaps.OutdoorRange,
                    indoorWeights: CloneInsideWeights(dict =>
                    {
                        dict["Jester"] = 26;
                        dict["Nutcracker"] = 22;
                        dict["Flowerman"] = 30;
                        dict["Masked"] = 20;
                    }),
                    outdoorWeights: CloneOutsideWeights(dict =>
                    {
                        dict["Forest Keeper"] = 32;
                        dict["Sand Worm"] = 32;
                        dict["Baboon Hawk"] = 34;
                    }),
                    indoorScale: 1.3f,
                    outdoorScale: 1.25f),
                new MoonSpawnPolicy(
                    name: "Embrion",
                    difficulty: "Nightmare (Paid)",
                    aliases: new[] { "Embrion", "??" },
                    indoorRange: TrapMoonCaps.IndoorRange,
                    outdoorRange: TrapMoonCaps.OutdoorRange,
                    indoorWeights: CloneInsideWeights(dict =>
                    {
                        dict["Jester"] = 30;
                        dict["Nutcracker"] = 24;
                        dict["Thumper"] = 34;
                        dict["Flowerman"] = 32;
                        dict["Masked"] = 22;
                        dict["Coil-Head"] = 26;
                    }),
                    outdoorWeights: CloneOutsideWeights(dict =>
                    {
                        dict["Forest Keeper"] = 36;
                        dict["Eyeless Dog"] = 48;
                        dict["Sand Worm"] = 34;
                    }),
                    indoorScale: 1.35f,
                    outdoorScale: 1.3f)
            };

            return policies;
        }

        private static MoonSpawnPolicy BuildGeneralFallbackPolicy()
        {
            return new MoonSpawnPolicy(
                name: "General",
                difficulty: "Adaptive",
                aliases: Array.Empty<string>(),
                indoorRange: GeneralUnknownCaps.IndoorRange,
                outdoorRange: GeneralUnknownCaps.OutdoorRange,
                indoorWeights: CloneInsideWeights(),
                outdoorWeights: CloneOutsideWeights(),
                indoorScale: 1f,
                outdoorScale: 1f,
                enableFacilityScaling: true);
        }

        private static Dictionary<string, int> CloneInsideWeights(Action<Dictionary<string, int>> mutator = null)
        {
            var clone = new Dictionary<string, int>(DefaultInsideWeights, StringComparer.OrdinalIgnoreCase);
            mutator?.Invoke(clone);
            return clone;
        }

        private static Dictionary<string, int> CloneOutsideWeights(Action<Dictionary<string, int>> mutator = null)
        {
            var clone = new Dictionary<string, int>(DefaultOutsideWeights, StringComparer.OrdinalIgnoreCase);
            mutator?.Invoke(clone);
            return clone;
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

                map[alias] = Mathf.Max(0, weight);
            }
        }

        private static void LogInfo(string message)
        {
            AlgoritmaPuncakMod.Log?.LogInfo($"{LogPrefix} {message}");
        }

        private static void LogError(string message)
        {
            AlgoritmaPuncakMod.Log?.LogError($"{LogPrefix} {message}");
        }

        private static int CalculateFallbackWeight(IReadOnlyDictionary<string, int> overrides)
        {
            if (overrides == null || overrides.Count == 0)
            {
                return 6;
            }

            long sum = 0;
            int count = 0;
            foreach (var pair in overrides)
            {
                if (pair.Value <= 0)
                {
                    continue;
                }

                sum += pair.Value;
                count++;
            }

            if (count == 0)
            {
                return 6;
            }

            float average = (float)sum / count;
            return Mathf.Clamp(Mathf.RoundToInt(average), 1, 100);
        }

        private sealed class MoonSpawnPolicy
        {
            internal MoonSpawnPolicy(
                string name,
                string difficulty,
                string[] aliases,
                SpawnRange indoorRange,
                SpawnRange outdoorRange,
                IReadOnlyDictionary<string, int> indoorWeights,
                IReadOnlyDictionary<string, int> outdoorWeights,
                float indoorScale,
                float outdoorScale,
                bool disableUnlisted = false,
                bool enableFacilityScaling = false)
            {
                Name = name;
                Difficulty = difficulty;
                Aliases = aliases ?? Array.Empty<string>();
                IndoorRange = indoorRange;
                OutdoorRange = outdoorRange;
                IndoorWeights = indoorWeights;
                OutdoorWeights = outdoorWeights;
                IndoorWeightScale = indoorScale;
                OutdoorWeightScale = outdoorScale;
                DisableUnlistedEntries = disableUnlisted;
                EnableFacilityScaling = enableFacilityScaling;
            }

            internal string Name { get; }
            internal string Difficulty { get; }
            internal string[] Aliases { get; }
            internal SpawnRange IndoorRange { get; }
            internal SpawnRange OutdoorRange { get; }
            internal IReadOnlyDictionary<string, int> IndoorWeights { get; }
            internal IReadOnlyDictionary<string, int> OutdoorWeights { get; }
            internal float IndoorWeightScale { get; }
            internal float OutdoorWeightScale { get; }
            internal bool DisableUnlistedEntries { get; }
            internal bool EnableFacilityScaling { get; }

            internal bool Matches(object level)
            {
                if (level == null)
                {
                    return false;
                }

                var labels = new List<string>
                {
                    ReadString(level, "PlanetName") ?? ReadString(level, "planetName"),
                    ReadString(level, "levelName"),
                    ReadString(level, "sceneName"),
                    ReadString(level, "riskLevel")
                };

                foreach (var alias in Aliases)
                {
                    if (string.IsNullOrWhiteSpace(alias))
                    {
                        continue;
                    }

                    for (int i = 0; i < labels.Count; i++)
                    {
                        var label = labels[i];
                        if (string.IsNullOrWhiteSpace(label))
                        {
                            continue;
                        }

                        if (label.IndexOf(alias, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            return true;
                        }
                    }
                }

                return false;
            }
        }

        private readonly struct SpawnRange
        {
            internal SpawnRange(int min, int max)
            {
                Min = min;
                Max = max;
            }

            internal int Min { get; }
            internal int Max { get; }
        }

        private readonly struct SpawnCapProfile
        {
            internal SpawnCapProfile(int indoorMin, int indoorMax, int outdoorMin, int outdoorMax)
            {
                IndoorMin = indoorMin;
                IndoorMax = indoorMax;
                OutdoorMin = outdoorMin;
                OutdoorMax = outdoorMax;
            }

            internal SpawnRange IndoorRange => new SpawnRange(IndoorMin, IndoorMax);
            internal SpawnRange OutdoorRange => new SpawnRange(OutdoorMin, OutdoorMax);
            internal int IndoorMin { get; }
            internal int IndoorMax { get; }
            internal int OutdoorMin { get; }
            internal int OutdoorMax { get; }
        }
    }
}
