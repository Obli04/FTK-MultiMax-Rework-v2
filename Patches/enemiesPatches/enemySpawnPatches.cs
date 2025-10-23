using System.Collections.Generic;
using FTK_MultiMax_Rework_v2.PatchHelpers;
using static FTK_MultiMax_Rework_v2.Main;
using UnityEngine;
using System.Collections;
using System.Reflection;
using static FTK_MultiMax_Rework_v2.PatchHelpers.PatchPositions;


namespace FTK_MultiMax_Rework_v2.Patches
{
    [PatchType(typeof(EncounterSession))]
    public class fixEnemyNumbers
    {
        [PatchMethod("InitEnemyDummiesForCombat")]
        [PatchPosition(Prefix)]
        public static void ExpandEnemyTypes(ref string[] _enemyTypes)
        {
            int target = Mathf.Max(3, GameFlowMC.gMaxEnemies);

            if (_enemyTypes == null || _enemyTypes.Length == 0)
                return;

            if (_enemyTypes.Length >= target)
                return;

            List<string> list = new List<string>(_enemyTypes);
            while (list.Count < target)
            {
                string copy = list[list.Count % _enemyTypes.Length];
                list.Add(copy);
            }

            _enemyTypes = list.ToArray();
            Log("[MultiMax] Expanded enemyTypes to " + _enemyTypes.Length + ": " + string.Join(", ", _enemyTypes));
        }
    }
    
    [PatchType(typeof(EnemyDummy))]
    public static class EnemyAttackIndexResetPatch
    {
        [PatchMethod("SetAttackDecision")]
        [PatchPosition(Prefix)]
        public static void SafeResetIndex(ref EnemyDummy __instance)
        {
            var schedule = __instance.m_AttackSchedule;
            if (schedule == null) return;

            // Reflect m_AttackTypes and m_CurrentAttackIndex safely
            var fldList = schedule.GetType().GetField("m_AttackTypes",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            var fldIndex = schedule.GetType().GetField("m_CurrentAttackIndex",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

            if (fldList == null || fldIndex == null) return;

            var list = fldList.GetValue(schedule) as System.Collections.IList;
            if (list == null || list.Count == 0) return;

            int cur = (int)fldIndex.GetValue(schedule);
            if (cur >= list.Count)
            {
                fldIndex.SetValue(schedule, 0);
                Log($"[MultiMax] Reset bad attack index for {__instance.name}");
            }
        }
    }
}

