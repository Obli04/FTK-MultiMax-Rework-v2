using System.Linq;
using FTK_MultiMax_Rework.PatchHelpers;
using static FTK_MultiMax_Rework.PatchHelpers.PatchPositions;
using static uiPopupMenu;

namespace FTK_MultiMax_Rework.Patches {
    [PatchType(typeof(uiPopupMenu))]
    public class uiPopupMenuPatches {
        [PatchMethod("Awake")]
        [PatchPosition(Prefix)]
        public static void PopupAwake(uiPopupMenu __instance) {
            if (!__instance || __instance.m_Popups == null) {
                return;
            }

            PopupButton givePopup = __instance.m_Popups.FirstOrDefault(popup => popup.m_Action == Action.Give);

            if (givePopup != null) {
                givePopup.m_Count = GameFlowMC.gMaxPlayers - 1;
            }
        }

    }

}