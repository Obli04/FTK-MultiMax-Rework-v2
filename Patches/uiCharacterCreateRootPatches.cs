using FTK_MultiMax_Rework_v2.PatchHelpers;
using static FTK_MultiMax_Rework_v2.PatchHelpers.PatchPositions;
using UnityEngine;
using static FTK_MultiMax_Rework_v2.Main;

namespace FTK_MultiMax_Rework_v2.Patches
{
    [PatchType(typeof(uiCharacterCreateRoot))]
    public class uiCharacterCreateRootPatches
    {
        [PatchMethod("Start")]
        [PatchPosition(Prefix)]
        public static void AddMorePlayerSlotsInMenu(ref uiCharacterCreateRoot __instance) {
            Log(__instance);
            Log(__instance.m_CreateUITargets);
            Log(SelectScreenCamera.Instance.m_PlayerTargets.Length);

            // These variables are a crime
            
            if (__instance.m_CreateUITargets.Length < GameFlowMC.gMaxPlayers) {
                Transform[] array = new Transform[GameFlowMC.gMaxPlayers];
                Transform[] array2 = new Transform[GameFlowMC.gMaxPlayers];
                
                Vector3 position = SelectScreenCamera.Instance.m_PlayerTargets[0].position;
                Vector3 position2 = SelectScreenCamera.Instance.m_PlayerTargets[2].position;
                
                for (int i = 0; i < GameFlowMC.gMaxPlayers; i++) {
                    if (i < __instance.m_CreateUITargets.Length) {
                        array[i] = __instance.m_CreateUITargets[i];
                        array2[i] = SelectScreenCamera.Instance.m_PlayerTargets[i];
                    } else {
                        array[i] = Object.Instantiate(array[i - 1], array[i - 1].parent);
                        array2[i] = Object.Instantiate(array2[i - 1], array2[i - 1].parent);
                    }
                }
                
                __instance.m_CreateUITargets = array;
                SelectScreenCamera.Instance.m_PlayerTargets = array2;
                for (int j = 0; j < __instance.m_CreateUITargets.Length; j++) {
                    __instance.m_CreateUITargets[j].GetComponent<RectTransform>().anchoredPosition = new Vector2(Mathf.Lerp(-550f, 550f, j / (float)(__instance.m_CreateUITargets.Length - 1)), 129f);
                }
                
                for (int k = 0; k < SelectScreenCamera.Instance.m_PlayerTargets.Length; k++) {
                    SelectScreenCamera.Instance.m_PlayerTargets[k].position = Vector3.Lerp(position, position2, k / (float)(SelectScreenCamera.Instance.m_PlayerTargets.Length - 1));
                }
            }
            Log("Slot Count: " + __instance.m_CreateUITargets.Length);
        }
    }
}