using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI; // ← REQUIRED for LayoutElement, Slider, GridLayoutGroup
using FTK_MultiMax_Rework_v2.PatchHelpers;
using static FTK_MultiMax_Rework_v2.Main;
using static FTK_MultiMax_Rework_v2.PatchHelpers.PatchPositions;
using Object = UnityEngine.Object;

namespace FTK_MultiMax_Rework_v2.Patches
{
    [PatchType(typeof(uiEnemyHUD))]
    public static class uiEnemyHUDPatch
    {
        [PatchMethod("InitializeEnemyHud")]
        [PatchPosition(Prefix)]
        public static void SafeExpandHUDArray(uiEnemyHUD __instance, EnemyDummy _ed, ref int _index)
        {
            try
            {
                int desired = Mathf.Max(GameFlowMC.gMaxEnemies, 3);
                var arr = __instance.m_EachEnemyHuds;

                if (arr == null || arr.Length < desired)
                {
                    var newArr = new uiEachEnemyHud[desired];
                    if (arr != null)
                        for (int i = 0; i < arr.Length; i++)
                            newArr[i] = arr[i];

                    for (int i = arr?.Length ?? 0; i < desired; i++)
                    {
                        var newHud = Object.Instantiate(__instance.m_EachEnemyHudsPrefab, __instance.m_GridRow);
                        newHud.gameObject.SetActive(false);
                        newArr[i] = newHud;
                    }

                    __instance.m_EachEnemyHuds = newArr;
                    Log($"[MultiMax] Expanded HUD array → {desired}");
                }

                if (_ed != null)
                {
                    _index = Mathf.Clamp(_ed.FID.m_TurnIndex, 0, __instance.m_EachEnemyHuds.Length - 1);
                    Log($"[MultiMax] HUD index for {_ed.name}: {_index}");
                }
            }
            catch (Exception e)
            {
                Log($"[MultiMax] uiEnemyHUDPatch error: {e}");
            }
        }
        [PatchMethod("InitializeEnemyHud")]
        [PatchPosition(Postfix)]
        public static void RebuildHUDLayout(uiEnemyHUD __instance)
        {
            try
            {
                var enc = EncounterSession.Instance;
                if (enc == null || enc.m_EnemyDummies == null || __instance.m_EachEnemyHuds == null)
                    return;

                // 1) Lower the whole HUD row and make sure it renders above world, below menus
                var rootRect = __instance.GetComponent<RectTransform>();
                if (rootRect != null)
                {
                    rootRect.anchoredPosition = new Vector2(0f, -115f);
                }

                var canvas = __instance.GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    canvas.overrideSorting = true;
                    canvas.sortingOrder = 5; // UI > world, < menus
                }

                // 2) Determine correct visual order: left→right by diorama index (fallback to turn index)
                var enemies = enc.m_EnemyDummies.Values
                .Where(e => e != null)
                .OrderBy(e => e.m_DioramaTargetIndex)   // ascending is correct
                .ToList();


                __instance.m_EnemyHudDictionary.Clear();
                // 3) Clear any old layout groups and add a single one
                var row  = __instance.m_GridRow;
                var huds = __instance.m_EachEnemyHuds.Where(h => h != null).ToList();
                int n = Mathf.Min(enemies.Count, huds.Count);
                if (row == null) return;

                // Remove all layout groups first
                var oldGrid = row.GetComponent<GridLayoutGroup>();
                if (oldGrid) Object.DestroyImmediate(oldGrid);
                var oldHLG = row.GetComponent<HorizontalLayoutGroup>();
                if (oldHLG) Object.DestroyImmediate(oldHLG);

                // Add one clean HLayoutGroup
                var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 250f; // increase for more distance
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.childControlWidth = false;
                hlg.childControlHeight = false;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;

                // Rebuild HUDs
                __instance.m_EnemyHudDictionary.Clear();
                // Correct left-to-right alignment
                for (int i = 0; i < n; i++)
                {
                    var dummy = enemies[i];
                    var hud   = huds[i]; // 1:1 index, not reversed

                    hud.gameObject.SetActive(true);
                    hud.transform.SetParent(row, false);
                    hud.transform.SetSiblingIndex(i);
                    __instance.m_EnemyHudDictionary[dummy] = hud;

                    var rt = hud.GetComponent<RectTransform>();
                    if (rt != null)
                    {
                        rt.localScale = Vector3.one;
                        rt.localRotation = Quaternion.identity;
                        rt.anchoredPosition = Vector2.zero;
                    }
                }

                // flip row visually (horizontal inversion)
                var rect = row.GetComponent<RectTransform>();
                rect.localScale = new Vector3(-1, 1, 1);
                foreach (Transform child in row)
                    child.localScale = new Vector3(-1, 1, 1);


                LayoutRebuilder.ForceRebuildLayoutImmediate(row.GetComponent<RectTransform>());
                Log($"[MultiMax] HUD rebuilt cleanly: {n} enemies, spacing={hlg.spacing}, y=-115");
            }
            catch (Exception e)
            {
                Log($"[MultiMax] RebuildHUDLayout error: {e}");
            }
        }
    }
}
