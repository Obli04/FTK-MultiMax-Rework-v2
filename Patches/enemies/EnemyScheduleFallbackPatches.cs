// File: Patches/EnemyScheduleFallbackPatches.cs
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using FTK_MultiMax_Rework_v2.PatchHelpers;
using static FTK_MultiMax_Rework_v2.PatchHelpers.PatchPositions;
using static FTK_MultiMax_Rework_v2.Main;

namespace FTK_MultiMax_Rework_v2.Patches
{
    // 1) Capture a safe per-enemy (and global) fallback attack at init time
    [PatchType(typeof(EnemyDummy))]
    public static class EnemyScheduleFallbackInitPatch
    {
        private static readonly Dictionary<int, object> _perEnemyFallback = new Dictionary<int, object>();
        private static object _globalFallback;

        public static object GetPerEnemyFallback(EnemyDummy ed)
        {
            _perEnemyFallback.TryGetValue(ed.GetInstanceID(), out var atk);
            return atk ?? _globalFallback;
        }

        [PatchMethod("InitEnemyDummyForCombat")]
        [PatchPosition(Postfix)]
        public static void CaptureFallback(ref EnemyDummy __instance)
        {
            try
            {
                var schedule = __instance.m_AttackSchedule;
                if (schedule == null) return;

                var fld = schedule.GetType().GetField("m_AttackTypes",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (fld == null) return;

                var list = fld.GetValue(schedule) as IList;
                if (list == null || list.Count == 0) return;

                var candidate = list[0];
                if (candidate == null) return;

                _perEnemyFallback[__instance.GetInstanceID()] = candidate;
                if (_globalFallback == null) _globalFallback = candidate;
            }
            catch (Exception e)
            {
                Log($"[MultiMax] CaptureFallback error: {e}");
            }
        }

        public static bool TryGetSafeAttack(EnemyDummy ed, out object attack)
        {
            attack = GetPerEnemyFallback(ed);
            return attack != null;
        }
    }

    [PatchType(typeof(EnemyDummy))]
    public static class EnemyScheduleGuardPatch_Strict
    {
        [PatchMethod("GetNextAttackScheduleItem")]
        [PatchPosition(Prefix)]
        public static bool ForceSafeNextAttack(ref object __result, ref EnemyDummy __instance)
        {
            try
            {
                var schedule = __instance.m_AttackSchedule;
                if (schedule != null)
                {
                    var type = schedule.GetType();
                    var fldList = type.GetField("m_AttackTypes", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                    var fldIndex = type.GetField("m_CurrentAttackIndex", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                    var list = fldList?.GetValue(schedule) as IList;
                    if (list != null && list.Count > 0)
                    {
                        int cur = (fldIndex != null) ? (int)fldIndex.GetValue(schedule) : 0;

                        // wrap around if needed
                        if (cur >= list.Count) cur = 0;
                        __result = list[cur];

                        // increment and wrap
                        if (fldIndex != null)
                            fldIndex.SetValue(schedule, (cur + 1) % list.Count);

                        // this keeps combat flow moving normally
                        return false; // skip vanilla
                    }
                }

                // fallback if list invalid/empty
                if (EnemyScheduleFallbackInitPatch.TryGetSafeAttack(__instance, out var atk))
                {
                    __result = atk;
                    return false;
                }

                // last resort synthetic
                var atkType = schedule?.GetType().GetNestedType("AttackType", BindingFlags.Public | BindingFlags.NonPublic);
                if (atkType != null)
                {
                    __result = Activator.CreateInstance(atkType);
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                Log($"[MultiMax] ForceSafeNextAttack error: {e}");
                return true;
            }
        }
    }
}
