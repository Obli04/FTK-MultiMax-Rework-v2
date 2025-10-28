using FTK_MultiMax_Rework.PatchHelpers;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using static FTK_MultiMax_Rework.Main;
using static FTK_MultiMax_Rework.PatchHelpers.PatchPositions;

namespace FTK_MultiMax_Rework.PatchHelpers
{
    // ✅ Sistema di ownership custom per 4+ giocatori
    public static class OwnershipManager
    {
        // Mappa PhotonID → lista di TurnIndex controllati
        private static Dictionary<int, HashSet<int>> _ownershipMap = new Dictionary<int, HashSet<int>>();
        private static bool _initialized = false;

        public static void Initialize()
        {
            try
            {
                _ownershipMap.Clear();
                _initialized = false;

                var flow = GameFlowMC.Instance;
                if (flow?.m_IngamePlayerIDs == null)
                {
                    Debug.LogWarning("[MultiMax] Cannot initialize ownership: no IngamePlayerIDs");
                    return;
                }

                Debug.Log($"[MultiMax] Building ownership map for {flow.m_IngamePlayerIDs.Count} players");

                // Costruisci la mappa: ogni PhotonID controlla quali TurnIndex
                foreach (var pid in flow.m_IngamePlayerIDs)
                {
                    if (!_ownershipMap.ContainsKey(pid.m_PhotonID))
                    {
                        _ownershipMap[pid.m_PhotonID] = new HashSet<int>();
                    }
                    _ownershipMap[pid.m_PhotonID].Add(pid.m_TurnIndex);
                }

                // Log della mappa
                foreach (var kv in _ownershipMap)
                {
                    Debug.Log($"[MultiMax] PhotonID {kv.Key} controls TurnIndices: {kv.Value}");
                }

                _initialized = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] Initialize ownership error: {e}");
            }
        }

        // ✅ Verifica se un TurnIndex appartiene al client locale
        public static bool IsMine(int turnIndex)
        {
            if (!_initialized)
            {
                Debug.LogWarning("[MultiMax] Ownership map not initialized, forcing init");
                Initialize();
            }

            int myPhotonID = PhotonNetwork.player?.ID ?? -1;
            if (myPhotonID < 0)
            {
                Debug.LogWarning("[MultiMax] IsMine: invalid PhotonID");
                return false;
            }

            if (_ownershipMap.TryGetValue(myPhotonID, out var myIndices))
            {
                bool result = myIndices.Contains(turnIndex);
                Debug.Log($"[MultiMax] IsMine({turnIndex}): PhotonID={myPhotonID}, mine={myIndices}, result={result}");
                return result;
            }
                
            Debug.LogWarning($"[MultiMax] IsMine: PhotonID {myPhotonID} not in ownership map");
            return false;
        }

        // ✅ Ottieni tutti i TurnIndex che controllo
        public static HashSet<int> GetMyTurnIndices()
        {
            if (!_initialized) Initialize();

            int myPhotonID = PhotonNetwork.player?.ID ?? -1;
            if (myPhotonID < 0) return new HashSet<int>();

            if (_ownershipMap.TryGetValue(myPhotonID, out var myIndices))
            {
                return new HashSet<int>(myIndices);
            }

            return new HashSet<int>();
        }
    }
}
