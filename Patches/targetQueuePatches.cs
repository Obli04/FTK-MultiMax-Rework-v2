using System.Collections.Generic;
using FTK_MultiMax_Rework_v2.PatchHelpers;
using UnityEngine;
using System;
using HarmonyLib;
using System.Reflection;
using static FTK_MultiMax_Rework_v2.Main;
using static FTK_MultiMax_Rework_v2.PatchHelpers.PatchPositions;

namespace FTK_MultiMax_Rework_v2.Patches
{
    [PatchType(typeof(Diorama))]
    public static class DioramaGetQueuePatch
    {
        [PatchMethod("_getTargetQueue")]
        [PatchPosition(Prefix)]
        public static bool ExpandTargetQueue(
            ref Diorama __instance,
            bool _isPlayer,
            List<Transform> _targetList,
            int _count,
            ref List<Transform> _queue,
            ref Transform _center,
            out Vector3 _centerPos)
        {
            _centerPos = Vector3.zero;

            if (!Application.isPlaying || __instance == null)
                return true;

            try
            {
                if (_targetList == null || _targetList.Count == 0)
                {
                    Log("[MultiMax] Target list vuota o non inizializzata.");
                    return true; // lascia l’originale
                }

                _queue?.Clear();

                int available = _targetList.Count;
                int num = 0;

                if (_isPlayer && _count == 1)
                    num = 1;
                else if (_count == 2)
                    num = 3;

                if (num >= available)
                    num = 0;

                if (num + _count > available)
                {
                    int newCount = Mathf.Max(1, available - num);
                    Log($"[MultiMax] Adjusting target count {_count} → {newCount} (offset {num})");
                    _count = newCount;
                }

                for (int i = 0; i < _count; i++)
                {
                    int idx = num + i;
                    if (idx >= available)
                        break;

                    Transform t = _targetList[idx];
                    if (t == null) continue;

                    if (i == 0) _center = t;
                    _queue.Add(t);
                }

                // Riempi se necessario
                while (_queue.Count < GameFlowMC.gMaxEnemies && _queue.Count > 0)
                    _queue.Add(_queue[_queue.Count - 1]);

                // Calcola il centro
                if (_queue.Count > 0)
                {
                    Vector3 sum = Vector3.zero;
                    foreach (var t in _queue)
                        sum += t.position;
                    _centerPos = sum / _queue.Count;
                }

                return false;
            }
            catch (System.Exception e)
            {
                Log($"[MultiMax] ExpandTargetQueue exception: {e}");
                return true; // fallback al vanilla
            }
        }
    }
    
    [PatchType(typeof(EncounterSessionMC))]
    public static class ReadyPhase_SafeAliveListPatch
    {
        
        [PatchMethod("InitiateCurrentEncounter")]
        [PatchPosition(Postfix)]        
       public static void initiateEncounter(EncounterSessionMC __instance)
        {
            try
            {
                var allAlive = __instance.m_AllCombtatantsAlive; // public [NonSerialized]
                if (allAlive == null) return;

                var trimmed = new List<FTKPlayerID>(allAlive.Count);
                foreach (var fid in allAlive)
                {
                    var cow = FTKHub.Instance.GetCharacterOverworldByFID(fid);
                    if (cow != null && !cow.m_WaitForRespawn && fid.IsPlayer())
                        trimmed.Add(fid);
                }

                // Only replace if something changed (reduces churn)
                if (trimmed.Count != allAlive.Count)
                {
                    allAlive.Clear();
                    allAlive.AddRange(trimmed);
                }
            }
            catch (Exception e)
            {
                Main.Log($"[MultiMax][ReadyFix] SafeAliveListPatch error: {e}");
            }
        }
    } 
}
