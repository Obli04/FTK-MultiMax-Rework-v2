using System;
using UnityEngine;
using FTK_MultiMax_Rework_v2.PatchHelpers;
using static FTK_MultiMax_Rework_v2.PatchHelpers.PatchPositions;

namespace FTK_MultiMax_Rework_v2.Patches
{
    [PatchType(typeof(DummyAttackSlide))]
    public class DummyAttackSlideExpandPatch
    {
        [PatchMethod("SetMidPoint")]
        [PatchPosition(Prefix)]
        public static void ExpandDistances(ref DummyAttackSlide __instance)
        {
            if (__instance.m_Distances == null || __instance.m_Distances.Length < GameFlowMC.gMaxPlayers)
            {
                float[] old = __instance.m_Distances;
                __instance.m_Distances = new float[GameFlowMC.gMaxPlayers];

                if (old != null)
                    Array.Copy(old, __instance.m_Distances, Math.Min(old.Length, __instance.m_Distances.Length));

                Debug.Log($"[MultiMax] Expanded DummyAttackSlide distances → {__instance.m_Distances.Length}");
            }
        }
    }
}
