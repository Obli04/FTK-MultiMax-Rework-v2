using System.Collections.Generic;
using FTK_MultiMax_Rework.PatchHelpers;
using UnityEngine;
using static FTK_MultiMax_Rework.PatchHelpers.PatchPositions;

namespace FTK_MultiMax_Rework.Patches
{
    [PatchType(typeof(uiActiveTimings))]
    public static class ExpandBattleStanceUI
    {
        [PatchMethod("Init")]
        [PatchPosition(Postfix)]
        public static void ExpandUIArrays(uiActiveTimings __instance)
        {
            try
            {
                int desiredPlayers = Mathf.Min(GameFlowMC.gMaxPlayers, 5);
                if (__instance.m_uiBattleStance.Length >= desiredPlayers)
                    return;

                var newArray = new uiBattleStance[desiredPlayers];
                for (int i = 0; i < __instance.m_uiBattleStance.Length; i++)
                    newArray[i] = __instance.m_uiBattleStance[i];

                __instance.m_uiBattleStance = newArray;
                Debug.Log($"[MultiMax] Expanded battle stance UI array to {desiredPlayers}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MultiMax] ExpandBattleStanceUI error: {e}");
            }
        }
    }

    [PatchType(typeof(EncounterSessionMC))]
    public static class EnsureValidEnemyBeforeUI
    {
        [PatchMethod("CommenceBattleRPC")]
        [PatchPosition(Postfix)]
        public static void FixCurrentEnemy()
        {
            try
            {
                var enc = EncounterSession.Instance;
                if (enc == null || enc.m_EnemyDummies == null || enc.m_EnemyDummies.Count == 0)
                    return;

                var currentEnemy = enc.GetCurrentEnemy();
                if (currentEnemy == null || !enc.m_EnemyDummies.ContainsKey(enc.m_CurrentEnemy))
                {
                    var firstAlive = enc.m_EnemyDummies.Values.FirstOrDefault(e => e != null && e.m_IsAlive && e.m_CurrentHealth > 0);
                    if (firstAlive != null)
                    {
                        enc.m_CurrentEnemy = firstAlive.FID;
                        Debug.Log($"[MultiMax] Fixed current enemy before UI init → {firstAlive.name}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] EnsureValidEnemyBeforeUI error: {e}");
            }
        }
    }
}
