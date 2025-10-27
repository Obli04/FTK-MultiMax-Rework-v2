using FTK_MultiMax_Rework_v2.PatchHelpers;
using static FTK_MultiMax_Rework_v2.PatchHelpers.PatchPositions;
using UnityEngine;

namespace FTK_MultiMax_Rework_v2.Patches
{
    [PatchType(typeof(uiPlayerMainHud))]
    public class uiPlayerMainHudPatches
    {
        [PatchMethod("Update")]
        [PatchPosition(Prefix)]
        public static void PlaceUI(ref uiPlayerMainHud __instance) {
            int turnIndex = __instance.m_Cow.m_FTKPlayerID.TurnIndex;
            int gMaxPlayers = GameFlowMC.gMaxPlayers;
            
            // Whoever wrote this does not know how to name variables, had to change a bunch of "num" variables, and still didn't get them all
            // I'm guessing it's a modified version of some dnSpy decompiled code
            // [Polars Bear] - TODO: Fix bad naming
            float delta = 725f;
            float distance = 2 * delta;
            
            RectTransform rectTransform = __instance.GetComponent<RectTransform>();
            float num4 = rectTransform.rect.width - 220f;
            float num5 = distance / gMaxPlayers;
            float num6 = Mathf.Min(1f, num5 / num4);
            
            rectTransform.anchoredPosition = new Vector2(Mathf.Lerp(-delta, delta, turnIndex / (float)(gMaxPlayers - 1)), rectTransform.anchoredPosition.y);
            rectTransform.localScale = new Vector3(num6, num6, num6);
        }
    }
}