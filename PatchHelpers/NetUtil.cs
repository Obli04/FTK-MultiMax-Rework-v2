// File: PatchHelpers/NetUtil.cs
using System.Linq;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using FTK_MultiMax_Rework.PatchHelpers;
using static FTK_MultiMax_Rework.PatchHelpers.PatchPositions;
using static FTK_MultiMax_Rework.Main;

namespace FTK_MultiMax_Rework.PatchHelpers
{
    [PatchType(typeof(EncounterSession))]
    public static class InitializeOwnershipMap
    {
        [PatchMethod("InitPlayerDummiesForCombat")]
        [PatchPosition(Postfix)]
        public static void BuildOwnershipMap()
        {
            OwnershipManager.Initialize();
        }
    }
    
    public static class NetUtil
    {
        public static bool IsMyTurn()
        {
            try
            {
                var mc = EncounterSessionMC.Instance;
                if (mc == null) return false;

                // Ottieni i TurnIndex che controllo
                var myIndices = OwnershipManager.GetMyTurnIndices();
                if (myIndices.Count == 0)
                {
                    Debug.LogWarning("[MultiMax] IsMyTurn: no owned turn indices");
                    return false;
                }

                // ✅ Metodo 1: Usa m_PlayerAttacker
                if (mc.m_PlayerAttacker != null)
                {
                    bool result = myIndices.Contains(mc.m_PlayerAttacker.m_TurnIndex);
                    Debug.Log($"[MultiMax] IsMyTurn via m_PlayerAttacker: {mc.m_PlayerAttacker.m_TurnIndex}, mine={myIndices}, result={result}");
                    return result;
                }

                // ✅ Metodo 2: Usa m_CurrentCombatantIndex
                var indexField = typeof(EncounterSessionMC).GetField("m_CurrentCombatantIndex",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                int currentIndex = (int)(indexField?.GetValue(mc) ?? -1);

                var orderField = typeof(EncounterSessionMC).GetField("m_FightOrder",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                var fightOrder = orderField?.GetValue(mc) as System.Collections.IList;

                if (fightOrder == null || currentIndex < 0 || currentIndex >= fightOrder.Count)
                {
                    Debug.LogWarning($"[MultiMax] IsMyTurn: invalid index {currentIndex}");
                    return false;
                }

                var currentEntry = fightOrder[currentIndex];
                var pidField = currentEntry.GetType().GetField("m_Pid");
                var currentPID = (FTKPlayerID)pidField?.GetValue(currentEntry);

                if (currentPID == null || currentPID.IsEnemy())
                {
                    Debug.Log("[MultiMax] IsMyTurn: current combatant is enemy");
                    return false;
                }

                bool resultFinal = myIndices.Contains(currentPID.m_TurnIndex);
                Debug.Log($"[MultiMax] IsMyTurn via FightOrder: {currentPID.m_TurnIndex}, mine={myIndices}, result={resultFinal}");
                return resultFinal;
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] IsMyTurn error: {e}");
                return false;
            }
        }
}
}
    