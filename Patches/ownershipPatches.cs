using System;
using System.Collections.Generic;
using UnityEngine;

namespace FTK_MultiMax_Rework.PatchHelpers
{
    public static class OwnershipManager
    {
        private static Dictionary<int, HashSet<int>> _ownershipMap = new Dictionary<int, HashSet<int>>();
        private static bool _initialized = false;

        // ✅ Inizializza DOPO che CommenceBattleRPC è stato chiamato
        public static void Initialize()
        {
            try
            {
                _ownershipMap.Clear();
                _initialized = false;

                var enc = EncounterSession.Instance;
                if (enc?.m_PlayerDummies == null)
                {
                    Debug.LogWarning("[MultiMax] Cannot initialize ownership: no PlayerDummies");
                    return;
                }

                int myPhotonID = PhotonNetwork.player?.ID ?? -1;
                if (myPhotonID < 0)
                {
                    Debug.LogWarning("[MultiMax] Invalid local PhotonID");
                    return;
                }

                // ✅ Usa PlayerDummies invece di GameFlowMC (più affidabile)
                var myIndices = new HashSet<int>();
                foreach (var kv in enc.m_PlayerDummies)
                {
                    var dummy = kv.Value;
                    if (dummy == null) continue;

                    if (!_ownershipMap.ContainsKey(dummy.FID.m_PhotonID))
                    {
                        _ownershipMap[dummy.FID.m_PhotonID] = new HashSet<int>();
                    }
                    _ownershipMap[dummy.FID.m_PhotonID].Add(dummy.FID.m_TurnIndex);

                    if (dummy.FID.m_PhotonID == myPhotonID)
                    {
                        myIndices.Add(dummy.FID.m_TurnIndex);
                    }
                }

                Debug.Log($"[MultiMax] Building ownership map:");
                foreach (var kv in _ownershipMap)
                {
                    Debug.Log($"[MultiMax]   PhotonID {kv.Key} controls TurnIndices: {kv.Value}");
                }
                Debug.Log($"[MultiMax] My PhotonID={myPhotonID}, My TurnIndices: {myIndices}");

                _initialized = true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] Initialize ownership error: {e}");
            }
        }

        public static bool IsMine(int turnIndex)
        {
            if (!_initialized)
            {
                Debug.LogWarning("[MultiMax] Ownership map not initialized, forcing init");
                Initialize();
            }

            int myPhotonID = PhotonNetwork.player?.ID ?? -1;
            if (myPhotonID < 0) return false;

            if (_ownershipMap.TryGetValue(myPhotonID, out var myIndices))
            {
                bool result = myIndices.Contains(turnIndex);
                Debug.Log($"[MultiMax] IsMine({turnIndex}): PhotonID={myPhotonID}, result={result}");
                return result;
            }

            Debug.LogWarning($"[MultiMax] IsMine: PhotonID {myPhotonID} not in ownership map");
            return false;
        }

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