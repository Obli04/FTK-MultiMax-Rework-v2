#nullable enable
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using FTK_MultiMax_Rework.PatchHelpers;
using static FTK_MultiMax_Rework.Main;
using static FTK_MultiMax_Rework.PatchHelpers.PatchPositions;
using GridEditor;
using StartGameFE;

namespace FTK_MultiMax_Rework.Patches
{
    [PatchType(typeof(GameDefinition))]
    public static class HildebrantCustomDifficulties
    {
        public enum HildebrantDifficulty
        {
            Apprentice = 100,
            Novice = 101,
            Master = 102,
            Grandmaster = 103,
            Godlike = 104
        }

        private static readonly HashSet<string> initialized = new();

        [PatchMethod("Initialize")]
        [PatchPosition(Postfix)]
        public static void AddDifficultiesAfterInit(GameDefinition __instance)
        {
            Log("[MMR3] GameDefinition.Initialize() called for: " + (__instance?.GetDisplayName() ?? "unnamed"));

            try
            {
                if (__instance == null) return;

                var field = typeof(GameDefinition).GetField("m_GameDifficulties",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

                if (field == null)
                {
                    Log("[MMR3] Could not find m_GameDifficulties field.");
                    return;
                }

                var dict = field.GetValue(__instance) as IDictionary;
                if (dict == null)
                {
                    Log("[MMR3] m_GameDifficulties is null!");
                    return;
                }

                string key = __instance.GetHashCode().ToString();
                if (initialized.Contains(key))
                    return;

                initialized.Add(key);

                AddAllDifficulties(dict);
                Log($"[MMR3] Injected difficulties (runtime): total {dict.Count}");
            }
            catch (Exception e)
            {
                Log("[MMR3] Failed to inject difficulties: " + e);
            }
        }
        internal static void AddAllDifficulties(IDictionary dict)
        {
            AddDifficulty(dict, HildebrantDifficulty.Apprentice, "Apprentice",
                0.5f, 100, 0.8f, true, 1f, 8, 0.8f,
                "A relaxed experience for newcomers. Enemies are weaker and you recover faster.");

            AddDifficulty(dict, HildebrantDifficulty.Novice, "Novice",
                0.8f, 80, 0.9f, true, 0.5f, 7, 0.9f,
                "Slightly harder but still forgiving. Good for casual runs.");

            AddDifficulty(dict, HildebrantDifficulty.Master, "Master",
                1.1f, 40, 1.1f, false, 0f, 5, 1.1f,
                "Tough enemies and limited healing. Only the prepared survive.");

            AddDifficulty(dict, HildebrantDifficulty.Grandmaster, "Grandmaster",
                1.2f, 20, 1.25f, false, 0f, 4, 1.25f,
                "Punishing fights and scarce rewards. Expect no mercy.");

            AddDifficulty(dict, HildebrantDifficulty.Godlike, "Godlike",
                2f, 0, 1.5f, false, 0f, 3, 1.5f,
                "A brutal test of endurance. One mistake ends it all.");
        }

        private static void AddDifficulty(IDictionary dict, HildebrantDifficulty id,
            string name, float loreMult, int gold, float hp, bool regen,
            float bonus, int life, float inflation, string description)
        {
            var key = (GameDifficulty.DifficultyType)((int)id);
            if (dict.Contains(key))
                return;

            var diff = new GameDifficulty
            {
                m_DisplayName = name,
                m_LoreMultiplier = loreMult,
                m_ExtraGold = gold,
                m_EnemyHealthMultiplier = hp,
                m_EndTurnHealthGain = regen,
                m_HealthGainBonus = bonus,
                m_InfoText = description
            };

            var p = diff.m_CustomizableRules.GetParams();
            p[FTK_gameParams.ID.lifepool] = life;
            p[FTK_gameParams.ID.inflation] = inflation;

            dict[key] = diff;
            Log($"[MMR3] Added difficulty: {name} ({(int)id})");
        }
    }
    [PatchType(typeof(StartGameFE.GameConfig))]
    public static class HildebrantDifficulty_UIInject
    {
        [PatchMethod("set__selectedGameDefPreview")]
        [PatchPosition(Postfix)]
        public static void AfterPreviewSelected(StartGameFE.GameConfig __instance)
        {
            try
            {
                if (__instance == null) return;

                // It is a PROPERTY, not a field
                var prop = typeof(StartGameFE.GameConfig)
                    .GetProperty("_selectedGameDefPreview", BindingFlags.Instance | BindingFlags.NonPublic);
                var preview = prop?.GetValue(__instance, null) as GameDefinitionPreview;
                if (preview == null) return;

                // Only for Hildebrant modes
                var saveNameField = typeof(GameDefinitionBase)
                    .GetField("m_SaveFileName", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var saveName = (saveNameField?.GetValue(preview) as string) ?? "";

                if (saveName.IndexOf("hildebrant", StringComparison.OrdinalIgnoreCase) < 0) return;

                var dictField = typeof(GameDefinitionBase)
                    .GetField("m_GameDifficulties", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var dict = dictField?.GetValue(preview) as IDictionary;
                if (dict == null) return;

                HildebrantCustomDifficulties.AddAllDifficulties(dict);

                Log($"[MMR3] UI inject added custom difficulties to {saveName} (now {dict.Count}).");
            }
            catch (Exception e)
            {
                Log("[MMR3] UI difficulty inject failed: " + e);
            }
        }
    }
    [PatchType(typeof(GameDefinitionPreview))]
    public static class HildebrantDifficulty_RuntimeInject
    {
        [PatchMethod("GetNewGameDefInstance")]
        [PatchPosition(Postfix)]
        public static void AfterNewGameDef(GameDefinitionPreview __instance, ref GameDefinition __result)
        {
            try
            {
                if (__instance == null || __result == null) return;

                // check save name on the PREVIEW
                var saveNameField = typeof(GameDefinitionBase)
                    .GetField("m_SaveFileName", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var saveName = (saveNameField?.GetValue(__instance) as string) ?? "";
                if (saveName.IndexOf("hildebrant", StringComparison.OrdinalIgnoreCase) < 0) return;

                var dictField = typeof(GameDefinitionBase)
                    .GetField("m_GameDifficulties", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                var dict = dictField?.GetValue(__result) as IDictionary;
                if (dict == null) return;

                HildebrantCustomDifficulties.AddAllDifficulties(dict);
                Log($"[MMR3] Runtime inject: {saveName} now has {dict.Count} difficulties.");
            }
            catch (Exception e)
            {
                Log("[MMR3] Runtime difficulty inject failed: " + e);
            }
        }
    }

    [PatchType(typeof(GameConfig))]
    public static class HildebrantDifficulty_UIRefresh
    {
        [PatchMethod("set__selectedGameDefPreview")]
        [PatchPosition(Postfix)]
        public static void AfterPreviewSelected(GameConfig __instance)
        {
            try
            {
                var previewField = typeof(GameConfig)
                    .GetField("_selectedGameDefPreview", BindingFlags.Instance | BindingFlags.NonPublic);
                if (previewField == null)
                {
                    Main.Log("[MMR3] UIRefresh: No _selectedGameDefPreview field found.");
                    return;
                }

                GameDefinitionPreview? preview = previewField.GetValue(__instance) as GameDefinitionPreview;
                if (preview == null)
                {
                    Main.Log("[MMR3] UIRefresh: preview is null.");
                    return;
                }

                GameDefinition def = preview.GetNewGameDefInstance();
                if (def == null)
                {
                    Main.Log("[MMR3] UIRefresh: GameDefinition is null.");
                    return;
                }

                var dictField = typeof(GameDefinition)
                    .GetField("m_GameDifficulties", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                IDictionary dict = dictField != null ? dictField.GetValue(def) as IDictionary : null;
                if (dict == null)
                {
                    Main.Log("[MMR3] UIRefresh: m_GameDifficulties not found.");
                    return;
                }

                // Inject difficulties
                var addMethod = typeof(HildebrantCustomDifficulties)
                    .GetMethod("AddAllDifficulties", BindingFlags.Static | BindingFlags.NonPublic);
                if (addMethod != null)
                    addMethod.Invoke(null, new object[] { dict });

                // Log all difficulty names
                List<string> names = new List<string>();
                foreach (DictionaryEntry kvp in dict)
                {
                    GameDifficulty diff = kvp.Value as GameDifficulty;
                    if (diff != null)
                        names.Add(diff.m_DisplayName);
                }

                string allNames = "";
                for (int i = 0; i < names.Count; i++)
                {
                    allNames += names[i];
                    if (i < names.Count - 1)
                        allNames += ", ";
                }

                Main.Log("[MMR3] UIRefresh AfterPreviewSelected: " + allNames);
            }
            catch (Exception e)
            {
                Main.Log("[MMR3] UIRefresh AfterPreviewSelected failed: " + e);
            }
        }
    }
}
