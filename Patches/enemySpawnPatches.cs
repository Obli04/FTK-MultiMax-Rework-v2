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
using FTK_MultiMax_Rework_v2;


namespace FTK_MultiMax_Rework_v2.Patches
{
    [PatchType(typeof(EncounterSession))]
    public static class ExpandEnemyTypesPatch
    {
        [PatchMethod("InitEnemyDummiesForCombat")]
        [PatchPosition(Prefix)]
        public static void ExpandEnemies(ref string[] _enemyTypes)
        {
            try
            {
                int desired = Mathf.Min(GameFlowMC.gMaxEnemies, 5);
                if (_enemyTypes == null || _enemyTypes.Length == 0) return;

                List<string> list = new List<string>(_enemyTypes);

                // Ottieni lista globale di possibili nemici per il livello attuale
                var enemyDB = FTK_enemyDB.GetAll(); // se non esiste, usa reflection su DB globale
                var rng = new System.Random();

                while (list.Count < desired)
                {
                    var rand = enemyDB[rng.Next(enemyDB.Count)];
                    list.Add(rand.name);
                }

                _enemyTypes = list.ToArray();
                Debug.Log($"[MultiMax] Expanded enemies to {list.Count}: {string.Join(", ", list)}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] ExpandEnemyTypesPatch error: {e}");
            }
        }
    }

    [PatchType(typeof(EncounterSession))]
    static class EncounterSession_InitEnemyDummiesForCombat_Patch
    {
        [PatchMethod("InitEnemyDummiesForCombat")]
        [PatchPosition(Postfix)]
        static void initEnemyDummiesforCombat(EncounterSession __instance, string[] _enemyTypes, bool _reuse, bool _softTransition)
        {
            var dio = __instance.m_ActiveDiorama;               // Diorama instance
            if (dio == null) return;

            for (int idx = 0; idx < _enemyTypes.Length; idx++)
            {
                // Get/ensure dummy
                FTKPlayerID eid = new FTKPlayerID(idx, -1);
                if (!__instance.m_EnemyDummies.TryGetValue(eid, out var dummy) || dummy == null)
                    continue; // created by original, we just reposition

                // Ask Diorama for the next enemy slot
                dio.EnemyPopTarget(out int dioramaIndex, out int dioramaID);
                Transform slot = dio.GetTargetEnemy(dioramaID);

                // Place visually
                var t = dummy.transform;
                t.position = slot.position;
                t.rotation = slot.rotation;

                // Optional: orient to face players’ center
                var look = dio.GetCenterTargetPlayer();
                if (look != null)
                    t.LookAt(look.position, Vector3.up);
            }
        }
    }
    [PatchType(typeof(Diorama))]
    static class Diorama_ResetTargetQueue_Patch
    {
        [PatchMethod("_resetTargetQueue")]
        [PatchPosition(Postfix)]
        static void resetTargetPatch(Diorama __instance)
        {
            // enemy count the diorama planned for
            var bf = BindingFlags.Instance | BindingFlags.NonPublic;
            int enemyCount = (int)typeof(Diorama)
                .GetProperty("_dioramaEnemyCount", bf | BindingFlags.Public)
                .GetValue(__instance, null);

            // 1) Resize player slides
            foreach (var tr in __instance.m_PlayerTargets)
            {
                var slide = tr.GetComponent<DummyAttackSlide>();
                if (slide == null) continue;
                if (slide.m_Distances == null || slide.m_Distances.Length < enemyCount)
                    slide.m_Distances = new float[enemyCount];
            }

            // 2) Resize enemy slides (they also hold distances to players)
            foreach (var tr in __instance.m_EnemyTargets)
            {
                var slide = tr.GetComponent<DummyAttackSlide>();
                if (slide == null) continue;
                // Players count used for enemy->player distances
                int playerCount = (int)typeof(Diorama)
                    .GetProperty("_dioramaPlayerCount", bf | BindingFlags.Public)
                    .GetValue(__instance, null);

                if (playerCount < 1) playerCount = __instance.m_PlayerTargets.Count;

                if (slide.m_Distances == null || slide.m_Distances.Length < playerCount)
                    slide.m_Distances = new float[playerCount];
            }
        }
    }
    [PatchType(typeof(EnemyDummy))]
    public static class EnsureAttackSlidePatch
    {
        [PatchMethod("InitEnemyDummyForCombat")]
        [PatchPosition(Postfix)]
        public static void EnsureSlide(EnemyDummy __instance)
        {
            try
            {
                var slide = __instance.GetComponent<DummyAttackSlide>();
                if (slide == null)
                {
                    slide = __instance.gameObject.AddComponent<DummyAttackSlide>();
                    Debug.Log($"[MultiMax] Added missing DummyAttackSlide to {__instance.name}");
                }

                var diorama = EncounterSession.Instance?.m_ActiveDiorama;
                if (diorama == null) return;

                int playerCount = diorama.m_PlayerTargets?.Count ?? 3;
                if (slide.m_Distances == null || slide.m_Distances.Length < playerCount)
                {
                    slide.m_Distances = new float[playerCount];
                    for (int i = 0; i < playerCount; i++)
                    {
                        var pT = diorama.m_PlayerTargets[i];
                        slide.m_Distances[i] = Vector3.Distance(__instance.transform.position, pT.position) * diorama.m_AttackDistanceScale;
                    }
                    Debug.Log($"[MultiMax] Rebuilt attack slide distances for {__instance.name} ({slide.m_Distances.Length})");
                }

                // Assign to dummy
                __instance.m_AttackSlide = slide;

                // Ensure valid diorama index
                if (__instance.m_DioramaTargetIndex < 0 || __instance.m_DioramaTargetIndex >= (diorama.m_EnemyTargets?.Count ?? 0))
                {
                    __instance.m_DioramaTargetIndex = Mathf.Min((diorama.m_EnemyTargets?.Count ?? 1) - 1, 0);
                    Debug.Log($"[MultiMax] Fixed invalid DioramaTargetIndex for {__instance.name}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] EnsureSlidePatch error: {e}");
            }
        }
    }

    [PatchType(typeof(EnemyDummy))]
    public static class EnemyDummy_EngageBattle_NonOwnerFix
    {
        [PatchMethod("EngageBattle")]
        [PatchPosition(Postfix)]
        public static void ForceEngageForNonOwner(EnemyDummy __instance)
        {
            try
            {
                // Only for enemies (not players) that didn't pass the vanilla IsOwner gate
                if (!__instance.FID.IsPlayer() && !__instance.IsOwner)
                {
                    var fsm = __instance.m_EngageBattleFSM;
                    if (fsm != null)
                    {
                        if (!fsm.gameObject.activeSelf)
                            fsm.gameObject.SetActive(true);

                        fsm.SendEvent("BattleEngage");
                        Debug.Log($"[MultiMax] Forced EngageBattle FSM on non-owner for {__instance.name}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] ForceEngageForNonOwner error: {e}");
            }
        }
    }

    [PatchType(typeof(EnemyDummy))]
    public static class EnemyDummy_EngageBattle_Fix
    {
        [PatchMethod("EngageBattle")]
        [PatchPosition(Prefix)]
        public static void EnsureCameraTarget(EnemyDummy __instance)
        {
            try
            {
                // Usa il manager corretto
                var camManager = GameObject.FindObjectOfType<CameraCutManager>();
                if (camManager == null) return;

                var field = typeof(CameraCutManager).GetField("m_CurrentEnemyDummy",
                    BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

                if (field != null)
                {
                    field.SetValue(camManager, __instance);
                    Debug.Log($"[MultiMax] Forced camera target to {__instance.name}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] EnsureCameraTarget error: {e}");
            }
        }

        [PatchMethod("EngageBattle")]
        [PatchPosition(Postfix)]
        public static void ForceEngageForNonOwner(EnemyDummy __instance)
        {
            try
            {
                if (!__instance.FID.IsPlayer() && !__instance.IsOwner)
                {
                    var fsm = __instance.m_EngageBattleFSM;
                    if (fsm != null)
                    {
                        if (!fsm.gameObject.activeSelf)
                            fsm.gameObject.SetActive(true);

                        fsm.SendEvent("BattleEngage");
                        Debug.Log($"[MultiMax] Forced EngageBattle FSM on non-owner for {__instance.name}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] ForceEngageForNonOwner error: {e}");
            }
        }
    }
    [PatchType(typeof(EnemyDummy))]
    public static class EnemyDeathStatusFix
    {
        [PatchMethod("RespondToHit")]
        [PatchPosition(Postfix)]
        public static void MarkEnemyDead(EnemyDummy __instance)
        {
            try
            {
                if (__instance.m_CurrentHealth <= 0f)
                {
                    var encounter = EncounterSessionMC.Instance;
                    if (encounter == null) return;

                    var statuses = encounter.GetType().GetField("m_EnemyStatuses",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                        ?.GetValue(encounter) as IDictionary;
                    if (statuses == null) return;

                    if (statuses.Contains(__instance.FID))
                    {
                        var status = statuses[__instance.FID];
                        var aliveField = status.GetType().GetField("m_Alive",
                            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                        if (aliveField != null)
                        {
                            aliveField.SetValue(status, false);
                            Debug.Log($"[MultiMax] Marked {__instance.name} as dead in EnemyStatuses");
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] EnemyDeathStatusFix error: {e}");
            }
        }
    }

    [PatchType(typeof(EncounterSessionMC))]
    public static class FixEnemyEngageBattleGlobals
    {
        [PatchMethod("StartNextCombatRound2")]
        [PatchPosition(Prefix)]
        public static void FixEnemyGlobalsPreBattle(EncounterSessionMC __instance)
        {
            try
            {
                var encounter = EncounterSession.Instance;
                if (encounter == null || encounter.m_EnemyDummies == null)
                    return;

                foreach (var kv in encounter.m_EnemyDummies)
                {
                    var fid = kv.Key;
                    var dummy = kv.Value;
                    if (dummy == null || !dummy.m_IsAlive)
                        continue;

                    // Assign FSM globals explicitly before the coroutine runs
                    FTKUtil.SetFSMGlobalGameObject("goEnemyDummy", dummy.gameObject);
                    FTKUtil.SetFSMGlobalObject("compEnemyDummy", dummy);

                    // Defensive: also assign goCombatDummy for enemy-side FSMs
                    FTKUtil.SetFSMGlobalGameObject("goCombatDummy", dummy.gameObject);
                    FTKUtil.SetFSMGlobalObject("compCombatDummy", dummy);

                    Debug.Log($"[MultiMax] Synced FSM globals for {dummy.name} (TurnIndex {fid.m_TurnIndex})");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] FixEnemyGlobalsPreBattle error: {e}");
            }
        }
    }
    [PatchType(typeof(EncounterSessionMC))]
    public static class DioramaSyncPatch
    {
        [PatchMethod("CommenceBattleRPC")]
        [PatchPosition(Postfix)]
        public static void ResyncDiorama(EncounterSessionMC __instance)
        {
            try
            {
                var diorama = EncounterSession.Instance?.m_ActiveDiorama;
                if (diorama == null) return;

                var enemies = EncounterSession.Instance?.m_EnemyDummies;
                if (enemies == null) return;

                // Expand list safely
                while (diorama.m_EnemyTargets.Count < enemies.Count)
                {
                    var prefab = diorama.m_EnemyTargets[0];
                    var clone = UnityEngine.Object.Instantiate(prefab, prefab.parent);
                    clone.name = $"Enemy Target {diorama.m_EnemyTargets.Count}";
                    diorama.m_EnemyTargets.Add(clone);
                }

                // Update transforms for each dummy
                int idx = 0;
                foreach (var kv in enemies)
                {
                    var dummy = kv.Value;
                    if (dummy == null) continue;
                    if (idx >= diorama.m_EnemyTargets.Count) break;

                    var target = diorama.m_EnemyTargets[idx];
                    dummy.transform.position = target.position;
                    dummy.transform.rotation = target.rotation;
                    idx++;
                }

                Debug.Log($"[MultiMax] Diorama targets resynced: {diorama.m_EnemyTargets.Count}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] DioramaSyncPatch error: {e}");
            }
        }
    }
    [PatchType(typeof(EncounterSessionMC))]
    public static class EnsureEnemyVictimsPatch
    {
        [PatchMethod("CommenceBattleRPC")]
        [PatchPosition(Postfix)]
        public static void FixMissingVictims(EncounterSessionMC __instance)
        {
            try
            {
                var dummies = EncounterSession.Instance?.m_EnemyDummies;
                var players = EncounterSession.Instance?.m_PlayerDummies;
                if (dummies == null || players == null || players.Count == 0)
                    return;

                var firstPlayer = players.Values.FirstOrDefault();
                if (firstPlayer == null)
                    return;

                foreach (var kv in dummies)
                {
                    var dummy = kv.Value;
                    if (dummy == null) continue;

                    // Assign a fallback victim if missing
                    if (dummy.m_CurrentVictimID == null ||
                        !EncounterSession.Instance.m_Dummies.ContainsKey(dummy.m_CurrentVictimID))
                    {
                        dummy.m_CurrentVictimID = firstPlayer.FID;
                        Debug.Log($"[MultiMax] Assigned fallback victim to {dummy.name} → {firstPlayer.name}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] EnsureEnemyVictimsPatch error: {e}");
            }
        }
    }
    [PatchType(typeof(EncounterSessionMC))]
    public static class ExpandEnemyStatusesPatch
    {
        [PatchMethod("InitiateCurrentEncounter")]
        [PatchPosition(Postfix)]
        public static void AfterInitiateEncounter(EncounterSessionMC __instance)
        {
            try
            {
                const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                var fldStatuses = __instance.GetType().GetField("m_EnemyStatuses", BF);
                if (fldStatuses == null) return;

                var statuses = fldStatuses.GetValue(__instance) as IDictionary;
                if (statuses == null) return;

                // Just a debug count log
                Debug.Log($"[MultiMax] EnemyStatuses count after init: {statuses.Count}");

                // Example: Expand to match all combatants
                var fldAll = __instance.GetType().GetField("m_AllCombtatants", BF);
                if (fldAll == null) return;
                var combatants = fldAll.GetValue(__instance) as Array;
                if (combatants == null) return;

                foreach (var obj in combatants)
                {
                    var pid = obj; // FTKPlayerID struct
                    if (pid == null) continue;

                    // skip players
                    var isEnemy = (bool)pid.GetType().GetMethod("IsPlayer").Invoke(pid, null) == false;
                    if (!isEnemy) continue;

                    if (!statuses.Contains(pid))
                    {
                        var ctor = typeof(EncounterSessionMC)
                            .GetNestedType("EnemyStatus", BF)
                            ?.GetConstructor(new[] { typeof(string), typeof(HexLandID), typeof(bool), typeof(int) });

                        if (ctor == null) continue;

                        var status = ctor.Invoke(new object[] { "enemy_default", null, true, 0 });
                        statuses[pid] = status;
                        Debug.Log($"[MultiMax] Added missing EnemyStatus for dummy {pid}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] ExpandEnemyStatusesPatch failed: {e}");
            }
        }
    }
    [PatchType(typeof(EncounterSessionMC))]
    public static class ComputeCombatInitiativePatch
    {
        [PatchMethod("ComputeCombatInitiative")]
        [PatchPosition(Postfix)]
        public static void computeCombatInitiative(EncounterSessionMC __instance, ref List<FTKPlayerID> __result)
        {
            try
            {
                const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

                // Enemy dummies live on the base EncounterSession instance:
                var enemies = EncounterSession.Instance?.m_EnemyDummies;
                if (enemies == null) return;

                var set = new HashSet<FTKPlayerID>(__result);
                foreach (var kv in enemies)
                    if (kv.Value != null && kv.Value.m_IsAlive && set.Add(kv.Key))
                        __result.Add(kv.Key);

                Debug.Log($"[MultiMax] ComputeCombatInitiative expanded to {__result.Count} entries.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] ComputeCombatInitiativePatch error: {e}");
            }
        }
    }
    [PatchType(typeof(EncounterSessionMC))]
    public static class FixFightOrderRPCPatch
    {
        [PatchMethod("CommenceBattleRPC")]
        [PatchPosition(Postfix)]
        public static void CommenceBattleRPC_Postfix(EncounterSessionMC __instance, FTKPlayerID[] _fightOrder)
        {
            try
            {
                const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                FieldInfo fightOrderField = typeof(EncounterSessionMC).GetField("m_FightOrder", BF);
                if (fightOrderField == null)
                {
                    Debug.LogError("[MultiMax] m_FightOrder field not found!");
                    return;
                }

                var fightOrder = fightOrderField.GetValue(__instance) as List<EncounterSessionMC.FightOrderEntry>;
                if (fightOrder == null)
                {
                    fightOrder = new List<EncounterSessionMC.FightOrderEntry>();
                    fightOrderField.SetValue(__instance, fightOrder);
                }

                var ctor = typeof(EncounterSessionMC.FightOrderEntry).GetConstructor(new[] { typeof(FTKPlayerID), typeof(int) });
                if (ctor == null)
                {
                    Debug.LogError("[MultiMax] FightOrderEntry constructor not found!");
                    return;
                }

                var enemies = EncounterSession.Instance?.m_EnemyDummies;
                if (enemies != null)
                {
                    var existingIds = new HashSet<FTKPlayerID>();
                    foreach (var entry in fightOrder)
                        existingIds.Add(entry.m_Pid);

                    int nextIndex = fightOrder.Count;
                    foreach (var kv in enemies)
                    {
                        var id = kv.Key;
                        var dummy = kv.Value;

                        if (!existingIds.Contains(id) && dummy != null && dummy.m_IsAlive)
                        {
                            var entry = (EncounterSessionMC.FightOrderEntry)ctor.Invoke(new object[] { id, nextIndex++ });
                            fightOrder.Add(entry);
                            Debug.Log($"[MultiMax] Added enemy to fightOrder: {dummy.m_EnemyType} (index {nextIndex - 1})");
                        }
                    }
                }

                Debug.Log($"[MultiMax] FightOrderRPC final count: {fightOrder.Count}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[MultiMax] FixFightOrderRPCPatch error: {ex}");
            }
        }
    }

    [PatchType(typeof(EnemyDummy))]
    public static class EnemyDummyAttackScheduleFixPatch
    {
        [PatchMethod("InitEnemyDummyForCombat")]
        [PatchPosition(Postfix)]
        public static void ForceAttackScheduleSync(EnemyDummy __instance)
        {
            try
            {
                if (__instance.m_AttackSchedule != null && __instance.m_AttackSchedule.m_Schedule != null)
                {
                    // CRITICAL: Always populate m_AttackScheduleList, even if not owner
                    __instance.m_AttackScheduleList = new List<AttackSchedule.AttackType>(__instance.m_AttackSchedule.m_Schedule);
                    __instance.m_AttackScheduleIndex = 0;

                    Debug.Log($"[MultiMax] Forced AttackScheduleList sync for {__instance.name}: {__instance.m_AttackScheduleList.Count} attacks");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] ForceAttackScheduleSync error: {e}");
            }
        }

        [PatchMethod("SetAttackDecision")]
        [PatchPosition(Prefix)]
        public static void FixAttackScheduleIndex(EnemyDummy __instance)
        {
            try
            {
                // Check BOTH lists
                int scheduleCount = __instance.m_AttackSchedule?.m_Schedule?.Count ?? 0;
                int listCount = __instance.m_AttackScheduleList?.Count ?? 0;

                Debug.Log($"[MultiMax] {__instance.name} SetAttackDecision - Index: {__instance.m_AttackScheduleIndex}, Schedule: {scheduleCount}, List: {listCount}");

                // Use whichever list has data
                int maxCount = Mathf.Max(scheduleCount, listCount);

                if (maxCount == 0)
                {
                    Debug.LogError($"[MultiMax] {__instance.name} has NO attack data!");
                    return;
                }

                // CRITICAL FIX: Clamp index based on the actual list being used
                if (__instance.m_AttackScheduleIndex < 0 || __instance.m_AttackScheduleIndex >= maxCount)
                {
                    Debug.LogWarning($"[MultiMax] {__instance.name} index out of range: {__instance.m_AttackScheduleIndex}/{maxCount}, resetting to 0");
                    __instance.m_AttackScheduleIndex = 0;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] FixAttackScheduleIndex error: {e}");
            }
        }
    }

    [PatchType(typeof(EnemyDummy))]
    public static class EnemyDummyInitCombatPatch
    {
        [PatchMethod("InitEnemyDummyForCombat")]
        [PatchPosition(Postfix)]
        public static void enemyDummyDebug(EnemyDummy __instance)
        {
            try
            {
                Debug.Log($"[MultiMax] InitEnemyDummyForCombat COMPLETE: {__instance.name}");
                Debug.Log($"[MultiMax]   - AttackSchedule: {(__instance.m_AttackSchedule != null ? "OK" : "NULL")}");
                Debug.Log($"[MultiMax]   - Schedule count: {__instance.m_AttackSchedule?.m_Schedule?.Count ?? -1}");
                Debug.Log($"[MultiMax]   - AttackScheduleIndex: {__instance.m_AttackScheduleIndex}");
                Debug.Log($"[MultiMax]   - FID: {__instance.FID.m_TurnIndex}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] InitCombatDebug error: {e}");
            }
        }
        [PatchType(typeof(EncounterSessionMC))]
        public static class EnsureEnemyStatusesForAllDummiesPatch
        {
            [PatchMethod("CommenceBattleRPC")]
            [PatchPosition(Prefix)]
            public static void commenceBattle(EncounterSessionMC __instance)
            {
                try
                {
                    const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy;

                    var fldStatuses = typeof(EncounterSessionMC).GetField("m_EnemyStatuses", BF);
                    var statuses = fldStatuses?.GetValue(__instance) as IDictionary;
                    if (statuses == null) return;

                    var enemyDummies = EncounterSession.Instance?.m_EnemyDummies;
                    if (enemyDummies == null) return;

                    var statusValueType = fldStatuses.FieldType.GetGenericArguments()[1];
                    var ctor = statusValueType.GetConstructor(new[] { typeof(string), typeof(HexLandID), typeof(bool), typeof(int) });
                    if (ctor == null) return;

                    foreach (var kv in enemyDummies)
                    {
                        var fid = kv.Key;
                        var dummy = kv.Value;
                        if (dummy == null) continue;

                        if (!statuses.Contains(fid))
                        {
                            var status = ctor.Invoke(new object[] { dummy.m_EnemyType, null, true, 0 });
                            statuses[fid] = status;
                            Debug.Log($"[MultiMax] Added EnemyStatus for {dummy.m_EnemyType} (turn {fid.m_TurnIndex})");
                        }
                    }

                    Debug.Log($"[MultiMax] EnemyStatuses count = {statuses.Count}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MultiMax] EnsureEnemyStatusesForAllDummiesPatch error: {e}");
                }
            }
        }
        [PatchType(typeof(CharacterDummy))]
        public class CharacterDummyActionMoveFix
        {
            [PatchMethod("ActionMove")]
            [PatchPosition(Prefix)]
            public static bool SafeMove(ref CharacterDummy __instance, Animator _an, AnimatorStateInfo _info)
            {
                try
                {
                    // Validate victim exists
                    if (__instance.m_CurrentVictimID == null ||
                        !EncounterSession.Instance.m_Dummies.ContainsKey(__instance.m_CurrentVictimID))
                    {
                        Debug.LogWarning($"[MultiMax] Invalid victim ID during ActionMove for {__instance.name}, skipping");
                        return false;
                    }

                    var victim = EncounterSession.Instance.m_Dummies[__instance.m_CurrentVictimID];
                    if (victim == null)
                    {
                        Debug.LogWarning($"[MultiMax] Null victim during ActionMove for {__instance.name}");
                        return false;
                    }

                    var slide = __instance.m_AttackSlide;
                    if (slide == null)
                    {
                        Debug.LogWarning($"[MultiMax] Null AttackSlide for {__instance.name}");
                        return false;
                    }

                    // Validate and clamp DioramaTargetIndex
                    if (slide.m_Distances == null || slide.m_Distances.Length == 0)
                    {
                        Debug.LogWarning($"[MultiMax] Empty m_Distances array for {__instance.name}");
                        return false;
                    }

                    if (victim.m_DioramaTargetIndex >= slide.m_Distances.Length)
                    {
                        int clamped = Mathf.Clamp(victim.m_DioramaTargetIndex, 0, slide.m_Distances.Length - 1);
                        Debug.LogWarning($"[MultiMax] Clamping DioramaTargetIndex {victim.m_DioramaTargetIndex} → {clamped}");
                        victim.m_DioramaTargetIndex = clamped;
                    }

                    return true; // run original
                }
                catch (Exception e)
                {
                    Debug.LogError($"[MultiMax] SafeMove exception: {e}");
                    return false;
                }
            }
        }
        [PatchType(typeof(Diorama))]
        public static class DioramaEnemyLayoutPatch_CustomAttr
        {
            [PatchMethod("_resetTargetQueue")]
            [PatchPosition(Postfix)]
            public static void enemyLayoutPatch(ref Diorama __instance)
            {
                try
                {
                    if (__instance == null) return;
                    if (__instance.m_EnemyTargets == null || __instance.m_PlayerTargets == null) return;

                    int enemies = __instance.m_EnemyTargets.Count;
                    int players = __instance.m_PlayerTargets.Count;
                    if (enemies == 0 || players == 0) return;

                    EnsureSlides(__instance.m_PlayerTargets);
                    EnsureSlides(__instance.m_EnemyTargets);

                    foreach (var playerT in __instance.m_PlayerTargets)
                    {
                        var slide = playerT.GetComponent<DummyAttackSlide>();
                        if (slide == null) continue;

                        if (slide.m_Distances == null || slide.m_Distances.Length < enemies)
                            slide.m_Distances = new float[enemies];

                        int idx = 0;
                        foreach (var enemyT in __instance.m_EnemyTargets)
                        {
                            float dist = Vector3.Distance(enemyT.position, playerT.position) * __instance.m_AttackDistanceScale;
                            slide.m_Distances[idx++] = dist;
                        }
                    }

                    foreach (var enemyT in __instance.m_EnemyTargets)
                    {
                        var slide = enemyT.GetComponent<DummyAttackSlide>();
                        if (slide == null) continue;

                        if (slide.m_Distances == null || slide.m_Distances.Length < players)
                            slide.m_Distances = new float[players];

                        int idx = 0;
                        foreach (var playerT in __instance.m_PlayerTargets)
                        {
                            float dist = Vector3.Distance(enemyT.position, playerT.position) * __instance.m_AttackDistanceScale;
                            slide.m_Distances[idx++] = dist;
                        }
                    }
                }
                catch (Exception e)
                {
                    Main.Log($"[MultiMax] DioramaEnemyLayoutPatch error: {e}");
                }

                try
                {
                    var diorama = EncounterSession.Instance?.m_ActiveDiorama;
                    if (diorama == null) return;

                    int enemyCount = diorama.m_EnemyTargets?.Count ?? 0;
                    int playerCount = diorama.m_PlayerTargets?.Count ?? 0;
                    if (enemyCount == 0 || playerCount == 0) return;

                    var allSlides = UnityEngine.Object.FindObjectsOfType<DummyAttackSlide>();
                    foreach (var slide in allSlides)
                    {
                        if (slide == null) continue;

                        int size = Mathf.Max(enemyCount, playerCount);
                        if (slide.m_Distances == null || slide.m_Distances.Length < size)
                            slide.m_Distances = new float[size];

                        for (int i = 0; i < size; i++)
                        {
                            var p = diorama.m_PlayerTargets[Mathf.Min(i, playerCount - 1)];
                            var e = diorama.m_EnemyTargets[Mathf.Min(i, enemyCount - 1)];
                            slide.m_Distances[i] = Vector3.Distance(p.position, e.position) * diorama.m_AttackDistanceScale;
                        }
                    }
                }
                catch (Exception e)
                {
                    Main.Log($"[MultiMax] Resync error: {e}");
                }
            }
            private static void EnsureSlides(List<Transform> list)
            {
                if (list == null) return;
                foreach (var t in list)
                {
                    if (t == null) continue;
                    if (t.GetComponent<DummyAttackSlide>() == null)
                        t.gameObject.AddComponent<DummyAttackSlide>();
                }
            }
        }
    }
}

