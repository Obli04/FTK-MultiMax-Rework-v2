using FTK_MultiMax_Rework_v2.PatchHelpers;
using static FTK_MultiMax_Rework_v2.PatchHelpers.PatchPositions;

namespace FTK_MultiMax_Rework_v2.Patches
{
    [PatchType(typeof(uiPortraitHolderManager))]
    public class uiPortraitHolderManagerPatches
    {
        [PatchMethod("Create")]
        [PatchPosition(Postfix)]
        [PatchParams(typeof(HexLand))]
        public static void AddMorePlayersToUI(ref uiPortraitHolder __result) {
            int currentCount = __result.m_PortraitActionPoints.Count;

            for (int i = currentCount; i < GameFlowMC.gMaxPlayers; i++) {
                uiPortraitActionPoint newActionPoint = UnityEngine.Object.Instantiate(
                    __result.m_PortraitActionPoints[currentCount - 1],
                    __result.m_PortraitActionPoints[currentCount - 1].transform.parent
                );
                __result.m_PortraitActionPoints.Add(newActionPoint);
            }
        }
    }
}