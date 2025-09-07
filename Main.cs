using BoneLib.BoneMenu;
using BoneLib.Notifications;
using Il2CppSLZ.Marrow.SceneStreaming;
using Il2CppSLZ.Marrow.Warehouse;
using Il2CppSLZ.Marrow;
using Il2CppSLZ.Marrow.Pool;
using LabFusion.Data;
using LabFusion.Network;
using LabFusion.Player;
using LabFusion.Utilities;
using MelonLoader;
using UnityEngine;
using System;

namespace FsOptimizer
{
    public static class BuildInfo
    {
        public const string Name = "FsOptimizer";
        public const string Description = "A Fusion server cleaner/optimizer";
        public const string Author = "Popper";
        public const string Company = null;
        public const string Version = "1.6.0";
        public const string DownloadLink = null;
    }

    public class FsOptimizer : MelonMod
    {
        public static Page MainPage;
        public static MelonPreferences_Category Preferences;

        // Auto-Clean System
        private static MelonPreferences_Entry<bool> autoCleanEnabled;
        private static MelonPreferences_Entry<float> autoCleanInterval;
        private static float lastCleanTime = 0f;

        // Enum for interval selection
        public enum CleanInterval
        {
            Five_Minutes = 0,
            Ten_Minutes = 1,
            Fifteen_Minutes = 2,
            Twenty_Minutes = 3,
            Twentyfive_Minutes = 4,
            Thirty_Minutes = 5
        }

        public override void OnInitializeMelon()
        {
            // Setup preferences for auto-clean
            Preferences = MelonPreferences.CreateCategory(BuildInfo.Name);
            autoCleanEnabled = Preferences.CreateEntry("AutoClean", false, "Auto Clean Enabled");
            autoCleanInterval = Preferences.CreateEntry("AutoCleanInterval", 300f, "Auto Clean Interval (seconds)");

            SetupMenu();
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
        }

        private void SetupMenu()
        {
            // Enhanced UI with emojis and better colors
            MainPage = Page.Root.CreatePage("<color=#00CCFF>F</color><color=#00C3FE>s</color><color=#00BBFD>O</color><color=#00B2FD>p</color><color=#00AAFC>t</color><color=#00A1FC>i</color><color=#0099FB>m</color><color=#0090FB>i</color><color=#0088FA>z</color><color=#007FFA>e</color><color=#0077F9>r</color> <color=#FFD700>v1.5</color>", Color.white, 0, true);

            // Main cleaning functions with better visual design
            MainPage.CreateFunction("Clean Server", Color.green, () => CleanServer());
            MainPage.CreateFunction("Reload Level", Color.red, () => ReloadLevel());
            MainPage.CreateFunction("Memory Clean (warning may crash game)", Color.cyan, () => CleanMemory());

            // Auto-clean settings
            MainPage.CreateBool("Auto Clean", Color.cyan, autoCleanEnabled.Value, (value) => {
                autoCleanEnabled.Value = value;
                string status = value ? "enabled" : "disabled";
                ShowNotification($"Auto clean {status}", NotificationType.Information);
                MelonLogger.Msg($"Auto clean {status}");
            });

            // Fixed: Auto-clean interval selector with proper CreateEnum signature
            MainPage.CreateEnum("Clean Interval", Color.cyan, GetIntervalEnum(), (intervalEnum) => {
                float[] intervals = { 300f, 600f, 900f, 1200f, 1500f, 1800f };
                int index = Convert.ToInt32(intervalEnum);
                autoCleanInterval.Value = intervals[index];
                ShowNotification($"Interval set to {intervals[index] / 60} minutes", NotificationType.Information);
                MelonLogger.Msg($"Auto clean interval set to {intervals[index]} seconds");
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

        private void CleanMemory()
        {
            try
            {
                // Enhanced Memory Management - VR Optimized
                MelonLogger.Msg("Starting memory cleanup...");
                ShowNotification("Game might lag a bit Cleaning memory...", NotificationType.Information);

                // Get memory usage before cleanup
                long memoryBefore = GC.GetTotalMemory(false);

                // Less aggressive GC for VR - single pass to avoid stuttering
                GC.Collect(0, GCCollectionMode.Optimized);
                GC.WaitForPendingFinalizers();

                // Unity asset cleanup (this can be slow but is thorough)
                Resources.UnloadUnusedAssets();

                // Final light cleanup
                GC.Collect(0, GCCollectionMode.Optimized);

                // Get memory usage after cleanup
                long memoryAfter = GC.GetTotalMemory(false);
                long memoryFreed = memoryBefore - memoryAfter;

                string memoryMessage = memoryFreed > 0 ?
                    $"Memory cleaned - {FormatBytes(memoryFreed)} freed" :
                    "Memory cleaned";

                MelonLogger.Msg($"Memory cleanup completed. Freed: {FormatBytes(memoryFreed)}");
                ShowNotification(memoryMessage, NotificationType.Success);
            }
            catch (Exception e)
            {
                MelonLogger.Error($"Memory cleanup failed: {e.Message}");
                ShowNotification("Memory cleanup failed!", NotificationType.Error);
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
            float[] intervals = { 60f, 120f, 300f, 600f, 900f };
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
                    Title = "FsOptimizer v1.5",
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