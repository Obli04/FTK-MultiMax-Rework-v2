using BepInEx.Configuration;
using BepInEx;
using System.IO;
using UnityEngine;

namespace FTK_MultiMax_Rework_v2 {
    public static class ConfigHandler {
        public static ConfigEntry<int> MaxPlayersConfig { get; private set; }

        public static void InitializeConfig() {
            string configFilePath = Path.Combine(Paths.ConfigPath, "MultiMaxRework.cfg");
            var configFile = new ConfigFile(configFilePath, true);

            MaxPlayersConfig = configFile.Bind("General",
                                               "MaxPlayers",
                                               5,
                                               "The max number of players");

            if (!File.Exists(configFilePath)) {
                configFile.Save();
            }
        }

        public static void InitializeMaxPlayers() {
            if (MaxPlayersConfig != null) {
                GameFlowMC.gMaxPlayers = MaxPlayersConfig.Value;
                GameFlowMC.gMaxEnemies = GameFlowMC.gMaxPlayers;
                uiQuickPlayerCreate.Default_Classes = new int[GameFlowMC.gMaxPlayers];
            } else {
                Debug.LogError("maxPlayersConfig is not initialized!");
            }
        }
    }
}
