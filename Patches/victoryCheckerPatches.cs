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
using GridEditor;

namespace FTK_MultiMax_Rework.Patches
{
    [PatchType(typeof(uiActiveTime))]
    public static class ActiveTime_UpdateTimeline_Guard
    {
        [PatchMethod("UpdateTimeline")]
        [PatchPosition(Prefix)]
        public static bool SkipIfInactive(uiActiveTime __instance,
            System.Collections.Generic.Dictionary<EncounterSession.AnimEntryID, EncounterSession.AnimEntry> _animData,
            FTKPlayerID[] _fightOrderArray, int _startAt, int _animCount, ContinueFSM _cfsm)
        {
            if (__instance == null) return true;

            if (!__instance.gameObject.activeInHierarchy)
            {
                Debug.Log("[MultiMax] ActiveTime inactive → skipping timeline animation and continuing FSM.");
                _cfsm?.Continue();
                return false; // block original coroutine start
            }

            return true; // run vanilla if active
        }
    }

    [PatchType(typeof(EncounterSession))]
    public static class UpdateAttackTimeline2_Guard
    {
        [PatchMethod("UpdateAttackTimeline2")]
        [PatchPosition(Prefix)]
        public static bool SkipWhenVictory(EncounterSession __instance,
            System.Collections.Generic.Dictionary<EncounterSession.AnimEntryID, EncounterSession.AnimEntry> _animData,
            FTKPlayerID[] _fightOrderArray, ContinueFSM _cfsm)
        {
            try
            {
                var mc = EncounterSessionMC.Instance;
                // if combat already flagged off or no enemy alive → just continue, don't touch ActiveTime/UI
                bool mcOut = (mc != null && !mc.m_IsInCombat);
                bool noEnemy = __instance.m_EnemyDummies == null
                               || !__instance.m_EnemyDummies.Values.Any(d => d != null && d.m_IsAlive && d.m_CurrentHealth > 0);

                if (mcOut || noEnemy)
                {
                    _cfsm?.Continue();
                    return false; // skip original UpdateAttackTimeline2 (it starts the coroutine chain)
                }
            }
            catch (Exception e) { Debug.LogError($"[MultiMax] SkipWhenVictory error: {e}"); }
            return true; // run vanilla if still in combat
        }
    }

    [PatchType(typeof(EnemyDummy))]
    public static class DamageOverTime_DeathCheck
    {
        [PatchMethod("TakeSecondaryDamage")]
        [PatchPosition(Postfix)]
        public static void CheckForBleedDeath(EnemyDummy __instance, string _type, int _dmg,
            FTK_proficiencyTable.ID _profID, bool _spawnHud, FTKPlayerID _attackerID)
        {
            try
            {
                if (__instance == null) return;
                if (__instance.m_IsAlive && __instance.m_CurrentHealth > 0) return;

                var enc = EncounterSession.Instance;
                var mc = EncounterSessionMC.Instance;
                if (enc == null || mc == null) return;
                if (!enc.IsMasterClient) return;

                Debug.Log($"[MultiMax] Secondary-damage death detected on {__instance.name}, scheduling cleanup.");
                SafeCoroutine.Run(FinalizeBleedDeath(__instance.FID));
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] DamageOverTime_DeathCheck error: {e}");
            }
        }

        private static IEnumerator FinalizeBleedDeath(FTKPlayerID fid)
        {
            yield return new WaitForSeconds(0.3f);
            try
            {
                var enc = EncounterSession.Instance;
                var mc = EncounterSessionMC.Instance;
                if (enc == null || mc == null) yield break;

                var statusesFld = typeof(EncounterSessionMC).GetField("m_EnemyStatuses",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var statuses = statusesFld?.GetValue(mc) as IDictionary;
                if (statuses == null) yield break;

                if (statuses.Contains(fid))
                {
                    var status = statuses[fid];
                    var aliveFld = status.GetType().GetField("m_Alive",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    if (aliveFld != null && (bool)aliveFld.GetValue(status))
                    {
                        Debug.Log($"[MultiMax] Forcing CombatEnemyDie for {fid} (DoT death)");
                        mc.RPCAllViaServer("CombatEnemyDie", new object[] { fid, mc.m_PlayerAttacker });
                    }
                }

                SafeCoroutine.Run(ForceVictoryCheck());
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] FinalizeBleedDeath error: {e}");
            }
        }

        private static IEnumerator ForceVictoryCheck()
        {
            yield return new WaitForSeconds(0.4f);
            var enc = EncounterSession.Instance;
            if (enc == null) yield break;

            bool anyAlive = enc.m_EnemyDummies.Values.Any(e => e != null && e.m_IsAlive && e.m_CurrentHealth > 0);
            if (!anyAlive)
            {
                Debug.Log("[MultiMax] All enemies dead from DoT — invoking victory logic");
                SafeCoroutine.Run((IEnumerator)typeof(ForceVictoryOnTotalWipe)
                    .GetMethod("EndAfterDelay", BindingFlags.NonPublic | BindingFlags.Static)
                    ?.Invoke(null, null));
            }
        }
    }

    [PatchType(typeof(CharacterDummy))]
    public static class ForceVictoryOnTotalWipe
    {
        private static bool s_Ending;

        [PatchMethod("RespondToHit")]
        [PatchPosition(Postfix)]
        public static void AfterHit_EndIfAllEnemiesDead(CharacterDummy __instance)
        {
            try
            {
                var enc = EncounterSession.Instance;
                var mc = EncounterSessionMC.Instance;
                if (enc == null || mc == null) return;

                // Only host should drive the ending flow
                if (!enc.IsMasterClient) return;
                if (s_Ending) return;

                // Still any enemy alive?
                foreach (var kv in enc.m_EnemyDummies)
                {
                    var e = kv.Value;
                    if (e != null && e.m_IsAlive && e.m_CurrentHealth > 0 && !e.m_DidFlee)
                        return; // someone still alive → keep normal flow
                }

                // All enemies dead → force clean end after short delay to let death anims finish
                SafeCoroutine.Run(EndAfterDelay());
            }
            catch (Exception ex)
            {
                Debug.Log($"[MultiMax] ForceVictoryOnTotalWipe error: {ex}");
            }
        }

        private static IEnumerator EndAfterDelay()
        {
            yield return new WaitForSeconds(0.4f);

            var enc = EncounterSession.Instance;
            var mc = EncounterSessionMC.Instance;
            if (enc == null || mc == null) yield break;
            if (s_Ending) yield break;

            // Recheck after delay (another hit could have landed)
            bool anyAlive = false;
            foreach (var kv in enc.m_EnemyDummies)
            {
                var e = kv.Value;
                if (e != null && e.m_IsAlive && e.m_CurrentHealth > 0 && !e.m_DidFlee)
                {
                    anyAlive = true;
                    break;
                }
            }
            if (anyAlive) yield break;

            s_Ending = true;
            try
            {
                // Mark not in combat (MC + local), stop dangling coroutines (e.g., StepOutCR)
                mc.m_IsInCombat = false;
                enc.m_IsInCombat = false;
                GameLogic.Instance.StopAllCoroutines();

                // Stop the combat coroutine on MC directly
                var coroutinesField = typeof(EncounterSessionMC)
                    .GetField("m_UpdateTimeCR", BindingFlags.Instance | BindingFlags.NonPublic);
                var updateTimeCR = coroutinesField?.GetValue(mc) as Coroutine;
                if (updateTimeCR != null)
                {
                    mc.StopCoroutine(updateTimeCR);
                    coroutinesField.SetValue(mc, null);
                    Debug.Log("[MultiMax] Stopped UpdateTime_CR coroutine after total wipe");
                }
                
                // Play the victory camera cut for everyone
                enc.PlayVictoryCameraCutAllClient();

                // Reflect the private m_AckID needed by StartEndCombatSequence RPC
                const BindingFlags BF = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
                var ackFld = typeof(EncounterSession).GetField("m_AckID", BF);
                string ack = ackFld?.GetValue(enc) as string ?? string.Empty;

                // Host broadcasts the official end sequence (players win → _playEnemyVictory=false)
                if (enc.IsMasterClient)
                {
                    enc.RPCAllSelf("StartEndCombatSequence", new object[] { ack, /*_playEnemyVictory*/ false, /*_waitTime*/ 0.5f });
                }
            }
            finally
            {
                // small guard reset: allow future combats to end normally
                SafeCoroutine.Run(ResetGuardSoon());
            }
        }

        private static IEnumerator ResetGuardSoon()
        {
            yield return new WaitForSeconds(2f);
            s_Ending = false;
        }
    }

}
