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
public static class DioramaEnemyLayoutPatch
{
    [PatchMethod("_resetTargetQueue")]
    [PatchPosition(Postfix)]
    public static void OrderAndSpreadEnemies(ref Diorama __instance)
    {
        var targets = __instance.m_EnemyTargets;
        if (targets == null || targets.Count < 3) return;

        // 1) Sort enemy targets by "enemy N_target"
        targets.Sort((a, b) =>
        {
            int A = ParseIndex(a.name);
            int B = ParseIndex(b.name);
            return A.CompareTo(B);
        });

        // 2) Determine desired enemy count = min(gMaxEnemies, 5)
        int desired = Mathf.Clamp(GameFlowMC.gMaxEnemies, 1, 5);

        // 3) Ensure we have at least "desired" transforms (clone last if needed)
        var last = targets[targets.Count - 1];
        for (int i = targets.Count; i < desired; i++)
        {
            var clone = Object.Instantiate(last, last.parent);
            clone.name = $"enemy {i + 1}_target";
            targets.Add(clone);
        }

        // 4) Compute left/right from current extremes (by X) so we keep the diorama look
        float minX = targets.Min(t => t.localPosition.x);
        float maxX = targets.Max(t => t.localPosition.x);

        if (Mathf.Approximately(minX, maxX))
        {
            minX -= 15.2f;
            maxX += 15.2f;
        }

        float inset = 0.6f; // try 0.6–1.0 if you want more margin
        float center = 0.5f * (minX + maxX);
        minX = Mathf.Lerp(minX, center, inset * 0.15f);
        maxX = Mathf.Lerp(maxX, center, inset * 0.15f);

        // keep your existing padding logic if you like (or 0)
        float padding = (desired == 5) ? 0.0f : 0.08f;

        // final left/right for the lerp
        float left  = Mathf.Lerp(minX, maxX, 0.0f + padding);
        float right = Mathf.Lerp(minX, maxX, 1.0f - padding);

        // 5) Evenly distribute across [left, right] keeping Y/Z as-is
        for (int i = 0; i < desired; i++)
        {
            float t = (desired == 1) ? 0.5f : (float)i / (desired - 1);
            var p = targets[i].localPosition;
            p.x = Mathf.Lerp(left, right, t);
            targets[i].localPosition = p;
        }

        // 6) Trim any extras (keep the first "desired")
        if (targets.Count > desired)
            targets.RemoveRange(desired, targets.Count - desired);

        Log($"[MultiMax] Enemy targets ordered & spread for {desired} enemies.");
    }

    private static int ParseIndex(string name)
    {
        var m = Regex.Match(name, @"(\d+)");
        return m.Success ? int.Parse(m.Value) : 999;
    }

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
    }
}