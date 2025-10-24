using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;
using FTK_MultiMax_Rework_v2.PatchHelpers;
using static FTK_MultiMax_Rework_v2.Main;
using static FTK_MultiMax_Rework_v2.PatchHelpers.PatchPositions;

namespace FTK_MultiMax_Rework_v2.Patches
{
    using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using FTK_MultiMax_Rework_v2.PatchHelpers;
using UnityEngine;
using static FTK_MultiMax_Rework_v2.PatchHelpers.PatchPositions;
using static FTK_MultiMax_Rework_v2.Main;
using Object = UnityEngine.Object;

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
        public static void SafeExpandHUDArray(uiEnemyHUD __instance, EnemyDummy _ed, int _index)
        {
            try
            {
                int desired = Mathf.Max(GameFlowMC.gMaxEnemies, 3);
                var arr = __instance.m_EachEnemyHuds;

                if (arr == null)
                {
                    Log("[MultiMax] m_EachEnemyHuds is null, creating new array.");
                    arr = new uiEachEnemyHud[desired];
                }
                else if (arr.Length < desired)
                {
                    Log($"[MultiMax] Expanding HUD array {arr.Length} → {desired}");

                    // crea nuovo array e copia i precedenti
                    var newArr = new uiEachEnemyHud[desired];
                    for (int i = 0; i < arr.Length; i++)
                        newArr[i] = arr[i];

                    // istanzia i nuovi HUD
                    for (int i = arr.Length; i < desired; i++)
                    {
                        var newHud = UnityEngine.Object.Instantiate(
                            __instance.m_EachEnemyHudsPrefab,
                            __instance.transform.position,
                            Quaternion.identity);
                        newHud.transform.SetParent(__instance.m_GridRow, false);
                        newHud.gameObject.SetActive(false);
                        newArr[i] = newHud;
                    }

                    __instance.m_EachEnemyHuds = newArr;
                }

                // correzione: clamp dell’indice se troppo grande
                if (_index >= __instance.m_EachEnemyHuds.Length)
                {
                    Log($"[MultiMax] Clamping HUD index {_index} → {__instance.m_EachEnemyHuds.Length - 1}");
                    _index = __instance.m_EachEnemyHuds.Length - 1;
                }
            }
            catch (Exception e)
            {
                Log($"[MultiMax] uiEnemyHUDPatch error: {e}");
            }
            var grid = __instance.m_GridRow;
            if (grid != null)
            {
                var children = new List<RectTransform>();
                foreach (Transform child in grid)
                    children.Add(child as RectTransform);

                float xOffset = 0f;
                foreach (var child in children)
                {
                    child.anchoredPosition = new Vector2(xOffset, 0f);
                    xOffset += 180f; // distanza orizzontale tra gli HUD
                }
            }
        }
    }
}
