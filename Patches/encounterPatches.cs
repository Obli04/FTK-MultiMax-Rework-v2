using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using FTK_MultiMax_Rework.PatchHelpers;
using static FTK_MultiMax_Rework.PatchHelpers.PatchPositions;
using static FTK_MultiMax_Rework.Main;
using Newtonsoft.Json;

namespace FTK_MultiMax_Rework.Patches
{
    [PatchType(typeof(EncounterSessionMC))]
    public static class SyncViaDedicatedHandler
    {
        private static PhotonView GetRPCHandler()
        {
            var go = GameObject.Find("MultiMaxRPCHandler");
            if (go == null)
            {
                Debug.LogWarning("[MultiMax] RPC handler GameObject not found");
                return null;
            }

            var pv = go.GetComponent<PhotonView>();
            if (pv == null || pv.viewID == 0)
            {
                Debug.LogWarning($"[MultiMax] RPC handler has invalid PhotonView (viewID={pv?.viewID ?? 0})");
                return null;
            }

            return pv;
        }

        [PatchMethod("StartNextCombatRound2")]
        [PatchPosition(Postfix)]
        public static void BroadcastTurnAndOrder(EncounterSessionMC __instance)
        {
            if (!PhotonNetwork.isMasterClient) return;
            SafeCoroutine.Run(DelayedBroadcast(__instance));
        }

        private static IEnumerator DelayedBroadcast(EncounterSessionMC __instance)
        {
            // Aspetta un frame per evitare race conditions
            yield return new WaitForEndOfFrame();

            try
            {
                var handler = GetRPCHandler();
                if (handler == null)
                {
                    Debug.LogError("[MultiMax] ❌ Cannot broadcast: RPC handler missing");
                    yield break;
                }

                // 1️⃣ Sync turn index
                var indexField = typeof(EncounterSessionMC).GetField("m_CurrentCombatantIndex",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                int index = (int)(indexField?.GetValue(__instance) ?? -1);

                handler.RPC("SyncTurnIndex", PhotonTargets.Others, index);
                Debug.Log($"[MultiMax] ➡️ Sent turn index {index} via viewID {handler.viewID}");

                // 2️⃣ Sync fight order
                var orderField = typeof(EncounterSessionMC).GetField("m_FightOrder",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var fightOrder = orderField?.GetValue(__instance) as IList;

                if (fightOrder != null && fightOrder.Count > 0)
                {
                    var ids = new List<FTKPlayerID>();
                    foreach (var entry in fightOrder)
                    {
                        var pidField = entry.GetType().GetField("m_Pid");
                        if (pidField != null && pidField.GetValue(entry) is FTKPlayerID pid)
                            ids.Add(pid);
                    }

                    if (ids.Count > 0)
                    {
                        string json = JsonConvert.SerializeObject(ids);
                        handler.RPC("SyncFightOrder", PhotonTargets.Others, json);
                        Debug.Log($"[MultiMax] ➡️ Sent fight order ({ids.Count}) via viewID {handler.viewID}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] DelayedBroadcast error: {e}");
            }
        }
    }

    // ✅ Sync victim tramite handler dedicato
    [PatchType(typeof(EnemyDummy))]
    public static class SyncVictimPatch
    {
        private static PhotonView GetRPCHandler()
        {
            var go = GameObject.Find("MultiMaxRPCHandler");
            return go?.GetComponent<PhotonView>();
        }

        [PatchMethod("SetAttackDecision")]
        [PatchPosition(Postfix)]
        public static void BroadcastVictim(EnemyDummy __instance)
        {
            if (!PhotonNetwork.isMasterClient) return;
            if (__instance?.m_CurrentVictimID == null) return;

            SafeCoroutine.Run(DelayedVictimBroadcast(__instance));
        }

        private static IEnumerator DelayedVictimBroadcast(EnemyDummy __instance)
        {
            yield return new WaitForSeconds(0.3f);

            var handler = GetRPCHandler();
            if (handler != null && handler.viewID != 0)
            {
                handler.RPC("SyncVictim", PhotonTargets.Others, __instance.FID, __instance.m_CurrentVictimID);
                Debug.Log($"[MultiMax] Sent victim sync for {__instance.FID.m_TurnIndex}");
            }
            else
            {
                Debug.LogWarning("[MultiMax] Cannot send victim: handler invalid");
            }
        }
    }

    // ✅ Hard lock skip turn
    [PatchType(typeof(CharacterDummy))]
    public static class HardLock_SkipTurn
    {
        static bool IsHardLock(CharacterDummy d)
            => d != null && (d.Frozen || d.Petrified || d.Stunned);

        [PatchMethod("EngageAttack")]
        [PatchPosition(Prefix)]
        public static bool GuardSkip(CharacterDummy __instance)
        {
            if (!__instance.FID.IsPlayer()) return true;
            if (!IsHardLock(__instance)) return true;

            var stance = UnityEngine.Object.FindObjectOfType<uiBattleStanceButtons>();
            if (stance) stance.gameObject.SetActive(false);

            Debug.Log($"[MultiMax] {__instance.name} is hard-locked → skipping turn");
            EncounterSessionMC.Instance?.StartNextCombatRound2();
            return false;
        }
    }
    [PatchType(typeof(EncounterSessionMC))]
    public static class CreateRPCHandlerOnCombatStart
    {
        private static GameObject _handlerInstance;

        [PatchMethod("CommenceBattleRPC")]
        [PatchPosition(Prefix)]
        public static void EnsureHandlerExists()
        {
            try
            {
                // Verifica se esiste già
                if (_handlerInstance != null)
                {
                    Debug.Log("[MultiMax] RPC handler already exists");
                    return;
                }

                var existing = GameObject.Find("MultiMaxRPCHandler");
                if (existing != null)
                {
                    _handlerInstance = existing;
                    Debug.Log($"[MultiMax] Found existing handler, viewID={existing.GetComponent<PhotonView>()?.viewID ?? 0}");
                    return;
                }

                // ✅ SOLO il MasterClient può instantiare oggetti di scena
                if (!PhotonNetwork.isMasterClient)
                {
                    // Try read viewID from room custom properties
                    if (PhotonNetwork.room != null
                        && PhotonNetwork.room.CustomProperties != null
                        && PhotonNetwork.room.CustomProperties.ContainsKey("MM_RPCVID"))
                    {
                        int vid = (int)PhotonNetwork.room.CustomProperties["MM_RPCVID"];
                        var go = new GameObject("MultiMaxRPCHandler");
                        var pv = go.AddComponent<PhotonView>();
                        pv.viewID = vid;                         // <-- match Master's id
                        pv.ownershipTransfer = OwnershipOption.Fixed;
                        pv.synchronization = ViewSynchronization.Off;
                        go.AddComponent<MultiMaxNetworkRPC>();
                        GameObject.DontDestroyOnLoad(go);
                        _handlerInstance = go;
                        Debug.Log($"[MultiMax] Client created RPC handler with viewID={vid}");
                    }
                    else
                    {
                        Debug.Log("[MultiMax] Client: viewID not yet published, will poll...");
                        Main.SafeCoroutine.Run(WaitForHandlerViewId());
                    }
                    return;
                }

                // ✅ Crea manualmente ma marca come scene object
                var go = new GameObject("MultiMaxRPCHandler");
                var pv = go.AddComponent<PhotonView>();

                // Assegna un viewID di scena
                pv.viewID = PhotonNetwork.AllocateSceneViewID();
                // after allocating pv.viewID
                var props = new ExitGames.Client.Photon.Hashtable { { "MM_RPCVID", pv.viewID } };
                PhotonNetwork.room.SetCustomProperties(props);
                pv.ownershipTransfer = OwnershipOption.Fixed;
                pv.synchronization = ViewSynchronization.Off; // non serve sincronizzare transform

                go.AddComponent<MultiMaxNetworkRPC>();
                GameObject.DontDestroyOnLoad(go);

                _handlerInstance = go;

                Debug.Log($"[MultiMax] ✅ MasterClient created RPC handler, viewID={pv.viewID}");

                // ✅ Invia un RPC vuoto per "svegliare" i client
                SafeCoroutine.Run(NotifyClientsHandlerReady(pv));
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] CreateRPCHandlerOnCombatStart error: {e}");
            }
        }
        private static IEnumerator WaitForHandlerViewId()
        {
            float t = 0f;
            while (t < 5f) // 5s timeout
            {
                var room = PhotonNetwork.room;
                if (room != null && room.CustomProperties != null && room.CustomProperties.ContainsKey("MM_RPCVID"))
                {
                    int vid = (int)room.CustomProperties["MM_RPCVID"];
                    var go = new GameObject("MultiMaxRPCHandler");
                    var pv = go.AddComponent<PhotonView>();
                    pv.viewID = vid;
                    pv.ownershipTransfer = OwnershipOption.Fixed;
                    pv.synchronization = ViewSynchronization.Off;
                    go.AddComponent<MultiMaxNetworkRPC>();
                    GameObject.DontDestroyOnLoad(go);
                    _handlerInstance = go;
                    Debug.Log($"[MultiMax] Client late-created RPC handler with viewID={vid}");
                    yield break;
                }
                t += Time.deltaTime;
                yield return null;
            }
            Debug.LogError("[MultiMax] Timeout waiting for MM_RPCVID; RPCs will fail.");
        }

        private static IEnumerator NotifyClientsHandlerReady(PhotonView pv)
        {
            // Aspetta che Photon sincronizzi il viewID
            yield return new WaitForSeconds(1.5f);

            if (pv != null && pv.viewID != 0)
            {
                // Invia un ping per verificare che i client ricevano gli RPC
                pv.RPC("PingTest", PhotonTargets.Others);
                Debug.Log($"[MultiMax] Sent ping to clients via viewID {pv.viewID}");
            }
        }

        [PatchMethod("ReturnToOverworld")]
        [PatchPosition(Postfix)]
        public static void CleanupHandler()
        {
            if (_handlerInstance != null)
            {
                GameObject.Destroy(_handlerInstance);
                _handlerInstance = null;
                Debug.Log("[MultiMax] RPC handler destroyed after combat");
            }
            if (PhotonNetwork.isMasterClient && PhotonNetwork.room != null)
                PhotonNetwork.room.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { { "MM_RPCVID", 0 } });
        }
    }

    // ✅ Hide stance when locked
    [PatchType(typeof(uiBattleStanceButtons))]
    public static class HideStanceWhenLocked
    {
        [PatchMethod("Initialize")]
        [PatchPosition(Postfix)]
        public static void PostInit(uiBattleStanceButtons __instance, bool _refresh)
        {
            var cow = GameLogic.Instance?.GetCurrentCombatCOW();
            var actor = cow?.m_CurrentDummy;
            if (actor != null && (actor.Stunned || actor.Frozen || actor.Petrified))
            {
                __instance.gameObject.SetActive(false);
                Debug.Log("[MultiMax] Stance hidden (actor hard-locked)");
            }
        }
    }
}