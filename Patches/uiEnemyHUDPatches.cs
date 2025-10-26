using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using FTK_MultiMax_Rework_v2.PatchHelpers;
using static FTK_MultiMax_Rework_v2.Main;
using static FTK_MultiMax_Rework_v2.PatchHelpers.PatchPositions;
using System.Linq;
using System.Reflection;
using Object = UnityEngine.Object;

namespace FTK_MultiMax_Rework_v2.Patches
{

    [PatchType(typeof(Diorama))]
    public static class DioramaEnemyLayoutPatch
    {
        [PatchMethod("_resetTargetQueue")]
        [PatchPosition(Postfix)]
        public static void OrderAndRebuildEnemyTargets(Diorama __instance)
        {
            try
            {
                if (__instance == null)
                    return;

                var encounter = EncounterSession.Instance;
                if (encounter?.m_EnemyDummies == null)
                    return;

                var targets = __instance.m_EnemyTargets;
                if (targets == null)
                    return;

                // Numero desiderato
                int desired = Mathf.Clamp(GameFlowMC.gMaxEnemies, 1, 5);
                int current = targets.Count;

                // Espandi se necessario
                if (current < desired)
                {
                    var last = targets.Last();
                    for (int i = current; i < desired; i++)
                    {
                        var clone = Object.Instantiate(last, last.parent);
                        clone.name = $"enemy {i + 1}_target";
                        targets.Add(clone);
                    }
                    Log($"[MultiMax] Expanded Diorama enemy targets {current} → {desired}");
                }

                // Ordina per TurnIndex (ordine logico HUD)
                targets = targets.OrderBy(t =>
                {
                    foreach (var kv in encounter.m_EnemyDummies)
                    {
                        if (kv.Value == null) continue;
                        if (t.name.Contains(kv.Value.name.Replace("(Clone)", "").Trim()))
                            return kv.Key.m_TurnIndex;
                    }
                    return 999;
                }).ToList();

                __instance.m_EnemyTargets = targets;

                // Aggiorna conteggio interno (usato per la spaziatura HUD)
                var bf = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                typeof(Diorama).GetField("m_DioramaEnemyCount", bf)?.SetValue(__instance, targets.Count);

                // Ricrea la queue privata
                var queueField = typeof(Diorama).GetField("m_EnemyTargetQueues", bf);
                if (queueField != null)
                {
                    var queue = new List<Transform>(targets);
                    queueField.SetValue(__instance, queue);
                    Log($"[MultiMax] Rebuilt m_EnemyTargetQueues ({queue.Count})");
                }

                // Spaziatura dinamica
                SpreadTargets(__instance.m_EnemyTargets);
                Log($"[MultiMax] Enemy targets reordered & spread ({desired})");
            }
            catch (Exception e)
            {
                Log($"[MultiMax] DioramaEnemyLayoutPatch error: {e}");
            }
        }

        private static void SpreadTargets(List<Transform> targets)
        {
            if (targets == null || targets.Count == 0)
                return;

            float spacing = targets.Count switch
            {
                3 => 2.2f,
                4 => 1.8f,
                5 => 1.5f,
                _ => 2.0f
            };

            // Calcola centro medio per mantenere simmetria
            Vector3 center = Vector3.zero;
            foreach (var t in targets)
                center += t.localPosition;
            center /= targets.Count;

            float start = -(targets.Count - 1) * spacing * 0.5f;
            for (int i = 0; i < targets.Count; i++)
            {
                var pos = targets[i].localPosition;
                pos.x = center.x + start + i * spacing;
                targets[i].localPosition = pos;
            }

            Log($"[MultiMax] Re-spaced {targets.Count} enemies (spacing {spacing})");
        }
    }
    [PatchType(typeof(uiEnemyHUD))]
    public static class uiEnemyHUDPatch
    {
        [PatchMethod("InitializeEnemyHud")]
        [PatchPosition(Prefix)]
        public static void SafeExpandHUDArray(uiEnemyHUD __instance, EnemyDummy _ed, ref int _index)
        {
            try
            {
                int desired = Mathf.Max(GameFlowMC.gMaxEnemies, 4);
                var arr = __instance.m_EachEnemyHuds;

                if (arr == null || arr.Length < desired)
                {
                    var newArr = new uiEachEnemyHud[desired];

                    if (arr != null)
                    {
                        for (int i = 0; i < arr.Length; i++)
                            newArr[i] = arr[i];
                    }

                    for (int i = arr?.Length ?? 0; i < desired; i++)
                    {
                        var newHud = UnityEngine.Object.Instantiate(
                            __instance.m_EachEnemyHudsPrefab,
                            __instance.m_GridRow);

                        newHud.gameObject.SetActive(false);
                        newArr[i] = newHud;
                    }

                    __instance.m_EachEnemyHuds = newArr;
                    Log($"[MultiMax] Expanded HUD array → {desired}");
                }

                // CRITICAL: Usa TurnIndex per allineamento HUD
                if (_ed != null)
                {
                    _index = _ed.FID.m_TurnIndex;
                    _index = Mathf.Clamp(_index, 0, __instance.m_EachEnemyHuds.Length - 1);
                    Log($"[MultiMax] HUD index for {_ed.name}: {_index}");
                }
            }
            catch (Exception e)
            {
                Log($"[MultiMax] uiEnemyHUDPatch error: {e}");
            }
        }
        [PatchType(typeof(uiEnemyHUD))]
        public static class FixHUDOrderPatch
        {
            [PatchMethod("InitializeEnemyHud")]
            [PatchPosition(Postfix)]
            public static void ReorderHUDAfterInit(uiEnemyHUD __instance)
            {
                try
                {
                    if (__instance.m_EnemyHudDictionary == null || __instance.m_EnemyHudDictionary.Count == 0)
                        return;

                    // Ordina gli HUD in base al TurnIndex degli enemy dummy
                    var orderedPairs = __instance.m_EnemyHudDictionary
                        .Where(kv => kv.Key != null && kv.Value != null)
                        .OrderBy(kv => kv.Key.FID.m_TurnIndex)
                        .ToList();

                    // Riposiziona visivamente gli HUD
                    for (int i = 0; i < orderedPairs.Count; i++)
                    {
                        var hud = orderedPairs[i].Value;
                        if (hud != null)
                        {
                            // Forza il sibling index per ordinare visivamente
                            hud.transform.SetSiblingIndex(i);
                        }
                    }

                    Log($"[MultiMax] HUD reordered by TurnIndex: {orderedPairs.Count} elements");
                }
                catch (Exception e)
                {
                    Log($"[MultiMax] ReorderHUDAfterInit error: {e}");
                }
            }
        }

        [PatchMethod("InitializeEnemyHud")]
        [PatchPosition(Postfix)]
        public static void RepositionHUDElements(uiEnemyHUD __instance)
        {
            try
            {
                var grid = __instance.m_GridRow;
                if (grid == null)
                    return;

                int activeCount = 0;
                foreach (var hud in __instance.m_EachEnemyHuds)
                {
                    if (hud != null && hud.gameObject.activeSelf)
                        activeCount++;
                }

                if (activeCount == 0)
                    return;

                // Spaziatura ridotta per 4 nemici
                float spacing = activeCount switch
                {
                    4 => 140f,
                    5 => 120f,
                    _ => 160f
                };

                // Centra gli HUD
                float totalWidth = (activeCount - 1) * spacing;
                float startX = -totalWidth * 0.5f;

                int index = 0;
                for (int i = 0; i < __instance.m_EachEnemyHuds.Length; i++)
                {
                    var hud = __instance.m_EachEnemyHuds[i];
                    if (hud == null || !hud.gameObject.activeSelf)
                        continue;

                    var rect = hud.GetComponent<RectTransform>();
                    if (rect != null)
                    {
                        rect.anchoredPosition = new Vector2(startX + index * spacing, 0f);

                        // Ridimensiona leggermente per 4+ nemici
                        float scale = activeCount >= 4 ? 0.9f : 1f;
                        rect.localScale = Vector3.one * scale;

                        index++;
                    }
                }

                Log($"[MultiMax] Repositioned {activeCount} HUD elements (spacing {spacing})");
            }
            catch (Exception e)
            {
                Log($"[MultiMax] RepositionHUDElements error: {e}");
            }
        }
    }
}
