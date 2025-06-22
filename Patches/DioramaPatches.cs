using System;
using FTK_MultiMax_Rework_v2.PatchHelpers;
using JetBrains.Annotations;
using static FTK_MultiMax_Rework_v2.Main;
using static FTK_MultiMax_Rework_v2.PatchHelpers.PatchPositions;
using Object = UnityEngine.Object;

namespace FTK_MultiMax_Rework_v2.Patches
{
    [PatchType(typeof(Diorama))]
    public class DioramaPatches
    {
        [PatchMethod("_resetTargetQueue")]
        [PatchPosition(Prefix)]
        public static void DummySlide() {
            DummyAttackSlide[] attackSlides = Object.FindObjectsOfType<DummyAttackSlide>();
            foreach (DummyAttackSlide dummyAttackSlide in attackSlides) {
                
                if (dummyAttackSlide.m_Distances.Length < 1000) {
                    float[] newDistances = new float[1000];
                    
                    Array.Copy(dummyAttackSlide.m_Distances, newDistances, dummyAttackSlide.m_Distances.Length);
                    
                    dummyAttackSlide.m_Distances = newDistances;
                    
                    Log(dummyAttackSlide.m_Distances);
                }
            }
        }
    }
}