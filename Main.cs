using BepInEx;
using HarmonyLib;
using System;
using System.Collections;
using System.Reflection;
using FTK_MultiMax_Rework.PatchHelpers;
using static FTK_MultiMax_Rework.PatchHelpers.PatchUtils;
using Debug = UnityEngine.Debug;
using UnityEngine;

namespace FTK_MultiMax_Rework
{
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]

    public class Main : BaseUnityPlugin
    {
        private const string pluginGuid = "polarsbear.ftk.multimaxrework.patched";
        private const string pluginName = "MultiMaxReworkV3";
        private const string pluginVersion = "3.0.0";

        public static Harmony Harmony { get; } = new(pluginGuid);

        public static void Log(System.Object message)
        {
            Debug.Log($"[{pluginName}]: {message}");
        }

        public void Start()
        {
            Log("Initializing version " + pluginVersion + "...");

            ConfigHandler.InitializeConfig();
            ConfigHandler.InitializeMaxPlayers();
            PatchMethods();
            SetupPlayerCreationUI();
            Log("Startup done");
        }
        // Patch all methods with [PatchMethod(...)] attribute,
        // Inside of all classes with [PatchType(...)]
        // Very elegant, if I dare say so myself - yes indeed very elegant unlike my code.
        private void PatchMethods()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            foreach (Type type in assembly.GetTypesWithAttribute<PatchType>())
            {
                PatchClass(type);
            }
        }
        private static void SetupPlayerCreationUI()
        {
            Type playerCreationUI = typeof(uiQuickPlayerCreate);
            FieldInfo playerCreateArray =
                playerCreationUI.GetField("guiQuickPlayerCreates", BindingFlags.Static | BindingFlags.NonPublic);
            playerCreateArray?.SetValue(null, new uiQuickPlayerCreate[GameFlowMC.gMaxPlayers]);
            uiQuickPlayerCreate.Default_Classes = new int[GameFlowMC.gMaxPlayers];
            Log("Player creation GUI reshaped");
        }

        // tiny helper that uses your existing GameLogic coroutine host safely
        public static class SafeCoroutine
        {
            public static void Run(IEnumerator r)
            {
                if (r == null) return;
                if (GameLogic.Instance == null) return;
                GameLogic.Instance.StartCoroutine(r);
            }
        }
    }
}