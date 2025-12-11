using System;
using AlgoritmaPuncakMod.AI;
using AlgoritmaPuncakMod.Directors;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace AlgoritmaPuncakMod
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class AlgoritmaPuncakMod : BaseUnityPlugin
    {
        public const string modGUID = "Sen.AlgoritmaPuncakMod";
        public const string modName = "AlgoritmaPuncak"; 
        public const string modVersion = "1.0.0.0";

        internal static AlgoritmaPuncakMod Instance { get; private set; }
        internal static ManualLogSource Log { get; private set; }
        internal static AIBalanceProfile BalanceProfile { get; private set; } = new AIBalanceProfile(5f, 12f, 18f, 3f, 12f, 25f, 10f, 1.2f);

        private Harmony _harmony;

        private ConfigEntry<float> _stalkMinDistance;
        private ConfigEntry<float> _stalkMaxDistance;
        private ConfigEntry<float> _huntAggroDistance;
        private ConfigEntry<float> _huntPredictionLead;
        private ConfigEntry<float> _territoryRadius;
        private ConfigEntry<float> _lureCooldown;
        private ConfigEntry<float> _packCohesionRadius;
        private ConfigEntry<float> _reactiveMultiplier;

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            BindConfig();
            RefreshBalanceProfile();

            _harmony = new Harmony(modGUID);
            _harmony.PatchAll();

            Logger.LogMessage("Algoritma Puncak AI overhaul loaded successfully.");
        }

        private void OnDestroy()
        {
            AIBehaviorCoordinator.Reset();
            AssuranceSpawnDirector.Reset();
            _harmony?.UnpatchSelf();
        }

        private void Update()
        {
            AssuranceSpawnDirector.Tick();
        }

        private void BindConfig()
        {
            _stalkMinDistance = Config.Bind("Stalking", "MinDistance", 6f, "Preferred minimum distance when stalking the player.");
            _stalkMaxDistance = Config.Bind("Stalking", "MaxDistance", 15f, "Maximum distance before the AI closes the gap while stalking.");
            _huntAggroDistance = Config.Bind("Hunting", "AggroDistance", 20f, "Distance at which the AI switches from stalking to hunting.");
            _huntPredictionLead = Config.Bind("Hunting", "PredictionLead", 4f, "How far ahead (in meters) the AI predicts the player's next location.");
            _territoryRadius = Config.Bind("Territorial", "Radius", 12f, "Radius of territorial aggression around the spawn point.");
            _lureCooldown = Config.Bind("Luring", "Cooldown", 30f, "Cooldown between lure attempts.");
            _packCohesionRadius = Config.Bind("Pack", "CohesionRadius", 10f, "Radius used to coordinate pack / swarm behaviour.");
            _reactiveMultiplier = Config.Bind("Reactive", "AggressionMultiplier", 1.35f, "Multiplier applied to reactive aggression calculations.");

            _stalkMinDistance.SettingChanged += OnConfigValueChanged;
            _stalkMaxDistance.SettingChanged += OnConfigValueChanged;
            _huntAggroDistance.SettingChanged += OnConfigValueChanged;
            _huntPredictionLead.SettingChanged += OnConfigValueChanged;
            _territoryRadius.SettingChanged += OnConfigValueChanged;
            _lureCooldown.SettingChanged += OnConfigValueChanged;
            _packCohesionRadius.SettingChanged += OnConfigValueChanged;
            _reactiveMultiplier.SettingChanged += OnConfigValueChanged;
        }

        private void OnConfigValueChanged(object sender, EventArgs e)
        {
            RefreshBalanceProfile();
        }

        private void RefreshBalanceProfile()
        {
            BalanceProfile = new AIBalanceProfile(
                _stalkMinDistance.Value,
                _stalkMaxDistance.Value,
                _huntAggroDistance.Value,
                _huntPredictionLead.Value,
                _territoryRadius.Value,
                _lureCooldown.Value,
                _packCohesionRadius.Value,
                _reactiveMultiplier.Value);

            AIBehaviorCoordinator.Initialize(BalanceProfile.TerritoryRadius);
        }
    }

    [HarmonyPatch(typeof(EnemyAI))]
    [HarmonyPatch("Update")]
    internal static class EnemyAIUpdatePatch
    {
        [HarmonyPostfix]
        private static void Postfix(EnemyAI __instance)
        {
            if (__instance == null || !__instance.isClientCalculatingAI || __instance.isEnemyDead)
            {
                return;
            }

            AIBehaviorCoordinator.Update(__instance);
        }
    }
}