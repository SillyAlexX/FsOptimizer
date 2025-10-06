// ===============================
// FsOptimizer - Fusion Server Cleaner/Optimizer
// ===============================

#region Usings

// External Libs
using BoneLib;
using BoneLib.BoneMenu;
using BoneLib.Notifications;
using HarmonyLib;
// SLZ & Marrow
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Pool;
using Il2CppSLZ.Marrow.SceneStreaming;
using Il2CppSLZ.Marrow.Warehouse;
// LabFusion
using LabFusion.Data;
using LabFusion.Downloading.ModIO;
using LabFusion.Entities;
using LabFusion.Network;
using LabFusion.Player;
using LabFusion.Representation;
using LabFusion.Senders;
using LabFusion.Utilities;
using Steamworks;
// System
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json;
// Unity
using UnityEngine;
using static FsOptimizer.ConfigManager.FsOptimizer;

#endregion

namespace FsOptimizer
{
    #region Config Management

    public static class ConfigManager
    {
        private static string ConfigFilePath => Path.Combine(Paths.ConfigPath, "Config.json");

        [Serializable]
        public class FsOptimizerConfig
        {
            public bool AutoCleanEnabled { get; set; } = false;
            public float AutoCleanInterval { get; set; } = 300f;
            public bool AdaptiveAutoCleanEnabled { get; set; } = false;
            public bool AntiGriefEnabled { get; set; } = false;
            public int ObjectThreshold { get; set; } = 100;
            public long MemoryThreshold { get; set; } = 1024;
            public string LastUsedPreset { get; set; } = "Default";

            // === AntiGrief Settings ===
            public int MaxActionsPerSecond { get; set; } = 6;      // Rate limiter max actions
            public float RateWindowSeconds { get; set; } = 1.0f;   // Time window in seconds
            public bool EnableFileLogging { get; set; } = true;    // Whether to log to text file
            public bool EnableSpawnBlocking { get; set; } = true;  // Whether to block spawn spam
        }

        private static FsOptimizerConfig currentConfig = new FsOptimizerConfig();
        public static FsOptimizerConfig Config => currentConfig;

        public static void SaveConfig()
        {
            try
            {
                currentConfig.AutoCleanEnabled = FsOptimizer.autoCleanEnabled?.Value ?? false;
                currentConfig.AutoCleanInterval = FsOptimizer.autoCleanInterval?.Value ?? 300f;
                currentConfig.AdaptiveAutoCleanEnabled = FsOptimizer.adaptiveAutoCleanEnabled?.Value ?? false;
                currentConfig.AntiGriefEnabled = FsOptimizer.AntiGriefEnabled?.Value ?? false;

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
                    SaveConfig();
                    MelonLogger.Msg("Created default FsOptimizer config");
                }
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Failed to load config: {e.Message}");
                currentConfig = new FsOptimizerConfig();
            }
        }

        public static void ApplyConfigToPreferences()
        {
            try
            {
                // === Apply AutoClean ===
                if (FsOptimizer.autoCleanEnabled != null)
                    FsOptimizer.autoCleanEnabled.Value = currentConfig.AutoCleanEnabled;

                if (FsOptimizer.autoCleanInterval != null)
                    FsOptimizer.autoCleanInterval.Value = currentConfig.AutoCleanInterval;

                if (FsOptimizer.adaptiveAutoCleanEnabled != null)
                    FsOptimizer.adaptiveAutoCleanEnabled.Value = currentConfig.AdaptiveAutoCleanEnabled;

                // === Apply AntiGrief ===
                if (FsOptimizer.AntiGriefEnabled != null)
                    FsOptimizer.AntiGriefEnabled.Value = currentConfig.AntiGriefEnabled;

                // Configure rate limiter from config
                RateLimiter.SetLimits(
                    currentConfig.MaxActionsPerSecond,
                    currentConfig.RateWindowSeconds
                );

                MelonLogger.Msg($"[AntiGrief] Config applied: " +
                    $"{currentConfig.MaxActionsPerSecond}/{currentConfig.RateWindowSeconds}s, " +
                    $"Logging={(currentConfig.EnableFileLogging ? "On" : "Off")}, " +
                    $"SpawnBlocking={(currentConfig.EnableSpawnBlocking ? "On" : "Off")}");
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Failed to apply config: {e.Message}");
            }
        }

        #endregion

        #region Paths & Build Info

        public static class Paths
        {
            private static string ConfigFolder => Path.Combine(MelonEnvironment.UserDataDirectory, "FsOptimizer");
            public static string ConfigPath => Path.Combine(MelonEnvironment.UserDataDirectory, "FsOptimizer/Config");

            public static void InitFolders()
            {
                try
                {
                    MelonLogger.Msg($"Creating FsOptimizer folders in: {MelonEnvironment.UserDataDirectory}");

                    if (!Directory.Exists(ConfigFolder))
                        Directory.CreateDirectory(ConfigFolder);

                    if (!Directory.Exists(ConfigPath))
                        Directory.CreateDirectory(ConfigPath);

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
            public const string Author = "SillyAlex";
            public const string Company = null;
            public const string Version = "1.6.7";
            public const string DownloadLink = "https://github.com/SillyAlexX/FsOptimizer";
        }

        #endregion

        #region Main Mod Class

        public class FsOptimizer : MelonMod
        {
            // -----------------------------
            // Fields
            // -----------------------------
            public static Page MainPage;
            public static MelonPreferences_Category Preferences;
            public static HarmonyLib.Harmony HarmonyInstance;

            internal static MelonPreferences_Entry<bool> autoCleanEnabled;
            internal static MelonPreferences_Entry<bool> adaptiveAutoCleanEnabled;
            internal static MelonPreferences_Entry<float> autoCleanInterval;
            internal static MelonPreferences_Entry<bool> AntiGriefEnabled;

            private static float lastCleanTime = 0f;
            private static float lastAdaptiveCheck = 0f;

            // -----------------------------
            // Enums
            // -----------------------------
            public enum CleanInterval
            {
                Five_Minutes,
                Ten_Minutes,
                Fifteen_Minutes,
                Twenty_Minutes,
                Twenty_Five_Minutes,
                Thirty_Minutes
            }

            // -----------------------------
            // Lifecycle
            // -----------------------------
            public override void OnInitializeMelon()
            {
                Paths.InitFolders();

                Preferences = MelonPreferences.CreateCategory(BuildInfo.Name);
                autoCleanEnabled = Preferences.CreateEntry("AutoClean", false, "Auto Clean Enabled");
                autoCleanInterval = Preferences.CreateEntry("AutoCleanInterval", 300f, "Auto Clean Interval (seconds)");
                adaptiveAutoCleanEnabled = Preferences.CreateEntry("AdaptiveAutoClean", false, "Adaptive Auto Clean Enabled");
                AntiGriefEnabled = Preferences.CreateEntry("AntiGrief", false, "Anti-Grief Protection Enabled");

                Hooking.OnLevelLoaded += OnLevelLoaded;

                ConfigManager.LoadConfig();
                ConfigManager.ApplyConfigToPreferences();

                LoggerInstance.Msg("FsOptimizer Online");

                SetupMenu();
            }

            public override void OnUpdate()
            {
                if (autoCleanEnabled.Value && NetworkInfo.HasServer && NetworkInfo.IsHost)
                {
                    if (Time.time - lastCleanTime >= autoCleanInterval.Value)
                    {
                        PerformAutoClean();
                        lastCleanTime = Time.time;
                    }
                }

                if (adaptiveAutoCleanEnabled.Value && autoCleanEnabled.Value && NetworkInfo.HasServer && NetworkInfo.IsHost)
                {
                    if (Time.time - lastAdaptiveCheck >= 30f)
                    {
                        CheckAndUpdateAdaptiveInterval();
                        lastAdaptiveCheck = Time.time;
                    }
                }
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
                ConfigManager.ApplyConfigToPreferences();
                RateLimiter.Reset();
            }

            // -----------------------------
            // Permissions
            // -----------------------------
            public static bool HasFusionPermission(NetworkPlayer player)
            {
                if (player?.PlayerID == null) return false;

                try
                {
                    PermissionLevel playerLevel;
                    if (MetadataHelper.TryGetPermissionLevel(player.PlayerID, out playerLevel))
                    {
                        return playerLevel >= PermissionLevel.OPERATOR;
                    }
                }
                catch (Exception e)
                {
                    MelonLogger.Warning($"Failed to check Fusion permissions: {e.Message}");
                    ShowNotification($"Failed to check Fusion permissions: {e.Message}", NotificationType.Error);
                }

                return false;
            }

            // -----------------------------
            // Cleaning Systems
            // -----------------------------
            public static void CleanServerStyle()
            {
                try
                {
                    var requester = LocalPlayer.GetNetworkPlayer();

                    if (!HasFusionPermission(requester))
                    {
                        MelonLogger.Warning($"{requester?.Username} tried to clean without permission!");
                        ShowNotification("Nuh Uh", NotificationType.Error);
                        return;
                    }

                    var entities = GetNetworkEntities();
                    foreach (var entity in entities)
                        DespawnActual(entity, true);

                    MelonLogger.Msg("Server clean completed!");
                    ShowNotification("Server cleaned", NotificationType.Success);
                }
                catch (Exception e)
                {
                    MelonLogger.Error($"Server clean failed: {e.Message}");
                    ShowNotification("Clean failed! Check console", NotificationType.Error);
                }
            }

            public static void DespawnActual(NetworkEntity entity, bool despawnEffect)
            {
                try
                {
                    NetworkPlayer hostPlayer = NetworkPlayer.Players.FirstOrDefault(p => p != null && p.PlayerID.IsHost);

                    if (hostPlayer == null)
                    {
                        MelonLogger.Warning("No host player found for despawn.");
                        return;
                    }

                    var despawnData = new DespawnResponseData()
                    {
                        Despawner = new PlayerReference(hostPlayer.PlayerID),
                        Entity = new NetworkEntityReference(entity.ID),
                        DespawnEffect = despawnEffect
                    };

                    MessageRelay.RelayNative(despawnData, NativeMessageTag.DespawnResponse, CommonMessageRoutes.ReliableToClients);
                    MessageRelay.RelayNative(despawnData, NativeMessageTag.DespawnResponse, CommonMessageRoutes.ReliableToOtherClients);
                    MessageRelay.RelayNative(despawnData, NativeMessageTag.DespawnResponse, CommonMessageRoutes.ReliableToServer);
                }
                catch (Exception e)
                {
                    MelonLogger.Error($"Despawn failed: {e.Message}");
                }
            }

            public static HashSet<NetworkEntity> GetNetworkEntities()
            {
                try
                {
                    var idManager = NetworkEntityManager.IDManager;
                    if (idManager?.RegisteredEntities?.EntityIDLookup == null)
                        return new HashSet<NetworkEntity>();

                    var playerEntities = NetworkPlayer.Players
                        .Where(p => p?.NetworkEntity != null)
                        .Select(p => p.NetworkEntity)
                        .ToHashSet();

                    return idManager.RegisteredEntities.EntityIDLookup.Keys
                        .Where(entity => !playerEntities.Contains(entity))
                        .ToHashSet();
                }
                catch (Exception e)
                {
                    MelonLogger.Error($"Failed to get network entities: {e.Message}");
                    return new HashSet<NetworkEntity>();
                }
            }

            private void PerformAutoClean()
            {
                try
                {
                    MelonLogger.Msg("Performing auto-clean...");
                    PooleeUtilities.DespawnAll();
                    CleanProblematicSpawns();
                    MelonLogger.Msg("Auto-clean completed");
                    ShowNotification("Auto-cleaned server", NotificationType.Success);
                }
                catch (Exception e)
                {
                    MelonLogger.Error($"Auto-clean failed: {e.Message}");
                }
            }

            private void CleanProblematicSpawns()
            {
                try
                {
                    Vector3 mapCenter = new Vector3(0f, 5f, 0f);
                    var transform = new SerializedTransform(mapCenter, Quaternion.identity);

                    PooleeUtilities.RequestSpawn("Sileqoenn.DeltaruneFountainMaker.Spawnable.SealallDarkFountains", transform, 0, true);
                    PooleeUtilities.RequestSpawn("FragileDeviations.PlantLab.Spawnable.SelfDestructionPartTwo", transform, 0, true);
                }
                catch (Exception e)
                {
                    MelonLogger.Warning($"Failed to clean problematic spawns: {e.Message}");
                }
            }

            // -----------------------------
            // Interval Logic
            // -----------------------------
            private void CheckAndUpdateAdaptiveInterval()
            {
                try
                {
                    int playerCount = PlayerIDManager.PlayerIDs.Count;
                    float newInterval = GetIntervalForPlayers(playerCount);

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
                if (playerCount >= 8) return 10f * 60f;
                if (playerCount == 5) return 15f * 60f;
                if (playerCount >= 3 && playerCount <= 4) return 25f * 60f;
                return 30f * 60f;
            }

            private CleanInterval GetIntervalEnum()
            {
                float[] intervals = { 300f, 600f, 900f, 1200f, 1500f, 1800f };
                for (int i = 0; i < intervals.Length; i++)
                {
                    if (Math.Abs(intervals[i] - autoCleanInterval.Value) < 0.1f)
                        return (CleanInterval)i;
                }
                return CleanInterval.Five_Minutes;
            }

            // -----------------------------
            // Level Management
            // -----------------------------
            private void ReloadLevel()
            {
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
                    ShowNotification("Level reload failed! Check console", NotificationType.Error);
                }
            }

            // -----------------------------
            // UI / Menu
            // -----------------------------
            private void SetupMenu()
            {
                MainPage = Page.Root.CreatePage("<color=#00CCFF>F</color><color=#00C3FE>s</color><color=#00BBFD>O</color><color=#00B2FD>p</color><color=#00AAFC>t</color><color=#00A1FC>i</color><color=#0099FB>m</color><color=#0090FB>i</color><color=#0088FA>z</color><color=#007FFA>e</color><color=#0077F9>r</color>", Color.white, 0, true);

                MainPage.CreateFunction("Clean Server", Color.green, () => CleanServer());
                MainPage.CreateFunction("Reload Level", Color.red, () => ReloadLevel());
                MainPage.CreateFunction("Admin Clean", Color.red, () => CleanServerStyle());

                MainPage.CreateBool("Auto Clean", Color.cyan, autoCleanEnabled.Value, (value) =>
                {
                    autoCleanEnabled.Value = value;
                    string status = value ? "enabled" : "disabled";
                    ShowNotification($"Auto clean {status}", NotificationType.Information);
                    MelonLogger.Msg($"Auto clean {status}");

                    if (!value && adaptiveAutoCleanEnabled.Value)
                    {
                        adaptiveAutoCleanEnabled.Value = false;
                        ShowNotification("Adaptive auto clean disabled (requires Auto Clean)", NotificationType.Information);
                        MelonLogger.Msg("Adaptive auto clean automatically disabled");
                    }
                });

                MainPage.CreateBool("Adaptive Auto Clean", Color.magenta, adaptiveAutoCleanEnabled.Value, (value) =>
                {
                    adaptiveAutoCleanEnabled.Value = value;
                    string status = value ? "enabled" : "disabled";
                    ShowNotification($"Adaptive auto clean {status}", NotificationType.Information);
                    MelonLogger.Msg($"Adaptive auto clean {status}");

                    if (value && NetworkInfo.HasServer)
                        CheckAndUpdateAdaptiveInterval();
                });

                MainPage.CreateBool("Anti-Grief Protection", Color.yellow, AntiGriefEnabled.Value, (value) =>
                {
                    AntiGriefEnabled.Value = value;
                    string status = value ? "enabled" : "disabled";
                    ShowNotification($"Anti-Grief Protection {status}", NotificationType.Information);
                    MelonLogger.Msg($"Anti-Grief Protection {status}");

                    if (value == true)
                    {
                        MelonLogger.Warning("Admin clean may not work with Anti-Grief enabled");
                        ShowNotification("Admin clean may not work with Anti-Grief enabled", NotificationType.Warning);
                    }
                });

                MainPage.CreateEnum("Clean Interval", Color.cyan, GetIntervalEnum(), (intervalEnum) =>
                {
                    if (adaptiveAutoCleanEnabled.Value)
                    {
                        ShowNotification($"Adaptive mode active - current interval: {autoCleanInterval.Value / 60:F0} minutes", NotificationType.Information);
                    }
                    else
                    {
                        float[] intervals = { 300f, 600f, 900f, 1200f, 1500f, 1800f };
                        int index = Convert.ToInt32(intervalEnum);
                        autoCleanInterval.Value = intervals[index];
                        ShowNotification($"Manual interval set to {intervals[index] / 60} minutes", NotificationType.Information);
                        MelonLogger.Msg($"Manual auto clean interval set to {intervals[index]} seconds");
                    }
                });

                MainPage.CreateFunction("Show Current Status", Color.white, () =>
                {
                    string autoStatus = autoCleanEnabled.Value ? "enabled" : "disabled";
                    string adaptiveStatus = adaptiveAutoCleanEnabled.Value ? "enabled" : "disabled";
                    string AntiGriefStatus = AntiGriefEnabled.Value ? "enabled" : "disabled";
                    int playerCount = PlayerIDManager.PlayerIDs.Count;

                    ShowNotification($"Auto: {autoStatus} | Adaptive: {adaptiveStatus} | Interval: {autoCleanInterval.Value / 60:F0}min | Anti-Grief: {AntiGriefStatus} | Players: {playerCount}", NotificationType.Information);
                });

                MainPage.CreateFunction("Save Config", Color.white, () =>
                {
                    ConfigManager.SaveConfig();
                    ShowNotification("Configuration saved successfully!", NotificationType.Success);
                });
            }

            private void CleanServer()
            {
                if (!ValidateServerConnection()) return;

                try
                {
                    PooleeUtilities.DespawnAll();
                    CleanProblematicSpawns();
                    MelonLogger.Msg("Server cleaned!");
                    ShowNotification("Server cleaned", NotificationType.Success);
                }
                catch (Exception e)
                {
                    MelonLogger.Error($"Clean server failed: {e.Message}");
                    ShowNotification("Clean failed! Check console for details", NotificationType.Error);
                }
            }

            // -----------------------------
            // Utility
            // -----------------------------
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

            public static class RateLimiter
            {
                private static int _maxActionsPerSecond = 6;
                private static float _rateWindow = 1.0f;

                private static readonly Dictionary<ulong, PlayerRateData> _playerRates = new Dictionary<ulong, PlayerRateData>();
                private static float _lastResetTime;

                private class PlayerRateData
                {
                    public int Count;
                    public float LastActionTime;
                }

                public static void SetLimits(int maxActions, float window)
                {
                    _maxActionsPerSecond = maxActions;
                    _rateWindow = window;
                    MelonLogger.Msg($"[AntiGrief] RateLimiter configured: {_maxActionsPerSecond} actions per {_rateWindow}s");
                }

                public static bool AllowAction(ulong playerId, string actionName)
                {
                    if (Time.time - _lastResetTime > _rateWindow)
                    {
                        _playerRates.Clear();
                        _lastResetTime = Time.time;
                    }

                    if (!_playerRates.TryGetValue(playerId, out var rate))
                    {
                        rate = new PlayerRateData();
                        _playerRates[playerId] = rate;
                    }

                    rate.Count++;
                    rate.LastActionTime = Time.time;

                    if (rate.Count > _maxActionsPerSecond)
                    {
                        string msg = $"Player {playerId} exceeded rate limit for {actionName} ({rate.Count}/{_maxActionsPerSecond})";
                        MelonLogger.Warning($"[AntiGrief] {msg}");

                        if (ConfigManager.Config.EnableFileLogging)
                            AntiGriefLogger.Log(msg);

                        return false;
                    }

                    return true;
                }
                public static void Reset()
                {
                    _playerRates.Clear();
                    _lastResetTime = Time.time;
                }
            }


            [HarmonyPatch(typeof(ConnectionRequestMessage))]
            public static class ConnectionRequestMessagePatch
            {
                [HarmonyPatch("OnHandleMessage")]
                [HarmonyPrefix]
                public static bool OnHandleMessage_Prefix(object __instance, ReceivedMessage received)
                {
                    // If anti-grief is DISABLED or null, skip logic
                    if (AntiGriefEnabled?.Value != true) return true;

                    try
                    {
                        var id = NetworkInfo.LastReceivedUser.Value;
                        if (NetworkInfo.IsHost)
                        {
                            var data = received.ReadData<ConnectionRequestData>();
                            MelonLogger.Msg($"[FSOAntiGrief] Incoming connection: PlatformID={id}, Version={data.Version}");
                            if (data.PlatformID != id)
                            {

                                MelonLogger.Warning($"[FSOAntiGrief] Spoofed ID detected! Blocking connection: {id} ");
                                ShowNotification($"[FSOAntiGrief] Spoofed ID detected! Blocking connection: {id}", NotificationType.Warning);
                                ConnectionSender.SendConnectionDeny(id, "[FSOAntiGrief]");
                                return false;
                            }
                        }
                        if (NetworkInfo.Layer.RequiresValidId)
                        {
                            var data = received.ReadData<ConnectionRequestData>();
                            if (NetworkInfo.IsSpoofed(data.PlatformID))
                            {

                                MelonLogger.Warning($"[FSOAntiGrief] Spoofed ID detected! Blocking connection: {id}");
                                ShowNotification($"[FSOAntiGrief] Spoofed ID detected! Blocking connection: {id}", NotificationType.Warning);
                                ConnectionSender.SendConnectionDeny(id, "[FSOAntiGrief]");
                                return false;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[FSOAntiGrief] Error processing connection attempt: {ex}");
                    }
                    return true;
                }
            }

            [HarmonyPatch(typeof(SpawnRequestMessage))]
            public static class SpawnRequestMessagePatch
            {
                [HarmonyPatch("OnHandleMessage")]
                [HarmonyPrefix]
                public static bool OnHandleMessage_Prefix(ReceivedMessage received)
                {
                    try
                    {
                        // Only the host should filter incoming spawn requests
                        if (!NetworkInfo.IsHost)
                            return true;

                        // True sender of this packet
                        ulong sender = (ulong)NetworkInfo.LastReceivedUser;

                        // Make sure this sender exists in the player list
                        var player = NetworkPlayer.Players.FirstOrDefault(p => p.PlayerID.PlatformID == sender);
                        string username = player?.Username ?? sender.ToString();

                        if (player == null)
                        {
                            MelonLogger.Warning($"[FSOAntiGrief] Spawn request from unknown sender {sender}");
                            return false;
                        }

                        // Detect spoofing (pretending to be host)
                        var host = NetworkPlayer.Players.FirstOrDefault(p => p.PlayerID.IsHost);
                        if (host != null && player.PlayerID.IsHost && sender != host.PlayerID.PlatformID)
                        {
                            MelonLogger.Warning($"[FSOAntiGrief] Spoofed host ID detected from {username} ({sender})");
                            return false;
                        }

                        // Apply spawn rate limiting
                        if (!RateLimiter.AllowAction(sender, "SpawnRequest"))
                        {
                            MelonLogger.Warning($"[FSOAntiGrief] Blocked spawn from {username} ({sender}) – rate limit exceeded.");
                            CleanServerStyle();
                            return false;
                        }
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Error($"[FSOAntiGrief] SpawnRequestMessage limiter failed: {ex}");
                    }

                    return true; // let Fusion handle normal spawns
                }
            }

            public static class AntiGriefLogger
            {
                private static readonly string LogDirectory =
                    Path.Combine(MelonEnvironment.UserDataDirectory, "FsOptimizer", "Logs");

                private static readonly string LogFilePath =
                    Path.Combine(LogDirectory, $"AntiGrief_{DateTime.Now:yyyy-MM-dd}.txt");

                static AntiGriefLogger()
                {
                    try
                    {
                        if (!Directory.Exists(LogDirectory))
                            Directory.CreateDirectory(LogDirectory);

                        File.AppendAllText(LogFilePath,
                            $"\n==== FSO AntiGrief Log Started {DateTime.Now:G} ====\n");
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[AntiGriefLogger] Failed to init log file: {ex.Message}");
                    }
                }

                public static void Log(string message)
                {
                    try
                    {
                        if (!NetworkInfo.HasServer) return;
                        if (!NetworkInfo.IsHost) return;
                        if (FsOptimizer.AntiGriefEnabled?.Value != true) return;
                        if (!ConfigManager.Config.EnableFileLogging) return;

                        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                        string line = $"[{timestamp}] {message}";

                        // Write to file
                        File.AppendAllText(LogFilePath, line + Environment.NewLine);

                        // Also print to console
                        MelonLogger.Msg($"[FSOAntiGrief] {message}");
                    }
                    catch (Exception ex)
                    {
                        MelonLogger.Warning($"[AntiGriefLogger] Failed to write log: {ex.Message}");
                    }
                }
            }

            private string FormatBytes(long bytes)
            {
                if (bytes < 1024) return $"{bytes} B";
                if (bytes < 1024 * 1024) return $"{bytes / 1024} KB";
                return $"{bytes / (1024 * 1024)} MB";
            }

            private static void ShowNotification(string message, NotificationType type)
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

        #endregion
    }
}
