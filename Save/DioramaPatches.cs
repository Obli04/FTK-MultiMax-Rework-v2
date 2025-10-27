using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FTK_MultiMax_Rework_v2.PatchHelpers;
using UnityEngine;
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
        public static void DummySlide()
        {
            DummyAttackSlide[] attackSlides = Object.FindObjectsOfType<DummyAttackSlide>();
            foreach (DummyAttackSlide dummyAttackSlide in attackSlides)
            {

                // 1000 Seems like a lot... The default value is 3 for god's sake
                // [Polars Bear] TODO: Fix
                if (dummyAttackSlide.m_Distances.Length < 1000)
                {
                    float[] newDistances = new float[1000];

                    Array.Copy(dummyAttackSlide.m_Distances, newDistances, dummyAttackSlide.m_Distances.Length);

                    dummyAttackSlide.m_Distances = newDistances;

                    Log(dummyAttackSlide.m_Distances);
                }
            }
        }

        [PatchMethod("SetupTargets")]
        [PatchPosition(Postfix)]
        public static void SortTargets(ref List<Transform> _targetList)
        {
            // Lazy solution alert
            // Run it enough times to guarantee proper sorting
            for (int j = 0; j < GameFlowMC.gMaxPlayers; j++)
            {
                // WARN: hmmm... if they aren't grouped up, this could cause issues... NAAHHHHH, not a big deal
                for (int i = 0; i < _targetList.Count - 1; i++)
                {
                    Transform target = _targetList[i];
                    if (!target.name.Contains("Player "))
                        continue;

                    Transform next = _targetList[i + 1];

                    if (!next.name.Contains("Player "))
                        continue;

                    // Unsafe...
                    var targetIndex = int.Parse(Regex.Match(target.name, "\\d+").Value);
                    var nextIndex = int.Parse(Regex.Match(next.name, "\\d+").Value);

                    // Crescent: 1-2-3-4-5-...
                    if (targetIndex > nextIndex)
                    {
                        _targetList[i] = next;
                        _targetList[i + 1] = target;
                    }
                }
            }
        }
    }
}