using FTK_MultiMax_Rework.PatchHelpers;
using static FTK_MultiMax_Rework.PatchHelpers.PatchPositions;
using UnityEngine;
using System;
using static FTK_MultiMax_Rework.Main;

namespace FTK_MultiMax_Rework.Patches
{
    [PatchType(typeof(uiCharacterCreateRoot))]
    public class uiCharacterCreateRootPatches
    {
        [PatchMethod("Start")]
        [PatchPosition(Prefix)]
        public static void ExpandCharacterCreateSlots(ref uiCharacterCreateRoot __instance)
        {
            try
            {
                int target = GameFlowMC.gMaxPlayers;

                if (__instance.m_CreateUITargets == null)
                {
                    Log("[MultiMax] m_CreateUITargets is null — skipping expansion");
                    return;
                }

                if (__instance.m_CreateUITargets.Length >= target)
                {
                    Log("[MultiMax] Slots already " + __instance.m_CreateUITargets.Length + ", skipping expansion");
                    return;
                }

                Log("[MultiMax] Expanding character create slots to " + target);

                Transform[] oldUITargets = __instance.m_CreateUITargets;
                Transform[] oldCamTargets = SelectScreenCamera.Instance.m_PlayerTargets;

                Transform uiLast = oldUITargets[oldUITargets.Length - 1];
                Transform camLast = oldCamTargets[oldCamTargets.Length - 1];

                Transform[] newUITargets = new Transform[target];
                Transform[] newCamTargets = new Transform[target];

                int i;
                for (i = 0; i < target; i++)
                {
                    if (i < oldUITargets.Length)
                    {
                        newUITargets[i] = oldUITargets[i];
                    }
                    else
                    {
                        newUITargets[i] = UnityEngine.Object.Instantiate(uiLast, uiLast.parent);
                        newUITargets[i].name = "Player " + (i + 1) + "_UITarget";
                    }

                    if (i < oldCamTargets.Length)
                    {
                        newCamTargets[i] = oldCamTargets[i];
                    }
                    else
                    {
                        newCamTargets[i] = UnityEngine.Object.Instantiate(camLast, camLast.parent);
                        newCamTargets[i].name = "Player " + (i + 1) + "_CameraTarget";
                    }
                }

                __instance.m_CreateUITargets = newUITargets;
                SelectScreenCamera.Instance.m_PlayerTargets = newCamTargets;

                for (i = 0; i < newUITargets.Length; i++)
                {
                    RectTransform rect = newUITargets[i].GetComponent<RectTransform>();
                    if (rect != null)
                    {
                        float t = (newUITargets.Length > 1) ? (i / (float)(newUITargets.Length - 1)) : 0f;
                        rect.anchoredPosition = new Vector2(Mathf.Lerp(-550f, 550f, t), 129f);
                    }
                }

                Vector3 left = newCamTargets[0].position;
                int rightIndex = (newCamTargets.Length >= 3) ? 2 : (newCamTargets.Length - 1);
                if (rightIndex < 0) rightIndex = 0;
                Vector3 right = newCamTargets[rightIndex].position;

                for (i = 0; i < newCamTargets.Length; i++)
                {
                    float t = (newCamTargets.Length > 1) ? (i / (float)(newCamTargets.Length - 1)) : 0f;
                    newCamTargets[i].position = Vector3.Lerp(left, right, t);
                }

                Log("[MultiMax] Expanded slots: " + __instance.m_CreateUITargets.Length);
            }
            catch (Exception e)
            {
                Log("[MultiMax] Error expanding uiCharacterCreateRoot: " + e);
            }
        }
    }
}
