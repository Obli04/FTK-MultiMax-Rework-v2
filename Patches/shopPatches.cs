using GridEditor;
using HarmonyLib;
using System;
using System.Collections.Generic;
using FTK_MultiMax_Rework_v2.PatchHelpers;
using static FTK_MultiMax_Rework_v2.PatchHelpers.PatchPositions;
using static FTK_MultiMax_Rework_v2.Main;
using UnityEngine;

namespace FTK_MultiMax_Rework_v2.Patches
{
    [PatchType(typeof(MiniHexInfo))]
    public class ShopStockRefreshPatch
    {
        [PatchMethod("_onShopRefresh")]
        [PatchPosition(Postfix)]
        public static void AfterShopRefresh(MiniHexInfo __instance)
        {
            try
            {
                int playerCount = GameFlowMC.gMaxPlayers;
                if (playerCount <= 3) return;
                if (__instance.m_ShopItemStock == null || __instance.m_ShopItemStockCurrent == null) return;

                var itemKeys = new List<FTK_itembase.ID>(__instance.m_ShopItemStock.Keys);

                int delta = playerCount - 3;

                foreach (var id in itemKeys)
                {
                    int baseValue = __instance.m_ShopItemStock[id];
                    int newStock = baseValue + delta;
                    if (newStock < 0) newStock = 0;       

                    __instance.m_ShopItemStock[id] = newStock;

                    int currentValue = __instance.m_ShopItemStockCurrent.GetItemCount(id);
                    if (currentValue == 1) continue;
                    int newCurrent = currentValue + delta;
                    if (newCurrent < 0) newCurrent = 0;
                    if (newCurrent > newStock) newCurrent = newStock;
                    int toAdd = newCurrent - currentValue;
                    if (toAdd != 0) __instance.m_ShopItemStockCurrent.Add(id, toAdd);
                }
                
                Log($"[ShopScaling] Applied scaling post-refresh for {playerCount} players.");
            }

            catch (Exception ex)
            {
                Debug.LogError("[MultiMaxReworkV2]: Error post-refresh scaling: " + ex);
            }
        }
    }
    [PatchType(typeof(EncounterSession))]
    [HarmonyPatch(typeof(EncounterSession), "GiveOutLootXPGold")]
    public static class EncounterSessionPatches
    {
        [HarmonyPostfix]
        public static void XPModifierPatch(ref FTKPlayerID _recvPlayer, ref int _xp, ref int _gold)
        {
            CharacterOverworld cow = FTKHub.Instance.GetCharacterOverworldByFID(_recvPlayer);
            float xpMod = cow.m_CharacterStats.XpModifier;
            float goldMod = cow.m_CharacterStats.GoldModifier;

            int playerCount = GameFlowMC.gMaxPlayers;
            if (playerCount > 3)
            {
                _xp = Mathf.RoundToInt((_xp * xpMod) * 1.5f);
                _gold = Mathf.RoundToInt((_gold * goldMod) * 1.5f);
            }
        }
    }
}