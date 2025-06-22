using FTK_MultiMax_Rework_v2.PatchHelpers;
using static FTK_MultiMax_Rework_v2.PatchHelpers.PatchPositions;
using UnityEngine;

namespace FTK_MultiMax_Rework_v2.Patches {
    [PatchType(typeof(EncounterSession))]
    public class EncounterSessionPatches {
        [PatchMethod("GiveOutLootXPGold")]
        [PatchPosition(Prefix)]
        public static void XPModifierPatch(ref FTKPlayerID _recvPlayer, ref int _xp, ref int _gold) {

            CharacterOverworld characterOverworldByFid = FTKHub.Instance.GetCharacterOverworldByFID(_recvPlayer);

            float xpMod = characterOverworldByFid.m_CharacterStats.XpModifier;
            float goldMod = characterOverworldByFid.m_CharacterStats.GoldModifier;

            if (GameFlowMC.gMaxPlayers > 3) {
                _xp = Mathf.RoundToInt((float)((_xp * xpMod) * 1.5));
                _gold = Mathf.RoundToInt((float)((_gold * goldMod) * 1.5));
            }
        }
    }
}