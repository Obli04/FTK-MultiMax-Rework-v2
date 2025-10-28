using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using FTK_MultiMax_Rework.PatchHelpers;
using static FTK_MultiMax_Rework.PatchHelpers.PatchPositions;
using static FTK_MultiMax_Rework.Main;
using UnityEngine.Animations;
using GridEditor;
using Google2u;
using Newtonsoft.Json;


namespace FTK_MultiMax_Rework.Patches
{
    [PatchType(typeof(CharacterDummy))]
    public static class ForceCombatEnemyDieOnAOE
    {
        [PatchMethod("RespondToHit")]
        [PatchPosition(Postfix)]
        public static void EnsureAOEDeaths(CharacterDummy __instance, bool _mainVictim)
        {
            var enemy = __instance as EnemyDummy;
            if (enemy == null) return;
            if (enemy.m_IsAlive || enemy.m_CurrentHealth > 0) return;

            var mc = EncounterSessionMC.Instance;
            var fld = typeof(EncounterSessionMC).GetField("m_EnemyStatuses", BindingFlags.Instance | BindingFlags.NonPublic);
            var statuses = fld?.GetValue(mc) as System.Collections.IDictionary;
            if (statuses == null || !statuses.Contains(enemy.FID)) return;

            var status = statuses[enemy.FID];
            var aliveFld = status.GetType().GetField("m_Alive", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (aliveFld != null && (bool)aliveFld.GetValue(status))
            {
                Debug.Log($"[MultiMax] Forcing CombatEnemyDie for {enemy.name} (AOE casualty)");
                mc.RPCAllViaServer("CombatEnemyDie", new object[] { enemy.FID, mc.m_PlayerAttacker });
            }
        }
    }
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
                // Recalculate attack slide distances for all enemies
                var dioInstance = __instance.m_ActiveDiorama;
                if (dioInstance != null)
                {
                    foreach (var dummy in __instance.m_EnemyDummies.Values)
                    {
                        if (dummy == null) continue;
                        var slide = dummy.GetComponent<DummyAttackSlide>() ?? dummy.gameObject.AddComponent<DummyAttackSlide>();
                        int playerCount = dioInstance.m_PlayerTargets?.Count ?? 3;

                        slide.m_Distances = new float[playerCount];
                        for (int i = 0; i < playerCount; i++)
                        {
                            var pT = dioInstance.m_PlayerTargets[i];
                            slide.m_Distances[i] = Vector3.Distance(dummy.transform.position, pT.position) * dioInstance.m_AttackDistanceScale;
                        }
                    }
                    Log($"[MultiMax] Rebuilt attack slide distances for splash accuracy ({__instance.m_EnemyDummies.Count} enemies)");
                }

            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] SyncClonedEnemiesToEnemyStatuses error: {e}");
            }
        }
        /// <summary>
        /// Single source of truth for: ordering, filtering, and neighbor lookup.
        /// Both the hexagon preview and splash damage must use this.
        /// </summary>
        public static class SplashTargeting
        {
            private static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            /// Alive enemies, sorted strictly by DioramaTargetIndex (left→right).
            public static List<EnemyDummy> GetOrderedAliveEnemies()
            {
                var enc = EncounterSession.Instance;
                if (enc?.m_EnemyDummies == null) return s_Empty;
                return enc.m_EnemyDummies.Values
                    .Where(e => e != null && e.m_IsAlive && e.m_CurrentHealth > 0)
                    .OrderBy(e => e.m_DioramaTargetIndex)
                    .ToList();
            }

            /// Index of the enemy in ordered-alive list. Returns -1 if not found.
            public static int IndexOf(EnemyDummy e, List<EnemyDummy> orderedAlive)
            {
                if (e == null || orderedAlive == null) return -1;
                // Reference equality is correct because CharacterDummy/EnemyDummy are the same instance.
                return orderedAlive.FindIndex(x => ReferenceEquals(x, e));
            }

            /// Neighbor by offset relative to center; offset -1 = left, +1 = right.
            public static EnemyDummy GetNeighborByOffset(EnemyDummy center, int offset)
            {
                if (center == null || offset == 0) return null;
                var ordered = GetOrderedAliveEnemies();
                int idx = IndexOf(center, ordered);
                if (idx < 0) return null;

                int target = idx + offset;
                if (target < 0 || target >= ordered.Count) return null;
                return ordered[target];
            }

            /// Returns center + neighbors (radius 1 = left+right).
            public static List<EnemyDummy> GetSplashGroup(EnemyDummy center, int radius = 1)
            {
                var result = new List<EnemyDummy>();
                if (center == null) return result;

                var ordered = GetOrderedAliveEnemies();
                int idx = IndexOf(center, ordered);
                if (idx < 0) return result;

                result.Add(ordered[idx]);
                for (int d = 1; d <= radius; d++)
                {
                    int li = idx - d;
                    int ri = idx + d;
                    if (li >= 0) result.Add(ordered[li]);
                    if (ri < ordered.Count) result.Add(ordered[ri]);
                }
                return result;
            }

            /// Clear all markers (network-safe) and light the exact splash group.
            public static void ShowMarkersForSplash(EnemyDummy center)
            {
                var enc = EncounterSession.Instance;
                if (enc?.m_EnemyDummies == null || center == null) return;

                // Clear old via RPC so visuals sync correctly
                foreach (var d in enc.m_EnemyDummies.Values.Where(v => v != null))
                    typeof(CharacterDummy).GetMethod("SetMarkersOffRPC", BF)?.Invoke(d, null);

                // Light center + neighbors
                var group = GetSplashGroup(center, radius: 1);

                foreach (var d in group)
                {
                    var type = CharacterDummy.DefendMarkerType.Normal;
                    if (ReferenceEquals(d, center))
                        type = CharacterDummy.DefendMarkerType.SplashCentre;
                    else
                    {
                        // choose left/right for neighbors based on relative index
                        var ordered = GetOrderedAliveEnemies();
                        int ci = IndexOf(center, ordered);
                        int di = IndexOf(d, ordered);
                        type = (di < ci) ? CharacterDummy.DefendMarkerType.SplashLeft
                                        : CharacterDummy.DefendMarkerType.SplashRight;
                    }

                    typeof(CharacterDummy).GetMethod("SetDefendMarkerRPC", BF, null,
                            new[] { typeof(CharacterDummy.DefendMarkerType) }, null)
                        ?.Invoke(d, new object[] { type });
                }
            }

            private static readonly List<EnemyDummy> s_Empty = new List<EnemyDummy>();
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Ensure the preview hexagons use the same targeting as damage.
        // ─────────────────────────────────────────────────────────────────────────────
        [PatchType(typeof(EncounterSession))]
        public static class FixAoeMarkers_Consistent
        {
            [PatchMethod("SetAllDefendMarkers")]
            [PatchPosition(Prefix)]
            public static bool UseUnifiedSplash(EncounterSession __instance, FTKPlayerID _target,
                CharacterDummy.TargetType _targetType, bool _broadcast)
            {
                if (_targetType != CharacterDummy.TargetType.Splash &&
                    _targetType != CharacterDummy.TargetType.Aoe)
                    return true; // vanilla for non-splash types

                var center = __instance.GetDummyByFID(_target) as EnemyDummy;
                if (center == null) return true; // fallback vanilla if something odd

                SplashTargeting.ShowMarkersForSplash(center);
                return false; // skip vanilla marker logic
            }
        }

        // ─────────────────────────────────────────────────────────────────────────────
        // Ensure the actual splash damage adjacency uses the SAME neighbors.
        // (These hooks are what the game queries for “adjacent” targets.)
        // ─────────────────────────────────────────────────────────────────────────────
        [PatchType(typeof(CharacterDummy))]
        public static class ExpandSplashAdjacency_Unified
        {
            [PatchMethod("GetLeftDummy")]
            [PatchPosition(Postfix)]
            public static void Left(CharacterDummy __instance, ref CharacterDummy __result)
            {
                if (__result != null) return;
                var center = __instance as EnemyDummy; // only relevant when asking around an enemy
                if (center == null) return;

                var left = SplashTargeting.GetNeighborByOffset(center, -1);
                if (left != null) __result = left;
            }

            [PatchMethod("GetRightDummy")]
            [PatchPosition(Postfix)]
            public static void Right(CharacterDummy __instance, ref CharacterDummy __result)
            {
                if (__result != null) return;
                var center = __instance as EnemyDummy;
                if (center == null) return;

                var right = SplashTargeting.GetNeighborByOffset(center, +1);
                if (right != null) __result = right;
            }
        }

        // (Optional but recommended) normalize after enemies are positioned so both
        // preview + damage lookups share the same DioramaTargetIndex ordering.
        [PatchType(typeof(EncounterSession))]
        public static class NormalizeEnemyOrder_PostInit
        {
            [PatchMethod("InitEnemyDummiesForCombat")]
            [PatchPosition(Postfix)]
            public static void Normalize(EncounterSession __instance)
            {
                try
                {
                    var dio = __instance.m_ActiveDiorama;
                    if (dio == null || dio.m_EnemyTargets == null || dio.m_EnemyTargets.Count == 0) return;

                    var sorted = __instance.m_EnemyDummies
                        .Values.Where(e => e != null)
                        .OrderBy(e => e.transform.position.x) // left → right in world
                        .ToList();

                    for (int i = 0; i < sorted.Count; i++)
                    {
                        var e = sorted[i];
                        e.m_DioramaTargetIndex = i;
                        e.m_DioramaTargetID = i;
                    }
                }
                catch (Exception ex)
                {
                    Log($"[MultiMax] NormalizeEnemyOrder_PostInit error: {ex}");
                }
            }
        }
    }
}