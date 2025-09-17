using BoneLib;
using BoneLib.BoneMenu;
using BoneLib.Notifications;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Pool;
using Il2CppSLZ.Marrow.SceneStreaming;
using Il2CppSLZ.Marrow.Warehouse;
using LabFusion.Data;
using LabFusion.Downloading.ModIO;
using LabFusion.Network;
using LabFusion.Player;
using LabFusion.Utilities;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace FsOptimizer
{
    // Config management system
    public static class ConfigManager
    {
        private static string ConfigFilePath => Path.Combine(Paths.ConfigPath, "settings.json");

        // Config data structure
        [Serializable]
        public class FsOptimizerConfig
        {
            public bool AutoCleanEnabled { get; set; } = false;
            public float AutoCleanInterval { get; set; } = 300f;
            public bool AdaptiveAutoCleanEnabled { get; set; } = false;
            public int ObjectThreshold { get; set; } = 100;
            public long MemoryThreshold { get; set; } = 1024;
            public string LastUsedPreset { get; set; } = "Default";
        }

        private static FsOptimizerConfig currentConfig = new FsOptimizerConfig();

        public static FsOptimizerConfig Config => currentConfig;

        public static void SaveConfig()
        {
            try
            {
                // Update config with current preference values
                currentConfig.AutoCleanEnabled = FsOptimizer.autoCleanEnabled?.Value ?? false;
                currentConfig.AutoCleanInterval = FsOptimizer.autoCleanInterval?.Value ?? 300f;
                currentConfig.AdaptiveAutoCleanEnabled = FsOptimizer.adaptiveAutoCleanEnabled?.Value ?? false;

                string jsonString = JsonConvert.SerializeObject(currentConfig, Formatting.Indented);
                File.WriteAllText(ConfigFilePath, jsonString);

                MelonLogger.Msg("FsOptimizer config saved successfully");
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Failed to save config: {e.Message}");
            }
        }

        public static void LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string jsonString = File.ReadAllText(ConfigFilePath);
                    currentConfig = JsonConvert.DeserializeObject<FsOptimizerConfig>(jsonString) ?? new FsOptimizerConfig();
                    MelonLogger.Msg("FsOptimizer config loaded successfully");
                }
                else
                {
                    // Create default config file
                    SaveConfig();
                    MelonLogger.Msg("Created default FsOptimizer config");
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Failed to load config: {e.Message}");
                currentConfig = new FsOptimizerConfig(); // Fallback to defaults
            }
        }

        public static void ApplyConfigToPreferences()
        {
            try
            {
                if (FsOptimizer.autoCleanEnabled != null)
                    FsOptimizer.autoCleanEnabled.Value = currentConfig.AutoCleanEnabled;
                if (FsOptimizer.autoCleanInterval != null)
                    FsOptimizer.autoCleanInterval.Value = currentConfig.AutoCleanInterval;
                if (FsOptimizer.adaptiveAutoCleanEnabled != null)
                    FsOptimizer.adaptiveAutoCleanEnabled.Value = currentConfig.AdaptiveAutoCleanEnabled;
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Failed to apply config: {e.Message}");
            }
        }
    }

    public static class Paths
    {
        private static string ConfigFolder => Path.Combine(MelonEnvironment.UserDataDirectory, "FsOptimizer");
        public static string ConfigPath => Path.Combine(MelonEnvironment.UserDataDirectory, "FsOptimizer/Config");

        public static void InitFolders()
        {
            try
            {
                MelonLogger.Msg($"Creating FsOptimizer folders in: {MelonEnvironment.GameRootDirectory}");

                // Create main FsOptimizer folder first
                if (!Directory.Exists(ConfigFolder))
                {
                    Directory.CreateDirectory(ConfigFolder);
                }

                // Create subfolders
                if (!Directory.Exists(ConfigPath))
                {
                    Directory.CreateDirectory(ConfigPath);
                }

                MelonLogger.Msg("FsOptimizer folders initialized successfully");
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Failed to create FsOptimizer folders: {e.Message}");
            }
        }
    }

    public static class BuildInfo
    {
        public const string Name = "FsOptimizer";
        public const string Description = "A Fusion server cleaner/optimizer";
        public const string Author = "Popper";
        public const string Company = null;
        public const string Version = "1.6.5";
        public const string DownloadLink = "https://github.com/PopperVids/FsOptimizer";
    }

    public class FsOptimizer : MelonMod
    {
        public static Page MainPage;
        public static MelonPreferences_Category Preferences;

        // Auto-Clean System
        internal static MelonPreferences_Entry<bool> autoCleanEnabled;
        internal static MelonPreferences_Entry<bool> adaptiveAutoCleanEnabled;
        internal static MelonPreferences_Entry<float> autoCleanInterval;
        private static float lastCleanTime = 0f;
        private static float lastAdaptiveCheck = 0f;

        // Enum for interval selection
        public enum CleanInterval
        {
            Five_Minutes = 0,
            Ten_Minutes = 1,
            Fifteen_Minutes = 2,
            Twenty_Minutes = 3,
            Twenty_Five_Minutes = 4,
            Thirty_Minutes = 5
        }

        public override void OnInitializeMelon()
        {
            // Initialize config folders
            Paths.InitFolders();

            // Setup Preferences
            Preferences = MelonPreferences.CreateCategory(BuildInfo.Name);
            autoCleanEnabled = Preferences.CreateEntry("AutoClean", false, "Auto Clean Enabled");
            autoCleanInterval = Preferences.CreateEntry("AutoCleanInterval", 300f, "Auto Clean Interval (seconds)");
            adaptiveAutoCleanEnabled = Preferences.CreateEntry("AdaptiveAutoClean", false, "Adaptive Auto Clean Enabled");

            Hooking.OnLevelLoaded += OnLevelLoaded;
            ((MelonBase)this).LoggerInstance.Msg("FsOptimizer Online");
            SetupMenu();
        }

        private void OnLevelLoaded(LevelInfo info)
        {
            var notification = new Notification
            {
                Title = "FsOptimizer | Ready",
                Message = "FsOptimizer has launched successfully!",
                Type = NotificationType.Success,
                PopupLength = 3f,
                ShowTitleOnPopup = true
            };
            Notifier.Send(notification);
            ConfigManager.LoadConfig();
        }

        public override void OnUpdate()
        {
            // Auto-Clean System
            if (autoCleanEnabled.Value && NetworkInfo.HasServer && NetworkInfo.IsHost)
            {
                if (Time.time - lastCleanTime >= autoCleanInterval.Value)
                {
                    PerformAutoClean();
                    lastCleanTime = Time.time;
                }
            }

            // Adaptive check every 30 seconds
            if (adaptiveAutoCleanEnabled.Value && autoCleanEnabled.Value && NetworkInfo.HasServer && NetworkInfo.IsHost)
            {
                if (Time.time - lastAdaptiveCheck >= 30f)
                {
                    CheckAndUpdateAdaptiveInterval();
                    lastAdaptiveCheck = Time.time;
                }
            }
        }

        private void CheckAndUpdateAdaptiveInterval()
        {
            try
            {
                int playerCount = PlayerIDManager.PlayerIDs.Count;
                float newInterval = GetIntervalForPlayers(playerCount);

                // Only update if the interval actually changes significantly
                if (Math.Abs(autoCleanInterval.Value - newInterval) > 0.1f)
                {
                    float oldInterval = autoCleanInterval.Value;
                    autoCleanInterval.Value = newInterval;
                    MelonLogger.Msg($"Adaptive interval adjusted from {oldInterval / 60:F0}min to {newInterval / 60:F0}min for {playerCount} players");
                    ShowNotification($"Auto-clean adapted: {newInterval / 60:F0}min for {playerCount} players", NotificationType.Information);
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Failed to update adaptive interval: {e.Message}");
            }
        }

        private float GetIntervalForPlayers(int playerCount)
        {
            if (playerCount >= 8) return 10f * 60f; // 10 minutes for 8+ players
            if (playerCount == 5) return 15f * 60f; // 15 minutes for exactly 5 players
            if (playerCount >= 3 && playerCount <= 4) return 25f * 60f; // 25 minutes for 3-4 players
            return 30f * 60f; // 30 minutes for 1-2 players (and 6-7 players as fallback)
        }

        private void SetupMenu()
        {
            MainPage = Page.Root.CreatePage("<color=#00CCFF>F</color><color=#00C3FE>s</color><color=#00BBFD>O</color><color=#00B2FD>p</color><color=#00AAFC>t</color><color=#00A1FC>i</color><color=#0099FB>m</color><color=#0090FB>i</color><color=#0088FA>z</color><color=#007FFA>e</color><color=#0077F9>r</color>", Color.white, 0, true);

            // Main cleaning functions
            MainPage.CreateFunction("Clean Server", Color.green, () => CleanServer());
            MainPage.CreateFunction("Reload Level", Color.red, () => ReloadLevel());

            // Auto-clean settings
            MainPage.CreateBool("Auto Clean", Color.cyan, autoCleanEnabled.Value, (value) => {
                autoCleanEnabled.Value = value;
                string status = value ? "enabled" : "disabled";
                ShowNotification($"Auto clean {status}", NotificationType.Information);
                MelonLogger.Msg($"Auto clean {status}");

                // If auto clean is disabled, also disable adaptive
                if (!value && adaptiveAutoCleanEnabled.Value)
                {
                    adaptiveAutoCleanEnabled.Value = false;
                    ShowNotification("Adaptive auto clean disabled (requires Auto Clean)", NotificationType.Information);
                    MelonLogger.Msg("Adaptive auto clean automatically disabled");
                }
            });

            MainPage.CreateBool("Adaptive Auto Clean", Color.magenta, adaptiveAutoCleanEnabled.Value, (value) => {
                adaptiveAutoCleanEnabled.Value = value;
                string status = value ? "enabled" : "disabled";
                ShowNotification($"Adaptive auto clean {status}", NotificationType.Information);
                MelonLogger.Msg($"Adaptive auto clean {status}");

                // Immediately check when enabled
                if (value && NetworkInfo.HasServer)
                {
                    CheckAndUpdateAdaptiveInterval();
                }
            });

            MainPage.CreateEnum("Clean Interval", Color.cyan, GetIntervalEnum(), (intervalEnum) => {
                if (adaptiveAutoCleanEnabled.Value)
                {
                    // When adaptive is enabled, show current adaptive setting instead of changing it
                    ShowNotification($"Adaptive mode active - current interval: {autoCleanInterval.Value / 60:F0} minutes", NotificationType.Information);
                }
                else
                {
                    // Manual interval setting
                    float[] intervals = { 300f, 600f, 900f, 1200f, 1500f, 1800f };
                    int index = Convert.ToInt32(intervalEnum);
                    autoCleanInterval.Value = intervals[index];
                    ShowNotification($"Manual interval set to {intervals[index] / 60} minutes", NotificationType.Information);
                    MelonLogger.Msg($"Manual auto clean interval set to {intervals[index]} seconds");
                }
            });

            // Add a status display function
            MainPage.CreateFunction("Show Current Status", Color.white, () => {
                if (NetworkInfo.HasServer && NetworkInfo.IsHost)
                {
                    string autoStatus = autoCleanEnabled.Value ? "enabled" : "disabled";
                    string adaptiveStatus = adaptiveAutoCleanEnabled.Value ? "enabled" : "disabled";
                    int playerCount = PlayerIDManager.PlayerIDs.Count;

                    ShowNotification($"Auto: {autoStatus} | Adaptive: {adaptiveStatus} | Interval: {autoCleanInterval.Value / 60:F0}min | Players: {playerCount}", NotificationType.Information);
                }
                else
                {
                    ShowNotification("Not connected as server host", NotificationType.Warning);
                }
            });

            // Add save config button
            MainPage.CreateFunction("Save Config", Color.white, () => {
                ConfigManager.SaveConfig();
                ShowNotification("Configuration saved successfully!", NotificationType.Success);
            });
        }

        private void CleanServer()
        {
            // Better Error Handling
            if (!ValidateServerConnection()) return;

            try
            {
                // Clean everything first
                PooleeUtilities.DespawnAll();

                // Clean problematic spawns
                CleanProblematicSpawns();

                MelonLogger.Msg($"Server cleaned!");
                ShowNotification($"Server cleaned", NotificationType.Success);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Clean server failed: {e.Message}");
                ShowNotification("Clean failed! Check console for details", NotificationType.Error);
            }
        }

        private void ReloadLevel()
        {
            // Better Error Handling
            if (!ValidateServerConnection()) return;

            try
            {
                if (SceneStreamer.Session != null && SceneStreamer.Session.Level != null)
                {
                    var currentLevel = SceneStreamer.Session.Level;
                    Barcode levelBarcode = currentLevel.Barcode;

                    MelonLogger.Msg($"Reloading level: {currentLevel.Title} ({levelBarcode.ID})");
                    ShowNotification($"Reloading {currentLevel.Title}...", NotificationType.Information);

                    SceneStreamer.Load(levelBarcode);
                }
                else
                {
                    MelonLogger.Warning("No active level session found!");
                    ShowNotification("No active level found!", NotificationType.Warning);
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Level reload failed: {e.Message}");
                ShowNotification("Level reload failed! Check console for details", NotificationType.Error);
            }
        }

        private void PerformAutoClean()
        {
            try
            {
                MelonLogger.Msg("Performing auto-clean...");
                PooleeUtilities.DespawnAll();
                CleanProblematicSpawns();
                MelonLogger.Msg($"Auto-clean completed");
                ShowNotification($"Auto-cleaned server", NotificationType.Success);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Auto-clean failed: {e.Message}");
                // Don't show error notification for auto-clean to avoid spam
            }
        }

        private void DespawnSpecificObject(string barcode)
        {
            try
            {
                // Look up the spawnable crate using the barcode
                var warehouse = AssetWarehouse.Instance;
                if (warehouse.TryGetCrate(new Barcode(barcode), out var crate))
                {
                    // Get the ushort ID from the crate's Barcode.ShortCode (uint), cast to ushort
                    ushort spawnableId = (ushort)crate.Barcode.ShortCode;

                    // Request despawn using the ushort ID
                    PooleeUtilities.RequestDespawn(spawnableId, true);
                }
                else
                {
                    MelonLogger.Warning($"Could not find spawnable: {barcode}");
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Failed to despawn {barcode}: {e.Message}");
            }
        }

        private void CleanProblematicSpawns()
        {
            try
            {
                // Your existing problematic spawn cleanup with better error handling
                Vector3 mapCenter = new Vector3(0f, 5f, 0f);
                var transform = new SerializedTransform(mapCenter, Quaternion.identity);

                PooleeUtilities.RequestSpawn("Sileqoenn.DeltaruneFountainMaker.Spawnable.SealallDarkFountains", transform, 0, true);
                PooleeUtilities.RequestSpawn("FragileDeviations.PlantLab.Spawnable.SelfDestructionPartTwo", transform, 0, true);
                DespawnSpecificObject("BlueScream.Patriotism.Spawnable.AmericanFlag");
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"Failed to clean problematic spawns: {e.Message}");
                // Don't show user notification for this as it's not critical
            }
        }

        private bool ValidateServerConnection()
        {
            if (!NetworkInfo.HasServer)
            {
                MelonLogger.Warning("Not connected to Fusion server");
                ShowNotification("Not connected to Fusion server!", NotificationType.Error);
                return false;
            }

            if (!NetworkInfo.IsHost)
            {
                MelonLogger.Warning("User is not the server host");
                ShowNotification("You must be the server host!", NotificationType.Error);
                return false;
            }

            return true;
        }

        private CleanInterval GetIntervalEnum()
        {
            float[] intervals = { 300f, 600f, 900f, 1200f, 1500f, 1800f };
            for (int i = 0; i < intervals.Length; i++)
            {
                if (Math.Abs(intervals[i] - autoCleanInterval.Value) < 0.1f)
                    return (CleanInterval)i;
            }
            return CleanInterval.Five_Minutes; // Default to 5 minutes
        }

        private string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024} KB";
            return $"{bytes / (1024 * 1024)} MB";
        }

        private void ShowNotification(string message, NotificationType type)
        {
            try
            {
                var notification = new Notification
                {
                    Title = "FsOptimizer",
                    Message = message,
                    Type = type,
                    PopupLength = 3f,
                    ShowTitleOnPopup = true
                };
                Notifier.Send(notification);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Failed to show notification: {e.Message}");
            }
        }
    }
}