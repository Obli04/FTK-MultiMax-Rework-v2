using System.Collections.Generic;
using FTK_MultiMax_Rework_v2.PatchHelpers;
using static FTK_MultiMax_Rework_v2.PatchHelpers.PatchPositions;
using HarmonyLib;

namespace FTK_MultiMax_Rework_v2.Patches {
    [PatchType(typeof(uiGoldMenu))]
    public class uiGoldMenuPatches {
        [PatchMethod("Awake")]
        [PatchPosition(Prefix)]
        public static bool GoldAwake(uiGoldMenu __instance) {
            var goldEntriesField = Traverse.Create(__instance).Field("m_GoldEntries");

            __instance.m_InputFocus = __instance.gameObject.GetComponent<FTKInputFocus>();
            __instance.m_InputFocus.m_InputMode = FTKInput.InputMode.InGameUI;
            __instance.m_InputFocus.m_Cancel = __instance.OnButtonCancel;

            if (goldEntriesField.GetValue() != null) {
                int maxEntries = (GameFlowMC.gMaxPlayers - 2);

                goldEntriesField.GetValue<List<uiGoldMenuEntry>>().Add(__instance.m_FirstEntry);

                for (int i = 0; i < maxEntries; i++) {
                    uiGoldMenuEntry newEntry = UnityEngine.Object.Instantiate(__instance.m_FirstEntry, 
                        __instance.m_FirstEntry.transform.parent, 
                        false);
                    
                    goldEntriesField.GetValue<List<uiGoldMenuEntry>>().Add(newEntry);
                }
            }

            return false;
        }
    }
}
