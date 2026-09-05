using System.IO;
using UnityEngine;
using Newtonsoft.Json;

namespace SkaterMod
{
    internal static class ModConfigManager
    {
        public static readonly string ModBaseDirectory = Path.Combine(
            Path.GetFullPath(Path.Combine(Application.dataPath, "..")), 
            "config",
            "SkaterMod"
        );

        public static ModConfig Config { get; private set; }
        private static readonly string configPath = Path.Combine(ModBaseDirectory, "SkaterMod.json");

        public static void LoadConfig()
        {
            try
            {
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    Config = JsonConvert.DeserializeObject<ModConfig>(json);
                    ModLogger.Log($"Configuration loaded successfully from: {configPath}");
                }
                else
                {
                    Config = new ModConfig();
                    ModLogger.Log("No config file found. Creating a default one.");
                    SaveConfig();
                }
            }
            catch (System.Exception e)
            {
                ModLogger.Log($"Error loading config, creating a default. Error: {e.Message}");
                Config = new ModConfig();
                SaveConfig();
            }
        }

        public static void SaveConfig()
        {
            try
            {
                Directory.CreateDirectory(ModBaseDirectory);
                string json = JsonConvert.SerializeObject(Config, Formatting.Indented);
                File.WriteAllText(configPath, json);
            }
            catch (System.Exception e)
            {
                ModLogger.Log($"Error saving config: {e.Message}");
            }
        }
    }
}