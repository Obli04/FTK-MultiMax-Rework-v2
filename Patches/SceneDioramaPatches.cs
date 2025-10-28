#nullable enable
using FTK_MultiMax_Rework.PatchHelpers;
using UnityEngine;
using static FTK_MultiMax_Rework.Main;
using static FTK_MultiMax_Rework.PatchHelpers.PatchPositions;

namespace FTK_MultiMax_Rework.Patches;

[PatchType(typeof(SceneDiorama))]
public class SceneDioramaPatches
{
    [PatchMethod("Awake")]
    [PatchPosition(Postfix)]
    public static void FixDummyPositions(SceneDiorama __instance)
    {
        foreach (var diorama in __instance.GetComponentsInChildren<Diorama>())
        {
            if (diorama)
                FixDiorama(diorama);
        }
    }

    private static void FixDiorama(Diorama diorama)
    {
        Log($"Fixing dummy positions for {diorama.name}");
        foreach (var layout in diorama.m_LayoutTable.Values)
        {
            Transform root = layout.m_TargetRoot;
            TargetPositions.Fix(root);
        }
    } 
}