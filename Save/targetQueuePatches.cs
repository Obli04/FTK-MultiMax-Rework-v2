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
            Diorama __instance,
            bool _isPlayer,
            List<Transform> _targetList,
            int _count,
            ref List<Transform> _queue,
            ref Transform _center,
            out Vector3 _centerPos)
        {
            _centerPos = Vector3.zero;

            // ✅ Skip safely if nothing to target
            if (_targetList == null || _targetList.Count == 0 || _count <= 0)
            {
                _queue = _queue ?? new List<Transform>();
                _queue.Clear();
                _center = null;
                _centerPos = Vector3.zero;
                return false; // skip vanilla safely — avoids IndexOutOfRange
            }

            if (!Application.isPlaying || __instance == null)
                return true;

            try
            {
                if (_queue == null) _queue = new List<Transform>(_count);
                else _queue.Clear();

                int available = _targetList.Count;
                int startIndex = 0;

                if (_isPlayer && _count == 1)
                    startIndex = 1;
                else if (_count == 2)
                    startIndex = 0;

                if (startIndex + _count > available)
                    _count = Mathf.Clamp(available - startIndex, 0, available);

                for (int i = 0; i < _count; i++)
                {
                    int idx = startIndex + i;
                    if (idx >= available) break;

                    var t = _targetList[idx];
                    if (t == null) continue;

                    if (i == 0) _center = t;
                    _queue.Add(t);
                }

                // ✅ Fill remainder if necessary to prevent underflows
                while (_queue.Count < Mathf.Max(1, GameFlowMC.gMaxEnemies) && _queue.Count > 0)
                    _queue.Add(_queue[_queue.Count - 1]);

                // ✅ Calculate center safely
                if (_queue.Count > 0)
                {
                    Vector3 sum = Vector3.zero;
                    foreach (var t in _queue)
                        sum += t.position;
                    _centerPos = sum / _queue.Count;
                }

                return false; // use our safe custom queue
            }
            catch (Exception e)
            {
                Log($"[MultiMax] ExpandTargetQueue exception: {e}");
                return true; // fallback to vanilla if anything breaks
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
