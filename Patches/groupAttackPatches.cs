using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using FTK_MultiMax_Rework_v2.PatchHelpers;
using static FTK_MultiMax_Rework_v2.PatchHelpers.PatchPositions;
using static FTK_MultiMax_Rework_v2.Main;
using UnityEngine.Animations;
using GridEditor;
using Google2u;
using Newtonsoft.Json;

[PatchType(typeof(EncounterSession))]
public static class SyncClonedEnemiesToEnemyStatuses
{
    [PatchMethod("InitEnemyDummiesForCombat")]
    [PatchPosition(Postfix)]
    public static void AddClonedToStatuses(EncounterSession __instance, string[] _enemyTypes)
    {
        try
        {
            if (__instance.m_EnemyDummies == null || __instance.m_EnemyDummies.Count == 0)
                return;

            // Get EnemyStatuses list from EncounterSessionMC
            var mc = EncounterSessionMC.Instance;
            if (mc == null) return;

            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var fldStatuses = typeof(EncounterSessionMC).GetField("m_EnemyStatuses", BF);
            var statuses = fldStatuses?.GetValue(mc) as IDictionary;
            if (statuses == null) return;

            // Also sync vanilla EnemyStatuses list in EncounterSession
            var encStatuses = typeof(EncounterSession).GetField("m_EnemyStatuses", BF);
            var encStatusesList = encStatuses?.GetValue(__instance) as IList;

            Log($"[MultiMax] Syncing {__instance.m_EnemyDummies.Count} enemies to vanilla lists");

            foreach (var kv in __instance.m_EnemyDummies)
            {
                var dummy = kv.Value;
                if (dummy == null) continue;

                // Add to MC statuses if missing
                if (!statuses.Contains(kv.Key))
                {
                    var statusValueType = fldStatuses.FieldType.GetGenericArguments()[1];
                    var ctor = statusValueType.GetConstructor(new[] { typeof(string), typeof(HexLandID), typeof(bool), typeof(int) });
                    if (ctor != null)
                    {
                        var status = ctor.Invoke(new object[] { dummy.m_EnemyType, null, true, 0 });
                        statuses[kv.Key] = status;

                        // Also add to vanilla list
                        if (encStatusesList != null)
                        {
                            encStatusesList.Add(status);
                        }

                        Log($"[MultiMax] Added {dummy.name} to EnemyStatuses");
                    }
                }
            }
            // --- FIX TARGET LINKS (ensures correct selection & highlight) ---
            try
            {
                var diorama = EncounterSession.Instance?.m_ActiveDiorama;
                if (diorama != null && diorama.m_EnemyTargets != null)
                {
                    var allEnemies = __instance.m_EnemyDummies.Values.ToList();
                    for (int i = 0; i < allEnemies.Count; i++)
                    {
                        var dummy = allEnemies[i];
                        if (dummy == null) continue;

                        // Make sure the index stays within the correct range
                        if (dummy.m_DioramaTargetIndex < 0 || dummy.m_DioramaTargetIndex >= diorama.m_EnemyTargets.Count)
                        {
                            dummy.m_DioramaTargetIndex = i;
                            dummy.m_DioramaTargetID = i;
                        }

                        // Move the enemy's transform to its correct diorama slot
                        if (dummy.transform != null && dummy.m_DioramaTargetIndex < diorama.m_EnemyTargets.Count)
                        {
                            var slot = diorama.m_EnemyTargets[dummy.m_DioramaTargetIndex];
                            if (slot != null)
                            {
                                dummy.transform.position = slot.position;
                                dummy.transform.rotation = slot.rotation;
                            }
                        }
                    }
                    Log($"[MultiMax] Re-synced enemy transforms to Diorama ({diorama.m_EnemyTargets.Count} slots)");
                }
            }
            catch (Exception ex)
            {
                Log($"[MultiMax] Diorama re-sync error: {ex}");
            }
            Log("[MultiMax] Re-synced enemy targets and DioramaTargetIndex for proper selection");
        }
        catch (Exception e)
        {
            Debug.LogError($"[MultiMax] SyncClonedEnemiesToEnemyStatuses error: {e}");
        }
    }

    public static class MultiMaxCombatUtil
    {
        public static List<EnemyDummy> GetAdjacentEnemies(EnemyDummy center, int radius = 1)
        {
            var enc = EncounterSession.Instance;
            if (enc == null || enc.m_EnemyDummies == null || center == null) return new List<EnemyDummy>();

            var ordered = enc.m_EnemyDummies.Values
                .Where(e => e != null && e.m_IsAlive && e.m_CurrentHealth > 0)
                .OrderBy(e => e.m_DioramaTargetIndex)
                .ToList();

            int idx = ordered.FindIndex(e => e == center);
            if (idx < 0) return new List<EnemyDummy>();

            var list = new List<EnemyDummy>();
            for (int d = 1; d <= radius; d++)
            {
                if (idx - d >= 0) list.Add(ordered[idx - d]);
                if (idx + d < ordered.Count) list.Add(ordered[idx + d]);
            }
            return list;
        }
    }
}