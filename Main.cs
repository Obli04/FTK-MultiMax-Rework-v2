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

        public IEnumerator Start()
        {
            Log("Initializing version " + pluginVersion + "...");

            ConfigHandler.InitializeConfig();
            ConfigHandler.InitializeMaxPlayers();
            PatchMethods();
            SetupPlayerCreationUI();

            /*Log("Waiting for Photon room...");
            while (!PhotonNetwork.inRoom)
                yield return null;

            // ✅ Aspetta che la room sia completamente caricata
            yield return new WaitForSeconds(2f);

            CreateRpcHandler();*/

            // ✅ Verifica che l'handler esista
            yield return new WaitForSeconds(0.5f);
            var handler = GameObject.Find("MultiMaxRPCHandler");
            if (handler != null)
            {
                var pv = handler.GetComponent<PhotonView>();
                Log($"RPC handler verified: viewID={pv?.viewID ?? 0}");
            }
            else
            {
                Log("ERROR: RPC handler creation failed!");
            }

            Log("Startup done");
        }

        private static void CreateRpcHandler()
        {
            const string HandlerName = "MultiMaxRPCHandler";
            var existing = GameObject.Find(HandlerName);
            if (existing != null)
            {
                Log("RPC handler already exists");
                return;
            }

            var go = new GameObject(HandlerName);
            var pv = go.AddComponent<PhotonView>();
            pv.ownershipTransfer = OwnershipOption.Fixed;

            // ✅ TUTTI allocano un viewID se sono MasterClient
            if (PhotonNetwork.isMasterClient)
            {
                pv.viewID = PhotonNetwork.AllocateSceneViewID();
                Log($"MasterClient allocated viewID={pv.viewID}");
            }
            else
            {
                Log("Client waiting for MasterClient to allocate viewID");
            }

            go.AddComponent<MultiMaxNetworkRPC>();
            GameObject.DontDestroyOnLoad(go);

            Log($"RPC handler created");
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