using System;
using System.Collections.Generic;
using FTK_MultiMax_Rework_v2.PatchHelpers;
using static FTK_MultiMax_Rework_v2.PatchHelpers.PatchPositions;
using UnityEngine;

namespace FTK_MultiMax_Rework_v2.Patches
{
    [PatchType(typeof(uiHudScroller))]
    public class uiHudScrollerPatches
    {
        [PatchMethod("Init")]
        [PatchPosition(Prefix)]
        public static bool InitHUD(ref uiHudScroller __instance, uiPlayerMainHud _playerHud, ref int ___m_Index, ref Dictionary<uiPlayerMainHud, int> ___m_TargetIndex, ref List<uiPlayerMainHud> ___m_Huds, ref float ___m_HudWidth, ref float[] ___m_Positions) {
            int num;
            
            CharacterOverworld overworldCharacter = _playerHud.m_Cow;
            
            if (GameLogic.Instance.IsSinglePlayer()) {
                num = overworldCharacter.m_FTKPlayerID.TurnIndex + 1;
                ___m_Index = 0;
            } else {
                num = overworldCharacter.m_FTKPlayerID.TurnIndex + 1;
                ___m_Index = overworldCharacter.m_FTKPlayerID.TurnIndex;
            }
            
            ___m_TargetIndex[_playerHud] = num;
            ___m_Huds.Add(_playerHud);
            
            RectTransform rectTransform = _playerHud.GetComponent<RectTransform>();
            ___m_HudWidth = rectTransform.rect.width;
            
            Vector3 localPosition = rectTransform.localPosition;
            localPosition.y = 0f - rectTransform.anchoredPosition.y;
            
            if (num >= ___m_Positions.Length) {
                float[] array = new float[num + 1];
                Array.Copy(___m_Positions, array, ___m_Positions.Length);
                ___m_Positions = array;
            }
            
            localPosition.x = ___m_Positions[num];
            rectTransform.localPosition = localPosition;
            return false;
        }
    }
}