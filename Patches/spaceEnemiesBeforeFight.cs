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
    [PatchType(typeof(EncounterSession))]
    public static class DioramaIntroSpacingFix
    {
        [PatchMethod("InitEnemyDummiesForCombat")]
        [PatchPosition(Postfix)]
        public static void SpaceEnemiesImmediately(EncounterSession __instance)
        {
            try
            {
                var enc = __instance;
                var diorama = enc?.m_ActiveDiorama;
                if (enc == null || diorama == null) return;
                var enemies = enc.m_EnemyDummies;
                if (enemies == null || enemies.Count == 0) return;

                // Ensure enough targets exist
                while (diorama.m_EnemyTargets.Count < enemies.Count)
                {
                    var prefab = diorama.m_EnemyTargets[0];
                    var clone = UnityEngine.Object.Instantiate(prefab, prefab.parent);
                    clone.name = $"Enemy Target {diorama.m_EnemyTargets.Count}";
                    diorama.m_EnemyTargets.Add(clone);
                }

                // Order enemies by turn index for a stable line
                var enemyList = enemies.Values
                    .Where(e => e != null)
                    .OrderBy(e => e.FID.m_TurnIndex)
                    .ToList();

                // Evenly spread targets horizontally (keep their Y/Z)
                int n = enemyList.Count;
                float width = 6.5f;
                for (int i = 0; i < n; i++)
                {
                    float x = (n == 1) ? 0f : (-width * 0.5f + (width / (n - 1)) * i);
                    var t = diorama.m_EnemyTargets[i];
                    t.localPosition = new Vector3(x, t.localPosition.y, t.localPosition.z);
                }

                // Snap dummies to targets *now* (so intro/scroll shows them spaced)
                for (int i = 0; i < n; i++)
                {
                    var dummy = enemyList[i];
                    var target = diorama.m_EnemyTargets[i];
                    dummy.transform.position = target.position;
                    dummy.transform.rotation = target.rotation;
                    dummy.m_DioramaTargetIndex = i;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] DioramaIntroSpacingFix error: {e}");
            }
        }
    }

    [PatchType(typeof(Diorama))]
    public static class DioramaResetQueueSpacingFix
    {
        [PatchMethod("_resetTargetQueue")]
        [PatchPosition(Postfix)]
        public static void ReapplyEvenSpacing(Diorama __instance)
        {
            try
            {
                var enc = EncounterSession.Instance;
                if (enc == null) return;
                var enemies = enc.m_EnemyDummies?.Values.Where(v => v != null).OrderBy(v => v.FID.m_TurnIndex).ToList();
                if (enemies == null || enemies.Count == 0) return;

                int n = enemies.Count;
                float width = 6.5f;
                for (int i = 0; i < n; i++)
                {
                    float x = (n == 1) ? 0f : (-width * 0.5f + (width / (n - 1)) * i);
                    var t = __instance.m_EnemyTargets[i];
                    t.localPosition = new Vector3(x, t.localPosition.y, t.localPosition.z);
                    enemies[i].m_DioramaTargetIndex = i;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] DioramaResetQueueSpacingFix error: {e}");
            }
        }
    }
}
