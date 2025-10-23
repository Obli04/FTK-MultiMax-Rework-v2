using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using FTK_MultiMax_Rework_v2.PatchHelpers;
using JetBrains.Annotations;
using UnityEngine;
using static FTK_MultiMax_Rework_v2.Main;
using static FTK_MultiMax_Rework_v2.PatchHelpers.PatchPositions;
using System.Reflection; // ← this fixes FieldInfo + BindingFlags
using System.Linq;
using UnityEngine.UI; 
using Object = UnityEngine.Object;

    [PatchType(typeof(Diorama))]    
    public class DioramaEnemyTargetExpandPatch
    {
        [PatchMethod("_resetTargetQueue")]
        [PatchPosition(Postfix)]
        public static void ExpandEnemyTargets(ref Diorama __instance)
        {
            int desired = Mathf.Min(GameFlowMC.gMaxEnemies, 5);

            if (__instance.m_EnemyTargets == null || __instance.m_EnemyTargets.Count == 0)
                return;

            int currentCount = __instance.m_EnemyTargets.Count;
            if (currentCount < desired)
            {
                Log($"[MultiMax] Expanding enemy transforms: {currentCount} → {desired}");

                Transform last = __instance.m_EnemyTargets[currentCount - 1];
                for (int i = currentCount; i < desired; i++)
                {
                    Transform clone = Object.Instantiate(last, last.parent);
                    clone.name = $"enemy {i + 1}_target";
                    __instance.m_EnemyTargets.Add(clone);
                }
            }

            // Reflect and rebuild the private queue
            FieldInfo queueField = typeof(Diorama).GetField("m_EnemyTargetQueues", BindingFlags.NonPublic | BindingFlags.Instance);
            if (queueField == null)
            {
                Log("[MultiMax] ERROR: Could not find private field m_EnemyTargetQueues in Diorama!");
                return;
            }

            var enemyQueue = queueField.GetValue(__instance) as List<Transform>;
            if (enemyQueue == null)
            {
                enemyQueue = new List<Transform>();
                queueField.SetValue(__instance, enemyQueue);
            }

            enemyQueue.Clear();

            for (int i = 0; i < Mathf.Min(desired, __instance.m_EnemyTargets.Count); i++)
            {
                enemyQueue.Add(__instance.m_EnemyTargets[i]);
            }

            Log($"[MultiMax] Enemy target queue rebuilt with {enemyQueue.Count} entries.");

            // Spread them horizontally for visibility
            SpreadTargets(__instance.m_EnemyTargets);
        }

        private static void SpreadTargets(List<Transform> targets)
        {
            if (targets == null || targets.Count == 0)
                return;

            float spacing;
            switch (targets.Count)
            {
                case 4: spacing = 2.5f; break;  // closer for 4
                case 5: spacing = 1.4f; break;  // tighter for 5
                default: spacing = 1.2f; break;
            }

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

            Log($"[MultiMax] Re-spaced {targets.Count} enemy targets (spacing {spacing}).");
        }
        [PatchMethod("Awake")]
        [PatchPosition(Postfix)]
        public static void TuneGrid(ref uiEnemyHUD __instance)
        {
            if (__instance == null || __instance.m_GridRow == null) return;

            int desired = Mathf.Clamp(GameFlowMC.gMaxEnemies, 3, 5);
            var grid = __instance.m_GridRow.GetComponent<GridLayoutGroup>();
            if (grid == null) return;

            // Slightly smaller cells & more spacing to breathe
            Vector2 cell = (desired == 5) ? new Vector2(182f, 62f)
                           : (desired == 4) ? new Vector2(198f, 66f)
                                            : new Vector2(220f, 72f);

            Vector2 space = (desired == 5) ? new Vector2(68f, 0f)
                           : (desired == 4) ? new Vector2(76f, 0f)
                                            : new Vector2(64f, 0f);

            grid.cellSize = cell;
            grid.spacing = space;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = desired;

            // NEW: add left/right padding so the first/last bar isn’t glued to the edges
            if (grid.padding == null) grid.padding = new RectOffset();
            grid.padding.left = 24;   // try 24–36 if you want more
            grid.padding.right = 24;

            // Ensure existing slots adopt the cell size without extra scaling
            for (int i = 0; i < __instance.m_EachEnemyHuds.Length; i++)
            {
                var hud = __instance.m_EachEnemyHuds[i];
                if (!hud) continue;
                var rt = hud.GetComponent<RectTransform>();
                if (rt) rt.sizeDelta = cell;
                hud.transform.localScale = Vector3.one;
            }

            Log($"[MultiMax] Enemy HUD grid tuned for {desired} enemies (cell {cell}, spacing {space}, pad L/R 24).");
        }
        [PatchType(typeof(uiEnemyHUD))]
        public class uiEnemyHUDExpandPatch
        {
            [PatchMethod("Awake")]
            [PatchPosition(Prefix)]
            public static void ExpandEnemyHudSlots(ref uiEnemyHUD __instance)
            {
                if (__instance == null)
                    return;

                int desired = Mathf.Max(3, GameFlowMC.gMaxEnemies);

                FieldInfo hudArrayField = typeof(uiEnemyHUD).GetField("m_EachEnemyHuds", BindingFlags.Public | BindingFlags.Instance);
                if (hudArrayField == null)
                {
                    Log("[MultiMax] Could not find m_EachEnemyHuds field!");
                    return;
                }

                uiEachEnemyHud[] array = hudArrayField.GetValue(__instance) as uiEachEnemyHud[];
                if (array == null || array.Length >= desired)
                    return;

                Log($"[MultiMax] Expanding enemy HUD array: {array.Length} → {desired}");

                uiEachEnemyHud prefab = __instance.m_EachEnemyHudsPrefab;
                RectTransform parent = __instance.m_GridRow;
                uiEachEnemyHud[] newArray = new uiEachEnemyHud[desired];

                // copy existing elements
                for (int i = 0; i < array.Length; i++)
                    newArray[i] = array[i];

                // add new ones
                for (int i = array.Length; i < desired; i++)
                {
                    uiEachEnemyHud clone = Object.Instantiate(prefab, parent, false);
                    clone.name = $"EnemyHUD_{i}";
                    clone.gameObject.SetActive(false);
                    newArray[i] = clone;
                }

                hudArrayField.SetValue(__instance, newArray);
            }

            [PatchMethod("Awake")]
            [PatchPosition(Postfix)]
            public static void ResizeEnemyHudUI(ref uiEnemyHUD __instance)
            {
                foreach (var hud in __instance.m_EachEnemyHuds)
                {
                    if (hud == null) continue;
                    var rect = hud.GetComponent<RectTransform>();
                    rect.localScale = Vector3.one * 0.8f;        // smaller bars
                    rect.anchoredPosition = new Vector2(rect.anchoredPosition.x * 0.8f,
                                                        rect.anchoredPosition.y);
                }
                Log("[MultiMax] Enemy HUD resized for multi-enemy fights.");
            }
        }
    }