using BepInEx;
using HarmonyLib;
using System;
using System.Collections;
using System.Reflection;
using FTK_MultiMax_Rework_v2.PatchHelpers;
using static FTK_MultiMax_Rework_v2.PatchHelpers.PatchUtils;
using Debug = UnityEngine.Debug;

namespace FTK_MultiMax_Rework_v2 {
    [BepInPlugin(pluginGuid, pluginName, pluginVersion)]

    public class Main : BaseUnityPlugin {
        private const string pluginGuid = "polarsbear.ftk.multimaxrework.patched";
        private const string pluginName = "MultiMaxReworkV2";
        private const string pluginVersion = "2.1";

        public static Harmony Harmony { get; } = new(pluginGuid);

        public static void Log(Object message)
        {
            Debug.Log($"[{pluginName}]: {message}");
        }
        
        public IEnumerator Start() {
            Log("Initializing version " + pluginVersion + "...");
            ConfigHandler.InitializeConfig();
            ConfigHandler.InitializeMaxPlayers();
            Log("Finished initializing");
            
            Log("Patching...");
            PatchMethods();
            Log("Finished patching");
            
            Log("Reshaping player creation gui array");
            Type playerCreationUI = typeof(uiQuickPlayerCreate);
            FieldInfo playerCreateArray =
                playerCreationUI.GetField("guiQuickPlayerCreates", BindingFlags.Static | BindingFlags.NonPublic);
            playerCreateArray?.SetValue(null, new uiQuickPlayerCreate[GameFlowMC.gMaxPlayers]);
            
            uiQuickPlayerCreate.Default_Classes = new int[GameFlowMC.gMaxPlayers];

            Log("Waiting for game to load...");
            while (!FTKHub.Instance) {
                yield return null;
            }

            Log("Startup done");
        }

        // Patch all methods with [PatchMethod(...)] attribute,
        // Inside of all classes with [PatchType(...)]
        // Very elegant, if I dare say so myself
        private void PatchMethods()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            foreach (Type type in assembly.GetTypesWithAttribute<PatchType>())
            {
                PatchClass(type);
            }
        }
        [HarmonyPatch(typeof(EncounterSessionMC))]
        public static class DebugListMethodsPatch
        {
            [HarmonyPostfix]
            [HarmonyPatch("Start")] // qualsiasi metodo sicuro che gira a inizio partita
            public static void ListAllMethods()
            {
                foreach (var m in typeof(EncounterSessionMC).GetMethods(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    if (m.Name.ToLower().Contains("init") || m.Name.ToLower().Contains("fight") || m.Name.ToLower().Contains("initiative"))
                    {
                        Log($"[MultiMax][Debug] Found method: {m}");
                    }
                }
            }
        }
    }
}