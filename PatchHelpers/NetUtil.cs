using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using FTK_MultiMax_Rework.PatchHelpers;
using static FTK_MultiMax_Rework.PatchHelpers.PatchPositions;

namespace FTK_MultiMax_Rework.PatchHelpers
{
    // ✅ Inizializza ownership DOPO CommenceBattleRPC
    [PatchType(typeof(EncounterSessionMC))]
    public static class InitializeOwnershipOnCombatStart
    {
        [PatchMethod("CommenceBattleRPC")]
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

                var myIndices = OwnershipManager.GetMyTurnIndices();
                if (myIndices.Count == 0)
                {
                    Debug.LogWarning("[MultiMax] IsMyTurn: no owned turn indices");
                    return false;
                }

                // ✅ Usa m_PlayerAttacker
                if (mc.m_PlayerAttacker != null)
                {
                    bool result = myIndices.Contains(mc.m_PlayerAttacker.m_TurnIndex);
                    Debug.Log($"[MultiMax] IsMyTurn: attacker={mc.m_PlayerAttacker.m_TurnIndex}, mine={myIndices}, result={result}");
                    return result;
                }

                Debug.LogWarning("[MultiMax] IsMyTurn: m_PlayerAttacker is null");
                return false;
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiMax] IsMyTurn error: {e}");
                return false;
            }
        }
    }
}