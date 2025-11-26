using LoneEftDmaRadar.UI.Misc;
using System;
using System.Collections.Generic;

namespace LoneEftDmaRadar.Tarkov.IL2CPP
{
    /// <summary>
    /// Example usage of the IL2CPP Offset Dumper
    /// This demonstrates how to dump offsets for SDK generation at runtime
    /// </summary>
    public static class DumperExample
    {
        /// <summary>
        /// Run the IL2CPP dumper for common EFT classes
        /// Call this method after the game has loaded to dump offsets
        /// </summary>
        public static void RunDumperExample()
        {
            try
            {
                DebugLogger.LogInfo("\n========================================");
                DebugLogger.LogInfo("   IL2CPP Offset Dumper - POC");
                DebugLogger.LogInfo("========================================\n");

                var dumper = new Il2CppOffsetDumper();

                // Initialize the dumper
                if (!dumper.Initialize())
                {
                    DebugLogger.LogError("Failed to initialize IL2CPP dumper");
                    return;
                }

                // Example 1: Dump a specific assembly
                DebugLogger.LogInfo("\n--- Example 1: Dump Assembly Info ---");
                dumper.DumpAssembly("Assembly-CSharp.dll");

                // Example 2: Dump specific classes commonly used in EFT
                DebugLogger.LogInfo("\n--- Example 2: Dump Specific Classes ---");

                // GameWorld class
                dumper.DumpClass("GameWorld", "EFT");

                // Player class
                dumper.DumpClass("Player", "EFT");

                // Profile class
                dumper.DumpClass("Profile", "EFT");

                // Example 3: Batch dump multiple classes
                DebugLogger.LogInfo("\n--- Example 3: Batch Dump ---");

                var classesToDump = new Dictionary<string, string>
                {
                    { "ObservedPlayerView", "EFT.NextObservedPlayer" },
                    { "MovementContext", "EFT" },
                    { "ProceduralWeaponAnimation", "EFT.Animations" },
                    { "LootItem", "EFT.Interactive" }
                };

                dumper.DumpMultipleClasses(classesToDump);

                DebugLogger.LogInfo("\n========================================");
                DebugLogger.LogInfo("   Dumper POC Complete!");
                DebugLogger.LogInfo("========================================\n");
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex, "RunDumperExample");
            }
        }

        /// <summary>
        /// Dump only the most critical classes for SDK generation
        /// This is a minimal set for a working radar
        /// </summary>
        public static void DumpCriticalClasses()
        {
            try
            {
                DebugLogger.LogInfo("\n=== Dumping Critical Classes for SDK ===\n");

                var dumper = new Il2CppOffsetDumper();

                if (!dumper.Initialize())
                {
                    DebugLogger.LogError("Failed to initialize dumper");
                    return;
                }

                var criticalClasses = new Dictionary<string, string>
                {
                    // Core game classes
                    { "GameWorld", "EFT" },
                    { "Player", "EFT" },
                    { "Profile", "EFT" },

                    // Player-related
                    { "ObservedPlayerView", "EFT.NextObservedPlayer" },
                    { "ObservedPlayerController", "EFT.NextObservedPlayer" },
                    { "MovementContext", "EFT" },

                    // Items and loot
                    { "LootItem", "EFT.Interactive" },
                    { "Item", "EFT.InventoryLogic" },
                    { "ItemTemplate", "EFT.InventoryLogic" },

                    // Weapon/Animation
                    { "ProceduralWeaponAnimation", "EFT.Animations" },

                    // Inventory
                    { "InventoryController", "EFT" }
                };

                dumper.DumpMultipleClasses(criticalClasses);

                DebugLogger.LogInfo("\n=== Critical Classes Dump Complete ===\n");
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex, "DumpCriticalClasses");
            }
        }

        /// <summary>
        /// Quick test to verify the dumper is working
        /// </summary>
        public static void QuickTest()
        {
            try
            {
                DebugLogger.LogInfo("=== IL2CPP Dumper Quick Test ===\n");

                var dumper = new Il2CppOffsetDumper();

                if (dumper.Initialize())
                {
                    DebugLogger.LogInfo("✓ Dumper initialized successfully!");
                    DebugLogger.LogInfo("Ready to dump offsets.\n");

                    // Dump all assemblies first to see what's loaded
                    dumper.DumpAllAssemblies();

                    // Try dumping Assembly-CSharp details
                    dumper.DumpAssembly("Assembly-CSharp.dll");
                }
                else
                {
                    DebugLogger.LogError("✗ Dumper initialization failed");
                    DebugLogger.LogWarning("Check that the game is running and GameAssembly.dll is loaded");
                }
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex, "QuickTest");
            }
        }

        /// <summary>
        /// Extended test that attempts to dump class information
        /// </summary>
        public static void ExtendedTest()
        {
            try
            {
                DebugLogger.LogInfo("\n=== IL2CPP Dumper Extended Test ===\n");

                var dumper = new Il2CppOffsetDumper();

                if (!dumper.Initialize())
                {
                    DebugLogger.LogError("Failed to initialize dumper");
                    return;
                }

                // First, see all assemblies
                DebugLogger.LogInfo("Step 1: Listing all assemblies...");
                dumper.DumpAllAssemblies();

                // Dump specific assembly details
                DebugLogger.LogInfo("\nStep 2: Dumping Assembly-CSharp details...");
                dumper.DumpAssembly("Assembly-CSharp.dll");

                // Try to dump specific classes (will fail until FindClass is fully implemented)
                DebugLogger.LogInfo("\nStep 3: Attempting to dump specific classes...");
                dumper.DumpClass("GameWorld", "EFT");
                dumper.DumpClass("Player", "EFT");

                DebugLogger.LogInfo("\n=== Extended Test Complete ===");
            }
            catch (Exception ex)
            {
                DebugLogger.LogException(ex, "ExtendedTest");
            }
        }
    }
}
