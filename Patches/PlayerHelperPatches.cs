using FTK_MultiMax_Rework.PatchHelpers;
using static FTK_MultiMax_Rework.PatchHelpers.PatchPositions;
using Rewired;

namespace FTK_MultiMax_Rework.Patches
{
    [PatchType(typeof(ReInput.PlayerHelper))]
    public class PlayerHelperPatches
    {
        [PatchMethod("GetPlayer")]
        [PatchPosition(Prefix)]
        [PatchParams(typeof(int))]
        public static bool FixRewire(int playerId, ref Player __result) {
            if (playerId < ReInput.players.playerCount) {
                return true;
            }
            __result = ReInput.players.GetPlayer(2);
            return false;
        }
    }
}