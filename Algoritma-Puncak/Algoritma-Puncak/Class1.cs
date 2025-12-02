using BepInEx;
using HarmonyLib;
using System;
using Unity;
using UnityEngine;

namespace AlgoritmaPuncakMod
{
    [BepInPlugin(modGUID, modName, modVersion)]
    public class AlgoritmaPuncakMod : BaseUnityPlugin
    {
        public const string modGUID = "Sen.AlgoritmaPuncakMod";
        public const string modName = "AlgoritmaPuncak"; 
        public const string modVersion = "1.0.0.0";

        private readonly Harmony harmony = new Harmony(modGUID);

        void Awake()
        {
            var BepInExLogSource = BepInEx.Logging.Logger.CreateLogSource(modGUID); // creates a logger for the BepInEx console
            BepInExLogSource.LogMessage(modGUID + " has loaded succesfully."); // show the successful loading of the mod in the BepInEx console

            harmony.PatchAll(typeof(AlgoritmaPuncak));
        }
    }

    [HarmonyPatch(typeof(UnityEngine.AI.NavMeshAgent))]
    [HarmonyPatch("Update")]
    class AlgoritmaPuncak
    {
        //[HarmonyPostfix]
        //static void Postfix(ref NavMeshAgent ___instance)
        //{
        //}
    }

    // [HarmonyPatch(typeof(EnemyAI))]
    // [HarmonyPatch("Update")]
    // class AlgoritmaPuncak
    // {
    //     [HarmonyPostfix]
    //     static void Postfix(ref EnemyAI ___instance)
    //     {
    //     }
    // }
}