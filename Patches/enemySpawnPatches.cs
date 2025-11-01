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
    [PatchType(typeof(EnemyDummy))]
    public static class EnemySchedule_SafeDefaults
    {
        [PatchMethod("InitEnemyDummyForCombat")]
        [PatchPosition(Postfix)]
        public static void EnsureSchedule(EnemyDummy __instance)
        {
            if (__instance.m_AttackSchedule == null || __instance.m_AttackSchedule.m_Schedule == null)
            {
                // Provide a 1-step harmless fallback to avoid null deref / OOB
                __instance.m_AttackScheduleList = new List<AttackSchedule.AttackType> { AttackSchedule.AttackType.Normal };
                __instance.m_AttackScheduleIndex = 0;
                Debug.Log($"[MultiMax] {__instance.name} had NULL schedule → set harmless fallback");
            }
            else if (__instance.m_AttackScheduleList == null || __instance.m_AttackScheduleList.Count == 0)
            {
                __instance.m_AttackScheduleList = new List<AttackSchedule.AttackType>(__instance.m_AttackSchedule.m_Schedule);
                __instance.m_AttackScheduleIndex = 0;
            }
        }

        [PatchMethod("SetAttackDecision")]
        [PatchPosition(Prefix)]
        public static bool GuardDecision(EnemyDummy __instance)
        {
            int sched = __instance.m_AttackSchedule?.m_Schedule?.Count ?? 0;
            int list = __instance.m_AttackScheduleList?.Count ?? 0;
            if (Mathf.Max(sched, list) == 0)
            {
                // No real action available → just advance the round
                Debug.Log($"[MultiMax] {__instance.name} has no schedule → skipping enemy action");
                EncounterSessionMC.Instance?.StartNextCombatRound2();
                return false;
            }
            if (__instance.m_AttackScheduleIndex < 0 || __instance.m_AttackScheduleIndex >= Mathf.Max(sched, list))
                __instance.m_AttackScheduleIndex = 0;

            return true;
        }
    }
    [PatchType(typeof(EnemyDummy))]
    public static class EnemyDummy_SkipDeadVictims
    {
        [PatchMethod("SetAttackDecision")]
        [PatchPosition(Prefix)]
        public static bool GuardAgainstDeadVictim(EnemyDummy __instance)
        {
            var enc = EncounterSession.Instance;
            if (enc?.m_PlayerDummies == null) return true;

            // Try get current victim
            if (__instance.m_CurrentVictimID != null &&
                enc.m_Dummies.TryGetValue(__instance.m_CurrentVictimID, out var victim))
            {
                if (victim == null || !victim.m_IsAlive)
                {
                    Debug.Log($"[MultiMax] {__instance.name} had dead victim → reassigning target");

                    // Pick first alive player dummy
                    var newVictim = enc.m_PlayerDummies.Values
                        .FirstOrDefault(p => p != null && p.m_IsAlive && p.m_IsAlive);

                    if (newVictim != null)
                    {
                        __instance.m_CurrentVictimID = newVictim.FID;
                        Debug.Log($"[MultiMax] {__instance.name} retargeted to {newVictim.name}");
                    }
                    else
                    {
                        // No valid targets left → skip attack completely
                        Debug.Log($"[MultiMax] {__instance.name} found no alive players → skipping SetAttackDecision");
                        return false;
                    }
                }
            }
            else
            {
                // No target set at all → choose a live one
                var newVictim = enc.m_PlayerDummies.Values
                    .FirstOrDefault(p => p != null && p.m_IsAlive && p.m_IsAlive);

                if (newVictim == null)
                {
                    Debug.Log($"[MultiMax] {__instance.name} found no victim to target → skipping SetAttackDecision");
                    return false;
                }

                __instance.m_CurrentVictimID = newVictim.FID;
                Debug.Log($"[MultiMax] {__instance.name} assigned fallback victim {newVictim.name}");
            }

            return true; // continue with valid victim
        }
    }
    
    [PatchType(typeof(EncounterSession))]
    public static class ForceExpandEnemyTypes
    {
        [PatchMethod("InitEnemyDummiesForCombat")]
        [PatchPosition(Prefix)]
        public static void ExpandBeforeSpawn(EncounterSession __instance, ref string[] _enemyTypes)
        {
            try
            {
                if (_enemyTypes == null || _enemyTypes.Length == 0)
                    return;

                int playerCount = __instance.m_PlayerDummies?.Count ?? 4;
                if (playerCount < 1) playerCount = 4;

                if (_enemyTypes.Length >= playerCount)
                {
                    Log($"[MultiMax] Enemy types already sufficient: {_enemyTypes.Length}");
                    return;
                }

                // --- Forbidden / boss identifiers ---
                string[] forbidden = {
                "mimic", "boss", "scourge", "kraken", "mindlord", "snowqueen", "lavaqueen",
                "lich", "mecha", "demon", "dragon", "seaSerpent", "frostKnight"
            };

                // Detect indices of bosses or forbidden types
                var forbiddenIndices = new HashSet<int>();
                for (int i = 0; i < _enemyTypes.Length; i++)
                {
                    string e = _enemyTypes[i];
                    if (forbidden.Any(f => e.IndexOf(f, StringComparison.OrdinalIgnoreCase) >= 0))
                        forbiddenIndices.Add(i);
                }

                // --- Determine which enemies are safe to clone ---
                var cloneable = _enemyTypes
                    .Select((name, idx) => new { name, idx })
                    .Where(x => !forbiddenIndices.Contains(x.idx))
                    .Select(x => x.name)
                    .ToList();

                if (cloneable.Count == 0)
                {
                    Log($"[MultiMax] All enemies are special ({string.Join(", ", _enemyTypes)}) → skipping expansion");
                    return;
                }

                // --- Build final expanded list ---
                var expandedTypes = new List<string>(_enemyTypes);
                while (expandedTypes.Count < playerCount)
                {
                    // Prefer cloning guards (non-forbidden enemies)
                    string next = cloneable[expandedTypes.Count % cloneable.Count];
                    expandedTypes.Add(next);
                }

                _enemyTypes = expandedTypes.ToArray();

                Log($"[MultiMax] FORCED expansion in InitEnemyDummiesForCombat: {string.Join(", ", _enemyTypes)}");
            }
            catch (Exception ex)
            {
                Log($"[MultiMax] ForceExpandEnemyTypes error: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
    
    [PatchType(typeof(EncounterSessionMC))]
    public static class ExpandEnemyListBeforeDeserialize
    {
        [PatchMethod("InitiateEncounterSessionRPC")]
        [PatchPosition(Prefix)]
        public static void InitiateEncounter_ExpandEnemiesPrefix(
            FTKPlayerID _thisPlayer,
            FTKPlayerID[] _players,
            ref string _sessionJSON,
            ContinueFSM _cfsm)
        {
            try
            {
                if (string.IsNullOrEmpty(_sessionJSON)) return;

                var settings = GameCache.Cache.JSON.GetTypicalSettings();
                var sessionData = JsonConvert.DeserializeObject(_sessionJSON, typeof(EncounterSessionData), settings) as EncounterSessionData;
                if (sessionData == null || sessionData.EncounterDatas == null || sessionData.EncounterDatas.Count == 0)
                    return;

                int playerCount = Math.Max(1, _players?.Length ?? 1);
                int totalChanged = 0;

                foreach (var enc in sessionData.EncounterDatas)
                {
                    if (enc == null) continue;
                    if (enc.EncounterType != MiniHexDungeon.EncounterType.Enemy)
                        continue;

                    var types = enc.EnemyTypes;
                    if (types == null || types.Length == 0)
                        continue;

                    var list = new List<string>();
                    for (int i = 0; i < playerCount; i++)
                    {
                        list.Add(types[i % types.Length]);
                    }

                    enc.EnemyTypes = list.ToArray();
                    totalChanged++;
                    Log($"[MultiMax] Encounter expanded: {string.Join(", ", enc.EnemyTypes)}");
                }

                if (totalChanged > 0)
                {
                    _sessionJSON = JsonConvert.SerializeObject(sessionData, settings);
                    Log($"[MultiMax] Expanded {totalChanged} encounter(s) → {playerCount} enemies each");
                }
            }
            catch (Exception ex)
            {
                Log($"[MultiMax] ExpandEnemyList error: {ex.Message}\n{ex.StackTrace}");
            }
        }
    }

    [PatchType(typeof(EncounterSessionMC))]
    static class EnsureFullEnemyListPatch
    {
        [PatchMethod("InitiateEncounterSessionRPC")]
        [PatchPosition(Postfix)]
        public static void ExpandEnemiesAfterInit(
            EncounterSessionMC __instance,
            FTKPlayerID[] _players)
        {
            try
            {
                var sessionData = __instance.m_SessionData;
                if (sessionData == null || sessionData.EncounterDatas == null || sessionData.EncounterDatas.Count == 0)
                    return;

                int playerCount = Math.Max(1, _players?.Length ?? 1);

                foreach (var encounter in sessionData.EncounterDatas)
                {
                    if (encounter == null) continue;
                    if (encounter.EncounterType != MiniHexDungeon.EncounterType.Enemy)
                        continue;

                    var types = encounter.EnemyTypes;
                    if (types == null || types.Length == 0)
                        continue;

                    var newList = new List<string>();
                    for (int i = 0; i < playerCount; i++)
                    {
                        newList.Add(types[i % types.Length]);
                    }

                    encounter.EnemyTypes = newList.ToArray();
                    Log($"[MultiMax] PostInit enemy list: {string.Join(", ", encounter.EnemyTypes)}");
                }
            }
            catch (Exception ex)
            {
                Log($"[MultiMax] PostInit expand error: {ex.Message}");
            }
        }
    }

    [PatchType(typeof(Diorama))]
    static class Diorama_ResetTargetQueue_Patch
    {
        [PatchMethod("_resetTargetQueue")]
        [PatchPosition(Postfix)]
        public static void resetTargetPatch(Diorama __instance)
        {
            int enemyCount = 0;
            var enemyProp = typeof(Diorama).GetProperty("_dioramaEnemyCount", BindingFlags.NonPublic | BindingFlags.Instance);
            if (enemyProp != null)
                enemyCount = (int)(enemyProp.GetValue(__instance, null) ?? 0);

            foreach (var tr in __instance.m_PlayerTargets)
            {
                var slide = tr.GetComponent<DummyAttackSlide>();
                if (slide == null) continue;
                if (slide.m_Distances == null || slide.m_Distances.Length < enemyCount)
                    slide.m_Distances = new float[enemyCount];
            }

            foreach (var tr in __instance.m_EnemyTargets)
            {
                var slide = tr.GetComponent<DummyAttackSlide>();
                if (slide == null) continue;


                int playerCount = 0;
                var playerProp = typeof(Diorama).GetProperty("_dioramaPlayerCount", BindingFlags.NonPublic | BindingFlags.Instance);
                if (playerProp != null)
                    playerCount = (int)(playerProp.GetValue(__instance, null) ?? 0);
                if (playerCount < 1)
                    playerCount = __instance.m_PlayerTargets.Count;
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

                __instance.m_AttackSlide = slide;

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
    [PatchType(typeof(CharacterDummy))]
    public static class Guard_EngageBattle_WhenNoTargets
    {
        [PatchMethod("EngageBattle")]
        [PatchPosition(Prefix)]
        public static bool Guard(CharacterDummy __instance, int _randomCheck, bool _isFirstAttack, FTKPlayerID _playerVictim, bool _cheerIfDie)
        {
            var enc = EncounterSession.Instance;
            if (enc?.m_EnemyDummies == null) return true;

            bool anyAlive = enc.m_EnemyDummies.Values.Any(d => d != null && d.m_IsAlive && d.m_CurrentHealth > 0);

            // allow EngageBattle to continue so victory/flee logic runs
            if (!anyAlive)
            {
                Log("[MultiMax] 0 enemy alive → allow EngageBattle to finish cleanup");
                return true;
            }

            return true;
        }
    }
    [PatchType(typeof(CharacterDummy))]
    public static class CharacterDummy_EngageBattle_Fixes
    {
        [PatchMethod("EngageBattle")]
        [PatchPosition(Prefix)]
        public static void EnsureCameraTarget(CharacterDummy __instance, int _randomCheck, bool _isFirstAttack, FTKPlayerID _playerVictim, bool _cheerIfDie)
        {
            try
            {
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
        public static void gageForNonOwner(CharacterDummy __instance)
        {
            try
            {
                var enemy = __instance as EnemyDummy;
                if (enemy != null && !enemy.FID.IsPlayer() && !enemy.IsOwner)
                {
                    var fsm = enemy.m_EngageBattleFSM;
                    if (fsm != null)
                    {
                        if (!fsm.gameObject.activeSelf)
                            fsm.gameObject.SetActive(true);
                        if (enemy != null && !enemy.FID.IsPlayer() && !enemy.IsOwner)
                        {
                            var enc = EncounterSession.Instance;
                            if (enc?.m_EnemyDummies == null || !enc.m_EnemyDummies.Values.Any(d => d != null && d.m_IsAlive && d.m_CurrentHealth > 0))
                                return;
                        }
                        fsm.SendEvent("BattleEngage");
                        Debug.Log($"[MultiMax] Forced EngageBattle FSM on non-owner for {enemy.name}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] ForceEngageForNonOwner error: {e}");
            }
        }
    }
    [PatchType(typeof(EncounterSessionMC))]
    public static class FixBattleEndConsistency
    {
        [PatchMethod("StartNextCombatRound2")]
        [PatchPosition(Prefix)]
        public static void CleanDeadReferences(EncounterSessionMC __instance)
        {
            try
            {
                var enc = EncounterSession.Instance;
                if (enc == null || enc.m_EnemyDummies == null) return;

                // 1️⃣ Remove null or dead enemies from dictionaries
                var keysToRemove = enc.m_EnemyDummies
                    .Where(kv => kv.Value == null || !kv.Value.m_IsAlive || kv.Value.m_CurrentHealth <= 0)
                    .Select(kv => kv.Key)
                    .ToList();

                // 2️⃣ Purge FightOrder of any removed or dead enemies
                var field = typeof(EncounterSessionMC).GetField("m_FightOrder",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var fightOrder = field?.GetValue(__instance) as IList;
                if (fightOrder != null)
                {
                    var toRemove = new List<object>();
                    foreach (var entry in fightOrder)
                    {
                        var pidField = entry.GetType().GetField("m_Pid");
                        var pid = pidField?.GetValue(entry);
                        if (pid == null) continue;

                        bool isEnemy = (bool)pid.GetType().GetMethod("IsEnemy").Invoke(pid, null);
                        if (!isEnemy) continue;

                        if (!enc.m_EnemyDummies.ContainsKey((FTKPlayerID)pid))
                            toRemove.Add(entry);
                    }

                    foreach (var dead in toRemove)
                        fightOrder.Remove(dead);

                    foreach (var key in keysToRemove)
                        enc.m_EnemyDummies.Remove(key);
                        
                }
                if (!enc.m_EnemyDummies.Values.Any(d => d != null && d.m_IsAlive && d.m_CurrentHealth > 0))
                {
                    Log("[MultiMax] All enemies dead — forcing combat end");
                    __instance.m_IsInCombat = false;
                    GameLogic.Instance.StopAllCoroutines();
                }


                // 3️⃣ Log sanity check
                Log($"[MultiMax] Purged {keysToRemove.Count} dead enemies, fight order now clean ({enc.m_EnemyDummies.Count} left)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] FixBattleEndConsistency error: {e}");
            }
        }
    }


    [PatchType(typeof(uiBattleStanceButtons))]
    public static class SafeInitializeWithValidEnemy
    {
        [PatchMethod("Initialize")]
        [PatchPosition(Postfix)]
        public static void EnsureValidEnemySelected(uiBattleStanceButtons __instance, bool _refresh)
        {
            try
            {
                var enc = EncounterSession.Instance;
                if (enc?.m_EnemyDummies == null) return;

                var currentEnemy = enc.GetCurrentEnemy();
                if (currentEnemy == null || !currentEnemy.m_IsAlive || currentEnemy.m_CurrentHealth <= 0)
                {
                    // Find first alive enemy
                    var firstAlive = enc.m_EnemyDummies.Values.FirstOrDefault(e =>
                        e != null && e.m_IsAlive && e.m_CurrentHealth > 0);

                    if (firstAlive != null)
                    {
                        enc.m_CurrentEnemy = firstAlive.FID;

                        // Update player dummy's victim
                        var cow = GameLogic.Instance.GetCurrentCombatCOW();
                        if (cow?.m_CurrentDummy != null)
                        {
                            cow.m_CurrentDummy.m_CurrentVictimID = firstAlive.FID;
                        }

                        Log($"[MultiMax] Assigned valid enemy target: {firstAlive.name}");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] SafeInitializeWithValidEnemy error: {e}");
            }
        }
    }

    [PatchType(typeof(EncounterSession))]
    public static class SafeGetCurrentEnemy
    {
        [PatchMethod("GetCurrentEnemy")]
        [PatchPosition(Postfix)]
        public static void ValidateCurrentEnemy(EncounterSession __instance, ref EnemyDummy __result)
        {
            try
            {
                if (__result == null || !__result.m_IsAlive || __result.m_CurrentHealth <= 0)
                {
                    var firstAlive = __instance.m_EnemyDummies?.Values.FirstOrDefault(e =>
                        e != null && e.m_IsAlive && e.m_CurrentHealth > 0);

                    if (firstAlive != null)
                    {
                        __instance.m_CurrentEnemy = firstAlive.FID;
                        __result = firstAlive;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] SafeGetCurrentEnemy error: {e}");
            }
        }
    }
    [PatchType(typeof(EncounterSessionMC))]
    public static class ShowBattleUIOnCombatStart
    {
        [PatchMethod("CommenceBattleRPC")]
        [PatchPosition(Postfix)]
        public static void ShowUI()
        {
            try
            {
                var ui = FTKUI.Instance;
                if (ui == null) return;

                ui.StartCoroutine(ShowAfterDelay());
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] ShowBattleUIOnCombatStart error: {e}");
            }
        }

        private static IEnumerator ShowAfterDelay()
        {
            yield return new WaitForSeconds(0.5f);

            var stance = FTKUI.Instance?.m_BattleStanceButtons ?? UnityEngine.Object.FindObjectOfType<uiBattleStanceButtons>();
            if (stance == null)
            {
                Debug.LogWarning("[MultiMax] uiBattleStanceButtons still missing after battle start");
                yield break;
            }

            // Disable UI at battle start — only shown when player’s turn begins
            stance.gameObject.SetActive(false);
            Debug.Log("[MultiMax] Battle stance UI created but hidden until player turn");
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
                var enc = EncounterSession.Instance;
                var diorama = enc?.m_ActiveDiorama;
                if (enc == null || diorama == null) return;

                var enemies = enc.m_EnemyDummies;
                if (enemies == null || enemies.Count == 0) return;

                // 1. Expand target list if too short
                while (diorama.m_EnemyTargets.Count < enemies.Count)
                {
                    var prefab = diorama.m_EnemyTargets[0];
                    var clone = UnityEngine.Object.Instantiate(prefab, prefab.parent);
                    clone.name = $"Enemy Target {diorama.m_EnemyTargets.Count}";
                    diorama.m_EnemyTargets.Add(clone);
                }

                // 2. Rebuild target queue
                var queueField = typeof(Diorama).GetField("m_EnemyTargetQueue",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var queue = queueField?.GetValue(diorama) as IDictionary;
                queue?.Clear();

                var enemyList = enemies.Values
                    .Where(e => e != null)
                    .OrderBy(e => e.FID.m_TurnIndex)
                    .ThenBy(e => e.name)
                    .ToList();

                for (int i = 0; i < enemyList.Count; i++)
                {
                    if (i >= diorama.m_EnemyTargets.Count) break;
                    queue?.Add(enemyList[i].FID, diorama.m_EnemyTargets[i]);
                }

                // Try to set EncounterSession.m_EnemyList if it exists
                var listField = typeof(EncounterSession).GetField("m_EnemyList",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (listField != null)
                {
                    listField.SetValue(enc, enemyList);
                    Debug.Log($"[MultiMax] Synced EncounterSession.m_EnemyList ({enemyList.Count})");
                }
                else
                {
                    Debug.Log($"[MultiMax] EncounterSession has no m_EnemyList field, skipping sync ({enemyList.Count} enemies)");
                }

                // 4. Align enemies to targets
                for (int i = 0; i < enemyList.Count; i++)
                {
                    if (i >= diorama.m_EnemyTargets.Count) break;
                    var dummy = enemyList[i];
                    var target = diorama.m_EnemyTargets[i];

                    dummy.transform.position = target.position;
                    dummy.transform.rotation = target.rotation;
                    dummy.m_DioramaTargetIndex = i;
                }
                // Adjust Diorama enemy spacing dynamically
                float totalWidth = 6f; // width of battlefield in units
                int enemyCount = enemyList.Count;
                for (int i = 0; i < enemyCount; i++)
                {
                    float x = -totalWidth / 2f + (totalWidth / (enemyCount - 1)) * i;
                    var target = diorama.m_EnemyTargets[i];
                    target.localPosition = new Vector3(x, target.localPosition.y, target.localPosition.z);
                }
                Debug.Log($"[MultiMax] Adjusted Diorama spacing for {enemyCount} enemies");
                Debug.Log($"[MultiMax] Diorama targets resynced ({enemyList.Count})");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] DioramaSyncPatch error: {e}");
            }
        }
    }

    [PatchType(typeof(EncounterSessionMC))]
    static class ComputeCombatInitiativePatch_Safe
    {
        [PatchMethod("ComputeCombatInitiative")]
        [PatchPosition(Postfix)]
        public static void FilterDead(EncounterSessionMC __instance, ref List<FTKPlayerID> __result)
        {
            var enc = EncounterSession.Instance;
            if (enc?.m_EnemyDummies == null) return;

            // rimuovi PIDs di nemici morti
            var deadEnemies = new HashSet<FTKPlayerID>(
                enc.m_EnemyDummies.Where(p => p.Value == null || !p.Value.m_IsAlive || p.Value.m_CurrentHealth <= 0)
                                  .Select(p => p.Key)
            );
            __result.RemoveAll(pid => deadEnemies.Contains(pid));

            // se NON esistono nemici vivi, non aggiungere nulla
            bool anyAlive = enc.m_EnemyDummies.Any(p => p.Value != null && p.Value.m_IsAlive && p.Value.m_CurrentHealth > 0);
            if (!anyAlive) return;

            // (opz.) assicurati che i vivi ci siano
            var alive = new HashSet<FTKPlayerID>(__result);
            foreach (var kv in enc.m_EnemyDummies)
                if (kv.Value != null && kv.Value.m_IsAlive && kv.Value.m_CurrentHealth > 0 && alive.Add(kv.Key))
                    __result.Add(kv.Key);
        }
    }

    [PatchType(typeof(CharacterDummy))]
    static class CharacterDummy_EngageAttack_Guard
    {
        [PatchMethod("EngageAttack")]
        [PatchPosition(Prefix)]
        public static bool GuardNoTargets(CharacterDummy __instance)
        {
            var enc = EncounterSession.Instance;
            if (enc?.m_EnemyDummies == null)
            {
                Log("[MultiMax] No encounter session in EngageAttack");
                return true;
            }

            bool anyAlive = enc.m_EnemyDummies.Values.Any(d => d != null && d.m_IsAlive && d.m_CurrentHealth > 0);
            if (!anyAlive)
            {
                Log("[MultiMax] No enemy targets → skip EngageAttack");
                return false;
            }

            // Ensure UI is enabled for player turns
            if (__instance.FID.IsPlayer())
            {
                var stance = GameObject.FindObjectOfType<uiBattleStanceButtons>();
                if (stance != null && !stance.gameObject.activeSelf)
                {
                    stance.gameObject.SetActive(true);
                    Log("[MultiMax] Re-enabled uiBattleStanceButtons");
                }
            }

            return true;
        }
    }

    [PatchType(typeof(EncounterSessionMC))]
    static class FixFightOrderRPCPatch_Safe
    {
        [PatchMethod("CommenceBattleRPC")]
        [PatchPosition(Postfix)]
        public static void RebuildFightOrderAliveOnly(EncounterSessionMC __instance)
        {
            const BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var field = typeof(EncounterSessionMC).GetField("m_FightOrder", BF);
            var fightOrder = field?.GetValue(__instance) as List<EncounterSessionMC.FightOrderEntry>;
            if (fightOrder == null) return;

            var enc = EncounterSession.Instance;
            if (enc?.m_EnemyDummies == null) return;

            // togli voci morte
            fightOrder.RemoveAll(e =>
            {
                if (!e.m_Pid.IsEnemy()) return false;
                return !enc.m_EnemyDummies.TryGetValue(e.m_Pid, out var d) || d == null || !d.m_IsAlive || d.m_CurrentHealth <= 0;
            });

            // (opz.) aggiungi eventuali vivi mancanti
            var existing = new HashSet<FTKPlayerID>(fightOrder.Select(x => x.m_Pid));
            var ctor = typeof(EncounterSessionMC.FightOrderEntry).GetConstructor(new[] { typeof(FTKPlayerID), typeof(int) });
            int idx = fightOrder.Count;
            foreach (var kv in enc.m_EnemyDummies)
            {
                var d = kv.Value;
                if (d != null && d.m_IsAlive && d.m_CurrentHealth > 0 && existing.Add(kv.Key))
                    fightOrder.Add((EncounterSessionMC.FightOrderEntry)ctor.Invoke(new object[] { kv.Key, idx++ }));
            }
        }
    }

    [PatchType(typeof(uiEnemyHUD))]
    public static class ResetEnemyHUDForNewEncounter
    {
        [PatchMethod("InitializeEnemyHud")]
        [PatchPosition(Prefix)]
        public static void ClearOldReferencesBeforeInit(uiEnemyHUD __instance, EnemyDummy _ed, int _index)
        {
            try
            {
                // Se il dizionario contiene riferimenti vecchi, puliscili
                if (__instance.m_EnemyHudDictionary != null && __instance.m_EnemyHudDictionary.Count > 0)
                {
                    // Rimuovi entry con dummy morti o null
                    var deadKeys = new List<EnemyDummy>();
                    foreach (var kv in __instance.m_EnemyHudDictionary)
                    {
                        if (kv.Key == null || !kv.Key.m_IsAlive || kv.Key.m_CurrentHealth <= 0)
                        {
                            deadKeys.Add(kv.Key);
                        }
                    }
                    foreach (var key in deadKeys.ToList())
                        __instance.m_EnemyHudDictionary.Remove(key);

                    if (deadKeys.Count > 0)
                    {
                        Log($"[MultiMax] Cleaned {deadKeys.Count} dead enemy HUD references");
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] ResetEnemyHUD error: {e}");
            }
        }
    }

    [PatchType(typeof(EncounterSession))]
    public static class CleanupBetweenEncountersPatch
    {
        [PatchMethod("StartEncounterSession")]
        [PatchPosition(Prefix)]
        public static void ResetForNewEncounter(EncounterSession __instance)
        {
            try
            {
                // Clear combat data only — do NOT destroy UI
                if (__instance.m_EnemyDummies != null)
                    __instance.m_EnemyDummies.Clear();

                // Reset camera
                var camManager = UnityEngine.Object.FindObjectOfType<CameraCutManager>();
                if (camManager != null)
                {
                    var field = typeof(CameraCutManager).GetField("m_CurrentEnemyDummy",
                        BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
                    field?.SetValue(camManager, null);
                }

                // Reset diorama queue only
                if (__instance.m_ActiveDiorama != null)
                {
                    var queueField = typeof(Diorama).GetField("m_EnemyTargetQueue",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    var queue = queueField?.GetValue(__instance.m_ActiveDiorama) as IDictionary;
                    queue?.Clear();
                }

                Debug.Log("[MultiMax] Cleaned data for new encounter (UI preserved)");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] CleanupBetweenEncountersPatch error: {e}");
            }
        }
    }
    [PatchType(typeof(FTKUI))]
    public static class FTKUI_RestoreBattleUI
    {
        [PatchMethod("Awake")] // <-- esiste davvero
        [PatchPosition(Postfix)]
        public static void RestoreBattleUI(FTKUI __instance)
        {
            try
            {
                // Non creare nulla, non pulire nulla — solo usare i campi esistenti
                var stance = __instance.m_BattleStanceButtons;
                if (stance == null)
                {
                    Debug.LogWarning("[MultiMax] FTKUI Awake: m_BattleStanceButtons is null");
                    return;
                }

                // Evita errori in menù o overworld
                var enc = EncounterSession.Instance;
                bool inBattle = enc?.m_EnemyDummies?.Count > 0;

                if (inBattle)
                {
                    stance.gameObject.SetActive(true);
                    stance.Initialize(_refresh: true);
                    Debug.Log("[MultiMax] FTKUI Awake: Battle stance re-initialized (combat active)");
                }
                else
                {
                    stance.gameObject.SetActive(false);
                    Debug.Log("[MultiMax] FTKUI Awake: Battle stance hidden (no enemies)");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] RestoreBattleUI failed: {e}");
            }
        }
    }

    [PatchType(typeof(EncounterSessionMC))]
    public static class HideBattleUIWhenDone
    {
        [PatchMethod("ReturnToOverworld")]
        [PatchPosition(Postfix)]
        public static void HideUI()
        {
            try
            {
                var stance = UnityEngine.Object.FindObjectOfType<uiBattleStanceButtons>();
                if (stance != null && stance.gameObject.activeSelf)
                {
                    stance.gameObject.SetActive(false);
                    Debug.Log("[MultiMax] Stance UI hidden after ReturnToOverworld (combat ended)");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] HideBattleUIWhenDone error: {e}");
            }
        }
    }
    /*
    [PatchType(typeof(CharacterDummy))]
    public static class MarkEnemyDead_EnsureStatus
    {
        [PatchMethod("RespondToHit")]
        [PatchPosition(Postfix)]
        public static void EnsureEnemyStatusOnDeath(CharacterDummy __instance, bool _mainVictim)
        {
            try
            {
                var enemy = __instance as EnemyDummy;
                if (enemy == null) return;

                // Only act if REALLY dead
                if (enemy.m_IsAlive || enemy.m_CurrentHealth > 0) return;

                var mc = EncounterSessionMC.Instance;
                if (mc == null) return;
                if (!_mainVictim) return;
                // Only if the status exists and is still alive
                var statusesFld = typeof(EncounterSessionMC).GetField("m_EnemyStatuses", BindingFlags.Instance | BindingFlags.NonPublic);
                var statuses = statusesFld?.GetValue(mc) as IDictionary;
                if (statuses != null && statuses.Contains(enemy.FID))
                {
                    var status = statuses[enemy.FID];
                    var aliveFld = status.GetType().GetField("m_Alive", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (aliveFld != null && (bool)aliveFld.GetValue(status) == false) return; // already marked dead
                }
                EncounterSessionMC.Instance.RPCAllViaServer("CombatEnemyDie", new object[] { enemy.FID, mc.m_PlayerAttacker });
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] EnsureEnemyStatusOnDeath error: {e}");
            }
        }
    }*/

    [PatchType(typeof(uiEnemyHUD))]
    public static class SafeEnemyHUDOperations
    {
        [PatchMethod("SetEnemyHealth")]
        [PatchPosition(Prefix)]
        public static bool SafeSetHealth(uiEnemyHUD __instance, EnemyDummy _ed)
        {
            if (_ed == null || !__instance.m_EnemyHudDictionary.ContainsKey(_ed))
            {
                return false;
            }
            return true;
        }

        [PatchMethod("RefreshStatusIcons")]
        [PatchPosition(Prefix)]
        public static bool SafeRefreshIcons(uiEnemyHUD __instance, EnemyDummy _ed)
        {
            if (_ed == null || !__instance.m_EnemyHudDictionary.ContainsKey(_ed))
            {
                return false;
            }
            return true;
        }
    }

    [PatchType(typeof(uiActiveTime))]
    public static class SafeUIActiveTimePatch
    {
        [PatchMethod("Update")]
        [PatchPosition(Prefix)]
        public static bool SafeUpdate(uiActiveTime __instance)
        {
            var enc = EncounterSession.Instance;
            if (enc?.m_EnemyDummies == null) return true;

            bool anyAlive = enc.m_EnemyDummies.Values.Any(d => d != null && d.m_IsAlive && d.m_CurrentHealth > 0);
            if (!anyAlive)
            {
                // clear HUD highlights safely
                if (uiEnemyHUD.Instance != null)
                {
                    foreach (var hud in uiEnemyHUD.Instance.m_EnemyHudDictionary.Values)
                        hud?.SetCurrent(false);
                }

                foreach (var kv in __instance.m_ATPortraitTable.ToList())
                    kv.Value?.SetCurrent(false);

                var stance = GameObject.FindObjectOfType<uiBattleStanceButtons>();
                stance?.gameObject?.SetActive(false);

                return false; // skip original Update
            }

            return true; // let vanilla update run
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

    [PatchType(typeof(CharacterDummy))]
    public static class EnsureBattleUIOnPlayerDecision
    {
        [PatchMethod("EngageAttack")]
        [PatchPosition(Postfix)]
        public static void ForceInitializeBattleUI(CharacterDummy __instance)
        {
            try
            {
                if (__instance.FID.IsEnemy()) return;

                var stance = FTKUI.Instance?.m_BattleStanceButtons
                          ?? UnityEngine.Object.FindObjectOfType<uiBattleStanceButtons>();

                if (stance == null)
                {
                    Debug.LogError("[MultiMax] uiBattleStanceButtons not found!");
                    return;
                }

                Debug.Log($"[MultiMax] EngageAttack for {__instance.name}, TurnIndex={__instance.FID.m_TurnIndex}");

                bool isMyTurn = false;

                // ✅ Strategia 1: Singleplayer
                if (GameLogic.Instance?.IsSinglePlayer() ?? false)
                {
                    isMyTurn = true;
                    Debug.Log("[MultiMax] Singleplayer mode → my turn");
                }
                // ✅ Strategia 2: Usa la ownership map
                else if (OwnershipManager.IsMine(__instance.FID.m_TurnIndex))
                {
                    isMyTurn = true;
                    Debug.Log($"[MultiMax] OwnershipMap says {__instance.name} is mine → my turn");
                }
                // ✅ Strategia 3: Fallback su NetUtil.IsMyTurn()
                else
                {
                    isMyTurn = NetUtil.IsMyTurn();
                    Debug.Log($"[MultiMax] NetUtil.IsMyTurn() = {isMyTurn}");
                }

                if (isMyTurn)
                {
                    stance.gameObject.SetActive(true);
                    stance.Initialize(_refresh: false);
                    Debug.Log($"[MultiMax] ✅ Shown UI for {__instance.name}");
                }
                else
                {
                    stance.gameObject.SetActive(false);
                    Debug.Log($"[MultiMax] ❌ Hidden UI for {__instance.name}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] EnsureBattleUIOnPlayerDecision error: {e}");
            }
        }
    }

    [PatchType(typeof(EncounterSession))]
    public static class DebugDummyOwnership
    {
        [PatchMethod("InitPlayerDummiesForCombat")]
        [PatchPosition(Postfix)]
        public static void LogAllOwnership(EncounterSession __instance)
        {
            try
            {
                if (__instance.m_PlayerDummies == null) return;

                Debug.Log($"[MultiMax] === OWNERSHIP DEBUG (PhotonPlayer.ID={PhotonNetwork.player?.ID ?? -1}) ===");

                foreach (var kv in __instance.m_PlayerDummies)
                {
                    var dummy = kv.Value;
                    if (dummy == null) continue;

                    Debug.Log($"[MultiMax] {dummy.name}:");
                    Debug.Log($"  - TurnIndex: {dummy.FID.m_TurnIndex}");
                    Debug.Log($"  - PhotonID: {dummy.FID.m_PhotonID}");
                    Debug.Log($"  - IsOwner: {dummy.IsOwner}");
                }

                Debug.Log("[MultiMax] === END OWNERSHIP DEBUG ===");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] DebugDummyOwnership error: {e}");
            }
        }
    }

    [PatchType(typeof(CharacterDummy))]
    public static class DebugOwnership
    {
        [PatchMethod("EngageAttack")]
        [PatchPosition(Prefix)]
        public static void LogOwnership(CharacterDummy __instance)
        {
            if (__instance.FID.IsPlayer())
            {
                var mc = EncounterSessionMC.Instance;
                Debug.Log($"[MultiMax] DEBUG {__instance.name}:");
                Debug.Log($"  - IsOwner: {__instance.IsOwner}");
                Debug.Log($"  - FID.m_TurnIndex: {__instance.FID.m_TurnIndex}");
                Debug.Log($"  - FID.m_PhotonID: {__instance.FID.m_PhotonID}");
                Debug.Log($"  - PhotonNetwork.player.ID: {PhotonNetwork.player?.ID ?? -1}");

                if (mc != null)
                {
                    Debug.Log($"  - m_PlayerAttacker.m_TurnIndex: {mc.m_PlayerAttacker.m_TurnIndex}");
                }
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
    }
    
}