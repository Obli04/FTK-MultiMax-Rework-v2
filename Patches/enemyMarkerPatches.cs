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

public static class EnemyMarkerUtil
{
    [PatchType(typeof(EncounterSession))]
    public static class EnsureSingleMarker_OnSetTargetFinal
    {
        [PatchMethod("SetTargetEnemy")] // exists in vanilla FTK
        [PatchPosition(Postfix)]
        public static void Post(EncounterSession __instance, EnemyDummy _ed)
        {
            EnemyMarkerUtil.SetSingleMarker(_ed);
        }
    }

    [PatchType(typeof(CameraCutManager))]
    public static class SyncMarkerToCameraTarget
    {
        [PatchMethod("SetCameraTarget")] // called whenever the cam focuses an enemy
        [PatchPosition(Postfix)]
        public static void Post(CameraCutManager __instance, CharacterDummy _cd)
        {
            if (_cd is EnemyDummy enemy)
                EnemyMarkerUtil.SetSingleMarker(enemy);
        }
    }

    static readonly BindingFlags BF = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static void SetSingleMarker(EnemyDummy selected)
    {
        var enc = EncounterSession.Instance;
        if (enc?.m_EnemyDummies == null) return;

        foreach (var kv in enc.m_EnemyDummies)
        {
            var dummy = kv.Value;
            if (dummy == null) continue;
            var marker = typeof(EnemyDummy).GetField("m_EnemyMarker", BF)?.GetValue(dummy);
            if (marker == null) continue;

            // TurnOff()
            marker.GetType().GetMethod("TurnOff", BF)?.Invoke(marker, null);
        }

        if (selected == null) return;

        // Turn the selected one back on (try both signatures)
        var selMarker = typeof(EnemyDummy).GetField("m_EnemyMarker", BF)?.GetValue(selected);
        if (selMarker == null) return;

        var m0 = selMarker.GetType().GetMethod("TurnOn", BF, null, Type.EmptyTypes, null);
        if (m0 != null) { m0.Invoke(selMarker, null); return; }

        var m1 = selMarker.GetType().GetMethod("TurnOn", BF, null, new[] { typeof(CharacterDummy.DefendMarkerType) }, null);
        if (m1 != null) m1.Invoke(selMarker, new object[] { CharacterDummy.DefendMarkerType.Normal });
    }
    [PatchType(typeof(EncounterSession))]
    public static class EnsureSingleMarker_OnSetCurrentEnemy
    {
        [PatchMethod("SetCurrentEnemy")]            // if this exists
        [PatchPosition(Postfix)]
        public static void Post(EncounterSession __instance)
        {
            EnemyMarkerUtil.SetSingleMarker(__instance.GetCurrentEnemy());
        }
    }

    [PatchType(typeof(EncounterSessionMC))]
    public static class ClearMarkersOnRoundAdvance
    {
        [PatchMethod("StartNextCombatRound2")]      // when turns advance, clear old selection
        [PatchPosition(Postfix)]
        public static void Post()
        {
            EnemyMarkerUtil.SetSingleMarker(null);
        }
    }

}
