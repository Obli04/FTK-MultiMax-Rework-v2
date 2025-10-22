using GridEditor;
using HarmonyLib;
using System;
using System.Collections.Generic;
using FTK_MultiMax_Rework_v2.PatchHelpers;
using static FTK_MultiMax_Rework_v2.Main;
using static FTK_MultiMax_Rework_v2.PatchHelpers.PatchPositions;
using UnityEngine;
namespace FTK_MultiMax_Rework_v2.Patches
{
    [PatchType(typeof(MiniHexInfo))]
    public class ShopStockScalingPatch
    {
        [PatchMethod("SyncShopStock")]
        [PatchPosition(Postfix)]
        public static void ScaleShopStock(MiniHexInfo __instance)
        {
            try
            {
                int playerCount = GameFlowMC.gMaxPlayers;

                Log($"SyncShopStock called, playerCount: {playerCount}");

                if (playerCount <= 3)
                {
                    Log("playerCount <= 3, skipping");
                    return;
                }

                if (__instance.m_ShopItemStock == null || __instance.m_ShopItemStockCurrent == null)
                {
                    Log("Shop not initialized");
                    return;
                }

                List<FTK_itembase.ID> itemsToScale = new List<FTK_itembase.ID>();

                foreach (var kvp in __instance.m_ShopItemStock)
                {
                    if (kvp.Value == 3)
                    {
                        itemsToScale.Add(kvp.Key);
                    }
                }

                Log($"Found {itemsToScale.Count} items with stock 3");

                foreach (var itemId in itemsToScale)
                {
                    __instance.m_ShopItemStock[itemId] = playerCount;
                    __instance.m_ShopItemStockCurrent.Remove(itemId);
                    __instance.m_ShopItemStockCurrent.Add(itemId, playerCount);
                    Log($"Scaled {itemId} from 3 to {playerCount}");
                }

                Log("Finished scaling");
            }
            catch (Exception ex)
            {
                Debug.LogError("[MultiMaxReworkV2]: Error scaling shop: " + ex);
            }
        }
    }
}