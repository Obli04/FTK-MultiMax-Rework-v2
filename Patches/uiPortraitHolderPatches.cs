using System.Collections.Generic;
using System.Reflection;
using FTK_MultiMax_Rework.PatchHelpers;
using static FTK_MultiMax_Rework.PatchHelpers.PatchPositions;
using Object = UnityEngine.Object;

namespace FTK_MultiMax_Rework.Patches
{
    [PatchType(typeof(uiPortraitHolder))]
    public class uiPortraitHolderPatches
    {
        [PatchMethod("UpdateDisplay")]
        [PatchPosition(Prefix)]
        public static bool UpdateDisplayPatch(uiPortraitHolder __instance, ref bool __result) {
            if (__instance.m_PortraitActionPoints != null) {
                FieldInfo followCarrierField = typeof(uiPortraitHolder).GetField("m_FollowCarrier", BindingFlags.NonPublic | BindingFlags.Instance);
                FieldInfo carrierPassengersField = typeof(uiPortraitHolder).GetField("m_CarrierPassengers", BindingFlags.NonPublic | BindingFlags.Instance);

                MiniHexInfo followCarrier = (MiniHexInfo)followCarrierField?.GetValue(__instance);
                List<CharacterOverworld> carrierPassengers = (List<CharacterOverworld>)carrierPassengersField?.GetValue(__instance);
                
                // Seems impossible...
                // followCarrierField is always something...
                if (followCarrierField == null && __instance.m_HexLand.m_PlayersInHex.Count == 0) {
                    __instance.gameObject.SetActive(value: false);
                    Object.Destroy(__instance.gameObject);
                    return false;
                }
                __instance.gameObject.SetActive(true);
                __instance.m_PortraitAndName.Hide();
                __instance.m_PortraitRoot.gameObject.SetActive(true);

                foreach (uiPortraitActionPoint portraitActionPoint in __instance.m_PortraitActionPoints) {
                    portraitActionPoint.ResetShouldShow();
                }

                if (followCarrier != null) {
                    int num = 0;
                    foreach (CharacterOverworld carrierPassenger in carrierPassengers) {
                        if (num < __instance.m_PortraitActionPoints.Count) {
                            __instance.m_PortraitActionPoints[num].CalculateShouldShow(carrierPassenger, _alwaysShowPortrait: true);
                        }
                        num++;
                    }
                } else {
                    int num2 = 0;
                    foreach (CharacterOverworld item in __instance.m_HexLand.m_PlayersInHex) {
                        if (num2 < __instance.m_PortraitActionPoints.Count) {
                            __instance.m_PortraitActionPoints[num2].CalculateShouldShow(item);
                        }
                        num2++;
                    }
                }

                foreach (uiPortraitActionPoint portraitActionPoint2 in __instance.m_PortraitActionPoints) {
                    portraitActionPoint2.UpdateShow();
                }
            }
            __result = true;
            return false;
        }
    }
}