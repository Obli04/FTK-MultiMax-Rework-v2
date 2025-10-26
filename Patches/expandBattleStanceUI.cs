using System.Collections.Generic;
using FTK_MultiMax_Rework_v2.PatchHelpers;
using UnityEngine;
using static FTK_MultiMax_Rework_v2.PatchHelpers.PatchPositions;

namespace FTK_MultiMax_Rework_v2.Patches
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
}
