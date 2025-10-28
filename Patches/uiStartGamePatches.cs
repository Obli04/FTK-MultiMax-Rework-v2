using FTK_MultiMax_Rework.PatchHelpers;
using static FTK_MultiMax_Rework.PatchHelpers.PatchPositions;

namespace FTK_MultiMax_Rework.Patches
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