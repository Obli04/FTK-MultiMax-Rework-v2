using FTK_MultiMax_Rework_v2.PatchHelpers;
using static FTK_MultiMax_Rework_v2.PatchHelpers.PatchPositions;

namespace FTK_MultiMax_Rework_v2.Patches
{
    [PatchType(typeof(uiStartGame))]
    public class uiStartGamePatches
    {
        [PatchMethod("StartGame")]
        [PatchPosition(Prefix)]
        public static void RecreateDummies()
        {
            DummiesHandler.CreateDummies();
        }
    }
}